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
using Esiur.Misc;
using System.Reflection;

namespace Esiur.Data
{
    /// <summary>
    /// BinaryList holds a list of items to be converted to binary for storage and transmission
    /// </summary>
    public class BinaryList
    {
        private List<byte> held = new List<byte>();

        /// <summary>
        /// Create an instance of BinaryList
        /// </summary>
        public BinaryList()
        {

        }

        /// <summary>
        /// Converts parameters to binary in same order
        /// </summary>
        /// <param name="values">Variables to convert</param>
        public static byte[] ToBytes(params object[] values)
        {
            var held = new List<byte>();

            foreach (var i in values)
            {
                if (i is byte)
                    held.Add((byte)i);
                else
                {
#if NETSTANDARD1_5
                    MethodInfo mi = typeof(DC).GetTypeInfo().GetMethod("ToBytes", new Type[] { i.GetType() });
#else
                    MethodInfo mi = typeof(DC).GetMethod("ToBytes", new Type[] { i.GetType() });
#endif
                    if (mi != null)
                    {
                        var b = (byte[])mi.Invoke(null, new object[] { i });
                        held.AddRange(b);
                    }
                }
            }

            return held.ToArray();
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
                    held.Add((byte)i);
                else
                {
#if NETSTANDARD1_5
                    MethodInfo mi = typeof(DC).GetTypeInfo().GetMethod("ToBytes", new Type[] { i.GetType() });
#else
                    MethodInfo mi = typeof(DC).GetMethod("ToBytes", new Type[] { i.GetType() });
#endif
                    if (mi != null)
                    {
                        var b = (byte[])mi.Invoke(null, new object[] {i});
                        held.AddRange(b);
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
                    held.Insert(offset++, (byte)i);
                }
                else
                {
#if NETSTANDARD1_5
                    MethodInfo mi = typeof(DC).GetTypeInfo().GetMethod("ToBytes", new Type[] { i.GetType() });
#else
                    MethodInfo mi = typeof(DC).GetMethod("ToBytes", new Type[] { i.GetType() });
#endif
                    if (mi != null)
                    {
                        var b = (byte[])mi.Invoke(null, new object[] { i });
                        held.InsertRange(offset, b);
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
                return held.Count;
            }
        }

        /*
        public void Append(byte data)
        {
            held.Add(data);
        }

        public void Append(byte[] data)
        {
            held.AddRange(data);
        }

        public void Append(int data)
        {
            held.AddRange(DC.ToBytes(data));
        }

        public void Append(uint data)
        {
            held.AddRange(DC.ToBytes(data));
        }

        public void Append(float data)
        {
            held.AddRange(DC.ToBytes(data));
        }

        public void Append(short data)
        {
            held.AddRange(DC.ToBytes(data));
        }

        public void Append(ushort data)
        {
            held.AddRange(DC.ToBytes(data));
        }

        public void Append(double data)
        {
            held.AddRange(DC.ToBytes(data));
        }

        public void Append(sbyte data)
        {
            held.Add((byte)data);
        }
         */

        /// <summary>
        /// Convert the list to an array of bytes
        /// </summary>
        /// <returns>Bytes array</returns>
        public byte[] ToArray()
        {
            return held.ToArray();
        }
    }
}
