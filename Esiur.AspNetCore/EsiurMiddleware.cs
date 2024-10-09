
using Esiur.Net.IIP;
using Esiur.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;

namespace Esiur.AspNetCore
{
    public class EsiurMiddleware 
    {
        readonly DistributedServer server;
        readonly RequestDelegate next;
        readonly ILoggerFactory loggerFactory;

        public async Task InvokeAsync(HttpContext context)
        {
            var buffer = new ArraySegment<byte>(new byte[10240]);

            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync("iip");
                var socket = new FrameworkWebSocket(webSocket);
                var iipConnection = new DistributedConnection();
                server.Add(iipConnection);
                iipConnection.Assign(socket);
                socket.Begin();

                while (webSocket.State == WebSocketState.Open) ;
            }
            else
            {
                await next(context);
            }

        }


        public EsiurMiddleware(RequestDelegate next, IOptions<EsiurOptions> options, ILoggerFactory loggerFactory)
        {
            this.server = options.Value.Server;
            this.loggerFactory = loggerFactory;
            this.next = next;
        }
    }
}
