using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Engine;
using Esiur.Resource;

namespace Esiur.Net.UDP
{

    /* public class IIPConnection
    {
        public EndPoint SenderPoint;
        public 
    }*/
    public class UDPServer : IResource
    {
        Thread Receiver;
        UdpClient Udp;

        public event DestroyedEvent OnDestroy;

        public Instance Instance
        {
            get;
            set;
        }

        [Storable]
        string ip
        {
            get;
            set;
        }

        [Storable]
        ushort port
        {
            get;
            set;
        }

        public bool Trigger(ResourceTrigger trigger)
        {
            if (trigger == ResourceTrigger.Initialize)
            {
                var address = ip == null ? IPAddress.Any : IPAddress.Parse(ip);

                Udp = new UdpClient(new IPEndPoint(address, (int)port));
                Receiver = new Thread(Receiving);
                Receiver.Start();
            }
            else if (trigger == ResourceTrigger.Terminate)
            {
                if (Receiver != null)
                    Receiver.Abort();
            }
            return true;
        }

        private void Receiving()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            

            while (true)
            {
                byte[] b = Udp.Receive(ref ep);

                foreach(var child in Instance.Children)
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
                Udp.Send(Data, Count, EP);
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
                Udp.Send(Data, Data.Length, EP);
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
                Udp.Send(Data, Count, Host, Port);
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
                Udp.Send(Data, Data.Length, Host, Port);
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
                Udp.Send(Encoding.Default.GetBytes(Data), Data.Length, EP);
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
                Udp.Send(Encoding.Default.GetBytes(Data), Data.Length, Host, Port);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Destroy()
        {
            Udp.Close();
            OnDestroy?.Invoke(this);
        }
    }
}