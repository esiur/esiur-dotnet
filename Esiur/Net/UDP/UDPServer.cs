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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Resource;
using Esiur.Core;

namespace Esiur.Net.UDP;

/* public class IIPConnection
{
    public EndPoint SenderPoint;
    public 
}*/
public class UDPServer : IResource
{
    Thread receiver;
    UdpClient udp;
    UDPFilter[] filters = new UDPFilter[0];

    public event DestroyedEvent OnDestroy;

    public Instance Instance
    {
        get;
        set;
    }

    [Attribute]
    string IP
    {
        get;
        set;
    }

    [Attribute]
    ushort Port
    {
        get;
        set;
    }

    private void Receiving()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);


        while (true)
        {
            byte[] b = udp.Receive(ref ep);

            foreach (var child in filters)
            {
                var f = child as UDPFilter;

                try
                {
                    if (f.Execute(b, ep))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Global.Log("UDPServer", LogType.Error, ex.ToString());
                    //Console.WriteLine(ex.ToString());
                }
            }
        }
    }

    public bool Send(byte[] Data, int Count, IPEndPoint EP)
    {
        try
        {
            udp.Send(Data, Count, EP);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public bool Send(byte[] Data, IPEndPoint EP)
    {
        try
        {
            udp.Send(Data, Data.Length, EP);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public bool Send(byte[] Data, int Count, string Host, int Port)
    {
        try
        {
            udp.Send(Data, Count, Host, Port);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public bool Send(byte[] Data, string Host, int Port)
    {
        try
        {
            udp.Send(Data, Data.Length, Host, Port);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public bool Send(string Data, IPEndPoint EP)
    {
        try
        {
            udp.Send(Encoding.Default.GetBytes(Data), Data.Length, EP);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public bool Send(string Data, string Host, int Port)
    {
        try
        {
            udp.Send(Encoding.Default.GetBytes(Data), Data.Length, Host, Port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Destroy()
    {
        udp.Close();
        OnDestroy?.Invoke(this);
    }

    async AsyncReply<bool> IResource.Trigger(ResourceTrigger trigger)
    {
        if (trigger == ResourceTrigger.Initialize)
        {
            var address = IP == null ? IPAddress.Any : IPAddress.Parse(IP);

            udp = new UdpClient(new IPEndPoint(address, Port));

            receiver = new Thread(Receiving);
            receiver.Start();
        }
        else if (trigger == ResourceTrigger.Terminate)
        {
            if (receiver != null)
                receiver.Abort();
        }
        else if (trigger == ResourceTrigger.SystemInitialized)
        {
            filters = await Instance.Children<UDPFilter>();
        }

        return true;
    }
}
