// Echo.JsonRpc/Program.cs (Server)
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Esiur.Tests.RPC.JsonServer;
using System.Text.Json;
using System.Text.Json.Serialization;


var app = WebApplication.Create(args);
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

app.MapPost("/rpc", async (HttpRequest req) =>
{
    var rpc = await JsonSerializer.DeserializeAsync<JsonRpcReq>(req.Body, json);
    object? result = null;

    //switch (rpc!.Method)
    //{
    //    case "EchoBytes": result = rpc.Params.Deserialize<byte[]>(json); break;
    //    case "EchoDocuments": result = rpc.Params!.Value.GetProperty("docs").Deserialize<BusinessDocument[]>(json); break;
    //    case "EchoIntArray": result = rpc.Params!.Value.GetProperty("array").Deserialize<int[]>(json); break;
    //    case "EchoStringArray": result = rpc.Params!.Value.GetProperty("array").Deserialize<string[]>(json); break;
    //    case "EchoMap": result = rpc.Params!.Value.GetProperty("map").Deserialize<Dictionary<string, BusinessDocument>>(json); break;
    //    case "EchoEnumArray":
    //        var arr = rpc.Params!.Value.GetProperty("docTypes").Deserialize<DocType[]>(json)!;
    //        result = (arr.Length == 0) ? DocType.Quote : arr[^1];
    //        break;
    //    default: return Results.BadRequest();
    //}

    return Results.Json(new JsonRpcRes { Jsonrpc = "2.0", Id = rpc.Id, Result = rpc.Params }, json);
});

app.Urls.Add("http://0.0.0.0:5100");

app.Run();

record JsonRpcReq(string Jsonrpc, string Method, object Params, string Id);
record JsonRpcRes { public string Jsonrpc { get; init; } = "2.0"; public string Id { get; init; } = "1"; public object? Result { get; init; } }
