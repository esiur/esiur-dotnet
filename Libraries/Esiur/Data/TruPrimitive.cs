using Esiur.Data.Types;
using Esiur.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public class TruPrimitive:Tru
    {
        public override Type RuntimeType { get; protected set; }

        public override string ToString()
        {
            return Identifier.ToString() + (Nullable ? "?" : "");
        }

        public TruPrimitive(TruIdentifier identifier, bool nullable, Type type)
        {
            Identifier = identifier;
            Nullable = nullable;
            RuntimeType = type;
        }

        public override void SetNull(List<byte> flags)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() == 2);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }
        }


        public override void SetNull(byte flag)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flag == 2);
            }
        }

        public override void SetNotNull(List<byte> flags)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() != 1);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }
        }

        public override void SetNotNull(byte flag)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flag != 1);
            }
        }

        public override bool Match(Tru other)
        {

            if (other is TruPrimitive otherComposite)
            {
                if (other.Identifier != Identifier)
                    return false;

                return true;
            }

            return false;
        }

        public override byte[] Compose(EpConnection connection)
        {
            var rt = new BinaryList();

            if (Nullable)
                rt.AddUInt8((byte)(0x80 | (byte)Identifier));
            else
                rt.AddUInt8((byte)Identifier);

            return rt.ToArray();

        }

        public override Tru ToNullable()
        {
            throw new NotImplementedException();
        }

    }
}
