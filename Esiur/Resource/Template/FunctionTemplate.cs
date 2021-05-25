using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template
{
    public class FunctionTemplate : MemberTemplate
    {

        public string Expansion
        {
            get;
            set;
        }

        //public bool IsVoid
        //{
        //    get;
        //    set;
        //}

        public TemplateDataType ReturnType { get; set; }

        public ArgumentTemplate[] Arguments { get; set; }

        public MethodInfo MethodInfo
        {
            get;
            set;
        }


        public override byte[] Compose()
        {
 
            var name = base.Compose();
            
            var bl = new BinaryList()
                    //.AddUInt8(Expansion != null ? (byte)0x10 : (byte)0)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(ReturnType.Compose())
                    .AddUInt8((byte)Arguments.Length);

            for (var i = 0; i < Arguments.Length; i++)
                bl.AddUInt8Array(Arguments[i].Compose());


            if (Expansion != null)
            {
                var exp = DC.ToBytes(Expansion);
                bl.AddInt32(exp.Length)
                .AddUInt8Array(exp);
                bl.InsertUInt8(0, 0x10);
            }
            else
                bl.InsertUInt8(0, 0x0);

            return bl.ToArray();
        }


        public FunctionTemplate(ResourceTemplate template, byte index, string name, ArgumentTemplate[] arguments, TemplateDataType returnType, string expansion = null)
            : base(template, MemberType.Property, index, name)
        {
            //this.IsVoid = isVoid;
            this.Arguments = arguments;
            this.ReturnType = returnType;
            this.Expansion = expansion;
        }
    }
}
