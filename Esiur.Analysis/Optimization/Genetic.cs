using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Analysis.Optimization
{
    public class Genetic<T> where T : unmanaged
    {

        Random rand = new Random();

        public static unsafe byte[] Encode(in T value)
        {
            byte[] result = new byte[sizeof(T)];
            fixed (byte* dst = result)
                *(T*)dst = value;
            return result;
        }

        public static unsafe T Decode(byte[] data)
        {
            fixed (byte* src = data)
                return *(T*)src;
        }

        public List<T> Population = new List<T>();

        public int PopulationSize { get; set; }
        private int DataSize { get; set; }

        public Func<T, double> FitnessFunction { get; set; }

        public unsafe Genetic(int populationSize, Func<T, double> fitnessFunction)
        {
            FitnessFunction = fitnessFunction;
            PopulationSize = populationSize;
            DataSize = sizeof(T);
        }

        void GeneratePopultation()
        {
            for (var i = 0; i < PopulationSize; i++)
            {
                byte[] buffer = new byte[DataSize];
                rand.NextBytes(buffer);
                var record = Decode(buffer);
                Population.Add(record);
            }
        }


        KeyValuePair<T, double>[] GetFitness()
        {
            //var rt = new List<KeyValuePair<T, double>>();
            var rt = new ConcurrentBag<KeyValuePair<T, double>>();


            Parallel.ForEach(Population, record =>
            {
                rt.Add(new KeyValuePair<T, double>(record, FitnessFunction(record)));

            });

            //foreach (var record in Population)
            //    rt.Add(new KeyValuePair<T, double>( record, FitnessFunction(record)));

            return rt.ToArray();
        }

        T Mate(T parent1, T parent2)
        {
            var dp1 = Encode(parent1);
            var dp2 = Encode(parent2);

            var dc = new byte[dp1.Length];


            for (var i = 0; i < dc.Length; i++)
            {
                var prop = rand.NextDouble();
                if (prop < 0.45)
                    dc[i] = dp1[i];
                else if (prop < 0.9)
                    dc[i] = dp2[i];
                else
                    dc[i] = (byte)rand.Next(0, 255);
            }

            return Decode(dc);

        }

        public IEnumerable<(int, double, T)> Evaluate(int maxIterations)
        {
            GeneratePopultation();

            var generation = 0;

            KeyValuePair<T, double> best;

            do
            {
                var ordered = GetFitness().OrderBy(x => x.Value).ToArray();

                best = ordered[0];
                
                if (best.Value == 0)
                    break;

                yield return (generation, best.Value, best.Key);

                // Elitism selection ( 10% of fittest population )

                var eliteCount = (int)(ordered.Length * 0.1);
                var neededCount = (int)(ordered.Length * 0.9);

                var newGeneration = ordered.Select(x => x.Key).Take(eliteCount).ToList();

                // The rest 90% will be generated from mating the top 50% of the current poplulation
                for (var i = 0; i < neededCount; i++)
                {
                    var p1 = Population[rand.Next(0, PopulationSize / 2)];
                    var p2 = Population[rand.Next(0, PopulationSize / 2)];

                    var offspring = Mate(p1, p2);
                    newGeneration.Add(offspring);
                }

                Population = newGeneration;

                Debug.WriteLine($"Gen {generation} Fittest: {ordered.First().Value} {ordered.First().Key.ToString()} ");

                
            } while (generation++ < maxIterations);

            Debug.WriteLine($"Gen {generation} Best: {best.ToString()} ");

            yield return (generation, best.Value, best.Key);
        }

    }
}
