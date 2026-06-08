using Thrift.Server;
using Thrift.Protocol;
using Thrift.Transport.Server;
using Esiur.Tests.RPC.ThriftServer;

var handler = new EchoHandler();
var processor = new Echo.ThriftModel.EchoService.AsyncProcessor(handler);

var port = 5400;

var serverTransport = new TServerSocketTransport(port, new Thrift.TConfiguration());
var server = new TSimpleAsyncServer(processor, serverTransport, new TBinaryProtocol.Factory(), new TBinaryProtocol.Factory(),
    LoggingHelper.LogFactory);

Console.WriteLine($"Thrift server listening on port {port}...");
await server.ServeAsync(new CancellationToken());
