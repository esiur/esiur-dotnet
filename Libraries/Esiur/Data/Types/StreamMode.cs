using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum StreamMode:byte
    {
        None = 0,
        Push = 0x1, // Stream is in push mode, where data is sent from the source to the destination.
        Pull = 0x2, // Stream is in pull mode, where data is requested from the source by the destination.
    }
}
