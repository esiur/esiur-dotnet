using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Engine;
using Esiur.Data;
using System.Runtime.InteropServices;
using Esiur.Net.Packets;
using Esiur.Resource;

namespace Esiur.Net.DataLink
{
    public class PacketServer:IResource
    {
        List<PacketSource> sources = new List<PacketSource>();
        List<PacketFilter> filters = new List<PacketFilter>();

        
        [Storable]
        public string Mode
        {
            get;
            set;
        }

        public Instance Instance
        {
            get;
            set;
        }

        public List<PacketSource> Sources
        {
            get
            {
                return sources;
            }
        }

        public event DestroyedEvent OnDestroy;

        public void Destroy()
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            if (trigger == ResourceTrigger.Initialize)
            {

                foreach (Instance instance in Instance.Children)
                {

                    if (instance.Resource is PacketFilter)
                    {
                        filters.Add(instance.Resource as PacketFilter);
                    }
                    else if (instance.Resource is PacketSource)
                    {
                        sources.Add(instance.Resource as PacketSource);
                    }
                }

                foreach (var src in sources)
                {
                    src.OnNewPacket += PacketReceived;
                    src.Open();
                }
            }
            else if (trigger == ResourceTrigger.Terminate)
            {
    //            foreach (var src in sources)
      //              src.Close();
            }
            else if (trigger == ResourceTrigger.SystemReload)
            {
                foreach (var src in sources)
                {
                    src.Close();
                    src.Open();
                }
            }

            return new AsyncReply<bool>( true);
        }        

        void PacketReceived(Packet Packet)
        {
            foreach (var f in filters)
            {                
                if (f.Execute(Packet))
                {
                    break;
                }
            }
        }
    }
}
