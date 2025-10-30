using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketRequest : byte
    {
        // Request Invoke
        InvokeFunction = 0x0,
        SetProperty = 0x1,
        Subscribe = 0x2,
        Unsubscribe = 0x3,

        // Request Inquire
        TemplateFromClassName = 0x8,
        TemplateFromClassId = 0x9,
        TemplateFromResourceId = 0xA,
        Query = 0xB,
        LinkTemplates = 0xC,
        Token = 0xD,
        GetResourceIdByLink = 0xE,

        // Request Manage
        AttachResource = 0x10,
        ReattachResource = 0x11,
        DetachResource = 0x12,
        CreateResource = 0x13,
        DeleteResource = 0x14,
        MoveResource = 0x15,

        // Request Static
        KeepAlive = 0x18,
        ProcedureCall = 0x19,
        StaticCall = 0x1A,
        IndirectCall = 0x1B,
        PullStream = 0x1C,
        TerminateExecution = 0x1D,
        HaltExecution = 0x1E,

    }
}
