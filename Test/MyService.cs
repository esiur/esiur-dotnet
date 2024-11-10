using Esiur.Data;
using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable enable

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

    [Export] public event ResourceEventHandler<string>? StringEvent;
    [Export] public event ResourceEventHandler<object[]>? ArrayEvent;

    [Export] bool boolean = true;
    [Export] bool[] booleanArray = new bool[] { true, false, true, false, true };

    [Export]
    public MyGenericRecord<MyResource> GetGenericRecord()
    {
        return new MyGenericRecord<MyResource>() { Needed = 3, Start = 10, Results = new MyResource[0], Total = 102 };
    }

    [Export] public static string staticFunction(string name) => $"Hello {name}";

    [Export] byte uInt8Test = 8;
    [Export] byte? uInt8Null = null;
    [Export] byte[] uInt8Array = new byte[] { 0, 1, 2, 3, 4, 5 };
    [Export] byte?[] uInt8ArrayNull = new byte?[] { 0, null, 2, null, 4, null };

    [Export] sbyte int8 = -8;
    [Export] sbyte[] int8Array = new sbyte[] { -3, -2, -1, 0, 1, 2 };

    [Export] char char16 = 'ح';
    [Export] char[] char16Array = new char[] { 'م', 'ر', 'ح', 'ب', 'ا' };

    [Export] short int16 = -16;
    [Export] short[] int16Array = new short[] { -3, -2, -1, 0, 1, 2 };

    [Export] ushort uInt16 = 16;
    [Export] ushort[] uInt16Array = new ushort[] { 0, 1, 2, 3, 4, 5 };


    [Export] int int32Prop = -32;
    [Export] int[] int32Array = new int[] { -3, -2, -1, 0, 1, 2 };

    [Export] uint uInt32 = 32;
    [Export] uint[] uInt32Array = new uint[] { 0, 1, 2, 3, 4, 5 };

    [Export] long int64 = 323232323232;
    [Export] long[] int64Array = new long[] { -3, -2, -1, 0, 1, 2 };

    [Export] ulong uInt64;
    [Export] ulong[] uInt64Array = new ulong[] { 0, 1, 2, 3, 4, 5 };

    [Export] float float32 = 32.32f;
    [Export] float[] float32Array = new float[] { -3.3f, -2.2f, -1.1f, 0, 1.1f, 2.2f };

    [Export] double float64 = 32.323232;
    [Export] double[] float64Array = new double[] { -3.3, -2.2, -1.1, 0, 1.1, 2.2 };

    [Export] decimal float128 = 3232.323232323232m;
    [Export] decimal[] float128Array = new decimal[] { -3.3m, -2.2m, -1.1m, 0, 1.1m, 2.2m };

    [Export("Text")] string stringTest = "Hello World";
    [Export] string[] stringArray = new string[] { "Hello", "World" };

    [Export] DateTime time = DateTime.Now;


    [Export]
    Map<string, object> stringMap = new Map<string, object>()
    {
        ["int"] = 33,
        ["string"] = "Hello World"
    };

    [Export]
    Map<int, string> intStringMap = new()
    {
        [4] = "Abcd",
        [44] = "EfG"
    };


    [Export("Object")] object objectTest = "object";

    [Export] object[] objectArray = new object[] { 1, 1.2f, Math.PI, "Hello World" };

    [Export]
    public DistributedPropertyContext<int> PropertyContext
    {
        get => new DistributedPropertyContext<int>((sender) => sender.RemoteEndPoint.Port);
        set
        {
            Console.WriteLine($"PropertyContext Set: {value.Value} {value.Connection.RemoteEndPoint.Port}");
        }
    }

    [Export] public SizeEnum Enum => SizeEnum.Medium;


    [Export] public MyRecord Record => new MyRecord() { Id = 33, Name = "Test", Score = 99.33 };


    [Export] public MyRecord? RecordNullable => new MyRecord() { Id = 33, Name = "Test Nullable", Score = 99.33 };

    [Export] public List<int> IntList => new List<int>() { 1, 2, 3, 4, 5 };

    [Export] public IRecord[] RecordsArray => new IRecord[] { new MyRecord() { Id = 22, Name = "Test", Score = 22.1 } };
    [Export] public List<MyRecord> RecordsList => new() { new MyRecord() { Id = 22, Name = "Test", Score = 22.1 } };


    [Export] public MyResource[]? myResources;

    [Export] public MyResource? Resource { get; set; }
    [Export] public MyChildResource? ChildResource { get; set; }

    [Export] public MyChildRecord ChildRecord { get; set; } = new MyChildRecord() { ChildName = "Child", Id = 12, Name = "Parent", Score = 12.2 };

    [Export] public IResource[]? Resources { get; set; }

    [Export]
    public void Void() =>
        Console.WriteLine("Void()");

    [Export]
    public void InvokeEvents(string msg)
    {
        StringEvent?.Invoke(msg);
        ArrayEvent?.Invoke(new object[] { DateTime.UtcNow, "Event", msg });
    }

    [Export]
    public double Optional(object a1, int a2, string a3 = "Hello", string a4 = "World")
    {
        Console.WriteLine($"VoidArgs {a1} {a2} {a3}");
        return new Random().NextDouble();
    }


    [Export]
    public AsyncReply<List<Map<int, string?>?>> AsyncHello()
    {
        var rt = new List<Map<int, string?>?>();
        rt.Add(new Map<int, string?>() { [1] = "SSSSS", [2] = null });
        return new AsyncReply<List<Map<int, string?>?>>(rt);
    }

    [Export]
    public void Connection(object a1, int a2, DistributedConnection a3) =>
        Console.WriteLine($"VoidArgs {a1} {a2} {a3}");


    [Export]
    public void ConnectionOptional(object a1, int a2, string a3 = "sss", DistributedConnection? a4 = null) =>
        Console.WriteLine($"VoidArgs {a1} {a2} {a3}");

    [Export]
    public (int, string) GetTuple2(int a1, string a2) => (a1, a2);

    [Export]
    public (int, string, double) GetTuple3(int a1, string a2, double a3) => (a1, a2, a3);

    [Export]
    public (int, string, double, bool) GetTuple4(int a1, string a2, double a3, bool a4) => (a1, a2, a3, a4);

    [Export]
    public MyRecord SendRecord(MyRecord record)
    {
        Console.WriteLine(record.ToString());
        return record;
    }

    [Export] public const double PI = Math.PI;

    [Export] public MyService Me => this;
}
