using System.Diagnostics;
using Esiur.Analysis.DSP;
using Esiur.Analysis.Signals.Codes;

namespace Esiur.Analysis.Test
{
    internal static class Program
    {
        private const int V = -1;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var signalA = new double[] { 1, 1, 1, V, 1, 1, V, V, 1, V, 1, V, V, V, V };
            var signalB = new double[] { 1, V, V, V, V, 1, V, V, V, V, 1, V, V, V, V };
             var cor = signalA.CrossCorrelation(signalB, true);
            Debug.WriteLine(cor);

            var seq = Generators.GenerateSequence(1, 7);

            
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new FSoft());
        }
    }
}