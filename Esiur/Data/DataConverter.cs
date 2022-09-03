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
using Esiur.Net.IIP;

using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Esiur.Data;
using Esiur.Core;
using Esiur.Resource;

namespace Esiur.Data;

public static class DC // Data Converter
{
    public static object CastConvert(object value, Type destinationType)
    {
        if (value == null)
            return null;

        var sourceType = value.GetType();

        if (destinationType == sourceType)
        {
            return value;
        }
        else
        {
            if (sourceType.IsArray && (destinationType.IsArray || destinationType == typeof(object)))
            {
                destinationType = destinationType.GetElementType();

                var v = value as Array;

                var rt = Array.CreateInstance(destinationType, v.Length);

                for (var i = 0; i < rt.Length; i++)
                {
                    rt.SetValue(CastConvert(v.GetValue(i), destinationType), i);
                }

                return rt;

            }
            else
            {
                try
                {
                    var underType = Nullable.GetUnderlyingType(destinationType);
                    if (underType != null)
                    {
                        if (value == null)
                            return null;
                        else
                            destinationType = underType;
                    }

                    if (destinationType.IsInstanceOfType(value))
                    {
                        return value;
                    }
                    else if (typeof(IUserType).IsAssignableFrom(destinationType))
                    {
                        var rt = Activator.CreateInstance(destinationType) as IUserType;
                        rt.Set(value);
                        return rt;
                    }
                    //else if (sourceType == typeof(Structure) && sourceType.IsAssignableFrom(destinationType))
                    //{
                    //    return Structure.FromStructure((Structure)value, destinationType);
                    //}
                    else if (destinationType.IsEnum)
                    {
                        return Enum.ToObject(destinationType, value);
                    }
                    else
                    {
                        return Convert.ChangeType(value, destinationType);
                    }
                }
                catch
                {
                    return null;
                }
            }
        }
    }


    public static byte[] ToBytes(sbyte value)
    {
        return new byte[1] { (byte)value };
    }

    public static byte[] ToBytes(byte value)
    {
        return new byte[1] { value };
    }

    public static byte[] ToBytes(IPAddress ip)
    {
        return ip.GetAddressBytes();
    }

    public static byte[] ToBytes(PhysicalAddress mac)
    {
        return mac.GetAddressBytes();
    }

    public static byte[] ToBytes(bool value)
    {
        return new byte[1] { value ? (byte)1 : (byte)0 };
    }

    public static byte ToByte(bool value)
    {
        return value ? (byte)1 : (byte)0;
    }

    public static byte ToByte(sbyte value)
    {
        return (byte)value;
    }

    public static byte[] ToBytes(byte[] value)
    {
        return value;
    }


    public static byte[] ToBytes(bool[] value)
    {

        byte[] ba = new byte[value.Length];

        for (int i = 0; i < ba.Length; i++)
            ba[i] = DC.ToByte(value[i]);

        return ba;
    }

    public static byte[] ToBytes(sbyte[] value)
    {

        byte[] ba = new byte[value.Length];

        for (int i = 0; i < ba.Length; i++)
            ba[i] = DC.ToByte(value[i]);

        return ba;
    }

    public static byte[] ToBytes(char value)
    {
        byte[] ret = BitConverter.GetBytes(value);
        Array.Reverse(ret);
        return ret;
    }

    public static byte[] ToBytes(Guid value)
    {
        return value.ToByteArray();
    }


    public static void Append(ref byte[] dst, byte[] src)
    {
        Append(ref dst, src, (uint)0, (uint)src.Length);
    }

    public static void Append(ref byte[] dst, byte[] src, uint srcOffset, uint length)
    {
        var dstOffset = dst.Length;
        Array.Resize<byte>(ref dst, dstOffset + (int)length);
        Buffer.BlockCopy(src, (int)srcOffset, dst, dstOffset, (int)length);
    }

    public static byte[] Combine(byte[] src1, uint src1Offset, uint src1Length, byte[] src2, uint src2Offset, uint src2Length)
    {
        var rt = new byte[src1Length + src2Length];
        Buffer.BlockCopy(src1, (int)src1Offset, rt, 0, (int)src1Length);
        Buffer.BlockCopy(src2, (int)src2Offset, rt, (int)src1Length, (int)src2Length);
        return rt;
    }

    public static byte[] Merge(params byte[][] arrays)
    {
        var s = arrays.Sum(x => x.Length);
        var r = new byte[s];
        var offset = 0;
        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, r, offset, array.Length);
            offset += array.Length;
        }

        return r;
    }



    public static byte[] ToBytes(this string[] value)
    {
        List<byte> rt = new List<byte>();

        for (int i = 0; i < value.Length; i++)
        {
            byte[] ba = ToBytes(value[i]);
            // add string length
            rt.AddRange(ToBytes(ba.Length, Endian.Little));
            // add encoded string
            rt.AddRange(ba);
        }

        return rt.ToArray();
    }


    public static unsafe byte[] ToBytes(this int value, Endian endian)
    {
        var rt = new byte[4];

        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((int*)ptr) = value;
        }
        else
        {
            byte* p = (byte*)&value;

            rt[0] = *(p + 3);
            rt[1] = *(p + 2);
            rt[2] = *(p + 1);
            rt[3] = *(p + 0);

        }

        return rt;
    }



    public static unsafe byte[] ToBytes(this short value, Endian endian)
    {
        var rt = new byte[2];
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((short*)ptr) = value;
        }
        else
        {
            byte* p = (byte*)&value;

            rt[0] = *(p + 1);
            rt[1] = *(p + 0);
        }

        return rt;
    }

    public static unsafe byte[] ToBytes(this float value, Endian endian)

    {
        var rt = new byte[4];
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((float*)ptr) = value;
        }
        else
        {
            byte* p = (byte*)&value;
            rt[0] = *(p + 3);
            rt[1] = *(p + 2);
            rt[2] = *(p + 1);
            rt[3] = *(p);
        }

        return rt;
    }









     
    public static byte[] ToBytes(this string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    public unsafe static byte[] ToBytes(this double value, Endian endian)
    {
        var rt = new byte[8];
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((double*)ptr) = value;
        }
        else
        {
            byte* p = (byte*)&value;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);
        }

        return rt;
    }

    public static unsafe byte[] ToBytes(this long value, Endian endian)
    {
        var rt = new byte[8];
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((long*)ptr) = value;
        }
        else
        {
            byte* p = (byte*)&value;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);
        }

        return rt;
    }
    public static unsafe byte[] ToBytes(this DateTime value)
    {
        var rt = new byte[8];
        var v = value.ToUniversalTime().Ticks;

        fixed (byte* ptr = rt)
            *((long*)ptr) = v;
        return rt;
    }


    public static unsafe byte[] ToBytes(this ulong value, Endian endia)
    {
        var rt = new byte[8];
        if (endia == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((ulong*)ptr) = value;
        }
        else
        {

            byte* p = (byte*)&value;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);
        }

        return rt;
    }

    public static unsafe byte[] ToBytes(this uint value, Endian endian)
    {
        var rt = new byte[4];
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((uint*)ptr) = value;
        }
        else
        {

            byte* p = (byte*)&value;

            rt[0] = *(p + 3);
            rt[1] = *(p + 2);
            rt[2] = *(p + 1);
            rt[3] = *(p + 0);
        }

        return rt;
    }

    public static unsafe byte[] ToBytes(this ushort value, Endian endian)
    {
        var rt = new byte[2];
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = rt)
                *((ushort*)ptr) = value;
        }
        else
        {
            byte* p = (byte*)&value;

            rt[0] = *(p + 1);
            rt[1] = *(p);
        }
        return rt;
    }

    public static unsafe byte[] ToBytes(this decimal value, Endian endian)
    {
        var rt = new byte[16];
        fixed (byte* ptr = rt)
            *((decimal*)ptr) = value;

        if (endian == Endian.Big)
            Array.Reverse(rt);

        return rt;
    }

    public static string ToHex(this byte[] ba)
    {
        if (ba == null)
            return "";
        return ToHex(ba, 0, (uint)ba.Length);
    }

    public static string ToHex(this byte[] ba, uint offset, uint length, string separator = " ")
    {
        if (separator == null)
            separator = "";

        return string.Join(separator, ba.Skip((int)offset).Take((int)length).Select(x => x.ToString("x2")).ToArray());
    }

    public static byte[] FromHex(string hexString, string separator = " ")
    {
        var rt = new List<byte>();

        if (separator == null)
        {
            for (var i = 0; i < hexString.Length; i += 2)
                rt.Add(Convert.ToByte(hexString.Substring(i, 2), 16));
        }
        else
        {
            var hexes = hexString.Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var h in hexes)
                rt.Add(Convert.ToByte(h, 16));
        }

        return rt.ToArray();
    }

    public static string FlagsEnumToString<T>(ulong value)
    {

        string rt = typeof(T).Name + ":";

        for (int i = 0; i < 64; i++)
        {
            ulong bit = (ulong)(Convert.ToUInt64(Math.Pow(2, i)) & value);
            if (bit != 0)
            {
                rt += " " + Enum.GetName(typeof(T), bit);
            }
        }

        return rt;
    }

    public static bool TryParse<T>(object Input, out T Results)
    {
        try
        {
#if NETSTANDARD
            var tryParse = typeof(T).GetTypeInfo().GetDeclaredMethod("TryParse");
            if ((bool)tryParse.Invoke(null, new object[] { Input, null }))
            {
                var parse = typeof(T).GetTypeInfo().GetDeclaredMethod("Parse");

                Results = (T)parse.Invoke(null, new object[] { Input });
                return true;
            }
#else
                if ((bool)typeof(T).InvokeMember("TryParse", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { Input, null }))
                {
                    Results = (T)typeof(T).InvokeMember("Parse", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { Input });
                    return true;
                }

#endif
            else
            {
                Results = default(T);
                return false;
            }
        }
        catch //Exception ex)
        {
            Results = default(T);
            return false;
        }
    }



    public static DateTime FromUnixTime(uint seconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)seconds);
    }

    public static DateTime FromUnixTime(ulong milliseconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((double)milliseconds);
    }


    public static sbyte GetInt8(this byte[] data, uint offset)
    {
        return (sbyte)data[offset];
    }



     





    public static byte GetUInt8(this byte[] data, uint offset)
    {
        return data[offset];
    }

    public static unsafe short GetInt16(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(short*)ptr;
        }
        else
        {
            return (Int16)((data[offset] << 8) | data[offset + 1]);
        }
    }



    public static unsafe ushort GetUInt16(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(ushort*)ptr;
        }
        else
        {
            return (UInt16)((data[offset] << 8) | data[offset + 1]);
        }
    }

    public static unsafe int GetInt32(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(int*)ptr;
        }
        else
        {
            return (Int32)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }

    public static unsafe uint GetUInt32(this byte[] data, uint offset, Endian endian)
    {

        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(uint*)ptr;
        }
        else
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }




    public static unsafe ulong GetUInt64(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(ulong*)ptr;
        }
        else
        {
            UInt64 rt = 0;
            byte* p = (byte*)&rt;

            *(p + 7) = data[offset++];
            *(p + 6) = data[offset++];
            *(p + 5) = data[offset++];
            *(p + 4) = data[offset++];
            *(p + 3) = data[offset++];
            *(p + 2) = data[offset++];
            *(p + 1) = data[offset++];
            *(p) = data[offset++];

            return rt;
        }
    }


    public static unsafe long GetInt64(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(long*)ptr;
        }
        else
        {
            Int64 rt = 0;
            byte* p = (byte*)&rt;

            *(p + 7) = data[offset++];
            *(p + 6) = data[offset++];
            *(p + 5) = data[offset++];
            *(p + 4) = data[offset++];
            *(p + 3) = data[offset++];
            *(p + 2) = data[offset++];
            *(p + 1) = data[offset++];
            *(p) = data[offset++];

            return rt;

        }
    }

    public static unsafe float GetFloat32(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(float*)ptr;
        }
        else
        {
            float rt = 0;
            byte* p = (byte*)&rt;
            *p = data[offset + 3];
            *(p + 1) = data[offset + 2];
            *(p + 2) = data[offset + 1];
            *(p + 3) = data[offset];
            return rt;

        }
    }


    public static unsafe double GetFloat64(this byte[] data, uint offset, Endian endian)
    {
        if (endian == Endian.Little)
        {
            fixed (byte* ptr = &data[offset])
                return *(double*)ptr;
        }
        else
        {
            double rt = 0;
            byte* p = (byte*)&rt;

            *(p + 7) = data[offset++];
            *(p + 6) = data[offset++];
            *(p + 5) = data[offset++];
            *(p + 4) = data[offset++];
            *(p + 3) = data[offset++];
            *(p + 2) = data[offset++];
            *(p + 1) = data[offset++];
            *(p) = data[offset++];

            return rt;
        }
    }


    public static bool GetBoolean(this byte[] data, uint offset)
    {
        return data[offset] > 0;
    }

    public static char GetChar(this byte[] data, uint offset)
    {
        return Convert.ToChar(((data[offset] << 8) | data[offset + 1]));
    }


    public static string GetString(this byte[] data, uint offset, uint length)
    {
        return Encoding.UTF8.GetString(data, (int)offset, (int)length);
    }

    public static string[] GetStringArray(this byte[] data, uint offset, uint length)
    {
        List<string> ar = new List<string>();

        uint i = 0;

        while (i < length)
        {
            var cl = GetUInt32(data, offset + i, Endian.Little);
            i += 4;
            ar.Add(Encoding.UTF8.GetString(data, (int)(offset + i), (int)cl));
            i += cl;
        }

        return ar.ToArray();
    }

    public static Guid GetGuid(this byte[] data, uint offset)
    {
        return new Guid(Clip(data, offset, 16));
    }

    public static DateTime GetDateTime(this byte[] data, uint offset, Endian endian)
    {
        var ticks = GetInt64(data, offset, endian);
        return new DateTime(ticks, DateTimeKind.Utc);
    }


    public static IPAddress GetIPv4Address(this byte[] data, uint offset)
    {
        return new IPAddress((long)GetUInt32(data, offset, Endian.Little));
    }


    public static IPAddress GetIPv6Address(this byte[] data, uint offset)
    {
        return new IPAddress(Clip(data, offset, 16));
    }

    public static byte[] Clip(this byte[] data, uint offset, uint length)
    {
        if (data.Length < offset + length)
            return null;

        // if (length == data.Length && offset == 0)
        //   return data.ToArray();

        var b = new byte[length];
        Buffer.BlockCopy(data, (int)offset, b, 0, (int)length);
        return b;
    }

    public static string ToISODateTime(this DateTime date)
    {
        return date.ToString("yyyy-MM-dd HH:mm:ss");
    }
    public static uint ToUnixTime(this DateTime date)
    {
        return (uint)(date - new DateTime(1970, 1, 1)).TotalSeconds;
    }
}


