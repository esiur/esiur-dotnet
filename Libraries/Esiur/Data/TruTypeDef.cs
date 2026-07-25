using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Esiur.Data
{
    public class TruTypeDef : Tru
    {
        public TypeDef? TypeDef;

        Type _runtimeType = null;

        public override Type RuntimeType => _runtimeType ?? UpdateRuntimeType();


        private Type UpdateRuntimeType()
        {
            if (TypeDef is LocalTypeDef localTypeDef)
                _runtimeType = localTypeDef.DefinedType;
            else if (TypeDef is RemoteTypeDef remoteTypeDef)
            {
                _runtimeType = remoteTypeDef.ProxyType;
            }

            return _runtimeType ?? typeof(object);
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

        public TruTypeDef(bool nullable, TypeDef typeDef)
        {
            Nullable = nullable;
            TypeDef = typeDef;

            UpdateRuntimeType();
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
                    // this is local in respect to the connection, send the remote typdef id.
                    WriteTypeReference(rt, isLocal: true, Nullable, remoteTypeDef.Id);
                else
                    // this is remote in respect to the connection and the local typedef id is used.
                    WriteTypeReference(rt, isLocal: false, Nullable, remoteTypeDef.LocalTypeDefId);
            }
            else if (TypeDef is LocalTypeDef localTypeDef)
            {
                if (connection == null)
                    // if there is no connection, we assume it's local.
                    WriteTypeReference(rt, isLocal: true, Nullable, localTypeDef.Id);
                else
                    // this is remote, unless the connection is to self @TODO: solve for this state.
                    WriteTypeReference(rt, isLocal: false, Nullable, localTypeDef.Id);
            }
            else
                throw new NotImplementedException();

            return rt.ToArray();
        }

        // Picks the narrowest Local/RemoteType{8,16,32,64} identifier that fits `id`,
        // matching the widths Tru.Parse already knows how to read.
        // Internal (rather than private) so it can be unit-tested directly without
        // needing a real LocalTypeDef/Warehouse registration for every width tier.
        internal static void WriteTypeReference(BinaryList rt, bool isLocal, bool nullable, ulong id)
        {
            TruIdentifier identifier;

            if (id <= byte.MaxValue)
                identifier = isLocal ? TruIdentifier.LocalType8 : TruIdentifier.RemoteType8;
            else if (id <= ushort.MaxValue)
                identifier = isLocal ? TruIdentifier.LocalType16 : TruIdentifier.RemoteType16;
            else if (id <= uint.MaxValue)
                identifier = isLocal ? TruIdentifier.LocalType32 : TruIdentifier.RemoteType32;
            else
                identifier = isLocal ? TruIdentifier.LocalType64 : TruIdentifier.RemoteType64;

            rt.AddUInt8((byte)((nullable ? 0x80 : 0) | (byte)identifier));

            switch (identifier)
            {
                case TruIdentifier.LocalType8:
                case TruIdentifier.RemoteType8:
                    rt.AddUInt8((byte)id);
                    break;
                case TruIdentifier.LocalType16:
                case TruIdentifier.RemoteType16:
                    rt.AddUInt16((ushort)id);
                    break;
                case TruIdentifier.LocalType32:
                case TruIdentifier.RemoteType32:
                    rt.AddUInt32((uint)id);
                    break;
                default:
                    rt.AddUInt64(id);
                    break;
            }
        }

        public override Tru ToNullable()
        {
            throw new NotImplementedException();
        }

    }
}
