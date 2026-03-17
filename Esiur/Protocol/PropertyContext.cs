using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol;

public interface IPropertyContext
{
    object GetValue(EpConnection connection);
}

public class PropertyContext<T> : IPropertyContext
{
    public T Value { get; private set; }
    public EpConnection Connection { get; private set; }
    public Func<EpConnection, T> Method { get; private set; }

    public PropertyContext(EpConnection connection, T value)
    {
        this.Value = value;
        this.Connection = connection;
    }

    public PropertyContext(Func<EpConnection, T> method)
    {
        this.Method = method;
    }

    public static implicit operator PropertyContext<T>(Func<EpConnection, T> method)
                                    => new PropertyContext<T>(method);

    public object GetValue(EpConnection connection)
    {
        return Method.Invoke(connection);
    }
}
