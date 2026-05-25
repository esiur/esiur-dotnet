using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Esiur.Tests.Queueing.Client
{
    /// <summary>
    /// Point estimate accompanied by a 95% confidence-interval half-width
    /// (computed with Student's t for small samples). Use ToString() to
    /// render as "mean ± half" in print output.
    /// </summary>
    public readonly record struct MeanCi(double Mean, double Ci95HalfWidth, int N)
    {
        public static MeanCi From(IEnumerable<double> xs)
        {
            var arr = xs.ToArray();
            int n = arr.Length;
            if (n == 0) return new MeanCi(0, 0, 0);
            if (n == 1) return new MeanCi(arr[0], 0, 1);

            double mean = arr.Average();
            double sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                double d = arr[i] - mean;
                sumSq += d * d;
            }
            double std = Math.Sqrt(sumSq / (n - 1));
            double sem = std / Math.Sqrt(n);

            // Student's t two-sided 95% for small df. df = n - 1.
            // Values from standard tables; ≥10 falls back to normal (1.960).
            double t = (n - 1) switch
            {
                1 => 12.706,
                2 => 4.303,
                3 => 3.182,
                4 => 2.776,
                5 => 2.571,
                6 => 2.447,
                7 => 2.365,
                8 => 2.306,
                9 => 2.262,
                10 => 2.228,
                11 => 2.201,
                12 => 2.179,
                13 => 2.160,
                14 => 2.145,
                15 => 2.131,
                16 => 2.120,
                17 => 2.110,
                18 => 2.101,
                19 => 2.093,
                20 => 2.086,
                _ => 1.960   // normal approximation for df > 20
            };
            return new MeanCi(mean, t * sem, n);
        }

        public override string ToString() =>
            N <= 1
                ? Mean.ToString("F2", CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture,
                    $"{Mean:F2}±{Ci95HalfWidth:F2}");
    }

    /// <summary>
    /// Aggregated result over K replications of the same (delay, alpha)
    /// configuration. Carries point estimates plus per-metric 95% CI
    /// half-widths for the headline metrics reported in the paper:
    /// arrival rate λ, service rate μ, mean readiness R̄, mean HOL δ̄,
    /// and mean end-to-end latency D̄.
    ///
    /// The companion <see cref="EsiurQueueEval.EvalResult"/> field
    /// (PerRepMean) holds the existing-style averaged point estimates
    /// so downstream code that already consumed EvalResult continues
    /// to work unchanged.
    /// </summary>
    public sealed record ReplicatedResult(
        int Delay,
        double Alpha,
        int Replications,
        MeanCi Lambda,
        MeanCi Mu,
        MeanCi ReadinessMeanMs,
        MeanCi HolMeanMs,
        MeanCi EndToEndMeanMs,
        MeanCi EndToEndP99Ms,
        MeanCi QueueLengthMean,
        MeanCi BatchSizeMean,
        EsiurQueueEval.EvalResult PerRepMean);

    public static class ReplicatedEvalAggregator
    {
        /// <summary>
        /// Combine K per-replication EvalResult objects into a single
        /// ReplicatedResult, computing point estimates and 95% CIs.
        /// </summary>
        public static ReplicatedResult Aggregate(
            int delay,
            double alpha,
            IReadOnlyList<EsiurQueueEval.EvalResult> reps)
        {
            if (reps == null) throw new ArgumentNullException(nameof(reps));
            if (reps.Count == 0) throw new ArgumentException("reps is empty.", nameof(reps));

            var lambda = MeanCi.From(reps.Select(r => r.LambdaEventsPerSecond));
            var mu = MeanCi.From(reps.Select(r => r.MuEventsPerSecond));
            var readiness = MeanCi.From(reps.Select(r => r.Latency.ReadinessMs.Mean));
            var hol = MeanCi.From(reps.Select(r => r.Latency.HolMs.Mean));
            var e2eMean = MeanCi.From(reps.Select(r => r.Latency.EndToEndMs.Mean));
            var e2eP99 = MeanCi.From(reps.Select(r => r.Latency.EndToEndMs.P99));
            var qLen = MeanCi.From(reps.Select(r => r.QueueLength.Mean));
            var batch = MeanCi.From(reps.Select(
                                r => r.FlushSizeStats?.Mean ?? double.NaN)
                            .Where(v => !double.IsNaN(v)));

            // Use the existing Average helper for the carry-along point estimates.
            var perRepMean = EsiurQueueEval.Average(reps);

            return new ReplicatedResult(
                Delay: delay,
                Alpha: alpha,
                Replications: reps.Count,
                Lambda: lambda,
                Mu: mu,
                ReadinessMeanMs: readiness,
                HolMeanMs: hol,
                EndToEndMeanMs: e2eMean,
                EndToEndP99Ms: e2eP99,
                QueueLengthMean: qLen,
                BatchSizeMean: batch,
                PerRepMean: perRepMean);
        }

        public static string CsvHeader =>
            "delay_ms,alpha,replications," +
            "lambda_mean,lambda_ci95," +
            "mu_mean,mu_ci95," +
            "readiness_mean_ms,readiness_ci95," +
            "hol_mean_ms,hol_ci95," +
            "e2e_mean_ms,e2e_ci95," +
            "e2e_p99_ms,e2e_p99_ci95," +
            "queue_len_mean,queue_len_ci95," +
            "batch_mean,batch_ci95";

        public static string ToCsvRow(ReplicatedResult r)
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Create(inv,
                $"{r.Delay},{r.Alpha:F3},{r.Replications}," +
                $"{r.Lambda.Mean:F3},{r.Lambda.Ci95HalfWidth:F3}," +
                $"{r.Mu.Mean:F3},{r.Mu.Ci95HalfWidth:F3}," +
                $"{r.ReadinessMeanMs.Mean:F3},{r.ReadinessMeanMs.Ci95HalfWidth:F3}," +
                $"{r.HolMeanMs.Mean:F3},{r.HolMeanMs.Ci95HalfWidth:F3}," +
                $"{r.EndToEndMeanMs.Mean:F3},{r.EndToEndMeanMs.Ci95HalfWidth:F3}," +
                $"{r.EndToEndP99Ms.Mean:F3},{r.EndToEndP99Ms.Ci95HalfWidth:F3}," +
                $"{r.QueueLengthMean.Mean:F3},{r.QueueLengthMean.Ci95HalfWidth:F3}," +
                $"{r.BatchSizeMean.Mean:F3},{r.BatchSizeMean.Ci95HalfWidth:F3}");
        }

        /// <summary>
        /// Console-friendly compact summary, one configuration per call.
        /// </summary>
        public static void PrintSummary(ReplicatedResult r)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Configuration: delay={r.Delay} ms, α={r.Alpha:F2}, " +
                              $"replications={r.Replications} ===");
            Console.WriteLine("Metric          |        Mean ± 95% CI half-width");
            Console.WriteLine("----------------+----------------------------------------");
            Console.WriteLine($"λ  (/s)         | {r.Lambda}");
            Console.WriteLine($"μ  (/s)         | {r.Mu}");
            Console.WriteLine($"R̄  (ms)         | {r.ReadinessMeanMs}");
            Console.WriteLine($"δ̄  (ms)         | {r.HolMeanMs}");
            Console.WriteLine($"D̄  (ms)         | {r.EndToEndMeanMs}");
            Console.WriteLine($"P99(D) (ms)     | {r.EndToEndP99Ms}");
            Console.WriteLine($"Queue length    | {r.QueueLengthMean}");
            Console.WriteLine($"Batch size B    | {r.BatchSizeMean}");
        }
    }
}