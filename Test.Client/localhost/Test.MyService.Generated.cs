using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net.IIP;
namespace Test {
public class MyService : DistributedResource {
public MyService(DistributedConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) {}
public MyService() {}
public AsyncReply<object> Void() {
var rt = new AsyncReply<object>();
_InvokeByArrayArguments(0, new object[] {  })
.Then(x => rt.Trigger((object)x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<object> InvokeEvents(string msg) {
var rt = new AsyncReply<object>();
_InvokeByArrayArguments(1, new object[] { msg })
.Then(x => rt.Trigger((object)x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<double> Optional(object a1,int a2,string a3) {
var rt = new AsyncReply<double>();
_InvokeByArrayArguments(2, new object[] { a1, a2, a3 })
.Then(x => rt.Trigger((double)x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<object> Connection(object a1,int a2) {
var rt = new AsyncReply<object>();
_InvokeByArrayArguments(3, new object[] { a1, a2 })
.Then(x => rt.Trigger((object)x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<object> ConnectionOptional(object a1,int a2,string a3) {
var rt = new AsyncReply<object>();
_InvokeByArrayArguments(4, new object[] { a1, a2, a3 })
.Then(x => rt.Trigger((object)x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<(int,string)> Tuple2(int a1,string a2) {
var rt = new AsyncReply<(int,string)>();
_InvokeByArrayArguments(5, new object[] { a1, a2 })
.Then(x => rt.Trigger(((int,string))x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<(int,string,double)> Tuple3(int a1,string a2,double a3) {
var rt = new AsyncReply<(int,string,double)>();
_InvokeByArrayArguments(6, new object[] { a1, a2, a3 })
.Then(x => rt.Trigger(((int,string,double))x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public AsyncReply<(int,string,double,bool)> Tuple4(int a1,string a2,double a3,bool a4) {
var rt = new AsyncReply<(int,string,double,bool)>();
_InvokeByArrayArguments(7, new object[] { a1, a2, a3, a4 })
.Then(x => rt.Trigger(((int,string,double,bool))x))
.Error(x => rt.TriggerError(x))
.Chunk(x => rt.TriggerChunk(x));
return rt; }
public int PropertyContext {
get => (int)properties[0];
set =>  _Set(0, value);
}
public Test.SizeEnum Enum {
get => (Test.SizeEnum)properties[1];
set =>  _Set(1, value);
}
public Test.MyRecord Record {
get => (Test.MyRecord)properties[2];
set =>  _Set(2, value);
}
public int[] IntList {
get => (int[])properties[3];
set =>  _Set(3, value);
}
public IRecord[] RecordsArray {
get => (IRecord[])properties[4];
set =>  _Set(4, value);
}
public Test.MyRecord[] RecordsList {
get => (Test.MyRecord[])properties[5];
set =>  _Set(5, value);
}
public Test.MyResource Resource {
get => (Test.MyResource)properties[6];
set =>  _Set(6, value);
}
public Test.MyChildResource Child {
get => (Test.MyChildResource)properties[7];
set =>  _Set(7, value);
}
public IResource[] Resources {
get => (IResource[])properties[8];
set =>  _Set(8, value);
}
public Test.MyService Me {
get => (Test.MyService)properties[9];
set =>  _Set(9, value);
}
public bool Boolean {
get => (bool)properties[10];
set =>  _Set(10, value);
}
public bool[] BooleanArray {
get => (bool[])properties[11];
set =>  _Set(11, value);
}
public byte UInt8 {
get => (byte)properties[12];
set =>  _Set(12, value);
}
public byte? UInt8Null {
get => (byte?)properties[13];
set =>  _Set(13, value);
}
public byte[] UInt8Array {
get => (byte[])properties[14];
set =>  _Set(14, value);
}
public byte?[] UInt8ArrayNull {
get => (byte?[])properties[15];
set =>  _Set(15, value);
}
public sbyte Int8 {
get => (sbyte)properties[16];
set =>  _Set(16, value);
}
public sbyte[] Int8Array {
get => (sbyte[])properties[17];
set =>  _Set(17, value);
}
public char Char16 {
get => (char)properties[18];
set =>  _Set(18, value);
}
public char[] Char16Array {
get => (char[])properties[19];
set =>  _Set(19, value);
}
public short Int16 {
get => (short)properties[20];
set =>  _Set(20, value);
}
public short[] Int16Array {
get => (short[])properties[21];
set =>  _Set(21, value);
}
public ushort UInt16 {
get => (ushort)properties[22];
set =>  _Set(22, value);
}
public ushort[] UInt16Array {
get => (ushort[])properties[23];
set =>  _Set(23, value);
}
public int Int32 {
get => (int)properties[24];
set =>  _Set(24, value);
}
public int[] Int32Array {
get => (int[])properties[25];
set =>  _Set(25, value);
}
public uint UInt32 {
get => (uint)properties[26];
set =>  _Set(26, value);
}
public uint[] UInt32Array {
get => (uint[])properties[27];
set =>  _Set(27, value);
}
public long Int64 {
get => (long)properties[28];
set =>  _Set(28, value);
}
public long[] Int64Array {
get => (long[])properties[29];
set =>  _Set(29, value);
}
public ulong UInt64 {
get => (ulong)properties[30];
set =>  _Set(30, value);
}
public ulong[] UInt64Array {
get => (ulong[])properties[31];
set =>  _Set(31, value);
}
public float Float32 {
get => (float)properties[32];
set =>  _Set(32, value);
}
public float[] Float32Array {
get => (float[])properties[33];
set =>  _Set(33, value);
}
public double Float64 {
get => (double)properties[34];
set =>  _Set(34, value);
}
public double[] Float64Array {
get => (double[])properties[35];
set =>  _Set(35, value);
}
public decimal Float128 {
get => (decimal)properties[36];
set =>  _Set(36, value);
}
public decimal[] Float128Array {
get => (decimal[])properties[37];
set =>  _Set(37, value);
}
public string String {
get => (string)properties[38];
set =>  _Set(38, value);
}
public string[] StringArray {
get => (string[])properties[39];
set =>  _Set(39, value);
}
public DateTime DateTime {
get => (DateTime)properties[40];
set =>  _Set(40, value);
}
public Map<string,object> StringMap {
get => (Map<string,object>)properties[41];
set =>  _Set(41, value);
}
public Map<int,string> IntStringMap {
get => (Map<int,string>)properties[42];
set =>  _Set(42, value);
}
public object Object {
get => (object)properties[43];
set =>  _Set(43, value);
}
public object[] ObjectArray {
get => (object[])properties[44];
set =>  _Set(44, value);
}
public const double PI = 3.14159265358979;
protected override void _EmitEventByIndex(byte index, object args) {
switch (index) {
case 0: StringEvent?.Invoke((string)args); break;
case 1: ArrayEvent?.Invoke((object[])args); break;
}}
public event ResourceEventHandler<string> StringEvent;
public event ResourceEventHandler<object[]> ArrayEvent;


}
}
