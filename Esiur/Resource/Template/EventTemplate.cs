﻿using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class EventTemplate : MemberTemplate
    {
        public string Expansion
        {
            get;
            set;
        }

        public bool Listenable { get; set; }

        public EventInfo EventInfo { get; set; }

        public TemplateDataType ArgumentType { get; set; }

        public override byte[] Compose()
        {
            var name = base.Compose();

            if (Expansion != null)
            {
                var exp = DC.ToBytes(Expansion);
                return new BinaryList()
                        .AddUInt8(Listenable ? (byte) 0x58 : (byte) 0x50)
                        .AddUInt8((byte)name.Length)
                        .AddUInt8Array(name)
                        .AddUInt8Array(ArgumentType.Compose())
                        .AddInt32(exp.Length)
                        .AddUInt8Array(exp)
                        .ToArray();
            }
            else
                return new BinaryList()
                        .AddUInt8(Listenable ? (byte) 0x48 : (byte) 0x40)
                        .AddUInt8((byte)name.Length)
                        .AddUInt8Array(name)
                        .AddUInt8Array(ArgumentType.Compose())
                        .ToArray();
        }


        public EventTemplate(TypeTemplate template, byte index, string name, TemplateDataType argumentType, string expansion = null, bool listenable=false)
            :base(template, MemberType.Property, index, name)
        {
            this.Expansion = expansion;
            this.Listenable = listenable;
            this.ArgumentType = argumentType;
        }
    }
}
