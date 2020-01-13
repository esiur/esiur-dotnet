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

namespace Esyur.Data
{
    public enum DataType : byte
    {
        Void = 0x0,
        //Variant,
        Bool,
        Int8,
        UInt8,
        Char,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float32,
        Float64,
        Decimal,
        DateTime,
        Resource,
        DistributedResource,
        ResourceLink,
        String,
        Structure,
        //Stream,
        //Array = 0x80,
        VarArray = 0x80,
        BoolArray,
        Int8Array,
        UInt8Array,
        CharArray,
        Int16Array,
        UInt16Array,
        Int32Array,
        UInt32Array,
        Int64Array,
        UInt64Array,
        Float32Array,
        Float64Array,
        DecimalArray,
        DateTimeArray,
        ResourceArray,
        DistributedResourceArray,
        ResourceLinkArray,
        StringArray,
        StructureArray,
        NotModified = 0x7f,
        Unspecified = 0xff,
    }

    public static class DataTypeExpansions
    {
        public static int Size(this DataType t)
        {
            switch (t)
            {
                case DataType.Void:
                case DataType.NotModified:
                    return 0;
                case DataType.Bool:
                case DataType.UInt8:
                case DataType.Int8:
                    return 1;
                case DataType.Char:
                case DataType.UInt16:
                case DataType.Int16:
                    return 2;
                case DataType.Int32:
                case DataType.UInt32:
                case DataType.Float32:
                case DataType.Resource:
                    return 4;
                case DataType.Int64:
                case DataType.UInt64:
                case DataType.Float64:
                case DataType.DateTime:
                    return 8;
                case DataType.DistributedResource:
                    return 4;

                default:
                    return -1;
            }
        }


    }
}
