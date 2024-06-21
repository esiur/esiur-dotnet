using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketAction : byte
    {
        // Request Manage
        AttachResource = 0,
        ReattachResource,
        DetachResource,
        CreateResource,
        DeleteResource,
        AddChild,
        RemoveChild,
        RenameResource,

        // Request Inquire
        TemplateFromClassName = 0x8,
        TemplateFromClassId,
        TemplateFromResourceId,
        QueryLink,
        ResourceHistory,
        ResourceChildren,
        ResourceParents,
        LinkTemplates,

        // Request Invoke
        InvokeFunction = 0x10,
        Reserved,
        Listen,
        Unlisten,
        SetProperty,

        // Request Attribute
        GetAllAttributes = 0x18,
        UpdateAllAttributes,
        ClearAllAttributes,
        GetAttributes,
        UpdateAttributes,
        ClearAttributes,


        // Static calling
        KeepAlive = 0x20,
        ProcedureCall,
        StaticCall
    }
}
