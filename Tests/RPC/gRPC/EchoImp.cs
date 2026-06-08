using Echo.Model.Grpc;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Echo.Model.Grpc.EchoService;

namespace Echo
{
    public class EchoServiceImpl : EchoServiceBase
    {
        public override Task<BytesResponse> EchoBytes(BytesRequest request, ServerCallContext context)
        {
            var rt = new BytesResponse();
            rt.Data = ByteString.CopyFrom(request.Data.ToArray());
            return Task.FromResult(rt);
        }

        public override Task<DocumentsResponse> EchoDocuments(DocumentsRequest request, ServerCallContext context)
        {
            var rt = new DocumentsResponse();
            rt.Docs.AddRange(request.Docs);
            return Task.FromResult(rt);
        }

        public override Task<EnumResponse> EchoEnumArray(EnumArrayRequest request, ServerCallContext context)
        {
            var rt = new EnumResponse();
            rt.DocTypes.AddRange(request.DocTypes);
            return Task.FromResult(rt);
        }

        public override Task<IntArrayResponse> EchoIntArray(IntArrayRequest request, ServerCallContext context)
        {
            var rt = new IntArrayResponse();
            rt.Array.AddRange(request.Array);
            return Task.FromResult(rt);
        }

        public override Task<DocMapResponse> EchoMap(DocMapRequest request, ServerCallContext context)
        {
            var rt = new DocMapResponse();
            foreach(var kv in request.Map)
                rt.Map.Add(kv.Key, kv.Value);

            return Task.FromResult(rt);
        }

        public override Task<StringArrayResponse> EchoStringArray(StringArrayRequest request, ServerCallContext context)
        {
            var rt = new StringArrayResponse();
            rt.Array.AddRange(request.Array);
            return Task.FromResult(rt); 
        }
    }
}