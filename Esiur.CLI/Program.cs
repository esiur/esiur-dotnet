
/*

MIT License

Copyright (c) 2024 Esiur Foundation, Ahmed Kh. Zamil.

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

using CommandLine;
using Esiur.CLI;
using Esiur.Data;
using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;


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
                                        var path = Esiur.Proxy.TemplateGenerator.GetTemplate(url, o.Dir, false, o.Username, o.Password, o.AsyncSetters);
                                        Console.WriteLine($"Generated successfully: {path}");
                                    }
                                    catch (Exception ex)
                                    {
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

        Console.WriteLine("Esiur " + version);
        return;
    }
}

PrintHelp();


static void PrintHelp()
{
    var version = FileVersionInfo.GetVersionInfo(typeof(Esiur.Core.AsyncReply).Assembly.Location).FileVersion;


    Console.WriteLine("Usage: <command> [arguments]");
    Console.WriteLine("");
    Console.WriteLine("Available commands:");
    Console.WriteLine("\tget-template\tGet a template from an IIP link.");
    Console.WriteLine("\tversion\t\tPrint Esiur version.");
    Console.WriteLine("");
    Console.WriteLine("Global options:");
    Console.WriteLine("\t-u, --username\t\tAuthentication username.");
    Console.WriteLine("\t-p, --password\t\tAuthentication password.");
    Console.WriteLine("\t-d, --dir\t\tDirectory name where the generated models will be saved.");
    Console.WriteLine("\t-a, --async-setters\tUse asynchronous property setters.");

}