/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esyur.Net.IIP
{
    public class DistributedResourceQueueItem
    {
        public enum DistributedResourceQueueItemType
        {
            Propery,
            Event
        }

        DistributedResourceQueueItemType type;
        byte index;
        object value;
        DistributedResource resource;

        public DistributedResourceQueueItem(DistributedResource resource, DistributedResourceQueueItemType type, object value, byte index)
        {
            this.resource = resource;
            this.index = index;
            this.type = type;
            this.value = value;
        }

        public DistributedResource Resource
        {
            get { return resource; }
        }
        public DistributedResourceQueueItemType Type
        {
            get { return type; }
        }

        public byte Index
        {
            get { return index; }
        }

        public object Value
        {
            get { return value; }
        }
    }
}
