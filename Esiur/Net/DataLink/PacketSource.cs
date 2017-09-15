using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Engine;
using Esiur.Resource;

namespace Esiur.Net.DataLink
{
    public abstract class PacketSource: IResource
    {
        public delegate void NewPacket(Packet Packet);
        public abstract event NewPacket OnNewPacket;
        public event DestroyedEvent OnDestroy;

        public Instance Instance
        {
            get;
            set;
        }


        public abstract AsyncReply<bool> Trigger(ResourceTrigger trigger);


        public abstract bool RawMode
        {
            set;
            get;
        }

        //public PacketSource(PacketServer Server, bool RawMode)
        //{
        //  this.RawMode = RawMode;
        //}


        public abstract bool Open();

        public abstract bool Close();


        public abstract bool Write(Packet packet);

        public void Destroy()
        {
            throw new NotImplementedException();
        }

        /*
        public virtual string TypeName
        {
            get
            {
                return "Raw";
            }
        }
        */

        public abstract byte[] Address
        {
            get;
        }

        public abstract string DeviceId
        {
            get;
        }
    }
}
