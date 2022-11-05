using Esiur.Analysis.DSP;
using Esiur.Analysis.Fuzzy;
using Esiur.Analysis.Optimization;
using Esiur.Analysis.Signals;
using Esiur.Analysis.Units;
using Microsoft.VisualBasic.Logging;
using ScottPlot;
using ScottPlot.Drawing.Colormaps;
using System.Security.Cryptography;
using Esiur.Analysis.Statistics;
using System.Diagnostics;

namespace Esiur.Analysis.Test
{
    public partial class FMain : Form
    {
        public FMain()
        {
            InitializeComponent();

            //var outage = Capacity.ComputeOutage(20000000, new Capacity.CSI[]
            //{
            //    new Capacity.CSI(PowerUnit.FromDb(20), 0.1),
            //    new Capacity.CSI(PowerUnit.FromDb(15), 0.15),
            //    new Capacity.CSI(PowerUnit.FromDb(10), 0.25),
            //    new Capacity.CSI(PowerUnit.FromDb(5), 0.25),
            //    new Capacity.CSI(PowerUnit.FromDb(0), 0.15),
            //    new Capacity.CSI(PowerUnit.FromDb(-5), 0.1),
            //});
            var outage = Capacity.ComputeOutage(1, new Capacity.CSI[]
            {
                new Capacity.CSI(PowerUnit.FromDb(30), 0.2),
                new Capacity.CSI(PowerUnit.FromDb(20), 0.3),
                new Capacity.CSI(PowerUnit.FromDb(10), 0.3),
                new Capacity.CSI(PowerUnit.FromDb(0), 0.2),
             });


            var low = new ContinuousSet(MembershipFunctions.Descending(20, 40));
            var mid = new ContinuousSet(MembershipFunctions.Triangular(20, 40, 60));
            var high = new ContinuousSet(MembershipFunctions.Ascending(40, 60));

            var bad = new ContinuousSet(MembershipFunctions.Descending(0, 30));
            var ok = new ContinuousSet(MembershipFunctions.Triangular(20, 50, 80));
            var excelent = new ContinuousSet(MembershipFunctions.Ascending(70, 100));

            var small = new ContinuousSet(MembershipFunctions.Descending(100, 200));
            var avg = new ContinuousSet(MembershipFunctions.Triangular(100, 200, 300));
            var big = new ContinuousSet(MembershipFunctions.Ascending(200, 300));

            //var speedIsLowThenSmall = new FuzzyRule("Low=>Small", low, small);

            double rating = 80;

            for (double temp = 60; temp < 100; temp++)
            {
                var v = MamdaniDefuzzifier.Evaluate(new INumericalSet<double>[]
                {
                temp.Is(low).And(rating.Is(bad)).Then(small),
                temp.Is(mid).And(rating.Is(ok)).Then(avg),
                temp.Is(high).And(rating.Is(excelent)).Then(big),
                }, MamdaniDefuzzifierMethod.CenterOfGravity, 100, 300, 1);

            }

        }

        private void FMain_Load(object sender, EventArgs e)
        {

            var lowErr = new ContinuousSet(MembershipFunctions.Descending(-5, 4.5));
            var midErr = new ContinuousSet(MembershipFunctions.Triangular(-4, 6.5, 16.5));
            var highErr = new ContinuousSet(MembershipFunctions.Ascending(8, 18));

            var lowAccErr = new ContinuousSet(MembershipFunctions.Descending(0, 0.02));
            var midAccErr = new ContinuousSet(MembershipFunctions.Triangular(0.02, 0.04, 0.06));
            var highAccErr = new ContinuousSet(MembershipFunctions.Ascending(0.04, 0.06));

            var small = new ContinuousSet(MembershipFunctions.Descending(0.1, 0.5));
            var avg = new ContinuousSet(MembershipFunctions.Triangular(0.1, 0.5, 1.1));
            var big = new ContinuousSet(MembershipFunctions.Ascending(-10, 1.1));


            var x = Enumerable.Range(0, 1000).Select(x => x * 0.01).ToArray();


            var step = Enumerable.Repeat(1, 1000).Select(x => (double)x).ToArray();
            step[0] = 0;

            var motor = new TransferFunction(new double[] { 1, 2 }, new double[] { 1, 1, 2 }, 0.01);
            var motorPID = new TransferFunction(new double[] { 1, 2 }, new double[] { 1, 1, 2 }, 0.01);
            var motorFuzzyPID = new TransferFunction(new double[] { 1, 2 }, new double[] { 1, 1, 2 }, 0.01);

            //double Kp = 2, Ki = 0.4, Kd = 0.4;

            double Ki = -1.9181372, Kp = 18.625, Kd = 0.38281253;


            var pid = new TransferFunction(new double[] { Kd, Kp, Ki }, new double[] { 1, 1 }, 0.01);
            var fuzzyPID = new TransferFunction(new double[] { Kd, Kp, Ki }, new double[] { 1, 1 }, 0.01);


            var sysOut = new double[step.Length];
            var sysOutFuzzyPID = new double[step.Length];
            var sysOutPID = new double[step.Length];

            var pidOut = new double[step.Length];
            var pidOutFuzzy = new double[step.Length];
            var errorOutPID = new double[step.Length];

            var errOut = new double[step.Length];
            var errAccOut = new double[step.Length];

            //var errorAccOut = new double[step.Length];

            var errorOutFuzzy = new double[step.Length];
            var errorOutAccFuzzy = new double[step.Length];

            for (var i = 0; i < step.Length; i++)
            {
                sysOut[i] = motor.Evaluate(step[i]);
                errOut[i] = step[i] - sysOut[i];
                errAccOut[i] = errOut[i] - (i == 0 ? 0 : errOut[i - 1]);

                sysOutPID[i] = motorPID.Evaluate(step[i] + (i == 0 ? 0 : pidOut[i - 1]));
                sysOutFuzzyPID[i] = motorFuzzyPID.Evaluate(step[i] + (i == 0 ? 0 : pidOutFuzzy[i - 1]));


                errorOutPID[i] = (step[i] - sysOutPID[i]);
                errorOutFuzzy[i] = (step[i] - sysOutFuzzyPID[i]);
                errorOutAccFuzzy[i] = (errorOutFuzzy[i] - (i == 0 ? 0 : errorOutFuzzy[i - 1]));

                pidOut[i] = pid.Evaluate(errorOutPID[i]);
                pidOutFuzzy[i] = fuzzyPID.Evaluate(errorOutFuzzy[i]);


                var k = MamdaniDefuzzifier.Evaluate(new INumericalSet<double>[]
                {
                    errorOutFuzzy[i].Is(lowErr).And(errorOutAccFuzzy[i].Is(lowAccErr)).Then(small),
                    errorOutFuzzy[i].Is(lowErr).And(errorOutAccFuzzy[i].Is(midAccErr)).Then(small),
                    errorOutFuzzy[i].Is(lowErr).And(errorOutAccFuzzy[i].Is(highAccErr)).Then(avg),
                    errorOutFuzzy[i].Is(midErr).And(errorOutAccFuzzy[i].Is(lowAccErr)).Then(small),
                    errorOutFuzzy[i].Is(midErr).And(errorOutAccFuzzy[i].Is(midAccErr)).Then(avg),
                    errorOutFuzzy[i].Is(midErr).And(errorOutAccFuzzy[i].Is(highAccErr)).Then(big),
                    errorOutFuzzy[i].Is(highAccErr).And(errorOutAccFuzzy[i].Is(lowAccErr)).Then(avg),
                    errorOutFuzzy[i].Is(highAccErr).And(errorOutAccFuzzy[i].Is(midAccErr)).Then(big),
                    errorOutFuzzy[i].Is(highAccErr).And(errorOutAccFuzzy[i].Is(highAccErr)).Then(big),
                }, MamdaniDefuzzifierMethod.CenterOfGravity, 0, 1, 0.05);

                fuzzyPID.InputCoefficients[1] = k;
                fuzzyPID.InputCoefficients[1] = k;
                fuzzyPID.InputCoefficients[1] = k;
            }

            Debug.WriteLine($"Error Values Min: {errOut.Min()} Max: {errOut.Max()} ");
            Debug.WriteLine($"Error Acc Values Min: {errAccOut.Min()} Max: {errAccOut.Max()} ");

            formsPlot1.Plot.AddScatter(x, sysOut, Color.Red);
            formsPlot1.Plot.AddScatter(x, sysOutPID, Color.Blue);
            formsPlot1.Plot.AddScatter(x, sysOutFuzzyPID, Color.Green);

            formsPlot1.Refresh();

        }


        struct KK
        {
            public float Ki;
            public float Kp;
            public float Kd;

            public override string ToString()
            {
                return $"Ki {Ki} Kp {Kp} Kd {Kd}";
            }
        }

        struct FuzzyChromosome
        {
            public sbyte KiInputErrPosition;
            public sbyte KiInputErrScale;

            public sbyte KiInputErrAccPosition;
            public sbyte KiInputErrAccScale;

            public sbyte KiOutputPosition;
            public sbyte KiOutputScale;
        }

        private double CalculateFuzzyPIDStepError(FuzzyChromosome config, double errStart, double errEnd)
        {
            var errPos = config.KiInputErrPosition * 0.1;
            var errScale = config.KiInputErrPosition * 0.1;

            var lowErr = new ContinuousSet(MembershipFunctions.Descending(config.KiInputErrPosition * 0.1, config.kiLowStart * 0.1 + Math.Abs(config.kiLowEnd * 0.1)));
            var midErr = new ContinuousSet(MembershipFunctions.Triangular(config.KiInputErrPosition * 0.1, config.kiMidStart * 0.1 + Math.Abs(config.kiMidMid * 0.1), config.kiMidStart * 0.1 + Math.Abs(config.kiMidMid * 0.1) + Math.Abs(config.kiMidEnd * 0.1)));
            var highErr = new ContinuousSet(MembershipFunctions.Ascending(config.KiInputErrPosition * 0.1, config.kiHiStart * 0.1 + Math.Abs(config.kiHiEnd * 0.1)));

            var lowAccErr = new ContinuousSet(MembershipFunctions.Descending(config.KiInputErrAccPosition * 0.1, Math.Abs(config.kiLowAccStart * 0.1) + Math.Abs(config.kiLowAccEnd * 0.1)));
            var midAccErr = new ContinuousSet(MembershipFunctions.Triangular(config.KiInputErrAccPosition * 0.1, Math.Abs(config.kiMidAccStart * 0.1) + Math.Abs(config.kiMidAccMid * 0.1), config.kiMidAccStart * 0.1 + Math.Abs(config.kiMidAccMid * 0.1) + Math.Abs(config.kiMidAccEnd * 0.1)));
            var highAccErr = new ContinuousSet(MembershipFunctions.Ascending(config.KiInputErrAccPosition * 0.1, config.kiHiAccStart * 0.1 + Math.Abs(config.kiHiAccEnd * 0.1)));

            var small = new ContinuousSet(MembershipFunctions.Descending(config.kiSmallStart * 0.1, config.kiSmallStart * 0.1 + Math.Abs(config.kiSmallEnd * 0.1)));
            var avg = new ContinuousSet(MembershipFunctions.Triangular(config.kiAvgStart * 0.1,  config.kiAvgStart * 0.1 + Math.Abs(config.kiAvgMid * 0.1), config.kiAvgStart * 0.1 + Math.Abs(config.kiAvgMid * 0.1) + Math.Abs(config.kiAvgEnd * 0.1)));
            var big = new ContinuousSet(MembershipFunctions.Ascending(config.kiBigStart * 0.1, config.kiBigStart * 0.1 + Math.Abs(config.kiBigEnd * 0.1)));

            double Ki = -1.9181372, Kp = 18.625, Kd = 0.38281253;

            var step = Enumerable.Repeat(1, 1000).Select(x => (double)x).ToArray();
            step[0] = 0;

            var motor = new TransferFunction(new double[] { 1, 2 }, new double[] { 1, 1, 2 }, 0.01);
            var fuzzyPID = new TransferFunction(new double[] { Kd, Kp, Ki }, new double[] { 1, 1 }, 0.01);

            var sysOutFuzzyPID = new double[step.Length];

            var pidOut = new double[step.Length];
            var pidOutFuzzy = new double[step.Length];

            var errorOutFuzzy = new double[step.Length];
            var errorOutAccFuzzy = new double[step.Length];

            for (var i = 0; i < step.Length; i++)
            {
                sysOutFuzzyPID[i] = motor.Evaluate(step[i] + (i == 0 ? 0 : pidOutFuzzy[i - 1]));


                errorOutFuzzy[i] = (step[i] - sysOutFuzzyPID[i]);
                errorOutAccFuzzy[i] = (errorOutFuzzy[i] - (i == 0 ? 0 : errorOutFuzzy[i - 1]));

                pidOutFuzzy[i] = fuzzyPID.Evaluate(errorOutFuzzy[i]);


                var k = MamdaniDefuzzifier.Evaluate(new INumericalSet<double>[]
                {
                    errorOutFuzzy[i].Is(lowErr).And(errorOutAccFuzzy[i].Is(lowAccErr)).Then(small),
                    errorOutFuzzy[i].Is(lowErr).And(errorOutAccFuzzy[i].Is(midAccErr)).Then(small),
                    errorOutFuzzy[i].Is(lowErr).And(errorOutAccFuzzy[i].Is(highAccErr)).Then(avg),
                    errorOutFuzzy[i].Is(midErr).And(errorOutAccFuzzy[i].Is(lowAccErr)).Then(small),
                    errorOutFuzzy[i].Is(midErr).And(errorOutAccFuzzy[i].Is(midAccErr)).Then(avg),
                    errorOutFuzzy[i].Is(midErr).And(errorOutAccFuzzy[i].Is(highAccErr)).Then(big),
                    errorOutFuzzy[i].Is(highAccErr).And(errorOutAccFuzzy[i].Is(lowAccErr)).Then(avg),
                    errorOutFuzzy[i].Is(highAccErr).And(errorOutAccFuzzy[i].Is(midAccErr)).Then(big),
                    errorOutFuzzy[i].Is(highAccErr).And(errorOutAccFuzzy[i].Is(highAccErr)).Then(big),
                }, MamdaniDefuzzifierMethod.CenterOfGravity, -100, 100, 0.5);

                fuzzyPID.InputCoefficients[1] = k;
                //fuzzyPID.InputCoefficients[1] = k;
                //fuzzyPID.InputCoefficients[1] = k;
            }

            return errorOutFuzzy.RMS();

        }

        private double CalculatePIDStepError(double Kd, double Kp, double Ki)
        {
            var step = Enumerable.Repeat(1, 1000).Select(x => (double)x).ToArray();
            step[0] = 0;

            var motor = new TransferFunction(new double[] { 1, 2 }, new double[] { 1, 1, 2 }, 0.01);

            var sysOutPID = new double[step.Length];

            var pidOut = new double[step.Length];
            var errorOutPID = new double[step.Length];
            var pid = new TransferFunction(new double[] { Kd, Kp, Ki }, new double[] { 1, 1 }, 0.01);

            for (var i = 0; i < step.Length; i++)
            {
                sysOutPID[i] = motor.Evaluate(step[i] + (i == 0 ? 0 : pidOut[i - 1]));

                if (double.IsInfinity(sysOutPID[i]))
                    Console.WriteLine();

                errorOutPID[i] = (step[i] - sysOutPID[i]);

                if (double.IsNaN(errorOutPID[i]))
                    Console.WriteLine();

                pidOut[i] = pid.Evaluate(errorOutPID[i]);

                if (double.IsInfinity(pidOut[i]))
                    Console.WriteLine();

            }

            return errorOutPID.RMS();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            //var gen = new Genetic<KK>(100, k =>
            //{
            //    if (float.IsNaN(k.Ki) || float.IsNaN(k.Kp) || float.IsNaN(k.Kd))
            //        return (double.MaxValue);


            //    var r = CalculatePIDStepError(k.Kd, k.Kp, k.Ki);
            //    if (double.IsNaN(r))
            //        Console.WriteLine();
            //    return r;
            //});

            var gen = new Genetic<KKFF>(100, k =>
            {
                if (float.IsNaN(k.kiAvgEnd)
                || float.IsNaN(k.kiAvgMid)
                || float.IsNaN(k.kiAvgStart)
                || float.IsNaN(k.kiBigEnd)
                || float.IsNaN(k.kiBigStart)
                || float.IsNaN(k.kiHiAccEnd)
                || float.IsNaN(k.kiHiAccStart)
                || float.IsNaN(k.kiHiEnd)
                || float.IsNaN(k.kiHiStart)
                || float.IsNaN(k.kiLowAccEnd)
                || float.IsNaN(k.kiLowAccStart)
                || float.IsNaN(k.kiLowEnd)
                || float.IsNaN(k.kiLowStart)
                || float.IsNaN(k.kiMidAccEnd)
                || float.IsNaN(k.kiMidAccMid)
                || float.IsNaN(k.kiMidAccStart)
                || float.IsNaN(k.kiMidEnd)
                || float.IsNaN(k.kiMidMid)
                || float.IsNaN(k.kiMidStart)
                || float.IsNaN(k.kiSmallEnd)
                || float.IsNaN(k.kiSmallStart))
                    return (double.MaxValue);


                var r = CalculateFuzzyPIDStepError(k);
                if (double.IsNaN(r))
                    Console.WriteLine();
                return r;
            });


            var ev = gen.Evaluate(1000);

            Console.WriteLine(ev);
        }
    }
}