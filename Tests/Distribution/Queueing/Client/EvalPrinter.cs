using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Queueing.Client
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

}
