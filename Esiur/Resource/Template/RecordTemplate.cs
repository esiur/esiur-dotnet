using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Esiur.Resource.Template
{
    public class RecordTemplate : ResourceTemplate
    {
        //Guid classId;
        //public Guid ClassId => classId;

        //string className;
        //public string ClassName => className;

        public RecordTemplate()
        {

        }

        public new static RecordTemplate Parse(byte[] data, uint offset, uint contentLength)
        {

            uint ends = offset + contentLength;

            uint oOffset = offset;

            // start parsing...

            var od = new RecordTemplate();
            od.content = data.Clip(offset, contentLength);

            od.classId = data.GetGuid(offset);
            offset += 16;
            od.className = data.GetString(offset + 1, data[offset]);
            offset += (uint)data[offset] + 1;

            od.version = data.GetInt32(offset);
            offset += 4;

            ushort methodsCount = data.GetUInt16(offset);
            offset += 2;

            

            for (int i = 0; i < methodsCount; i++)
            {
                var type = data[offset] >> 5;

                if (type == 0) // function
                {
                    string expansion = null;
                    var hasExpansion = ((data[offset++] & 0x10) == 0x10);

                    var name = data.GetString(offset + 1, data[offset]);
                    offset += (uint)data[offset] + 1;

                    // return type
                    var (rts, returnType) = TemplateDataType.Parse(data, offset);
                    offset += rts;

                    // arguments count
                    var argsCount = data[offset++];
                    List<ArgumentTemplate> arguments = new();

                    for (var a = 0; a < argsCount; a++)
                    {
                        var (cs, argType) = ArgumentTemplate.Parse(data, offset);
                        arguments.Add(argType);
                        offset += cs;
                    }

                    // arguments
                    if (hasExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        expansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    var ft = new FunctionTemplate(od, functionIndex++, name, arguments.ToArray(), returnType, expansion);

                    od.functions.Add(ft);
                }
                else if (type == 1)    // property
                {

                    string readExpansion = null, writeExpansion = null;

                    var hasReadExpansion = ((data[offset] & 0x8) == 0x8);
                    var hasWriteExpansion = ((data[offset] & 0x10) == 0x10);
                    var recordable = ((data[offset] & 1) == 1);
                    var permission = (PropertyTemplate.PropertyPermission)((data[offset++] >> 1) & 0x3);
                    var name = data.GetString(offset + 1, data[offset]);// Encoding.ASCII.GetString(data, (int)offset + 1, data[offset]);

                    offset += (uint)data[offset] + 1;

                    var (dts, valueType) = TemplateDataType.Parse(data, offset);

                    offset += dts;

                    if (hasReadExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        readExpansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    if (hasWriteExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        writeExpansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    var pt = new PropertyTemplate(od, propertyIndex++, name, valueType, readExpansion, writeExpansion, recordable);

                    od.properties.Add(pt);
                }
                else if (type == 2) // Event
                {

                    string expansion = null;
                    var hasExpansion = ((data[offset] & 0x10) == 0x10);
                    var listenable = ((data[offset++] & 0x8) == 0x8);

                    var name = data.GetString(offset + 1, data[offset]);// Encoding.ASCII.GetString(data, (int)offset + 1, (int)data[offset]);
                    offset += (uint)data[offset] + 1;

                    var (dts, argType) = TemplateDataType.Parse(data, offset);

                    offset += dts;

                    if (hasExpansion) // expansion ?
                    {
                        var cs = data.GetUInt32(offset);
                        offset += 4;
                        expansion = data.GetString(offset, cs);
                        offset += cs;
                    }

                    var et = new EventTemplate(od, eventIndex++, name, argType, expansion, listenable);

                    od.events.Add(et);

                }
            }

            // append signals
            for (int i = 0; i < od.events.Count; i++)
                od.members.Add(od.events[i]);
            // append slots
            for (int i = 0; i < od.functions.Count; i++)
                od.members.Add(od.functions[i]);
            // append properties
            for (int i = 0; i < od.properties.Count; i++)
                od.members.Add(od.properties[i]);


            //od.isReady = true;
            /*
            var oo = owner.Socket.Engine.GetObjectDescription(od.GUID);
            if (oo != null)
            {
                Console.WriteLine("Already there ! description");
                return oo;
            }
            else
            {
                owner.Socket.Engine.AddObjectDescription(od);
                return od;
            }
            */

            return od;
        }

        public RecordTemplate(Type type)
        {
            if (!Codec.ImplementsInterface(type, typeof(IRecord)))
                throw new Exception("Type is not a record.");

            className = type.FullName;
            classId = ResourceTemplate.GetTypeGuid(className);

#if NETSTANDARD
            PropertyInfo[] propsInfo = type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance);
#else
            PropertyInfo[] propsInfo = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
#endif

            bool classIsPublic = type.GetCustomAttribute<PublicAttribute>() != null;

            byte i = 0;

            if (classIsPublic)
            {
                foreach (var pi in propsInfo)
                {
                    var privateAttr = pi.GetCustomAttribute<PrivateAttribute>(true);

                    if (privateAttr == null)
                        continue;

                    var annotationAttr = pi.GetCustomAttribute<AnnotationAttribute>(true);
                    var storageAttr = pi.GetCustomAttribute<StorageAttribute>(true);

                    var pt = new PropertyTemplate(this, i++, pi.Name, TemplateDataType.FromType(pi.PropertyType));

                    if (storageAttr != null)
                        pt.Recordable = storageAttr.Mode == StorageMode.Recordable;

                    if (annotationAttr != null)
                        pt.ReadExpansion = annotationAttr.Annotation;
                    else
                        pt.ReadExpansion = pi.PropertyType.Name;

                    pt.PropertyInfo = pi;
                    //pt.Serilize = publicAttr.Serialize;
                    properties.Add(pt);
                    members.Add(pt);
                }
            }
            else
            {
                foreach (var pi in propsInfo)
                {
                    var publicAttr = pi.GetCustomAttribute<PublicAttribute>(true);

                    if (publicAttr == null)
                        continue;


                    var annotationAttr = pi.GetCustomAttribute<AnnotationAttribute>(true);
                    var storageAttr = pi.GetCustomAttribute<StorageAttribute>(true);
                    var valueType = TemplateDataType.FromType(pi.PropertyType);

                    var pt = new PropertyTemplate(this, i++, pi.Name, valueType);//, rp.ReadExpansion, rp.WriteExpansion, rp.Storage);
                    if (storageAttr != null)
                        pt.Recordable = storageAttr.Mode == StorageMode.Recordable;

                    if (annotationAttr != null)
                        pt.ReadExpansion = annotationAttr.Annotation;
                    else
                        pt.ReadExpansion = pi.PropertyType.Name;

                    pt.PropertyInfo = pi;
                    //pt.Serilize = publicAttr.Serialize;
                    properties.Add(pt);
                    members.Add(pt);
                }
            }



            // bake it binarily
            var b = new BinaryList();
            b.AddGuid(classId)
             .AddUInt8((byte)className.Length)
             .AddString(className)
             .AddInt32(version)
             .AddUInt16((ushort)members.Count);


            foreach (var pt in properties)
                b.AddUInt8Array(pt.Compose());

            content = b.ToArray();


        }
    }
}
