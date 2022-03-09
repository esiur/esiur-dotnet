using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net.IIP;
namespace Test {
public class MyResource : DistributedResource {
public MyResource(DistributedConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) {}
public MyResource() {}
public string Description {
get => (string)properties[0];
set =>  _Set(0, value);
}
public int CategoryId {
get => (int)properties[1];
set =>  _Set(1, value);
}

}
}
