/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esyur.Data;
using Esyur.Core;
using Esyur.Misc;
using Esyur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esyur.Net.Packets
{
    class IIPPacket : Packet
    {

        public override string ToString()
        {
            var rt = Command.ToString();

            if (Command == IIPPacketCommand.Event)
            {
                rt += " " + Event.ToString();
                //if (Event == IIPPacketEvent.AttributesUpdated)
                //  rt += 
            }
            else if (Command == IIPPacketCommand.Request)
            {
                rt += " " + Action.ToString();
                if (Action == IIPPacketAction.AttachResource)
                {
                    rt += " CID: " + CallbackId + " RID: " + ResourceId;
                }
            }
            else if (Command == IIPPacketCommand.Reply)
                rt += " " + Action.ToString();
            else if (Command == IIPPacketCommand.Report)
                rt += " " + Report.ToString();

            return rt;
        }

        public enum IIPPacketCommand : byte
        {
            Event = 0,
            Request,
            Reply,
            Report,
        }

        public enum IIPPacketEvent : byte
        {
            // Event Manage
            ResourceReassigned = 0,
            ResourceDestroyed,
            ChildAdded,
            ChildRemoved,
            Renamed,
            // Event Invoke
            PropertyUpdated = 0x10,
            EventOccurred,

            // Attribute
            AttributesUpdated = 0x18
        }

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

            // Request Invoke
            InvokeFunctionArrayArguments = 0x10,
            GetProperty,
            GetPropertyIfModified,
            SetProperty,
            InvokeFunctionNamedArguments,

            // Request Attribute
            GetAllAttributes = 0x18,
            UpdateAllAttributes,
            ClearAllAttributes,
            GetAttributes,
            UpdateAttributes,
            ClearAttributes
        }

        public enum IIPPacketReport : byte
        {
            ManagementError,
            ExecutionError,
            ProgressReport = 0x8,
            ChunkStream = 0x9
        }


        public IIPPacketReport Report
        {
            get;
            set;
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

        public IIPPacketCommand PreviousCommand
        {
            get;
            set;
        }
        public IIPPacketAction PreviousAction
        {
            get;
            set;
        }

        public IIPPacketEvent PreviousEvent
        {
            get;
            set;
        }


        public uint ResourceId { get; set; }
        public uint NewResourceId { get; set; }
        //public uint ParentId { get; set; }
        public uint ChildId { get; set; }
        public uint StoreId { get; set; }

        public ulong ResourceAge { get; set; }
        public byte[] Content { get; set; }
        public ushort ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ClassName { get; set; }
        public string ResourceLink { get; set; }
        public Guid ClassId { get; set; }
        public byte MethodIndex { get; set; }
        public string MethodName { get; set; }
        public uint CallbackId { get; set; }
        public int ProgressValue { get; set; }
        public int ProgressMax { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public ulong FromAge { get; set; }
        public ulong ToAge { get; set; }

        private uint dataLengthNeeded;
        private uint originalOffset;

        public override bool Compose()
        {
            return base.Compose();
        }

        bool NotEnough(uint offset, uint ends, uint needed)
        {
            if (offset + needed > ends)
            {
                dataLengthNeeded = needed - (ends - offset);
                //dataLengthNeeded = needed - (ends - originalOffset);

                return true;
            }
            else
                return false;
        }

        public override long Parse(byte[] data, uint offset, uint ends)
        {
            originalOffset = offset;

            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            PreviousCommand = Command;

            Command = (IIPPacketCommand)(data[offset] >> 6);

            if (Command == IIPPacketCommand.Event)
            {
                Event = (IIPPacketEvent)(data[offset++] & 0x3f);

                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset);
                offset += 4;
            }
            else if (Command == IIPPacketCommand.Report)
            {
                Report = (IIPPacketReport)(data[offset++] & 0x3f);

                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                CallbackId = data.GetUInt32(offset);
                offset += 4;
            }
            else
            {
                PreviousAction = Action;
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

                    NewResourceId = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Event == IIPPacketEvent.ResourceDestroyed)
                {
                    // nothing to parse
                }
                else if (Event == IIPPacketEvent.ChildAdded
                        || Event == IIPPacketEvent.ChildRemoved)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ChildId = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Event == IIPPacketEvent.Renamed)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var cl = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);

                    offset += cl;
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
                else if (Event == IIPPacketEvent.EventOccurred)
                {
                    if (NotEnough(offset, ends, 5))
                        return -dataLengthNeeded;

                    MethodIndex = data[offset++];

                    var cl = data.GetUInt32(offset);
                    offset += 4;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);
                    offset += cl;

                }
                // Attribute
                else if (Event == IIPPacketEvent.AttributesUpdated)
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
                    if (NotEnough(offset, ends, 12))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    ResourceAge = data.GetUInt64(offset);
                    offset += 8;

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
                    if (NotEnough(offset, ends, 12))
                        return -dataLengthNeeded;

                    StoreId = data.GetUInt32(offset);
                    offset += 4;
                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    var cl = data.GetUInt32(offset);
                    offset += 4;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    this.Content = data.Clip(offset, cl);
                }
                else if (Action == IIPPacketAction.DeleteResource)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.AddChild
                        || Action == IIPPacketAction.RemoveChild)
                {
                    if (NotEnough(offset, ends, 8))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                    ChildId = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Action == IIPPacketAction.RenameResource)
                {
                    if (NotEnough(offset, ends, 6))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                    var cl = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);
                    offset += cl;
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
                else if (Action == IIPPacketAction.TemplateFromResourceId)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Action == IIPPacketAction.QueryLink)
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
                else if (Action == IIPPacketAction.ResourceChildren
                        || Action == IIPPacketAction.ResourceParents)
                {
                    if (NotEnough(offset, ends, 4))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                }
                else if (Action == IIPPacketAction.ResourceHistory)
                {
                    if (NotEnough(offset, ends, 20))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                    FromDate = data.GetDateTime(offset);
                    offset += 8;

                    ToDate = data.GetDateTime(offset);
                    offset += 8;

                }
                else if (Action == IIPPacketAction.InvokeFunctionArrayArguments
                       || Action == IIPPacketAction.InvokeFunctionNamedArguments)
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

                    ResourceAge = data.GetUInt64(offset);
                    offset += 8;

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
                // Attributes
                else if (Action == IIPPacketAction.UpdateAllAttributes
                        || Action == IIPPacketAction.GetAttributes
                        || Action == IIPPacketAction.UpdateAttributes
                        || Action == IIPPacketAction.ClearAttributes)
                {
                    if (NotEnough(offset, ends, 8))
                        return -dataLengthNeeded;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;
                    var cl = data.GetUInt32(offset);
                    offset += 4;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    Content = data.Clip(offset, cl);
                    offset += cl;
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

                    ResourceAge = data.GetUInt64(offset);
                    offset += 8;

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

                    //ClassId = data.GetGuid(offset);
                    //offset += 16;

                    ResourceId = data.GetUInt32(offset);
                    offset += 4;

                }
                else if (Action == IIPPacketAction.DetachResource)
                {
                    // nothing to do
                }
                // Inquire
                else if (Action == IIPPacketAction.TemplateFromClassName
                        || Action == IIPPacketAction.TemplateFromClassId
                        || Action == IIPPacketAction.TemplateFromResourceId
                        || Action == IIPPacketAction.QueryLink
                        || Action == IIPPacketAction.ResourceChildren
                        || Action == IIPPacketAction.ResourceParents
                        || Action == IIPPacketAction.ResourceHistory
                        // Attribute
                        || Action == IIPPacketAction.GetAllAttributes
                        || Action == IIPPacketAction.GetAttributes)
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
                else if (Action == IIPPacketAction.InvokeFunctionArrayArguments
                    || Action == IIPPacketAction.InvokeFunctionNamedArguments
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
            else if (Command == IIPPacketCommand.Report)
            {
                if (Report == IIPPacketReport.ManagementError)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    ErrorCode = data.GetUInt16(offset);
                    offset += 2;
                }
                else if (Report == IIPPacketReport.ExecutionError)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    ErrorCode = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var cl = data.GetUInt16(offset);
                    offset += 2;

                    if (NotEnough(offset, ends, cl))
                        return -dataLengthNeeded;

                    ErrorMessage = data.GetString(offset, cl);
                    offset += cl;
                }
                else if (Report == IIPPacketReport.ProgressReport)
                {
                    if (NotEnough(offset, ends, 8))
                        return -dataLengthNeeded;

                    ProgressValue = data.GetInt32(offset);
                    offset += 4;
                    ProgressMax = data.GetInt32(offset);
                    offset += 4;
                }
                else if (Report == IIPPacketReport.ChunkStream)
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
            }

            return offset - originalOffset;
        }
    }
}
