// Echo.SignalR/EchoHub.cs
using Microsoft.AspNetCore.SignalR;
namespace Esiur.Tests.RPC.SignalRServer;

public class EchoHub : Hub
{
    public byte[] EchoBytes(byte[] data) => data;
    public BusinessDocument[] EchoDocuments(BusinessDocument[] docs) => docs;
    public int[] EchoIntArray(int[] array) => array;
    public string[] EchoStringArray(string[] array) => array;
    public Dictionary<string, BusinessDocument> EchoMap(Dictionary<string, BusinessDocument> map) => map;
    public DocType EchoEnumArray(DocType[] docTypes) => docTypes.Length == 0 ? DocType.Quote : docTypes[^1];
}
