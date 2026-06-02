using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    internal class TruComposite : Tru
    {
        public Tru[] SubTypes;

        public override Type RuntimeType { get; protected set; }

        public TruComposite(TruIdentifier identifier, bool nullable, Tru[] subTypes, Type? type)
        {
            Identifier = identifier;
            Nullable = nullable;
            SubTypes = subTypes;
            RuntimeType = type;
            //_runtimeType = typeof(Tuple).MakeGenericType(subTypes.Select(x => x.RuntimeType).ToArray());
        }

        public override void SetNull(List<byte> flags)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() == 2);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }

            foreach (var st in SubTypes)
                st.SetNull(flags);
        }


        public override void SetNull(byte flag)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flag == 2);
            }

            foreach (var st in SubTypes)
                st.SetNull(flag);
        }

        public override void SetNotNull(List<byte> flags)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() != 1);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }

            foreach (var st in SubTypes)
                st.SetNotNull(flags);
        }

        public override void SetNotNull(byte flag)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flag != 1);
            }

            if (SubTypes != null)
                foreach (var st in SubTypes)
                    st.SetNotNull(flag);
        }

        public override bool Match(Tru other)
        {

            if (other is TruComposite otherComposite)
            {
                if (other.Identifier != Identifier)
                    return false;


                if (otherComposite.SubTypes.Length != (SubTypes?.Length ?? -1))
                    return false;

                for (var i = 0; i < SubTypes?.Length; i++)
                    if (!SubTypes[i].Match(otherComposite.SubTypes[i]))
                        return false;

                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return Identifier.ToString() + "<" + String.Join(",", SubTypes.Select(x => x.ToString())) + ">" + (Nullable ? "?" : "");
        }

        public override byte[] Compose(EpConnection connection)
        {
            var rt = new BinaryList();

            if (Nullable)
                rt.AddUInt8((byte)(0x80 | (byte)Identifier));
            else
                rt.AddUInt8((byte)Identifier);


            for (var i = 0; i < SubTypes.Length; i++)
                rt.AddUInt8Array(SubTypes[i].Compose(connection));

            return rt.ToArray();
        }

        public override Tru ToNullable()
        {
            throw new NotImplementedException();
        }
    }
}
