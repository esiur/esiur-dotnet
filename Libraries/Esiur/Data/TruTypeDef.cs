using Esiur.Data.Types;
using Esiur.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public class TruTypeDef : Tru
    {
        public TypeDef? TypeDef;

        public override Type RuntimeType { get; protected set; }

        public override void SetNull(List<byte> flags)
        {
            if (RefTypes.Contains(Identifier))
            {
                Nullable = (flags.FirstOrDefault() == 2);
                if (flags.Count > 0)
                    flags.RemoveAt(0);
            }
        }

        public TruTypeDef(bool nullable, TypeDef typeDef)
        {
            Nullable = nullable;
            TypeDef = typeDef;

            if (typeDef is LocalTypeDef localTypeDef)
                RuntimeType = localTypeDef.DefinedType;
            else if (typeDef is RemoteTypeDef remoteTypeDef)
                RuntimeType = remoteTypeDef.ProxyType;
        }

        public override void SetNull(byte flag)
        {
            Nullable = (flag == 2);
        }

        public override void SetNotNull(List<byte> flags)
        {
            Nullable = (flags.FirstOrDefault() != 1);
            if (flags.Count > 0)
                flags.RemoveAt(0);
        }

        public override void SetNotNull(byte flag)
        {
            Nullable = (flag != 1);
        }

        public override bool Match(Tru other)
        {
            if (other is TruTypeDef otherComposite)
            {
                if (otherComposite.TypeDef != TypeDef)
                    return false;

                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return Identifier.ToString() + (Nullable ? "?" : "");
        }

        public override byte[] Compose(EpConnection connection)
        {
            var rt = new BinaryList();

            if (TypeDef is RemoteTypeDef remoteTypeDef)
            {
                if (connection.RemoteDomain == remoteTypeDef.Domain)
                {
                    // this is local in respect to the connection, send the remote typdef id.
                    if (Nullable)
                        rt.AddUInt8(0x80 | (byte)TruIdentifier.LocalType8);
                    else
                        rt.AddUInt8((byte)TruIdentifier.LocalType8);

                    rt.AddUInt8((byte)remoteTypeDef.Id);
                }
                else
                {
                    // this is remote in respect to the connection and the local typedef id is used.
                    if (Nullable)
                        rt.AddUInt8(0x80 | (byte)TruIdentifier.RemoteType8);
                    else
                        rt.AddUInt8((byte)TruIdentifier.RemoteType8);

                    rt.AddUInt8((byte)remoteTypeDef.LocalTypeDefId);
                }
            }
            else if (TypeDef is LocalTypeDef localTypeDef)
            {
                if (connection == null)
                {
                    // if there is no connection, we assume it's local.
                    if (Nullable)
                        rt.AddUInt8(0x80 | (byte)TruIdentifier.LocalType8);
                    else
                        rt.AddUInt8((byte)TruIdentifier.LocalType8);

                    rt.AddUInt8((byte)localTypeDef.Id);
                }
                else
                {
                    // this is remote, unless the connection is to self @TODO: solve for this state.
                    if (Nullable)
                        rt.AddUInt8(0x80 | (byte)TruIdentifier.RemoteType8);
                    else
                        rt.AddUInt8((byte)TruIdentifier.RemoteType8);

                    rt.AddUInt8((byte)localTypeDef.Id);
                }
            }
            else
                throw new NotImplementedException();

            return rt.ToArray();
        }

        public override Tru ToNullable()
        {
            throw new NotImplementedException();
        }

    }
}
