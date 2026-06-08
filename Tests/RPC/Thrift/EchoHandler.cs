using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.RPC.ThriftServer;

using Echo.ThriftModel;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

// using Thrift;                // only needed if you reference Thrift exceptions directly

public sealed class EchoHandler : Echo.ThriftModel.EchoService.IAsync
{
    public Task<byte[]> EchoBytes(byte[] data, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(data);
    }

    public Task<List<BusinessDocument>> EchoDocuments(List<BusinessDocument> docs, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(docs);
    }

    public Task<List<int>> EchoIntArray(List<int> array, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(array);
    }

    public Task<List<string>> EchoStringArray(List<string> array, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(array);
    }

    public Task<Dictionary<string, BusinessDocument>> EchoMap(Dictionary<string, BusinessDocument> map, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(map);
    }


    Task<List<DocType>> EchoService.IAsync.EchoEnumArray(List<DocType> docTypes, CancellationToken cancellationToken)
    {
        return Task.FromResult(docTypes);
    }
}
