using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Net.IIP
{
    public class DistributedPropertyContext
    {
        public object Value { get; private set; }
        public DistributedConnection Connection { get; private set; }
        public Func<DistributedConnection, object> Method { get; private set; }

        public DistributedPropertyContext(DistributedConnection connection, object value)
        {
            this.Value = value;
            this.Connection = connection;
        }

        public DistributedPropertyContext(Func<DistributedConnection, object> method)
        {
            this.Method = method;
        }
    }
}
