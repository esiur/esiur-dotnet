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

using Esiur.Data;
using Esiur.Core;
using Esiur.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.Packets;
class IIPPacket : Packet
{

    public override string ToString()
    {
        var rt = Method.ToString();

        if (Method == IIPPacketMethod.Notification)
        {
            rt += " " + Notification.ToString();
        }
        else if (Method == IIPPacketMethod.Request)
        {
            rt += " " + Request.ToString();
        }
        else if (Method == IIPPacketMethod.Reply)
        {
            rt += " " + Reply.ToString();
        }

        return rt;
    }


    public uint CallbackId { get; set; }


    public IIPPacketMethod Method
    {
        get;
        set;
    }
    public IIPPacketRequest Request
    {
        get;
        set;
    }
    public IIPPacketReply Reply
    {
        get;
        set;
    }

    public byte Extention { get; set; }

    public IIPPacketNotification Notification
    {
        get;
        set;
    }



    public TransmissionType? DataType { get; set; }


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

        var hasDTU = (data[offset] & 0x20) == 0x20;

        Method = (IIPPacketMethod)(data[offset] >> 6);

        if (Method == IIPPacketMethod.Notification)
        {
            Notification = (IIPPacketNotification)(data[offset++] & 0x3f);

        }
        else if (Method == IIPPacketMethod.Request)
        {
            Request = (IIPPacketRequest)(data[offset++] & 0x3f);

            if (NotEnough(offset, ends, 4))
                return -dataLengthNeeded;

            CallbackId = data.GetUInt32(offset, Endian.Little);
            offset += 4;
        }
        else if (Method == IIPPacketMethod.Reply)
        {
            Reply = (IIPPacketReply)(data[offset++] & 0x3f);

            if (NotEnough(offset, ends, 4))
                return -dataLengthNeeded;

            CallbackId = data.GetUInt32(offset, Endian.Little);
            offset += 4;
        }
        else if (Method == IIPPacketMethod.Extension)
        {
            Extention = (byte)(data[offset++] & 0x3f);
        }
 
        if (Command == IIPPacketCommand.Event)
        {
            if (Event == IIPPacketEvent.ResourceReassigned)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                NewResourceId = data.GetUInt32(offset, Endian.Little);
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

                ChildId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
            }
            else if (Event == IIPPacketEvent.Renamed)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ResourceName = data.GetString(offset, cl);

                //Content = data.Clip(offset, cl);

                offset += cl;
            }
            else if (Event == IIPPacketEvent.PropertyUpdated
                  || Event == IIPPacketEvent.EventOccurred)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                MethodIndex = data[offset++];

                (var size, DataType) = TransmissionType.Parse(data, offset, ends);


                //var dt = (DataType)data[offset++];
                //var size = dt.Size();// Codec.SizeOf(dt);

                if (DataType == null)
                    return -(int)size;

                //Content = data.Clip(DataType.Value.Offset, (uint)DataType.Value.ContentLength);

                offset += (uint)size;

            }
            //else if (Event == IIPPacketEvent.EventOccurred)
            //{
            //    if (NotEnough(offset, ends, 5))
            //        return -dataLengthNeeded;

            //    MethodIndex = data[offset++];

            //    var cl = data.GetUInt32(offset);
            //    offset += 4;

            //    if (NotEnough(offset, ends, cl))
            //        return -dataLengthNeeded;

            //    Content = data.Clip(offset, cl);
            //    offset += cl;

            //}
            // Attribute
            else if (Event == IIPPacketEvent.AttributesUpdated)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                var cl = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                //@TODO: Fix this
                //Content = data.Clip(offset, cl);

                offset += cl;
            }
        }
        else if (Command == IIPPacketCommand.Request)
        {
            if (Action == IIPPacketRequest.AttachResource)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
            }
            else if (Action == IIPPacketRequest.ReattachResource)
            {
                if (NotEnough(offset, ends, 12))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                ResourceAge = data.GetUInt64(offset, Endian.Little);
                offset += 8;

            }
            else if (Action == IIPPacketRequest.DetachResource)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

            }
            else if (Action == IIPPacketRequest.CreateResource)
            {
                if (NotEnough(offset, ends, 12))
                    return -dataLengthNeeded;

                StoreId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                var cl = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                // @TODO: fix this
                //this.Content = data.Clip(offset, cl);
            }
            else if (Action == IIPPacketRequest.DeleteResource)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

            }
            else if (Action == IIPPacketRequest.AddChild
                    || Action == IIPPacketRequest.RemoveChild)
            {
                if (NotEnough(offset, ends, 8))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
                ChildId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
            }
            else if (Action == IIPPacketRequest.RenameResource)
            {
                if (NotEnough(offset, ends, 6))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ResourceName = data.GetString(offset, cl);
                //Content = data.Clip(offset, cl);
                offset += cl;
            }
            else if (Action == IIPPacketRequest.TemplateFromClassName)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                var cl = data[offset++];

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ClassName = data.GetString(offset, cl);
                offset += cl;

            }
            else if (Action == IIPPacketRequest.TemplateFromClassId)
            {
                if (NotEnough(offset, ends, 16))
                    return -dataLengthNeeded;

                ClassId = data.GetUUID(offset);
                offset += 16;

            }
            else if (Action == IIPPacketRequest.TemplateFromResourceId)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
            }
            else if (Action == IIPPacketRequest.QueryLink
                || Action == IIPPacketRequest.LinkTemplates)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ResourceLink = data.GetString(offset, cl);
                offset += cl;
            }
            else if (Action == IIPPacketRequest.ResourceChildren
                    || Action == IIPPacketRequest.ResourceParents)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
            }
            else if (Action == IIPPacketRequest.ResourceHistory)
            {
                if (NotEnough(offset, ends, 20))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                FromDate = data.GetDateTime(offset, Endian.Little);
                offset += 8;

                ToDate = data.GetDateTime(offset, Endian.Little);
                offset += 8;

            }
            else if (Action == IIPPacketRequest.InvokeFunction)
            {
                if (NotEnough(offset, ends, 6))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                MethodIndex = data[offset++];


                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

                //var cl = data.GetUInt32(offset);
                //offset += 4;

                //if (NotEnough(offset, ends, cl))
                //    return -dataLengthNeeded;

                //Content = data.Clip(offset, cl);
                //offset += cl;

            }
            else if (Action == IIPPacketRequest.Listen
                || Action == IIPPacketRequest.Unlisten)// .GetProperty)
            {
                if (NotEnough(offset, ends, 5))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                MethodIndex = data[offset++];

            }
            //else if (Action == IIPPacketAction.GetPropertyIfModified)
            //{
            //    if (NotEnough(offset, ends, 9))
            //        return -dataLengthNeeded;

            //    ResourceId = data.GetUInt32(offset);
            //    offset += 4;

            //    MethodIndex = data[offset++];

            //    ResourceAge = data.GetUInt64(offset);
            //    offset += 8;

            //}
            else if (Action == IIPPacketRequest.SetProperty)
            {
                if (NotEnough(offset, ends, 6))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                MethodIndex = data[offset++];

                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;


                //Content = data.Clip(DataType.Value.Offset, (uint)DataType.Value.ContentLength);

                offset += (uint)size;

            }
            // Attributes
            else if (Action == IIPPacketRequest.UpdateAllAttributes
                    || Action == IIPPacketRequest.GetAttributes
                    || Action == IIPPacketRequest.UpdateAttributes
                    || Action == IIPPacketRequest.ClearAttributes)
            {
                if (NotEnough(offset, ends, 8))
                    return -dataLengthNeeded;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;
                var cl = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                // @TODO: fix this
                //Content = data.Clip(offset, cl);
                offset += cl;
            }

            else if (Action == IIPPacketRequest.KeepAlive)
            {
                if (NotEnough(offset, ends, 12))
                    return -dataLengthNeeded;

                CurrentTime = data.GetDateTime(offset, Endian.Little);
                offset += 8;
                Interval = data.GetUInt32(offset, Endian.Little);
                offset += 4;

            }
            else if (Action == IIPPacketRequest.ProcedureCall)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                Procedure = data.GetString(offset, cl);
                offset += cl;

                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

            }
            else if (Action == IIPPacketRequest.StaticCall)
            {
                if (NotEnough(offset, ends, 18))
                    return -dataLengthNeeded;

                ClassId = data.GetUUID(offset);//, Endian.Little);
                offset += 16;

                MethodIndex = data[offset++];


                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;
            }
        }
        else if (Command == IIPPacketCommand.Reply)
        {
            if (Action == IIPPacketRequest.AttachResource
               || Action == IIPPacketRequest.ReattachResource)
            {

                if (NotEnough(offset, ends, 26))
                    return -dataLengthNeeded;

                ClassId = data.GetUUID(offset);
                offset += 16;

                ResourceAge = data.GetUInt64(offset, Endian.Little);
                offset += 8;

                uint cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ResourceLink = data.GetString(offset, cl);
                offset += cl;

                //if (NotEnough(offset, ends, 4))
                //  return -dataLengthNeeded;


                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

                //Content = data.Clip(DataType.Value.Offset, (uint)DataType.Value.ContentLength);

            }
            else if (Action == IIPPacketRequest.DetachResource)
            {
                // nothing to do
            }
            else if (Action == IIPPacketRequest.CreateResource)
            {
                if (NotEnough(offset, ends, 20))
                    return -dataLengthNeeded;

                //ClassId = data.GetGuid(offset);
                //offset += 16;

                ResourceId = data.GetUInt32(offset, Endian.Little);
                offset += 4;

            }
            else if (Action == IIPPacketRequest.DetachResource)
            {
                // nothing to do
            }
            // Inquire
            else if (Action == IIPPacketRequest.TemplateFromClassName
                    || Action == IIPPacketRequest.TemplateFromClassId
                    || Action == IIPPacketRequest.TemplateFromResourceId
                    || Action == IIPPacketRequest.QueryLink
                    || Action == IIPPacketRequest.ResourceChildren
                    || Action == IIPPacketRequest.ResourceParents
                    || Action == IIPPacketRequest.ResourceHistory
                    || Action == IIPPacketRequest.LinkTemplates
                    // Attribute
                    || Action == IIPPacketRequest.GetAllAttributes
                    || Action == IIPPacketRequest.GetAttributes)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

                //var cl = data.GetUInt32(offset);
                //offset += 4;

                //if (NotEnough(offset, ends, cl))
                //    return -dataLengthNeeded;

                //Content = data.Clip(offset, cl);
                //offset += cl;
            }
            else if (Action == IIPPacketRequest.InvokeFunction
                || Action == IIPPacketRequest.ProcedureCall
                || Action == IIPPacketRequest.StaticCall)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                (var size, DataType) = TransmissionType.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

                //Content = data.Clip(DataType.Value.Offset, (uint)DataType.Value.ContentLength);
            }
            else if (Action == IIPPacketRequest.SetProperty
                || Action == IIPPacketRequest.Listen
                || Action == IIPPacketRequest.Unlisten)
            {
                // nothing to do
            }
            else if (Action == IIPPacketRequest.KeepAlive)
            {
                if (NotEnough(offset, ends, 12))
                    return -dataLengthNeeded;

                CurrentTime = data.GetDateTime(offset, Endian.Little);
                offset += 8;
                Jitter = data.GetUInt32(offset, Endian.Little);
                offset += 4;
            }
        }
        else if (Command == IIPPacketCommand.Report)
        {
            if (Report == IIPPacketReport.ManagementError)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                ErrorCode = data.GetUInt16(offset, Endian.Little);
                offset += 2;
            }
            else if (Report == IIPPacketReport.ExecutionError)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                ErrorCode = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var cl = data.GetUInt16(offset, Endian.Little);
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

                ProgressValue = data.GetInt32(offset, Endian.Little);
                offset += 4;
                ProgressMax = data.GetInt32(offset, Endian.Little);
                offset += 4;
            }
            else if (Report == IIPPacketReport.ChunkStream)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;


                (var size, DataType) = TransmissionType.Parse(Data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

                //Content = data.Clip(DataType.Value.Offset, (uint)DataType.Value.ContentLength);

            }
        }

        return offset - originalOffset;
    }
}
