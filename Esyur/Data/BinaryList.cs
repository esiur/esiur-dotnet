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
using Esyur.Misc;
using System.Reflection;
using Esyur.Core;

namespace Esyur.Data
{
    /// <summary>
    /// BinaryList holds a list of items to be converted to binary for storage and transmission
    /// </summary>
    public class BinaryList
    {
        private List<byte> list = new List<byte>();

        /// <summary>
        /// Create an instance of BinaryList
        /// </summary>
        public BinaryList()
        {

        }

        /*
        /// <summary>
        /// Converts parameters to binary in same order
        /// </summary>
        /// <param name="values">Variables to convert</param>
        public static byte[] ToBytes(params object[] values)
        {
            var list = new List<byte>();

            foreach (var i in values)
            {
                if (i is byte)
                    list.Add((byte)i);
                else
                {
#if NETSTANDARD
                    MethodInfo mi = typeof(DC).GetTypeInfo().GetMethod("ToBytes", new Type[] { i.GetType() });
#else
                    MethodInfo mi = typeof(DC).GetMethod("ToBytes", new Type[] { i.GetType() });
#endif
                    if (mi != null)
                    {
                        var b = (byte[])mi.Invoke(null, new object[] { i });
                        list.AddRange(b);
                    }
                }
            }

            return list.ToArray();
        }
       

        /// <summary>
        /// Create a new instance of BinaryList
        /// </summary>
        /// <param name="values">Populate the list items</param>
        public BinaryList(params object[] values)
        {
            AddRange(values);
        }

        /// <summary>
        /// Add an array of items at the end of the list
        /// </summary>
        /// <param name="values">Array of items</param>
        public void AddRange(object[] values)
        {
            foreach (var i in values)
            {
                if (i is byte)
                    list.Add((byte)i);
                else
                {
#if NETSTANDARD
                    MethodInfo mi = typeof(DC).GetTypeInfo().GetMethod("ToBytes", new Type[] { i.GetType() });
#else
                    MethodInfo mi = typeof(DC).GetMethod("ToBytes", new Type[] { i.GetType() });
#endif
                    if (mi != null)
                    {
                        var b = (byte[])mi.Invoke(null, new object[] {i});
                        list.AddRange(b);
                    }
                }
            }
        }

        /// <summary>
        /// Add multiple items at the end of the list
        /// </summary>
        /// <param name="values">Parameters of items</param>
        public void Append(params object[] values)
        {
          AddRange(values);
        }

        /// <summary>
        /// Insert new items to the list at a specified index
        /// </summary>
        /// <param name="offset">Position in the list</param>
        /// <param name="values">Items to insert</param>
        public void Insert(int offset, params object[] values)
        {
            foreach (var i in values)
            {
                if (i is byte)
                {
                    list.Insert(offset++, (byte)i);
                }
                else
                {
#if NETSTANDARD
                    MethodInfo mi = typeof(DC).GetTypeInfo().GetMethod("ToBytes", new Type[] { i.GetType() });
#else
                    MethodInfo mi = typeof(DC).GetMethod("ToBytes", new Type[] { i.GetType() });
#endif
                    if (mi != null)
                    {
                        var b = (byte[])mi.Invoke(null, new object[] { i });
                        list.InsertRange(offset, b);
                        offset += b.Length;
                    }
                }
            }
        }

        /// <summary>
        /// Number of the items in the list
        /// </summary>
        public int Length
        {
            get
            {
                return list.Count;
            }
        }

        /*
        public void Append(byte data)
        {
            list.Add(data);
        }

        public void Append(byte[] data)
        {
            list.AddRange(data);
        }

        public void Append(int data)
        {
            list.AddRange(DC.ToBytes(data));
        }

        public void Append(uint data)
        {
            list.AddRange(DC.ToBytes(data));
        }

        public void Append(float data)
        {
            list.AddRange(DC.ToBytes(data));
        }

        public void Append(short data)
        {
            list.AddRange(DC.ToBytes(data));
        }

        public void Append(ushort data)
        {
            list.AddRange(DC.ToBytes(data));
        }

        public void Append(double data)
        {
            list.AddRange(DC.ToBytes(data));
        }

        public void Append(sbyte data)
        {
            list.Add((byte)data);
        }
         */


        public int Length => list.Count;

        public BinaryList AddDateTime(DateTime value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertDateTime(int position, DateTime value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddDateTimeArray(DateTime[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertDateTimeArray(int position, DateTime[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddGuid(Guid value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertGuid(int position, Guid value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddGuidArray(Guid[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertGuidArray(int position, Guid[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddUInt8Array(byte[] value)
        {
            list.AddRange(value);
            return this;
        }

        
        public BinaryList InsertUInt8Array(int position, byte[] value)
        {
            list.InsertRange(position, value);
            return this;
        }


        public BinaryList AddHex(string value)
        {
            return this.AddUInt8Array(DC.FromHex(value, null));
        }

        public BinaryList InsertHex(int position, string value)
        {
            return this.InsertUInt8Array(position, DC.FromHex(value, null));
        }


        
        public BinaryList AddString(string value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertString(int position, string value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddStringArray(string[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertStringArray(int position, string[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertUInt8(int position, byte value)
        {
            list.Insert(position, value);
            return this;
        }

        public BinaryList AddUInt8(byte value)
        {
            list.Add(value);
            return this;
        }

        public BinaryList AddInt8(sbyte value)
        {
            list.Add((byte)value);
            return this;
        }

        public BinaryList InsertInt8(int position, sbyte value)
        {
            list.Insert(position, (byte)value);
            return this;
        }
        
        public BinaryList AddInt8Array(sbyte[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertInt8Array(int position, sbyte[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddChar(char value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertChar(int position, char value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddCharArray(char[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertCharArray(int position, char[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddBoolean(bool value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertBoolean(int position, bool value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddBooleanArray(bool[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertBooleanArray(int position, bool[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddUInt16(ushort value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }
        public BinaryList InsertUInt16(int position, ushort value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddUInt16Array(ushort[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertUInt16Array(int position, ushort[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddInt16(short value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertInt16(int position, short value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

       
        public BinaryList AddInt16Array(short[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertInt16Array(int position, short[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddUInt32(uint value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }
        public BinaryList InsertUInt32(int position, uint value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddUInt32Array(uint[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }
        public BinaryList InsertUInt32Array(int position, uint[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddInt32(int value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }
        public BinaryList InsertInt32(int position, int value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddInt32Array(int[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }
        public BinaryList InsertInt32Array(int position, int[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddUInt64(ulong value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }
        public BinaryList InsertUInt64(int position, ulong value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddUInt64Array(ulong[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertUInt64Array(int position, ulong[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }




        public BinaryList AddInt64(long value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertInt64(int position, long value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddInt64Array(long[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertInt64Array(int position, long[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddFloat32(float value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertFloat32(int position, float value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddFloat32Array(float[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertFloat32Array(int position, float[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }


        public BinaryList AddFloat64(double value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertFloat64(int position, double value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }

        public BinaryList AddFloat64Array(double[] value)
        {
            list.AddRange(DC.ToBytes(value));
            return this;
        }

        public BinaryList InsertFloat64Array(int position, double[] value)
        {
            list.InsertRange(position, DC.ToBytes(value));
            return this;
        }



        public BinaryList Add(DataType type, object value)
        {
            switch (type)
            {
                case DataType.Bool:
                    AddBoolean((bool)value);
                    return this;
                case DataType.BoolArray:
                    AddBooleanArray((bool[])value);
                    return this;
                case DataType.UInt8:
                    AddUInt8((byte)value);
                    return this;
                case DataType.UInt8Array:
                    AddUInt8Array((byte[])value);
                    return this;
                case DataType.Int8:
                    AddInt8((sbyte)value);
                    return this;
                case DataType.Int8Array:
                    AddInt8Array((sbyte[])value);
                    return this;
                case DataType.Char:
                    AddChar((char)value);
                    return this;
                case DataType.CharArray:
                    AddCharArray((char[])value);
                    return this;
                case DataType.UInt16:
                    AddUInt16((ushort)value);
                    return this;
                case DataType.UInt16Array:
                    AddUInt16Array((ushort[])value);
                    return this;
                case DataType.Int16:
                    AddInt16((short)value);
                    return this;
                case DataType.Int16Array:
                    AddInt16Array((short[])value);
                    return this;
                case DataType.UInt32:
                    AddUInt32((uint)value);
                    return this;
                case DataType.UInt32Array:
                    AddUInt32Array((uint[])value);
                    return this;
                case DataType.Int32:
                    AddInt32((int)value);
                    return this;
                case DataType.Int32Array:
                    AddInt32Array((int[])value);
                    return this;
                case DataType.UInt64:
                    AddUInt64((ulong)value);
                    return this;
                case DataType.UInt64Array:
                    AddUInt64Array((ulong[])value);
                    return this;
                case DataType.Int64:
                    AddInt64((long)value);
                    return this;
                case DataType.Int64Array:
                    AddInt64Array((long[])value);
                    return this;

                case DataType.Float32:
                    AddFloat32((float)value);
                    return this;
                case DataType.Float32Array:
                    AddFloat32Array((float[])value);
                    return this;

                case DataType.Float64:
                    AddFloat64((double)value);
                    return this;
                case DataType.Float64Array:
                    AddFloat64Array((double[])value);
                    return this;

                case DataType.String:
                    AddString((string)value);
                    return this;
                case DataType.StringArray:
                    AddStringArray((string[])value);
                    return this;

                case DataType.DateTime:
                    AddDateTime((DateTime)value);
                    return this;
                case DataType.DateTimeArray:
                    AddDateTimeArray((DateTime[])value);
                    return this;

                default:
                    throw new Exception("Not Implemented " + type.ToString());
                    //return this;
            }
        }
        /// <summary>
        /// Convert the list to an array of bytes
        /// </summary>
        /// <returns>Bytes array</returns>
        public byte[] ToArray()
        {
            return list.ToArray();
        }

        public virtual IAsyncReply<object[]> Done()
        {
            return null;
            // 
        }
    }
}
