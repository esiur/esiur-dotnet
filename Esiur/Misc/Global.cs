/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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
using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using Esiur.Data;
using System.Collections.Generic;
//using Esiur.Net.Packets;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Linq;
using Esiur.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Esiur.Resource;
using System.Text.Json.Serialization;

namespace Esiur.Misc;
public static class Global
{
    private static KeyList<string, object> variables = new KeyList<string, object>();

    private static Random rand = new Random();// System.Environment.TickCount);



    public static KeyList<string, long> Counters = new KeyList<string, long>();

    public delegate void LogEvent(string service, LogType type, string message);

    public static event LogEvent SystemLog;


    public static string ToJson(this IResource resource)
    {
        try
        {
            return JsonSerializer.Serialize(resource, Global.SerializeOptions);
        }
        catch (Exception ex)
        {
            Log(ex);
            return "{}";
        }
    }

    public static JsonSerializerOptions SerializeOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented = true,
        Converters =
            {
                new ResourceJsonConverter(),
                new DoubleJsonConverter()
            }
    };



    public static string Version { get; } = typeof(Global).Assembly.GetName().Version.ToString(); //FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;


    public static void Log(Exception ex, params object[] arguments)
    {
        try
        {


            var stack = new StackTrace(ex, true);
            var frames = stack.GetFrames();
            var frame = frames?.FirstOrDefault();

            MethodBase? method = null;
            ParameterInfo[] parameters = Array.Empty<ParameterInfo>();
            string service = "Unknown";

            if (frame != null)
            {
                method = frame.GetMethod();
            }

            if (method == null)
            {
                // Fallback to TargetSite if available
                method = ex.TargetSite;
            }

            if (method != null)
            {
                parameters = method.GetParameters();
                service = method.DeclaringType != null ? method.DeclaringType.Name : method.Name;
            }
            var message = "";

            if (arguments.Length > 0 && parameters.Length > 0)
            {
                message = "Arguments ( ";

                for (int i = 0; i < parameters.Length && i < arguments.Length; i++)
                {
                    message += parameters[i].Name + ": " + arguments[i].ToString() + " ";
                }

                message += ")" + Environment.NewLine + "------------------------------------------------";
            }

            message += ex.ToString();

            Log(service, LogType.Error, message);



            Log(service, LogType.Error, ex.ToString());

        }
        catch
        {

        }
    }

    public static void Log(string service, LogType type, string message, bool appendHeader = true)
    {
        //if (type != LogType.Debug)
        Console.WriteLine(service + " " + message);

        SystemLog?.Invoke(service, type, message);
    }

 

    public static string RemoveControlCharacters(string inString)
    {
        if (inString == null) return null;

        StringBuilder newString = new StringBuilder();
        char ch;

        for (int i = 0; i < inString.Length; i++)
        {

            ch = inString[i];

            if (!char.IsControl(ch))
            {
                newString.Append(ch);
            }
        }
        return newString.ToString();
    }

    public static void PrintCounters()
    {
        string[] keys = new string[Counters.Keys.Count];
        Counters.Keys.CopyTo(keys, 0);

        foreach (string k in keys)
        {
            Console.WriteLine(k + ":" + Counters[k]);
        }
    }
  

     

    public static KeyList<string, object> Variables
    {
        get
        {
            return variables;
        }
    }


    public static uint CurrentUnixTime()
    {
        return (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
    }

    public static void SetConsoleColors(ConsoleColor ForegroundColor, ConsoleColor BackgroundColor)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            switch (ForegroundColor)
            {
                case ConsoleColor.Black:
                    Console.Write("\u001B[30m");
                    break;
                case ConsoleColor.Blue:
                    Console.Write("\u001B[1;34m");
                    break;
                case ConsoleColor.Cyan:
                    Console.Write("\u001B[1;36m");
                    break;
                case ConsoleColor.Gray:
                case ConsoleColor.DarkGray:
                    Console.Write("\u001B[1;30m");
                    break;
                case ConsoleColor.Green:
                    Console.Write("\u001B[1;32m");
                    break;
                case ConsoleColor.Magenta:
                    Console.Write("\u001B[1;35m");
                    break;
                case ConsoleColor.Red:
                    Console.Write("\u001B[1;31m");
                    break;
                case ConsoleColor.White:
                    Console.Write("\u001B[1;37m");
                    break;
                case ConsoleColor.Yellow:
                    Console.Write("\u001B[1;33m");
                    break;

                case ConsoleColor.DarkBlue:
                    Console.Write("\u001B[34m");
                    break;
                case ConsoleColor.DarkCyan:
                    Console.Write("\u001B[36m");
                    break;
                case ConsoleColor.DarkGreen:
                    Console.Write("\u001B[32m");
                    break;
                case ConsoleColor.DarkMagenta:
                    Console.Write("\u001B[35m");
                    break;
                case ConsoleColor.DarkRed:
                    Console.Write("\u001B[31m");
                    break;
                case ConsoleColor.DarkYellow:
                    Console.Write("\u001B[33m");
                    break;
            }


            switch (BackgroundColor)
            {
                case ConsoleColor.Black:
                    Console.Write("\u001B[40m");
                    break;
                case ConsoleColor.Blue:
                    Console.Write("\u001B[1;44m");
                    break;
                case ConsoleColor.Cyan:
                    Console.Write("\u001B[1;46m");
                    break;
                case ConsoleColor.Gray:
                case ConsoleColor.DarkGray:
                    Console.Write("\u001B[1;40m");
                    break;
                case ConsoleColor.Green:
                    Console.Write("\u001B[1;42m");
                    break;
                case ConsoleColor.Magenta:
                    Console.Write("\u001B[1;45m");
                    break;
                case ConsoleColor.Red:
                    Console.Write("\u001B[1;41m");
                    break;
                case ConsoleColor.White:
                    Console.Write("\u001B[1;47m");
                    break;
                case ConsoleColor.Yellow:
                    Console.Write("\u001B[1;43m");
                    break;

                case ConsoleColor.DarkBlue:
                    Console.Write("\u001B[44m");
                    break;
                case ConsoleColor.DarkCyan:
                    Console.Write("\u001B[46m");
                    break;
                case ConsoleColor.DarkGreen:
                    Console.Write("\u001B[42m");
                    break;
                case ConsoleColor.DarkMagenta:
                    Console.Write("\u001B[45m");
                    break;
                case ConsoleColor.DarkRed:
                    Console.Write("\u001B[41m");
                    break;
                case ConsoleColor.DarkYellow:
                    Console.Write("\u001B[43m");
                    break;
            }
        }
        else
        {
            Console.ForegroundColor = ForegroundColor;
            Console.BackgroundColor = BackgroundColor;
        }
    }

    

    /////////////////////////////////////

    /*
    public static bool IsUnix()
    {
        // Linux OSs under Mono 1.2 uses unknown integer numbers so this should identify all non windows as unix
        return (Environment.OSVersion.Platform != PlatformID.Win32NT
            && Environment.OSVersion.Platform != PlatformID.Win32Windows); // || Environment.OSVersion.Platform == PlatformID.Linux; 
    }
    */

    public static string GenerateCode()
    {
        return GenerateCode(16);
    }


    public static byte[] GenerateBytes(int length)
    {
        var b = new byte[length];
        rand.NextBytes(b);
        return b;
    }

    public static string GenerateCode(int length)
    {
        return GenerateCode(length, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"); 
    }

    public static string GenerateCode(int length, string chars)
    {
        var result = new string(
            Enumerable.Repeat(chars, length)
                      .Select(s => s[rand.Next(s.Length)])
                      .ToArray());
         return result;
     }
     

    public static Regex GetRouteRegex(string url)
    {
        var sc = Regex.Match(url, @"([^\{]*)\{([^\}]*)\}([^\{]*)");

        List<VarInfo> vars = new List<VarInfo>();

        while (sc.Success)
        {
            vars.Add(new VarInfo()
            {
                Pre = sc.Groups[1].Value,
                VarName = sc.Groups[2].Value,
                Post = sc.Groups[3].Value
            });
            sc = sc.NextMatch();
        }

        if (vars.Count > 0)
        {
            return new Regex("^" + String.Join("", vars.Select(x => x.Build()).ToArray()) + "$");
        }
        else
        {
            return new Regex("^" + Regex.Escape(url) + "$");
        }
    }
     
}
