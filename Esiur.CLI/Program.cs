
using CommandLine;
using Esiur.CLI;
using Esiur.Data;
using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics;
using System.IO;
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

                Parser.Default.ParseArguments<GetTemplateOptions>(args.Skip(2))
                                   .WithParsed<GetTemplateOptions>(o =>
                                    {
                                        try
                                        {
                                            var path = Esiur.Proxy.TemplateGenerator.GetTemplate(url, o.Dir, o.Username, o.Password, o.AsyncSetters);
                                            Console.WriteLine($"Generated successfully: {path}");
                                        }
                                        catch (Exception ex) { 
                                            Console.WriteLine(ex.ToString());
                                        }
                                    });
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }
        }
        else if (args[0].ToLower() == "version")
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Esiur.Core.AsyncReply).Assembly.Location).FileVersion;

            Console.WriteLine(version);
        }
    }

    PrintHelp();
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
    Console.WriteLine("\t-d, --dir\tDirectory name where the generated models will be saved.");
    Console.WriteLine("\t-a, --async-setters\tUse asynchronous property setters.");

}