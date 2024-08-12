
using Esiur.Data;
using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics;
using System.Reflection;

static void Main(string[] args)
{

    if (args.Length > 0)
    {
        if (args[0].ToLower() == "get-template" && args.Length >= 2)
        {
            try
            {
                var url = args[1];

                var parameters = GetParams(args, 2);


                var username = args.ElementAtOrDefault(3);
                var password = args.ElementAtOrDefault(4);
                var asyncSetters = Convert.ToBoolean(args.ElementAtOrDefault(5));

                var path = Esiur.Proxy.TemplateGenerator.GetTemplate(url,
                    parameters["-d"] ?? parameters["--dir"],
                    parameters["-u"] ?? parameters["--username"],
                    parameters["-p"] ?? parameters["--password"],
                    parameters["-d"] ?? parameters["--dir"]
                    parameters.Contains ["--a"] ?? parameters["--dir"]);

                Console.WriteLine($"Generated successfully: {path}");

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }
        }
    }

    PrintHelp();
}

static StringKeyList GetParams(string[] args, int offset)
{
    var rt = new StringKeyList();
    for(var i = offset; i< args.Length; i+=2)
    {
        var v = args.Length >= (i + 1) ? args[i+1] : null;
        rt.Add(args[i], v);
    }

    return rt;
}

static void PrintHelp()
{
    var version = FileVersionInfo.GetVersionInfo(typeof(Esiur.Core.AsyncReply).Assembly.Location).FileVersion;


    Console.WriteLine("Usage: <command> [arguments]");
    Console.WriteLine("");
    Console.WriteLine("Available commands:");
    Console.WriteLine("\tget-template\t\tGet a template from an IIP link.");
    Console.WriteLine("\tversion\t\tPrint Esiur version.");
    Console.WriteLine("");
    Console.WriteLine("Global options:");
    Console.WriteLine("\t-u, --username\tAuthentication username.");
    Console.WriteLine("\t-p, --password\tAuthentication password.");
    Console.WriteLine("\t-d, --dir\tName of the directory to generate model inside.");

}