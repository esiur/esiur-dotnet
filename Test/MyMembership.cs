﻿using Esiur.Data;
using Esiur.Engine;
using Esiur.Resource;
using Esiur.Security.Membership;
using System;
using System.Collections.Generic;
using System.Text;

namespace Test
{
    class MyMembership : IMembership
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;



        public void Destroy()
        {
        }

        public AsyncReply<byte[]> GetPassword(string username, string domain)
        {
           return new AsyncReply<byte[]>(DC.ToBytes("1234"));
        }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>(true);
        }

        public AsyncReply<bool> UserExists(string username)
        {
            return new AsyncReply<bool>(username == "demo");    
        }
    }

}
