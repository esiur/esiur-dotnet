using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum TypeDefField : byte
    {
        Version = 0x00,
        Id = 0x01,
        Name = 0x02,
        Namespace = 0x03,
        Kind = 0x04,
        Parent = 0x05,
        Properties = 0x06,
        Functions = 0x07,
        Events = 0x08,
        Constants = 0x09,

        Usage = 0x20,
        Description = 0x21,
        Example = 0x22,
        Category = 0x23,
        Since = 0x24,
        Annotations = 0x25,
    }
}
