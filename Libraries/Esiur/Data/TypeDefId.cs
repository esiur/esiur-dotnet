using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct TypeDefId
    {
        public ulong Value;
        public bool Remote;

        public TypeDefId(ulong value, bool remote)
        {
            Value = value;
            Remote = remote;
        }

        public unsafe override int GetHashCode()
        {
            // Fallback implementation when System.HashCode is not available.
            unchecked
            {
                int hash = ((int)Value) ^ ((int)(Value >> 32));
                hash = (hash * 397) ^ (Remote ? 1 : 0);
                return hash;
            }
        }

        public override string ToString()=> $"{(Remote ? "Remote" : "Local")}TypeDef{Value}";

        public override bool Equals(object obj)
        {
            if (obj is TypeDefId b)
                return Value == b.Value && Remote == b.Remote;

            return false;
        }

        public static bool operator == (TypeDefId a, TypeDefId b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TypeDefId a, TypeDefId b)
        {
            return !(a == b);
        }

    }

}
