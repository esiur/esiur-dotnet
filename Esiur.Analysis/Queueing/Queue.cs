/*
 
Copyright (c) 2023 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Analysis.Statistics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Esiur.Analysis.Queueing
{
    public enum ProcessType
    {
        Markovian
    }

    public enum Discipline
    {

    }

    public class Queue
    {
        public ProcessType ArrivalProcess { get; set; }
        public ProcessType ServiceProcess { get; set; }

        public double ArrivalRate { get; set; }
        public double VisitRatio { get; set; }  // for Mean Value Analysis
        public double ServiceRate { get; set; }

        public int Capacity { get; set; }
        public int Servers { get; set; }
        public int Population { get; set; }

        public string Label { get; set; }
        
        public double TrafficIntensity => Servers > 0 ? ArrivalRate / (Servers * ServiceRate) : ArrivalRate / ServiceRate;

        public double Pie(int n)
        {
            var rho = TrafficIntensity;

            // MM1
            if (Servers == 1 && Capacity == 0 && Population == 0)
                return Math.Pow(rho, n) * (1 - rho);

            // M/M/1/K
            if (Servers == 1 && Capacity == 1 && Population == 0)
            {
                if (rho == 1)
                    return Math.Pow(rho, n) * (1 / (Servers + Capacity + 1));
                else
                    return Math.Pow(rho, n) * ((1 - rho) / (1 - Math.Pow(rho, Servers + Capacity + 1)));
            }

            // M/M/Infinity
            if (Servers == 0 && Capacity == 0 && Population == 0)
                return (Math.Pow(rho, n) / n.Factorial()) * Math.Exp(-rho);

            // M/M/C
            if (Servers > 0 && Capacity > 0 && Population ==0)
            {
                double pie0 = 1;
                for (var i = 1; i < Capacity - 1; i++)
                    pie0 += Math.Pow(Servers * rho, i) / i.Factorial();
                pie0 += Math.Pow(Servers * rho, Servers) / (Servers.Factorial() * (1 - rho));

                pie0 = 1 / pie0;

                return (Math.Pow(Servers * rho, n) / n.Factorial()) * pie0;
            }

            // M/M/C/K
            if (Servers > 0 && Capacity > 0 && Population > 0)
            {
                double pie0 = 1;
                
                for (var i = 1; i < Capacity - 1; i++)
                    pie0 += Math.Pow(Servers * rho, i) / i.Factorial();


                double cc = Math.Pow(Servers, Servers) / Servers.Factorial();

                for(var i = Servers; i <= Servers + Capacity; i++)
                    pie0 += cc * Math.Pow(rho, i);
                
                pie0 = 1 / pie0;

                return (Math.Pow(Servers * rho, n) / n.Factorial()) * pie0;

            }

            return 0;
        }

    }
}
