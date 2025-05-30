using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.IIP;

public interface IPropertyContext
{
    object GetValue(DistributedConnection connection);
}

public class PropertyContext<T> : IPropertyContext
{
    public T Value { get; private set; }
    public DistributedConnection Connection { get; private set; }
    public Func<DistributedConnection, T> Method { get; private set; }

    public PropertyContext(DistributedConnection connection, T value)
    {
        this.Value = value;
        this.Connection = connection;
    }

    public PropertyContext(Func<DistributedConnection, T> method)
    {
        this.Method = method;
    }

    public static implicit operator PropertyContext<T>(Func<DistributedConnection, T> method)
                                    => new PropertyContext<T>(method);

    public object GetValue(DistributedConnection connection)
    {
        return Method.Invoke(connection);
    }
}
