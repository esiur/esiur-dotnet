using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.RPC.ThriftServer;
public static class LoggingHelper
{
    public static ILoggerFactory LogFactory { get; } = LoggerFactory.Create(builder => {
        ConfigureLogging(builder);
    });

    public static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.SetMinimumLevel(LogLevel.Trace);
        logging.AddConsole();
        logging.AddDebug();
    }

    public static ILogger<T> CreateLogger<T>() => LogFactory.CreateLogger<T>();
}