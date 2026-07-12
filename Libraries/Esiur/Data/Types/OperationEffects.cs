using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum OperationEffects : byte
    {
        None = 0x00,

        // Changes one or more properties of the target resource.
        ModifiesState = 0x01,

        // Creates a resource or another persistent object.
        CreatesResource = 0x02,

        // Deletes or disposes a resource.
        DeletesResource = 0x04,

        // Produces externally visible output or communication.
        External = 0x08,

        // Emits events or notifications.
        EmitsEvents = 0x10,

        // Effects are not fully known or described.
        Unknown = 0x80
    }
}
