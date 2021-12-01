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
using Esiur.Core;
using Esiur.Data;
using System.Runtime.InteropServices;
using Esiur.Net.Packets;
using Esiur.Resource;

namespace Esiur.Net.DataLink;
public class PacketServer : IResource
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
        OnDestroy?.Invoke(this);
    }

    public AsyncReply<bool> Trigger(ResourceTrigger trigger)
    {
        if (trigger == ResourceTrigger.Initialize)
        {
            /*
            foreach (var resource in Instance.Children<IResource>())
            {

                if (resource is PacketFilter)
                {
                    filters.Add(resource as PacketFilter);
                }
                else if (resource is PacketSource)
                {
                    sources.Add(resource as PacketSource);
                }
            }
            */
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

        return new AsyncReply<bool>(true);
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
