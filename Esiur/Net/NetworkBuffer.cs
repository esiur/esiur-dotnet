﻿/*
 
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
using Esiur.Data;
using Esiur.Misc;

namespace Esiur.Net;
public class NetworkBuffer
{
    byte[] data;

    uint neededDataLength = 0;
    //bool trim;

    object syncLock = new object();

    //public object SyncLock
    //{
    //  get { return syncLock; }
    //}

    public NetworkBuffer()
    {
        data = new byte[0];
    }

    public bool Protected
    {
        get
        {
            return neededDataLength > data.Length;
        }
    }

    public uint Available
    {
        get
        {
            return (uint)data.Length;
        }
    }


    //public void HoldForAtLeast(byte[] src, uint offset, uint size, uint needed)
    //{
    //    HoldFor(src, offset, size, needed);
    //    //trim = false;
    //}

    //public void HoldForAtLeast(byte[] src, uint needed)
    //{
    //    HoldForAtLeast(src, 0, (uint)src.Length, needed);
    //}

    public void HoldForNextWrite(byte[] src)
    {
        //HoldForAtLeast(src, (uint)src.Length + 1);
        HoldFor(src, (uint)src.Length + 1);
    }

    public void HoldForNextWrite(byte[] src, uint offset, uint size)
    {
        //HoldForAtLeast(src, offset, size, size + 1);
        HoldFor(src, offset, size, size + 1);
    }


    public void HoldFor(byte[] src, uint offset, uint size, uint needed)
    {
        lock (syncLock)
        {
            if (size >= needed)
                throw new Exception("Size >= Needed !");

            //trim = true;
            data = DC.Combine(src, offset, size, data, 0, (uint)data.Length);
            neededDataLength = needed;

            // Console.WriteLine("Hold StackTrace: '{0}'", Environment.StackTrace);

            //Console.WriteLine("Holded {0} {1} {2} {3} - {4}", offset, size, needed, data.Length, GetHashCode());
        }
    }

    public void HoldFor(byte[] src, uint needed)
    {
        HoldFor(src, 0, (uint)src.Length, needed);
    }

    public bool Protect(byte[] data, uint offset, uint needed)//, bool exact = false)
    {
        uint dataLength = (uint)data.Length - offset;

        // protection
        if (dataLength < needed)
        {
            //if (exact)
            //    HoldFor(data, offset, dataLength, needed);
            //else
            //HoldForAtLeast(data, offset, dataLength, needed);
            HoldFor(data, offset, dataLength, needed);
            return true;
        }
        else
            return false;
    }

    public void Write(byte[] src)
    {
        Write(src, 0, (uint)src.Length);
    }

    public void Write(byte[] src, uint offset, uint length)
    {
        //if (data.Length > 0)
        //  Console.WriteLine();

        lock (syncLock)
            DC.Append(ref data, src, offset, length);
    }

    public bool CanRead
    {
        get
        {
            if (data.Length == 0)
                return false;
            if (data.Length < neededDataLength)
                return false;

            return true;
        }
    }

    public byte[] Read()
    {
        lock (syncLock)
        {
            if (data.Length == 0)
                return null;

            byte[] rt = null;

            if (neededDataLength == 0)
            {
                rt = data;
                data = new byte[0];
                return rt;
            }
            else
            {
                //Console.WriteLine("P STATE:" + data.Length + " " + neededDataLength);

                if (data.Length >= neededDataLength)
                {
                    //Console.WriteLine("data.Length >= neededDataLength " + data.Length + " >= " + neededDataLength + " " + trim);

                    //if (trim)
                    //{
                    //  rt = DC.Clip(data, 0, neededDataLength);
                    //  data = DC.Clip(data, neededDataLength, (uint)data.Length - neededDataLength);
                    //}
                    //else
                    //{
                    // return all data
                    rt = data;
                    data = new byte[0];
                    //}

                    neededDataLength = 0;
                    return rt;
                }
                else
                {
                    return null;
                }
            }

        }
    }
}
