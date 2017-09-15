 using Esiur.Data;
using Esiur.Engine;
using Esiur.Misc;
using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.Packets
{
    class IIPPacket : Packet
    {
        public enum IIPPacketCommand : byte
        {
            Event = 0,
            Request,
            Reply,
            Error,
        }

        public enum IIPPacketEvent: byte
        {
            // Event Manage
            ResourceReassigned = 0,
            ResourceDestroyed,
            
            // Event Invoke
            PropertyUpdated = 0x10,
            EventOccured,
        }

        public enum IIPPacketAction : byte
        {
            // Request Manage
            AttachResource = 0,
            ReattachResource,
            DetachResource,
            CreateResource,
            DeleteResource,

            // Request Inquire
            TemplateFromClassName = 0x8,
            TemplateFromClassId,
            TemplateFromResourceLink,
            TemplateFromResourceId,
            ResourceIdFromResourceLink,

            // Request Invoke
            InvokeFunction = 0x10,
            GetProperty,
            GetPropertyIfModified,
            SetProperty,
        }

 
        public IIPPacketCommand Command
        {
            get;
            set;
        }
        public IIPPacketAction Action
        {
            get;
            set;
        }

        public IIPPacketEvent Event
        {
            get;
            set;
        }

     
        public uint ResourceId { get; set; }
        public uint NewResourceId { get; set; }

        public uint ResourceAge { get; set; }
        public byte[] Content { get; set; }
        public byte ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ClassName { get; set; }
        public string ResourceLink { get; set; }
        public Guid ClassId { get; set; }
        public byte MethodIndex { get; set; }
        public string MethodName { get; set; }
        public uint CallbackId { get; set; }

        private uint dataLengthNeeded;

        public override bool Compose()
        {
            return base.Compose();
        }

        bool NotEnough(uint offset, uint ends, uint needed)
        {
            if (offset + needed > ends)
            {
                dataLengthNeeded = needed - (ends - offset);
                return true;
            }
            else
                return false;
        }

        public override long Parse(byte[] data, uint offset, uint ends)
        {
            var oOffset = offset;

            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            Command = (IIPPacketCommand)(data[offset] >> 6);

            if (Command == IIPPacketCommand.Event)
            {
                Event = (IIPPacketEvent)(data[offset++] & 0x3f);

                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset);
                offset += 4;
            }
            else
            {
                Action = (IIPPacketAction)(data[offset++] & 0x3f);

                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                CallbackId = data.GetUInt32(offset);
                offset += 4;
            }

            if (Command == IIPPacketCommand.Event)
            {
                if (Event == IIPPacketEvent.ResourceReassigned)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    NewResourceId = data.GetUInt32( offset);
                    offset += 4;

                }
                else if (Event == IIPPacketEvent.ResourceDestroyed)
                {
                    // nothing to parse
                }
                else if (Event == IIPPacketEvent.PropertyUpdated)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    MethodIndex = data[offset++];

                    var dt = (DataType)data[offset++];
                    var size = dt.Size();// Codec.SizeOf(dt);

                    if (size < 0)
                    {
                        if (NotEnough(offset, ends, 4))
                            return -dataLengthNeeded;

                        var cl = data.GetUInt32( offset);
                        offset += 4;

                        if (NotEnough(offset, ends, cl))
                            return -dataLengthNeeded;

                        Content = data.Clip( offset - 5, cl + 5);
                        offset += cl;
                    }
                    else
                    {
                        if (NotEnough(offset, ends, (uint)size))
                            return -dataLengthNeeded;

                        Content = data.Clip(offset - 1, (uint)size + 1);
                        offset += (uint)size;
                    }
                }
                else if (Event == IIPPacketEvent.EventOccured)
                {
                    if (NotEnough(offset, ends, 5)) 
                        return -dataLengthNeeded;

                    MethodIndex = data[offset++];

                    var cl = data.GetUInt32( offset);
                    offset += 4;

                    Content = data.Clip( offset, cl);
                    offset += cl;

                }
            }
            else if (Command == IIPPacketCommand.Request)
            {
                if (Action == IIPPacketAction.AttachResource)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Action == IIPPacketAction.ReattachResource)
                {
                    if (NotEnough(offset, ends, 8))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    ResourceAge = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.DetachResource)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.CreateResource)
                {
                    if (NotEnough(offset, ends, 1))
                        return -dataLengthNeeded;

                    var cl = data[offset++];

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    ClassName = data.GetString(offset, cl);
                    offset += cl;
                }
                else if (Action == IIPPacketAction.DeleteResource)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.TemplateFromClassName)
                {
                    if (NotEnough(offset, ends, 1))
                        return -dataLengthNeeded;

                    var cl = data[offset++];

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    ClassName = data.GetString(offset, cl);
                    offset += cl;

                }
                else if (Action == IIPPacketAction.TemplateFromClassId)
                {
                    if (NotEnough(offset, ends, 16))
                        return -dataLengthNeeded;

                    ClassId = data.GetGuid(offset);
                    offset += 16;

                }
                else if (Action == IIPPacketAction.TemplateFromResourceLink)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var cl = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    ResourceLink = data.GetString(offset, cl);
                    offset += cl;
                }
                else if (Action == IIPPacketAction.TemplateFromResourceId)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Action == IIPPacketAction.ResourceIdFromResourceLink)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var cl = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    ResourceLink = data.GetString(offset, cl);
                    offset += cl;
                }
                else if (Action == IIPPacketAction.InvokeFunction)
                {
                    if (NotEnough(offset, ends, 9))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    MethodIndex = data[offset++];

                    var cl = data.GetUInt32(offset);
                    offset += 4;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);
                    offset += cl;

                }
                else if (Action == IIPPacketAction.GetProperty)
                {
                    if (NotEnough(offset, ends, 5))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    MethodIndex = data[offset++];

                }
                else if (Action == IIPPacketAction.GetPropertyIfModified)
                {
                    if (NotEnough(offset, ends, 9))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    MethodIndex = data[offset++];

                    ResourceAge = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.SetProperty)
                {
                    if (NotEnough(offset, ends, 6))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    MethodIndex = data[offset++];


                    var dt = (DataType)data[offset++];
                    var size = dt.Size();// Codec.SizeOf(dt);

                    if (size < 0)
                    {
                        if (NotEnough(offset, ends, 4))
                            return -dataLengthNeeded;

                        var cl = data.GetUInt32(offset);
                        offset += 4;

                        if (NotEnough(offset, ends, cl))
                            return -dataLengthNeeded;

                        Content = data.Clip(offset-5, cl + 5);
                        offset += cl;
                    }
                    else
                    {
                        if (NotEnough(offset, ends, (uint)size))
                            return -dataLengthNeeded;

                        Content = data.Clip(offset-1, (uint)size + 1);
                        offset += (uint)size;
                    }
                }
            }
            else if (Command == IIPPacketCommand.Reply)
            {
                if (Action == IIPPacketAction.AttachResource
                   || Action == IIPPacketAction.ReattachResource)
                {

                    if (NotEnough(offset, ends, 26))
                        return -dataLengthNeeded;

                    ClassId = data.GetGuid(offset);
                    offset += 16;

                    ResourceAge = data.GetUInt32(offset);
                    offset += 4;

                    uint cl = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    ResourceLink = data.GetString(offset, cl);
                    offset += cl;

                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    cl = data.GetUInt32(offset);
                    offset += 4;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);
                    offset += cl;
                }
                else if (Action == IIPPacketAction.DetachResource)
                {
                    // nothing to do
                }
                else if (Action == IIPPacketAction.CreateResource)
                {
                    if (NotEnough(offset, ends, 20))
                        return -dataLengthNeeded;

                    ClassId = data.GetGuid(offset);
                    offset += 16;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.DetachResource)
                {
                    // nothing to do
                }
                else if (Action == IIPPacketAction.TemplateFromClassName
                        || Action == IIPPacketAction.TemplateFromClassId
                        || Action == IIPPacketAction.TemplateFromResourceLink
                        || Action == IIPPacketAction.TemplateFromResourceId)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    var cl = data.GetUInt32(offset);
                    offset += 4;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);
                    offset += cl;
                }
                else if (Action == IIPPacketAction.ResourceIdFromResourceLink)
                {
                    if (NotEnough(offset, ends, 24))
                        return -dataLengthNeeded;

                    ClassId = data.GetGuid(offset);
                    offset += 16;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    ResourceAge = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Action == IIPPacketAction.InvokeFunction 
                    || Action == IIPPacketAction.GetProperty
                    || Action == IIPPacketAction.GetPropertyIfModified)
                {
                    if (NotEnough(offset, ends, 1))
                        return -dataLengthNeeded;

                    var dt = (DataType)data[offset++];
                    var size = dt.Size();// Codec.SizeOf(dt);

                    if (size < 0)
                    {
                        if (NotEnough(offset, ends, 4))
                            return -dataLengthNeeded;

                        var cl = data.GetUInt32(offset);
                        offset += 4;

                        if (NotEnough(offset, ends, cl))
                            return -dataLengthNeeded;

                        Content = data.Clip(offset - 5, cl + 5);
                        offset += cl;
                    }
                    else
                    {
                        if (NotEnough(offset, ends, (uint)size))
                            return -dataLengthNeeded;

                        Content = data.Clip(offset - 1, (uint)size + 1);
                        offset += (uint)size;
                    }
                }
                else if (Action == IIPPacketAction.SetProperty)
                {
                    // nothing to do
                }
            }
            else if (Command == IIPPacketCommand.Error)
            {
                // Error
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                CallbackId = data.GetUInt32(offset);

                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                ErrorCode = data[offset++];

                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                var cl = data.GetUInt32(offset);
                offset += 4;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ErrorMessage = data.GetString(offset, cl);
                offset += cl;
            } 

            return offset - oOffset;
        }
    }
}
