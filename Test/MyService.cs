using Esiur.Data;
using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Test;


public enum SizeEnum:short
{
    xSmall = -11,
    Small,
    Medium = 0,
    Large,
    XLarge = 22
}



[Resource]
public partial class MyService
{

    [Public] public event ResourceEventHandler<string> StringEvent;
    [Public] public event ResourceEventHandler<object[]> ArrayEvent;

    [Public] bool boolean = true;
    [Public] bool[] booleanArray = new bool[] { true, false, true, false, true };

    [Public]
    public MyGenericRecord<MyResource> GetGenericRecord()
    {
        return new MyGenericRecord<MyResource>() { Needed = 3, Start = 10, Results = new MyResource[0], Total = 102 };
    }

    [Public] public static string staticFunction(string name) => $"Hello {name}";

    [Public] byte uInt8Test = 8;
    [Public] byte? uInt8Null = null;
    [Public] byte[] uInt8Array = new byte[] { 0, 1, 2, 3, 4, 5 };
    [Public] byte?[] uInt8ArrayNull = new byte?[] { 0, null, 2, null, 4, null };

    [Public] sbyte int8 = -8;
    [Public] sbyte[] int8Array = new sbyte[] { -3, -2, -1, 0, 1, 2 };

    [Public] char char16 = 'ح';
    [Public] char[] char16Array = new char[] { 'م', 'ر', 'ح', 'ب', 'ا' };

    [Public] short int16 = -16;
    [Public] short[] int16Array = new short[] { -3, -2, -1, 0, 1, 2 };

    [Public] ushort uInt16 = 16;
    [Public] ushort[] uInt16Array = new ushort[] { 0, 1, 2, 3, 4, 5 };


    [Public] int int32Prop = -32;
    [Public] int[] int32Array = new int[] { -3, -2, -1, 0, 1, 2 };

    [Public] uint uInt32 = 32;
    [Public] uint[] uInt32Array = new uint[] { 0, 1, 2, 3, 4, 5 };

    [Public] long int64 = 323232323232;
    [Public] long[] int64Array = new long[] { -3, -2, -1, 0, 1, 2 };

    [Public] ulong uInt64;
    [Public] ulong[] uInt64Array = new ulong[] { 0, 1, 2, 3, 4, 5 };

    [Public] float float32 = 32.32f;
    [Public] float[] float32Array = new float[] { -3.3f, -2.2f, -1.1f, 0, 1.1f, 2.2f };

    [Public] double float64 = 32.323232;
    [Public] double[] float64Array = new double[] { -3.3, -2.2, -1.1, 0, 1.1, 2.2 };

    [Public] decimal float128 = 3232.323232323232m;
    [Public] decimal[] float128Array = new decimal[] { -3.3m, -2.2m, -1.1m, 0, 1.1m, 2.2m };

    [Public("Text")] string stringTest = "Hello World";
    [Public] string[] stringArray = new string[] { "Hello", "World" };

    [Public] DateTime time = DateTime.Now;


    [Public]
    Map<string, object> stringMap = new Map<string, object>()
    {
        ["int"] = 33,
        ["string"] = "Hello World"
    };

    [Public]
    Map<int, string> intStringMap = new()
    {
        [4] = "Abcd",
        [44] = "EfG"
    };


    [Public("Object")] object objectTest = "object";

    [Public] object[] objectArray = new object[] { 1, 1.2f, Math.PI, "Hello World" };

    [Public]
    public DistributedPropertyContext<int> PropertyContext
    {
        get => new DistributedPropertyContext<int>((sender) => sender.RemoteEndPoint.Port);
        set
        {
            Console.WriteLine($"PropertyContext Set: {value.Value} {value.Connection.RemoteEndPoint.Port}");
        }
    }

    [Public] public SizeEnum Enum => SizeEnum.Medium;


    [Public] public MyRecord Record => new MyRecord() { Id = 33, Name = "Test", Score = 99.33 };

    [Public] public List<int> IntList => new List<int>() { 1, 2, 3, 4, 5 };

    [Public] public IRecord[] RecordsArray => new IRecord[] { new MyRecord() { Id = 22, Name = "Test", Score = 22.1 } };
    [Public] public List<MyRecord> RecordsList => new() { new MyRecord() { Id = 22, Name = "Test", Score = 22.1 } };


    [Public] public MyResource[] myResources;

    [Public] public MyResource Resource { get; set; }
    [Public] public MyChildResource ChildResource { get; set; }

    [Public] public MyChildRecord ChildRecord { get; set; } = new MyChildRecord() { ChildName = "Child", Id = 12, Name = "Parent", Score = 12.2 };

    [Public] public IResource[] Resources { get; set; }

    [Public]
    public void Void() =>
        Console.WriteLine("Void()");

    [Public]
    public void InvokeEvents(string msg)
    {
        StringEvent?.Invoke(msg);
        ArrayEvent?.Invoke(new object[] { DateTime.UtcNow, "Event", msg });
    }

    [Public]
    public double Optional(object a1, int a2, string a3 = "Hello", string a4 = "World")
    {
        Console.WriteLine($"VoidArgs {a1} {a2} {a3}");
        return new Random().NextDouble();
    }


    [Public] public AsyncReply<List<Map<int, string?>?>> AsyncHello()
    {
        var rt = new List<Map<int, string?>?>();
        rt.Add(new Map<int, string?>() { [1] = "SSSSS", [2] = null });
        return new AsyncReply<List<Map<int, string?>?>>(rt);
    }

    [Public]
    public void Connection(object a1, int a2, DistributedConnection a3) =>
        Console.WriteLine($"VoidArgs {a1} {a2} {a3}");


    [Public]
    public void ConnectionOptional(object a1, int a2, string a3 = "sss", DistributedConnection a4 = null) =>
        Console.WriteLine($"VoidArgs {a1} {a2} {a3}");

    [Public]
    public (int, string) GetTuple2(int a1, string a2) => (a1, a2);

    [Public]
    public (int, string, double) GetTuple3(int a1, string a2, double a3) => (a1, a2, a3);

    [Public]
    public (int, string, double, bool) GetTuple4(int a1, string a2, double a3, bool a4) => (a1, a2, a3, a4);

    [Public]
    public MyRecord SendRecord(MyRecord record)
    {
        Console.WriteLine(record.ToString());
        return record;
    }

    [Public] public const double PI = Math.PI;

    [Public] public MyService Me => this;
}
