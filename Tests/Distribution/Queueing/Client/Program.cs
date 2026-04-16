// ============================================================
// Test 4: Fork-Join Queueing Test — CLIENT NODE
//
// Usage: dotnet run -- --host 127.0.0.1 --port 10901 --trials 10000
// ============================================================

using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Tests.Queueing.Client;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;

var results = new List<EsiurQueueEval.EvalResult>();
int counter = 0;



int currentAlpha = 0;
int currentDelay = 0;

var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10901"));
var trials = int.Parse(GetArg(args, "--trials", "1000"));
var delays = GetArg(args, "--delays", "5:8:10:20:30:100")
                  .Split(":").Select(x => Convert.ToInt32(x)).ToArray();
var alphas = GetArg(args, "--alphas", "0.0:0.25:0.5:0.75:1")
                  .Split(":").Select(y => Convert.ToDouble(y)).ToArray();


Console.WriteLine($"[Client-T2] Connecting to {host}:{port}, trials={trials}");

var wh = new Warehouse();

var serviceResource = await wh.Get<EpResource>(
                $"ep://{host}:{port}/sys/queueing");

var service = (dynamic)serviceResource;

serviceResource.PropertyChanged += Service_PropertyChanged;



Console.WriteLine("Starting test: Delay=" + delays[currentDelay] + " Alpha=" + alphas[currentAlpha]);

service.StartUpdatesLocal(delays[currentDelay], trials, alphas[currentAlpha]);

await Task.Delay(-1);


void Service_PropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    counter++;

    if (counter == trials)
    {
        var queue = service.DistributedResourceConnection.GetFinishedQueue();
        var result = EsiurQueueEval.Evaluate(queue);

        Console.WriteLine(result);
        counter = 0;

        if (currentAlpha == alphas.Length - 1)
        {
            currentAlpha = 0;
            currentDelay++;
        }
        else
        {
            currentAlpha++;
        }

        if (currentDelay == delays.Length)
        {
            System.Environment.Exit(0);
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Starting next test: Delay=" + delays[currentDelay] + " Alpha=" + alphas[currentAlpha]);

        service.StartUpdatesLocal(delays[currentDelay], trials, alphas[currentAlpha]);//, 0, resourceLink);

    }
}

static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
