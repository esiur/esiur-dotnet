using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketRequest : byte
    {
        // Request Manage
        AttachResource = 0x0,
        ReattachResource = 0x1,
        DetachResource = 0x2,
        CreateResource = 0x3,
        DeleteResource = 0x4,
        MoveResource = 0x5,

        // Request Inquire
        TemplateFromClassName = 0x8,
        TemplateFromClassId = 0x9,
        TemplateFromResourceId = 0xA,
        QueryLink = 0xB,
        LinkTemplates = 0xC,
        Token = 0xD,

        // Request Invoke
        InvokeFunction = 0x10,
        Subscribe = 0x11,
        Unsubscribe = 0x12,
        SetProperty = 0x13,

        // Static calling
        KeepAlive = 0x18,
        ProcedureCall = 0x19,
        StaticCall = 0x1A
    }
}
