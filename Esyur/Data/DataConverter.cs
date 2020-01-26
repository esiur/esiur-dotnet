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
using Esyur.Net.IIP;

using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Esyur.Data;
using Esyur.Core;
using Esyur.Resource;

namespace Esyur.Data
{
    public static class DC // Data Converter
    {
        public static object CastConvert(object value, Type destinationType)
        {

            if (value == null)
                return null;

            //if (destinationType.IsArray && destinationType.GetElementType().IsArray)
              //  Console.Beep();

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

                        //                        try
                        //                        {
                        //#if NETSTANDARD
                        //                            if (destinationType.GetTypeInfo().IsInstanceOfType(v.GetValue(i)))
                        //#else
                        //                            if (destinationType.IsInstanceOfType(v.GetValue(i)))
                        //#endif
                        //                                rt.SetValue(v.GetValue(i), i);
                        //                            else
                        //                                rt.SetValue(Convert.ChangeType(v.GetValue(i), destinationType), i);
                        //                        }
                        //                        catch
                        //                        {
                        //                            rt.SetValue(null, i);
                        //                        }
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
                        else if (sourceType == typeof(Structure) && sourceType.IsAssignableFrom(destinationType))
                        {
                            return Structure.FromStructure((Structure)value, destinationType);
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

        public static byte[] ToBytes(Guid[] value)
        {
            var rt = new List<byte>();
            foreach (var g in value)
                rt.AddRange(g.ToByteArray());
            return rt.ToArray();
        }

        public static byte[] ToBytes(char[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }

        public static byte[] ToBytes(short[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }



        public static byte[] ToBytes(ushort[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
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


        public static byte[] ToBytes(this int[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }

        public static byte[] ToBytes(this uint[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }

        public static byte[] ToBytes(this long[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }

        public static byte[] ToBytes(this ulong[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }

        public static byte[] ToBytes(this float[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }

        public static byte[] ToBytes(this double[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }


        public static byte[] ToBytes(this decimal[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }


        public static byte[] ToBytes(this DateTime[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
                rt.AddRange(ToBytes(value[i]));

            return rt.ToArray();
        }


        public static byte[] ToBytes(this string[] value)
        {
            List<byte> rt = new List<byte>();

            for (int i = 0; i < value.Length; i++)
            {
                byte[] ba = ToBytes(value[i]);
                // add string length
                rt.AddRange(ToBytes(ba.Length));
                // add encoded string
                rt.AddRange(ba);
            }

            return rt.ToArray();
        }

        public static unsafe byte[] ToBytes(this int value)
        {
            var rt = new byte[4];
            byte* p = (byte*)&value;

            rt[0] = *(p + 3);
            rt[1] = *(p + 2);
            rt[2] = *(p + 1);
            rt[3] = *(p + 0);

            return rt;
        }

        public static unsafe byte[] ToBytes(this short value)
        {
            var rt = new byte[2];
            byte* p = (byte*)&value;

            rt[0] = *(p + 1);
            rt[1] = *(p + 0);

            return rt;
        }

        public static unsafe byte[] ToBytes(this float value)

        {
            var rt = new byte[4];

            //float rt = 0;
            byte* p = (byte*)&value;
            rt[0] = *(p + 3);
            rt[1] = *(p + 2);
            rt[2] = *(p + 1);
            rt[3] = *(p);

            return rt;
        }




        public static byte[] ToBytes(this string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public unsafe static byte[] ToBytes(this double value)
        {
            var rt = new byte[8];

            byte* p = (byte*)&value;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);

            return rt;
        }

        public static unsafe byte[] ToBytes(this long value)
        {
            var rt = new byte[8];

            byte* p = (byte*)&value;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);

            return rt;

        }

        public static unsafe byte[] ToBytes(this DateTime value)
        {

            var rt = new byte[8];
            var v = value.ToUniversalTime().Ticks;

            byte* p = (byte*)&v;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);

            return rt;

        }


        public static unsafe byte[] ToBytes(this ulong value)
        {
            var rt = new byte[8];

            byte* p = (byte*)&value;

            rt[0] = *(p + 7);
            rt[1] = *(p + 6);
            rt[2] = *(p + 5);
            rt[3] = *(p + 4);
            rt[4] = *(p + 3);
            rt[5] = *(p + 2);
            rt[6] = *(p + 1);
            rt[7] = *(p + 0);

            return rt;
        }

        public static unsafe byte[] ToBytes(this uint value)
        {

            var rt = new byte[4];

            byte* p = (byte*)&value;

            rt[0] = *(p + 3);
            rt[1] = *(p + 2);
            rt[2] = *(p + 1);
            rt[3] = *(p + 0);

            return rt;
        }

        public static unsafe byte[] ToBytes(this ushort value)
        {
            var rt = new byte[2];

            byte* p = (byte*)&value;

            rt[0] = *(p + 1);
            rt[1] = *(p);

            return rt;
        }

        public static byte[] ToBytes(this decimal value)
        {
            byte[] ret = new byte[0];// BitConverter.GetBytes(value);

            Array.Reverse(ret);

            return ret;
        }

        public static string ToHex(this byte[] ba)
        {
            if (ba == null)
                return "NULL";
            return ToHex(ba, 0, (uint)ba.Length);
        }

        public static string ToHex(this byte[] ba, uint offset, uint length, string separator = " ")
        {
            StringBuilder hex = new StringBuilder((int)length * 2);

            for (var i = offset; i < offset + length; i++)
            {
                hex.AppendFormat("{0:x2}", ba[i]);
                if (separator != null)
                    hex.Append(separator);
            }

            return hex.ToString();
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

        public static sbyte[] GetInt8Array(this byte[] data, uint offset, uint length)
        {
            var rt = new sbyte[length];
            Buffer.BlockCopy(data, (int)offset, rt, 0, (int)length);
            return rt;
        }

        public static byte GetUInt8(this byte[] data, uint offset)
        {
            return data[offset];
        }

        public static byte[] GetUInt8Array(this byte[] data, uint offset, uint length)
        {
            var rt = new byte[length];
            Buffer.BlockCopy(data, (int)offset, rt, 0, (int)length);
            return rt;
        }

        public static Int16 GetInt16(this byte[] data, uint offset)
        {
            return (Int16)((data[offset] << 8) | data[offset + 1]);
        }

        public static Int16[] GetInt16Array(this byte[] data, uint offset, uint length)
        {
            var rt = new Int16[length / 2];
            for (var i = 0; i < length; i += 2)
                rt[i] = GetInt16(data, (uint)(offset + i));

            return rt;
        }

        public static UInt16 GetUInt16(this byte[] data, uint offset)
        {
            return (UInt16)((data[offset] << 8) | data[offset + 1]);
        }

        public static UInt16[] GetUInt16Array(this byte[] data, uint offset, uint length)
        {
            var rt = new UInt16[length / 2];
            for (var i = 0; i < length; i += 2)
                rt[i] = GetUInt16(data, (uint)(offset + i));
            return rt;
        }

        public static Int32 GetInt32(this byte[] data, uint offset)
        {
            return (Int32)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        public static Int32[] GetInt32Array(this byte[] data, uint offset, uint length)
        {
            var rt = new Int32[length / 4];
            for (var i = 0; i < length; i += 4)
                rt[i] = GetInt32(data, (uint)(offset + i));

            return rt;
        }

        public static UInt32 GetUInt32(this byte[] data, uint offset)
        {
            return (UInt32)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        public static UInt32[] GetUInt32Array(this byte[] data, uint offset, uint length)
        {
            var rt = new UInt32[length / 4];
            for (var i = 0; i < length; i += 4)
                rt[i] = GetUInt32(data, (uint)(offset + i));

            return rt;
        }


        public static unsafe UInt64 GetUInt64(this byte[] data, uint offset)
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

        public static Int64[] GetInt64Array(this byte[] data, uint offset, uint length)
        {
            var rt = new Int64[length / 8];
            for (var i = 0; i < length; i += 8)
                rt[i] = GetInt64(data, (uint)(offset + i));

            return rt;
        }

        public static unsafe Int64 GetInt64(this byte[] data, uint offset)
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

            /* Or 
            return (Int64)(
                             (data[offset] << 56)
                           | (data[offset + 1] << 48)
                           | (data[offset + 2] << 40)
                           | (data[offset + 3] << 32)
                           | (data[offset + 4] << 24)
                           | (data[offset + 5] << 16)
                           | (data[offset + 6] << 8)
                           | (data[offset + 7])
                );
           */
        }

        public static UInt64[] GetUInt64Array(this byte[] data, uint offset, uint length)
        {
            var rt = new UInt64[length / 8];
            for (var i = 0; i < length; i += 8)
                rt[i] = GetUInt64(data, (uint)(offset + i));

            return rt;
        }

        public static unsafe float GetFloat32(this byte[] data, uint offset)
        {
            float rt = 0;
            byte* p = (byte*)&rt;
            *p = data[offset + 3];
            *(p + 1) = data[offset + 2];
            *(p + 2) = data[offset + 1];
            *(p + 3) = data[offset];
            return rt;
        }

        public static float[] GetFloat32Array(this byte[] data, uint offset, uint length)
        {
            var rt = new float[length / 4];
            for (var i = 0; i < length; i += 4)
                rt[i] = GetFloat32(data, (uint)(offset + i));

            return rt;
        }

        public static unsafe double GetFloat64(this byte[] data, uint offset)
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

        public static double[] GetFloat64Array(this byte[] data, uint offset, uint length)
        {
            var rt = new double[length / 8];
            for (var i = 0; i < length; i += 8)
                rt[i] = GetFloat64(data, (uint)(offset + i));

            return rt;
        }

        public static bool GetBoolean(this byte[] data, uint offset)
        {
            return data[offset] > 0;
        }

        public static bool[] GetBooleanArray(this byte[] data, uint offset, uint length)
        {
            var rt = new bool[length];
            for (var i = 0; i < length; i++)
                rt[i] = data[offset + i] > 0;
            return rt;
        }

        public static char GetChar(this byte[] data, uint offset)
        {
            return Convert.ToChar(((data[offset] << 8) | data[offset + 1]));
        }

        public static char[] GetCharArray(this byte[] data, uint offset, uint length)
        {
            var rt = new char[length / 2];
            for (var i = 0; i < length; i += 2)
                rt[i] = GetChar(data, (uint)(offset + i));
            return rt;
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
                var cl = GetUInt32(data, offset + i);
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

        public static Guid[] GetGuidArray(this byte[] data, uint offset, uint length)
        {
            var rt = new Guid[length / 16];
            for (var i = 0; i < length; i += 16)
                rt[i] = GetGuid(data, (uint)(offset + i));
            return rt;
        }

        public static DateTime GetDateTime(this byte[] data, uint offset)
        {
            var ticks = GetInt64(data, offset);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        public static DateTime[] GetDateTimeArray(this byte[] data, uint offset, uint length)
        {
            var rt = new DateTime[length / 8];
            for (var i = 0; i < length; i += 8)
                rt[i] = GetDateTime(data, (uint)(offset + i));
            return rt;
        }

        /*
        public static PhysicalAddress GetPhysicalAddress(this byte[] data, uint offset)
        {
            return new PhysicalAddress(Clip(data, offset, 6));
        }

        public static PhysicalAddress[] GetPhysicalAddressArray(this byte[] data, uint offset, uint length)
        {
            var rt = new PhysicalAddress[length / 6];
            for (var i = 0; i < length; i += 6)
                rt[i] = GetPhysicalAddress(data, (uint)(offset + i));
            return rt;
        }
        */
        public static IPAddress GetIPv4Address(this byte[] data, uint offset)
        {
            return new IPAddress((long)GetUInt32(data, offset));
        }

        public static IPAddress[] GetIPv4AddressArray(this byte[] data, uint offset, uint length)
        {
            var rt = new IPAddress[length / 4];
            for (var i = 0; i < length; i += 4)
                rt[i] = GetIPv4Address(data, (uint)(offset + i));
            return rt;
        }

        public static IPAddress GetIPv6Address(this byte[] data, uint offset)
        {
            return new IPAddress(Clip(data, offset, 16));
        }

        public static IPAddress[] GetIPv6AddressArray(this byte[] data, uint offset, uint length)
        {
            var rt = new IPAddress[length / 16];
            for (var i = 0; i < length; i += 16)
                rt[i] = GetIPv6Address(data, (uint)(offset + i));
            return rt;
        }

        /*
        public static T FromBytes<T>(byte[] data, uint offset, uint length = 0)
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)(data[offset] == 1);
            }
            else if (typeof(T) == typeof(byte))
            {
                return (T)(object)data[offset];
            }
            else if (typeof(T) == typeof(char))
            {
                return (T)(object)BitConverter.ToChar(ReverseArray(data, offset, 2), 0);
            }
            else if (typeof(T) == typeof(short))
            {
                return (T)(object)BitConverter.ToInt16(ReverseArray(data, offset, 2), 0);
            }
            else if (typeof(T) == typeof(ushort))
            {
                return (T)(object)BitConverter.ToUInt16(ReverseArray(data, offset, 2), 0);
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)BitConverter.ToInt32(ReverseArray(data, offset, 4), 0);
            }
            else if (typeof(T) == typeof(uint))
            {
                return (T)(object)BitConverter.ToUInt32(ReverseArray(data, offset, 4), 0);
            }
            else if (typeof(T) == typeof(long))
            {
                return (T)(object)BitConverter.ToInt64(ReverseArray(data, offset, 8), 0);
            }
            else if (typeof(T) == typeof(ulong))
            {
                return (T)(object)BitConverter.ToUInt64(ReverseArray(data, offset, 8), 0);
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)BitConverter.ToSingle(ReverseArray(data, offset, 4), 0);
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)BitConverter.ToDouble(ReverseArray(data, offset, 8), 0);
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)Encoding.UTF8.GetString(data, (int)offset, (int)length);
            }
            else if (typeof(T) == typeof(Guid))
            {
                return (T)(object)new Guid(DC.Clip(data, offset, 16));
            }
            else if (typeof(T) == typeof(IPAddress))
            {
                if (length == 0)
                    return (T)(object)(new IPAddress((long)GetUInt32(data, offset)));
                else
                    return (T)(object)(new IPAddress(Clip(data, offset, length)));
            }
            else if (typeof(T) == typeof(PhysicalAddress))
            {
                return (T)(object)new PhysicalAddress(Clip(data, offset, 6));
            }
            else if (typeof(T) == typeof(DateTime))
            {
                long ticks = BitConverter.ToInt64(ReverseArray(data, offset, 8), 0);
                return (T)(object)new DateTime(ticks, DateTimeKind.Utc);
            }
            else
            {
                return default(T);
            }
        }
        */

        public static byte[] Clip(this byte[] data, uint offset, uint length)
        {
            if (data.Length < offset + length)
                return null;

            if (length == data.Length && offset == 0)
                return data;

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


}
