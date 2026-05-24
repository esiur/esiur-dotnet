using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class, Inherited = false)]
    public class RemoteAttribute:Attribute
    {
        public string Domain { get; private set; }
        public string FullName { get; private set; }

        public RemoteAttribute(string domain, string fullName)
        {
            Domain = domain;
            FullName = fullName;
        }
    }
}
