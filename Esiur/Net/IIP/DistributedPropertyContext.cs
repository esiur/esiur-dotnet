using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.IIP;

public interface IDistributedPropertyContext
{
    object GetValue(DistributedConnection connection);
}

public class DistributedPropertyContext<T> :  IDistributedPropertyContext
{
    public T Value { get; private set; }
    public DistributedConnection Connection { get; private set; }
    public Func<DistributedConnection, T> Method { get; private set; }

    public DistributedPropertyContext(DistributedConnection connection, T value)
    {
        this.Value = value;
        this.Connection = connection;
    }

    public DistributedPropertyContext(Func<DistributedConnection, T> method)
    {
        this.Method = method;
    }

    public static implicit operator DistributedPropertyContext<T>(Func<DistributedConnection, T> method)
                                    => new DistributedPropertyContext<T>(method);

    public object GetValue(DistributedConnection connection)
    {
        return Method.Invoke(connection);
    }
}
