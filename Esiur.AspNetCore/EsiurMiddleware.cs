/*

MIT License

Copyright (c) 2017-2024 Esiur Foundation, Ahmed Kh. Zamil.

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

            if (context.WebSockets.IsWebSocketRequest 
                && context.WebSockets.WebSocketRequestedProtocols.Contains("iip"))
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync("iip");
                var socket = new FrameworkWebSocket(webSocket);
                var iipConnection = new DistributedConnection();
                server.Add(iipConnection);
                iipConnection.Assign(socket);
                socket.Begin();

                // @TODO: Change this
                while (webSocket.State == WebSocketState.Open)
                    await Task.Delay(500);
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
