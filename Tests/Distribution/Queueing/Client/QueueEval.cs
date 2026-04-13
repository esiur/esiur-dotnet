using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Queueing.Client
{
    public static class EsiurQueueEval
    {

        public static class EvalPrinter
        {
            public static void Print(EsiurQueueEval.EvalResult r)
            {
                Console.WriteLine("=== Evaluation Result ===");
                Console.WriteLine($"α (HasResource probability): {r.Alpha:F3}");
                Console.WriteLine($"λ̂ (effective arrival rate): {r.LambdaEventsPerSecond:F2} events/s");
                Console.WriteLine();

                PrintLatencyTable(r.Latency);

                Console.WriteLine();
                PrintValidation(r.Validation);

                if (r.FlushSizeStats != null)
                {
                    Console.WriteLine();
                    PrintStats("Flush size (≤ window)", r.FlushSizeStats);
                }
            }

            private static void PrintLatencyTable(EsiurQueueEval.LatencyDecomposition l)
            {
                Console.WriteLine("Latency Decomposition (ms)");
                Console.WriteLine("-----------------------------------------------");
                Console.WriteLine($"{"Metric",-16} {"Mean",8} {"P50",8} {"P95",8} {"P99",8} {"Max",8}");
                Console.WriteLine("-----------------------------------------------");

                PrintRow("Readiness R", l.ReadinessMs);
                PrintRow("HOL Δ", l.HolMs);
                PrintRow("End-to-End D", l.EndToEndMs);

                Console.WriteLine("-----------------------------------------------");
            }

            private static void PrintValidation(EsiurQueueEval.ModelValidation v)
            {
                Console.WriteLine("Resequencing Model Validation");
                Console.WriteLine("-----------------------------------------------");
                Console.WriteLine("Absolute error |d − d̂| (ms)");
                PrintStats("Error", v.AbsErrorMs);

                if (v.MaxNegativeSlackMs > 0)
                {
                    Console.WriteLine($"Max negative slack (delivered earlier than model): {v.MaxNegativeSlackMs:F4} ms");
                }
                else
                {
                    Console.WriteLine("No negative slack observed (model is conservative).");
                }
            }

            private static void PrintRow(string name, EsiurQueueEval.Stats s)
            {
                Console.WriteLine(
                    $"{name,-16} " +
                    $"{s.Mean,8:F2} " +
                    $"{s.P50,8:F2} " +
                    $"{s.P95,8:F2} " +
                    $"{s.P99,8:F2} " +
                    $"{s.Max,8:F2}"
                );
            }

            private static void PrintStats(string name, EsiurQueueEval.Stats s)
            {
                Console.WriteLine(
                    $"{name,-20} " +
                    $"mean={s.Mean,8:F02}  " +
                    $"p50={s.P50,8:F02}  " +
                    $"p95={s.P95,8:F02}  " +
                    $"p99={s.P99,8:F02}  " +
                    $"max={s.Max,8:F02}"
                );
            }
        }

        public sealed record Stats(double Mean, double P50, double P95, double P99, double Max);

        public sealed record LatencyDecomposition(Stats ReadinessMs, Stats HolMs, Stats EndToEndMs);

        public sealed record ModelValidation(
            Stats AbsErrorMs,
            double MaxNegativeSlackMs,  // worst case where Delivered < predicted (if happens)
            int Count);

        public sealed record EvalResult(
            double Alpha,
            double LambdaEventsPerSecond,
            double MuEventsPerSecond,   // <-- NEW
            LatencyDecomposition Latency,
            ModelValidation Validation,
            Stats QueueLength,
            Stats? FlushSizeStats);

        /// <summary>
        /// Evaluates Esiur fork-join readiness + in-order resequencing using in-memory items.
        /// Assumes items refer to a single ordered stream (per resource queue).
        /// </summary>
        public static EvalResult Evaluate<T>(
            IReadOnlyList<AsyncQueueItem<T>> items,
            double flushWindowMs = 0.5,
            bool computeFlush = true)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) throw new ArgumentException("items is empty.");

            // Ensure deterministic order: prefer Sequence, then Arrival timestamp
            var ordered = items
                .OrderBy(x => x.Sequence)
                .ThenBy(x => x.Arrival)
                .ToArray();

            int n = ordered.Length;

            // Latency components in milliseconds
            var readiness = new double[n];  // R = r-a
            var hol = new double[n];        // Δ = d-r
            var endToEnd = new double[n];   // D = d-a

            int resCount = 0;
            for (int i = 0; i < n; i++)
            {
                var e = ordered[i];

                double Rms = (e.Ready - e.Arrival).TotalMilliseconds;
                double Hms = (e.Delivered - e.Ready).TotalMilliseconds;
                double Dms = (e.Delivered - e.Arrival).TotalMilliseconds;

                // Robustness against logging placement or clock issues
                if (Rms < 0) Rms = 0;
                if (Hms < 0) Hms = 0;
                if (Dms < 0) Dms = 0;

                readiness[i] = Rms;
                hol[i] = Hms;
                endToEnd[i] = Dms;

                if (e.HasResource) resCount++;
            }

            // α = P(HasResource)
            double alpha = (double)resCount / n;


            // Effective arrival rate λ̂ from arrival timeline
            double lambda = EstimateLambda(ordered);

            // Effective departure / readiness rate μ̂ from delivery timeline
            double mu = EstimateMu(ordered);

            var latency = new LatencyDecomposition(
                ReadinessMs: ComputeStats(readiness),
                HolMs: ComputeStats(hol),
                EndToEndMs: ComputeStats(endToEnd));

            // --- Resequencing validation: d_hat_i = max(r_i, d_hat_{i-1}) ---
            // Use DateTime ticks for exactness, then convert to ms.
            var absErrMs = new double[n];
            long prevPredictedTicks = long.MinValue;
            double maxNegativeSlackMs = 0;

            for (int i = 0; i < n; i++)
            {
                long ri = ordered[i].Ready.Ticks;

                long predicted = (i == 0)
                    ? ri
                    : Math.Max(ri, prevPredictedTicks);

                prevPredictedTicks = predicted;

                long observed = ordered[i].Delivered.Ticks;
                long errTicks = Math.Abs(observed - predicted);
                absErrMs[i] = TicksToMs(errTicks);

                // If observed delivery occurs earlier than model predicts (shouldn't, but track it)
                long slackTicks = observed - predicted; // negative => earlier than predicted
                if (slackTicks < 0)
                {
                    double slackMs = TicksToMs(-slackTicks);
                    if (slackMs > maxNegativeSlackMs) maxNegativeSlackMs = slackMs;
                }
            }

            var validation = new ModelValidation(
                AbsErrorMs: ComputeStats(absErrMs),
                MaxNegativeSlackMs: maxNegativeSlackMs,
                Count: n);

            // --- Flush sizes (optional): consecutive deliveries within window ---
            //Stats? flushStats = null;
            //if (computeFlush)
            //{
            //    var flushSizes = ComputeFlushSizes(ordered, flushWindowMs);
            //    flushStats = ComputeStats(flushSizes.Select(x => (double)x).ToArray());
            //}

            var queueLength = ComputeStats(ordered.Select(x => (double)x.NotificationsCountWaitingInTheQueueAtEnqueueing).ToArray());

            var flushStats = ComputeStats(ordered.GroupBy(x => x.FlushId).Select(x => (double)x.First().BatchSize).ToArray());

            return new EvalResult(alpha, lambda, mu, latency, validation, queueLength, flushStats);
        }

        // ---------------- Helpers ----------------

        private static double EstimateLambda<T>(AsyncQueueItem<T>[] ordered)
        {
            if (ordered.Length < 2) return 0;

            DateTime first = ordered[0].Arrival;
            DateTime last = ordered[^1].Arrival;
            double seconds = (last - first).TotalSeconds;
            if (seconds <= 0) return 0;

            // N-1 arrivals over observed interval
            return (ordered.Length - 1) / seconds;
        }

        private static double TicksToMs(long ticks) => ticks / 10_000.0; // 1 tick = 100ns

        private static int[] ComputeFlushSizes<T>(AsyncQueueItem<T>[] ordered, double windowMs)
        {
            var sizes = new List<int>(ordered.Length);
            long windowTicks = (long)(windowMs * 10_000.0);

            int i = 0;
            while (i < ordered.Length)
            {
                int j = i + 1;
                long baseD = ordered[i].Delivered.Ticks;

                while (j < ordered.Length)
                {
                    long dj = ordered[j].Delivered.Ticks;
                    if (Math.Abs(dj - baseD) <= windowTicks) j++;
                    else break;
                }

                sizes.Add(j - i);
                i = j;
            }

            return sizes.ToArray();
        }

        private static Stats ComputeStats(double[] values)
        {
            if (values.Length == 0) return new Stats(0, 0, 0, 0, 0);

            double mean = values.Average();
            double max = values.Max();

            var sorted = (double[])values.Clone();
            Array.Sort(sorted);

            return new Stats(
                Mean: mean,
                P50: QuantileSorted(sorted, 0.50),
                P95: QuantileSorted(sorted, 0.95),
                P99: QuantileSorted(sorted, 0.99),
                Max: max);
        }

        // Linear interpolation quantile
        private static double QuantileSorted(double[] sorted, double q)
        {
            if (sorted.Length == 1) return sorted[0];

            double pos = (sorted.Length - 1) * q;
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);
            if (lo == hi) return sorted[lo];

            double frac = pos - lo;
            return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
        }

        // Compute the element-wise average of a sequence of EvalResult
        public static EvalResult Average(IEnumerable<EvalResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var arr = results.ToArray();
            if (arr.Length == 0) throw new ArgumentException("results is empty.", nameof(results));

            double avgAlpha = arr.Average(r => r.Alpha);
            double avgLambda = arr.Average(r => r.LambdaEventsPerSecond);
            double avgMu = arr.Average(r => r.MuEventsPerSecond);

            Stats avgReadiness = AverageStats(arr.Select(r => r.Latency.ReadinessMs));
            Stats avgHol = AverageStats(arr.Select(r => r.Latency.HolMs));
            Stats avgE2E = AverageStats(arr.Select(r => r.Latency.EndToEndMs));

            var avgLatency = new LatencyDecomposition(avgReadiness, avgHol, avgE2E);

            Stats avgAbsError = AverageStats(arr.Select(r => r.Validation.AbsErrorMs));
            double worstNegativeSlack = arr.Max(r => r.Validation.MaxNegativeSlackMs);
            int totalCount = arr.Sum(r => r.Validation.Count);

            var avgValidation = new ModelValidation(avgAbsError, worstNegativeSlack, totalCount);

            Stats? avgFlush = null;
            var flushStatsSeq = arr.Select(r => r.FlushSizeStats).Where(s => s != null).Select(s => s!).ToArray();
            if (flushStatsSeq.Length > 0) avgFlush = AverageStats(flushStatsSeq);


            var avgQueue = AverageStats(arr.Select(x => x.QueueLength));

            return new EvalResult(avgAlpha, avgLambda, avgMu, avgLatency, avgValidation, avgQueue, avgFlush);
        }

        private static double EstimateMu<T>(AsyncQueueItem<T>[] ordered)
        {
            if (ordered.Length < 2) return 0;

            DateTime first = ordered[0].Delivered;
            DateTime last = ordered[^1].Delivered;
            double seconds = (last - first).TotalSeconds;
            if (seconds <= 0) return 0;

            // N-1 completions over observed interval
            return (ordered.Length - 1) / seconds;
        }

        private static Stats AverageStats(IEnumerable<Stats> seq)
        {
            var arr = seq.ToArray();
            if (arr.Length == 0) return new Stats(0, 0, 0, 0, 0);
            return new Stats(
                Mean: arr.Average(s => s.Mean),
                P50: arr.Average(s => s.P50),
                P95: arr.Average(s => s.P95),
                P99: arr.Average(s => s.P99),
                Max: arr.Average(s => s.Max));
        }
    }

}
