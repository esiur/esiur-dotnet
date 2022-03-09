using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net.IIP;
namespace Test {
public class MyChildResource : Test.MyResource {
public MyChildResource(DistributedConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) {}
public MyChildResource() {}
public AsyncReply<int> ChildMethod(string childName) {
var rt = new AsyncReply<int>();
_InvokeByArrayArguments(0, new object[] { childName })
.Then(x => rt.Trigger((int)x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public string ChildName {
get => (string)properties[0];
set =>  _Set(0, value);
}

}
}
