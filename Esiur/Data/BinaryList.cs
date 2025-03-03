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
using Esiur.Core;

namespace Esiur.Data;

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

    public Endian Endian { get; set; } = Endian.Little;

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


    public BinaryList AddUUID(UUID value)
    {
        list.AddRange(value.Data);
        return this;

    }
    //public BinaryList AddGuid(Guid value)
    //{
    //    list.AddRange(DC.ToBytes(value));
    //    return this;
    //}

    public BinaryList InsertGuid(int position, Guid value)
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

    public BinaryList AddUInt16(ushort value)
    {
        list.AddRange(DC.ToBytes(value, Endian));
        return this;
    }
    public BinaryList InsertUInt16(int position, ushort value)
    {
        list.InsertRange(position, DC.ToBytes(value, Endian));
        return this;
    }


    public BinaryList AddInt16(short value)
    {
        list.AddRange(DC.ToBytes(value, Endian));
        return this;
    }

    public BinaryList InsertInt16(int position, short value)
    {
        list.InsertRange(position, DC.ToBytes(value, Endian));
        return this;
    }


 
    public BinaryList AddUInt32(uint value)
    {
        list.AddRange(DC.ToBytes(value, Endian));
        return this;
    }
    public BinaryList InsertUInt32(int position, uint value)
    {
        list.InsertRange(position, DC.ToBytes(value, Endian));
        return this;
    }
     

    public BinaryList AddInt32(int value)
    {
        list.AddRange(DC.ToBytes(value, Endian));
        return this;
    }
    public BinaryList InsertInt32(int position, int value)
    {
        list.InsertRange(position, DC.ToBytes(value, Endian));
        return this;
    }

    public BinaryList AddUInt64(ulong value)
    {
        list.AddRange(DC.ToBytes(value, Endian));
        return this;
    }
    public BinaryList InsertUInt64(int position, ulong value)
    {
        list.InsertRange(position, DC.ToBytes(value, Endian));
        return this;
    }

 

    public BinaryList AddInt64(long value)
    {
        list.AddRange(DC.ToBytes(value, Endian));
        return this;
    }

    public BinaryList InsertInt64(int position, long value)
    {
        list.InsertRange(position, DC.ToBytes(value, Endian));
        return this;
    }
 

    public BinaryList AddFloat32(float value)
    {
        list.AddRange(value.ToBytes(Endian));
        return this;
    }

    public BinaryList InsertFloat32(int position, float value)
    {
        list.InsertRange(position, value.ToBytes(Endian));
        return this;
    }

 
    public BinaryList AddFloat64(double value)
    {
        list.AddRange(value.ToBytes(Endian));
        return this;
    }

    public BinaryList InsertFloat64(int position, double value)
    {
        list.InsertRange(position, value.ToBytes(Endian));
        return this;
    }
 
    /// <summary>
    /// Convert the list to an array of bytes
    /// </summary>
    /// <returns>Bytes array</returns>
    public byte[] ToArray()
    {
        return list.ToArray();
    }

    public virtual AsyncReply<object[]> Done()
    {
        return null;
    }
}
