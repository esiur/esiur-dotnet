using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Esiur.Net.IIP
{
    public class ResourcePropertyChangedEventArgs : PropertyChangedEventArgs
    {
        public ResourcePropertyChangedEventArgs(string propertyName) : base(propertyName)
        {

        }

        public ResourcePropertyChangedEventArgs(PropertyModificationInfo info) : base(info.Name)
        {
            Info = info;
        }

        public readonly PropertyModificationInfo Info;
    }
}
