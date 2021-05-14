
/********************************************************************************\
* Uruky Project                                                                  *
*                                                                                *
* Copyright (C) 2006 Ahmed Zamil - ahmed@dijlh.com 		                         *
*                                   http://www.dijlh.com                         *
*                                                                                * 
* Permission is hereby granted, free of charge, to any person obtaining a copy   *
* of this software and associated documentation files (the "Software"), to deal  *
* in the Software without restriction, including without limitation the rights   *
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell      *
* copies of the Software, and to permit persons to whom the Software is          *
* furnished to do so, subject to the following conditions:                       *
*                                                                                *
* The above copyright notice and this permission notice shall be included in all *
* copies or substantial portions of the Software.                                *
*                                                                                *
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR     *
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,       *
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE    *
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER         *
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,  *
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE  *
* SOFTWARE.                                                                      *
*                                                                                * 
* File: Packet.cs                                                                *
* Description: Ethernet/ARP/IPv4/TCP/UDP Packet Decoding & Encoding Class        *
* Compatibility: .Net Framework 2.0 / Mono 1.1.8                                 *
*                                                                                *
\********************************************************************************/



using System;
using System.Text;
using Esiur.Misc;
using Esiur.Net.DataLink;
using System.Net.NetworkInformation;
using Esiur.Data;

namespace Esiur.Net.Packets
{
    internal static class Functions
    {
        public static void AddData(ref byte[] dest, byte[] src)
        {
            int I = 0;
            if (src == null)
            {
                return;
            }
            if (dest != null)
            {
                I = dest.Length;
                Array.Resize(ref dest, dest.Length + src.Length);
                //dest = (byte[])Resize(dest, dest.Length + src.Length);
            }
            else
            {
                dest = new byte[src.Length];
            }
            Array.Copy(src, 0, dest, I, src.Length);
        }

        /*
        public static Array Resize(Array array, int newSize)
        {
            Type myType = Type.GetType(array.GetType().FullName.TrimEnd('[', ']'));
            Array nA = Array.CreateInstance(myType, newSize);
            Array.Copy(array, nA, (newSize > array.Length ? array.Length : newSize));
            return nA;
        } */

        //Computes the checksum used in IP, ARP..., ie the
        // "The 16 bit one's complement of the one 's complement sum
        //of all 16 bit words" as seen in RFCs
        // Returns a 4 characters hex string
        // data's lenght must be multiple of 4, else zero padding
        public static ushort IP_CRC16(byte[] data)
        {
            ulong Sum = 0;
            bool Padding = false;
            /// * Padding if needed
            if (data.Length % 2 != 0)
            {
                Array.Resize(ref data, data.Length + 1);
                //data = (byte[])Resize(data, data.Length + 1);
                Padding = true;
            }
            int count = data.Length;
            ///* add 16-bit words */
            while (count > 0) //1)
            {
                ///*  this is the inner loop  */ 
                Sum += GetInteger(data[count - 2], data[count - 1]);
                ///*  Fold 32-bit sum to 16-bit  */ 
                while (Sum >> 16 != 0)
                {
                    Sum = (Sum & 0XFFFF) + (Sum >> 16);
                }
                count -= 2;
            }
            /// * reverse padding 
            if (Padding)
            {
                Array.Resize(ref data, data.Length - 1);
                //data = (byte[])Resize(data, data.Length - 1);
            }
            ///* Return one's compliment of final sum. 
            //return (ushort)(ushort.MaxValue - (ushort)Sum);
            return (ushort)(~Sum);
        }

        public static ushort GetInteger(byte B1, byte B2)
        {
            return BitConverter.ToUInt16(new byte[] { B2, B1 }, 0);
            //return System.Convert.ToUInt16("&h" + GetHex(B1) + GetHex(B2));
        }

        public static uint GetLong(byte B1, byte B2, byte B3, byte B4)
        {
            return BitConverter.ToUInt32(new byte[] { B4, B3, B2, B1 }, 0);
            //return System.Convert.ToUInt32("&h" + GetHex(B1) + GetHex(B2) + GetHex(B3) + GetHex(B4));
        }

        public static string GetHex(byte B)
        {
            return (((B < 15) ? 0 + System.Convert.ToString(B, 16).ToUpper() : System.Convert.ToString(B, 16).ToUpper()));
        }

        public static bool GetBit(uint B, byte Pos)
        {
            //return BitConverter.ToBoolean(BitConverter.GetBytes(B), Pos + 1);
            return (B & (uint)(Math.Pow(2, (Pos - 1)))) == (Math.Pow(2, (Pos - 1)));
        }

        public static ushort RemoveBit(ushort I, byte Pos)
        {
            return (ushort)RemoveBit((uint)I, Pos);
        }

        public static uint RemoveBit(uint I, byte Pos)
        {
            if (GetBit(I, Pos))
            {
                return I - (uint)(Math.Pow(2, (Pos - 1)));
            }
            else
            {
                return I;
            }
        }

        public static void SplitInteger(ushort I, ref byte BLeft, ref byte BRight)
        {
            byte[] b = BitConverter.GetBytes(I);
            BLeft = b[1];
            BRight = b[0];
            //BLeft = I >> 8;
            //BRight = (I << 8) >> 8;
        }

        public static void SplitLong(uint I, ref byte BLeft, ref byte BLeftMiddle, ref byte BRightMiddle, ref byte BRight)
        {
            byte[] b = BitConverter.GetBytes(I);
            BLeft = b[3];
            BLeftMiddle = b[2];
            BRightMiddle = b[1];
            BRight = b[0];
            //BLeft = I >> 24;
            //BLeftMiddle = (I << 8) >> 24;
            //BRightMiddle = (I << 16) >> 24;
            //BRight = (I << 24) >> 24;
        }

    }

    public class PosixTime
    {
        ulong seconds;
        ulong microseconds;

        PosixTime(ulong Seconds, ulong Microseconds)
        {
            seconds = Seconds;
            microseconds = Microseconds;
        }

        public override string ToString()
        {
            return seconds + "." + microseconds;
        }
    }

    public class Packet
    {
        //public EtherServer2.EthernetSource Source;

        public PacketSource Source;

        public DateTime Timestamp;

        public enum PPPType : ushort
        {
            IP = 0x0021,                         // Internet Protocol version 4                   [RFC1332]
            SDTP = 0x0049,                       // Serial Data Transport Protocol (PPP-SDTP)     [RFC1963]
            IPv6HeaderCompression = 0x004f,      // IPv6 Header Compression
            IPv6 = 0x0057,                       // Internet Protocol version 6                   [RFC5072]
            W8021dHelloPacket = 0x0201,          // 802.1d Hello Packets                          [RFC3518]
            IPv6ControlProtocol = 0x8057,        // IPv6 Control Protocol                         [RFC5072]
        }

        public enum ProtocolType : ushort
        {
            IP = 0x800,                          // IPv4
            ARP = 0x806,                         // Address Resolution Protocol
            IPv6 = 0x86DD,                       // IPv6
            FrameRelayARP = 0x0808,              // Frame Relay ARP          [RFC1701]
            VINESLoopback = 0x0BAE,              // VINES Loopback           [RFC1701]
            VINESEcho = 0x0BAF,                  // VINES ECHO               [RFC1701]
            TransEtherBridging = 0x6558,         // TransEther Bridging      [RFC1701]
            RawFrameRelay = 0x6559,              // Raw Frame Relay          [RFC1701]
            IEE8021QVLAN = 0x8100,               // IEEE 802.1Q VLAN-tagged frames (initially Wellfleet)
            SNMP = 0x814C,                       // SNMP                     [JKR1]
            TCPIP_Compression = 0x876B,          // TCP/IP Compression       [RFC1144]
            IPAutonomousSystems = 0x876C,        // IP Autonomous Systems    [RFC1701]
            SecureData = 0x876D,                 // Secure Data              [RFC1701]
            PPP = 0x880B,                        // PPP                      [IANA] 
            MPLS = 0x8847,                       // MPLS                     [RFC5332]
            MPLS_UpstreamAssignedLabel = 0x8848, // MPLS with upstream-assigned label   [RFC5332]
            PPPoEDiscoveryStage = 0x8863,        // PPPoE Discovery Stage    [RFC2516]
            PPPoESessionStage = 0x8864,          // PPPoE Session Stage      [RFC2516]
        }


        /*
        public static void GetPacketMACAddresses(Packet packet, out byte[] srcMAC, out byte[] dstMAC)
        {

            // get the node address
            Packet root = packet.RootPacket;
            if (root is TZSPPacket)
            {

                TZSPPacket tp = (TZSPPacket)root;
                if (tp.Protocol == TZSPPacket.TZSPEncapsulatedProtocol.Ethernet)
                {
                    EthernetPacket ep = (EthernetPacket)tp.SubPacket;
                    srcMAC = ep.SourceMAC;
                    dstMAC = ep.DestinationMAC;
                }
                else if (tp.Protocol == TZSPPacket.TZSPEncapsulatedProtocol.IEEE802_11)
                {
                    W802_11Packet wp = (W802_11Packet)tp.SubPacket;
                    srcMAC = wp.SA;
                    dstMAC = wp.DA;
                }
                else
                {
                    srcMAC = null;
                    dstMAC = null;
                }
            }
            else if (root is EthernetPacket)
            {
                EthernetPacket ep = (EthernetPacket)root;
                srcMAC = ep.SourceMAC;
                dstMAC = ep.DestinationMAC;
            }
            else if (root is W802_11Packet)
            {
                W802_11Packet wp = (W802_11Packet)root;
                srcMAC = wp.SA;
                dstMAC = wp.DA;
            }
            else
            {
                srcMAC = null;
                dstMAC = null;
            }

        }

        
        public static void GetPacketAddresses(Packet packet, ref string srcMAC, ref string dstMAC, ref string srcIP, ref string dstIP)
        {

            if (packet is TCPv4Packet)
            {
                if (packet.ParentPacket is IPv4Packet)
                {
                    IPv4Packet ip = (IPv4Packet)packet.ParentPacket;
                    srcIP = ip.SourceIP.ToString();
                    dstIP = ip.DestinationIP.ToString();
                }
            }

            // get the node address
            Packet root = packet.RootPacket;
            if (root is TZSPPacket)
            {

                TZSPPacket tp = (TZSPPacket)root;
                if (tp.Protocol == TZSPPacket.TZSPEncapsulatedProtocol.Ethernet)
                {
                    EthernetPacket ep = (EthernetPacket)tp.SubPacket;
                    srcMAC = DC.GetPhysicalAddress(ep.SourceMAC, 0).ToString();
                    dstMAC = DC.GetPhysicalAddress(ep.DestinationMAC, 0).ToString();
                }
                else if (tp.Protocol == TZSPPacket.TZSPEncapsulatedProtocol.IEEE802_11)
                {
                    W802_11Packet wp = (W802_11Packet)tp.SubPacket;
                    srcMAC = DC.GetPhysicalAddress(wp.SA, 0).ToString();
                    dstMAC = DC.GetPhysicalAddress(wp.DA, 0).ToString();
                }
            }
            else if (root is EthernetPacket)
            {
                EthernetPacket ep = (EthernetPacket)root;
                srcMAC = DC.GetPhysicalAddress(ep.SourceMAC, 0).ToString();
                dstMAC = DC.GetPhysicalAddress(ep.DestinationMAC, 0).ToString();
            }
            else if (root is W802_11Packet)
            {
                W802_11Packet wp = (W802_11Packet)root;
                srcMAC = DC.GetPhysicalAddress(wp.SA, 0).ToString();
                dstMAC = DC.GetPhysicalAddress(wp.DA, 0).ToString();
            }
        }
        */

        //PosixTime Timeval;
        public byte[] Header;
        public byte[] Preamble;
        //public byte[] Payload;
        public byte[] Data;

        public Packet SubPacket;
        public Packet ParentPacket;
        public virtual long Parse(byte[] data, uint offset, uint ends) { return 0; }
        public virtual bool Compose() { return false; }

        public Packet RootPacket
        {
            get
            {
                Packet root = this;
                while (root.ParentPacket != null)
                    root = root.ParentPacket;
                return root;
            }
        }

        public Packet LeafPacket
        {
            get
            {
                Packet leaf = this;
                while (leaf.SubPacket != null)
                    leaf = leaf.SubPacket;
                return leaf;
            }
        }
    }

}
/************************************ EOF *************************************/


