using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Scheduling
{
    public class Process
    {
        public double Burst { get; set; }
        public double Arrival { get; set; }
        public double WaitTime => StartTime - Arrival;
        public double StartTime { get; set; }

        public int Priority { get; set; }

        public string Title { get; set; }

        public override string ToString()
        {
            return $"{Title} - Arrival: {Arrival} Priority: {Priority} WaitTime: {WaitTime} Burst: {Burst} Start: {StartTime}";
        }
    }

    public class SPF
    {


        public static void Schedule(Process[] processes)
        {

            processes = processes.OrderBy(x => x.Burst).ToArray();

            processes[0].StartTime = processes[0].Arrival;

            // Calculation of Waiting Times
            for (int i = 1; i < processes.Length; i++)
            {
                if (processes[i - 1].StartTime + processes[i - 1].Burst > processes[i].Arrival)
                    processes[i].StartTime = processes[i - 1].Arrival + processes[i - 1].Burst - processes[i].Arrival;

            }

        }

        public static Process[] ScheduleHybrid(Process[] processes)
        {

            processes = processes.OrderBy(x => x.Arrival)
                                        .ThenBy(x => x.Priority)
                                        .ThenBy(x=>x.Burst)
                                        .ToArray();

            processes[0].StartTime = processes[0].Arrival;

            // Calculation of StartTime.
            // WaitTime = StartTime - ArrivalTime 
            for (int i = 1; i < processes.Length; i++)
            {
                if (processes[i - 1].StartTime + processes[i - 1].Burst > processes[i].Arrival)
                    processes[i].StartTime = processes[i - 1].StartTime + processes[i - 1].Burst;
                else
                    processes[i].StartTime = processes[i].Arrival;
            }

            return processes;

        }

    }

}
