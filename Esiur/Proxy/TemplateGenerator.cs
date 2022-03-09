﻿using Esiur.Data;
using Esiur.Resource.Template;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Esiur.Resource;
using Esiur.Net.IIP;
using System.Diagnostics;

namespace Esiur.Proxy;
public static class TemplateGenerator
{
    internal static Regex urlRegex = new Regex(@"^(?:([\S]*)://([^/]*)/?)");

    internal static string GenerateRecord(TypeTemplate template, TypeTemplate[] templates)
    {
        var cls = template.ClassName.Split('.');

        var nameSpace = string.Join(".", cls.Take(cls.Length - 1));
        var className = cls.Last();

        var rt = new StringBuilder();

        rt.AppendLine("using System;\r\nusing Esiur.Resource;\r\nusing Esiur.Core;\r\nusing Esiur.Data;\r\nusing Esiur.Net.IIP;");
        rt.AppendLine($"namespace { nameSpace} {{");
        rt.AppendLine($"[Public] public class {className} : IRecord {{");


        foreach (var p in template.Properties)
        {
            var ptTypeName = GetTypeName(p.ValueType, templates);
            rt.AppendLine($"public {ptTypeName} {p.Name} {{ get; set; }}");
            rt.AppendLine();
        }

        rt.AppendLine("\r\n}\r\n}");

        return rt.ToString();
    }

    internal static string GenerateEnum(TypeTemplate template, TypeTemplate[] templates)
    {
        var cls = template.ClassName.Split('.');

        var nameSpace = string.Join(".", cls.Take(cls.Length - 1));
        var className = cls.Last();

        var rt = new StringBuilder();

        rt.AppendLine("using System;\r\nusing Esiur.Resource;\r\nusing Esiur.Core;\r\nusing Esiur.Data;\r\nusing Esiur.Net.IIP;");
        rt.AppendLine($"namespace { nameSpace} {{");
        rt.AppendLine($"[Public] public enum {className} {{");

        rt.AppendLine(String.Join(",\r\n", template.Constants.Select(x => $"{x.Name}={x.Value}")));

        rt.AppendLine("\r\n}\r\n}");

        return rt.ToString();
    }


    static string GetTypeName(RepresentationType representationType, TypeTemplate[] templates)
    {
        string name;

        if (representationType.Identifier == RepresentationTypeIdentifier.TypedResource)// == DataType.Resource)
            name = templates.First(x => x.ClassId == representationType.GUID && (x.Type == TemplateType.Resource || x.Type == TemplateType.Wrapper)).ClassName;
        else if (representationType.Identifier == RepresentationTypeIdentifier.TypedRecord)
            name = templates.First(x => x.ClassId == representationType.GUID && x.Type == TemplateType.Record).ClassName;
        else if (representationType.Identifier == RepresentationTypeIdentifier.Enum)
            name = templates.First(x => x.ClassId == representationType.GUID && x.Type == TemplateType.Enum).ClassName;
        else if (representationType.Identifier == RepresentationTypeIdentifier.TypedList)
            name = GetTypeName(representationType.SubTypes[0], templates) + "[]";
        else if (representationType.Identifier == RepresentationTypeIdentifier.TypedMap)
            name = "Map<" + GetTypeName(representationType.SubTypes[0], templates)
                    + "," + GetTypeName(representationType.SubTypes[1], templates)
                    + ">";
        else if (representationType.Identifier == RepresentationTypeIdentifier.Tuple2 ||
                 representationType.Identifier == RepresentationTypeIdentifier.Tuple3 ||
                 representationType.Identifier == RepresentationTypeIdentifier.Tuple4 ||
                 representationType.Identifier == RepresentationTypeIdentifier.Tuple5 ||
                 representationType.Identifier == RepresentationTypeIdentifier.Tuple6 ||
                 representationType.Identifier == RepresentationTypeIdentifier.Tuple7)
            name = "(" + String.Join(",", representationType.SubTypes.Select(x=> GetTypeName(x, templates)))
                    + ")";
        else
        {

            name = representationType.Identifier switch
            {
                RepresentationTypeIdentifier.Dynamic => "object",
                RepresentationTypeIdentifier.Bool => "bool",
                RepresentationTypeIdentifier.Char => "char",
                RepresentationTypeIdentifier.DateTime => "DateTime",
                RepresentationTypeIdentifier.Decimal => "decimal",
                RepresentationTypeIdentifier.Float32 => "float",
                RepresentationTypeIdentifier.Float64 => "double",
                RepresentationTypeIdentifier.Int16 => "short",
                RepresentationTypeIdentifier.Int32 => "int",
                RepresentationTypeIdentifier.Int64 => "long",
                RepresentationTypeIdentifier.Int8 => "sbyte",
                RepresentationTypeIdentifier.String => "string",
                RepresentationTypeIdentifier.Map => "Map<object, object>",
                RepresentationTypeIdentifier.UInt16 => "ushort",
                RepresentationTypeIdentifier.UInt32 => "uint",
                RepresentationTypeIdentifier.UInt64 => "ulong",
                RepresentationTypeIdentifier.UInt8 => "byte",
                RepresentationTypeIdentifier.List => "object[]",
                RepresentationTypeIdentifier.Resource => "IResource",
                RepresentationTypeIdentifier.Record => "IRecord",
                _ => "object"
            };
        }

        return (representationType.Nullable) ? name + "?" : name;
    }

    public static string GetTemplate(string url, string dir = null, string username = null, string password = null)
    {
        try
        {

            if (!urlRegex.IsMatch(url))
                throw new Exception("Invalid IIP URL");

            var path = urlRegex.Split(url);
            var con = Warehouse.Get<DistributedConnection>(path[1] + "://" + path[2],
                    !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password) ? new { Username = username, Password = password } : null
                ).Wait(20000);

            if (con == null)
                throw new Exception("Can't connect to server");

            if (string.IsNullOrEmpty(dir))
                dir = path[2].Replace(":", "_");

            var templates = con.GetLinkTemplates(path[3]).Wait(60000);
            // no longer needed
            Warehouse.Remove(con);

            var tempDir = new DirectoryInfo(Path.GetTempPath() + Path.DirectorySeparatorChar
                            + Misc.Global.GenerateCode(20) + Path.DirectorySeparatorChar + dir);

            if (!tempDir.Exists)
                tempDir.Create();
            else
            {
                foreach (FileInfo file in tempDir.GetFiles())
                    file.Delete();
            }

            // make sources
            foreach (var tmp in templates)
            {
                if (tmp.Type == TemplateType.Resource)
                {
                    var source = GenerateClass(tmp, templates);
                    File.WriteAllText(tempDir.FullName + Path.DirectorySeparatorChar + tmp.ClassName + ".Generated.cs", source);
                }
                else if (tmp.Type == TemplateType.Record)
                {
                    var source = GenerateRecord(tmp, templates);
                    File.WriteAllText(tempDir.FullName + Path.DirectorySeparatorChar + tmp.ClassName + ".Generated.cs", source);
                }
                else if (tmp.Type == TemplateType.Enum)
                {
                    var source = GenerateEnum(tmp, templates);
                    File.WriteAllText(tempDir.FullName + Path.DirectorySeparatorChar + tmp.ClassName + ".Generated.cs", source);
                }
            }

            // generate info class

            var typesFile = @"using System;
    namespace Esiur { 
        public static class Generated { 
            public static Type[] Resources {get;} = new Type[] { " +
                    string.Join(",", templates.Where(x => x.Type == TemplateType.Resource).Select(x => $"typeof({x.ClassName})"))
                + @" };
            public static Type[] Records { get; } = new Type[] { " +
                    string.Join(",", templates.Where(x => x.Type == TemplateType.Record).Select(x => $"typeof({x.ClassName})"))
                + @" };
            public static Type[] Enums { get; } = new Type[] { " +
                    string.Join(",", templates.Where(x => x.Type == TemplateType.Enum).Select(x => $"typeof({x.ClassName})"))
                + @" };" +
                "\r\n } \r\n}";


            File.WriteAllText(tempDir.FullName + Path.DirectorySeparatorChar + "Esiur.Generated.cs", typesFile);


            return tempDir.FullName;

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    internal static string GenerateClass(TypeTemplate template, TypeTemplate[] templates)
    {
        var cls = template.ClassName.Split('.');

        var nameSpace = string.Join(".", cls.Take(cls.Length - 1));
        var className = cls.Last();

        var rt = new StringBuilder();

        rt.AppendLine("using System;\r\nusing Esiur.Resource;\r\nusing Esiur.Core;\r\nusing Esiur.Data;\r\nusing Esiur.Net.IIP;");
        rt.AppendLine($"namespace { nameSpace} {{");

        // extends
        if (template.ParentId == null)
            rt.AppendLine($"public class {className} : DistributedResource {{");
        else
            rt.AppendLine($"public class {className} : {templates.First(x => x.ClassId == template.ParentId && x.Type == TemplateType.Resource).ClassName} {{");


        rt.AppendLine($"public {className}(DistributedConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) {{}}");
        rt.AppendLine($"public {className}() {{}}");

        foreach (var f in template.Functions)
        {
            if (f.Inherited)
                continue;

            var rtTypeName = GetTypeName(f.ReturnType, templates);
            rt.Append($"public AsyncReply<{rtTypeName}> {f.Name}(");
            rt.Append(string.Join(",", f.Arguments.Select(x => GetTypeName(x.Type, templates) + " " + x.Name)));

            rt.AppendLine(") {");
            rt.AppendLine($"var rt = new AsyncReply<{rtTypeName}>();");
            rt.AppendLine($"_InvokeByArrayArguments({f.Index}, new object[] {{ { string.Join(", ", f.Arguments.Select(x => x.Name)) } }})");
            rt.AppendLine($".Then(x => rt.Trigger(({rtTypeName})x))");
            rt.AppendLine($".Error(x => rt.TriggerError(x))");
            rt.AppendLine($".Chunk(x => rt.TriggerChunk(x));");
            rt.AppendLine("return rt; }");
        }

        foreach (var p in template.Properties)
        {
            if (p.Inherited)
                continue;

            var ptTypeName = GetTypeName(p.ValueType, templates);
            rt.AppendLine($"public {ptTypeName} {p.Name} {{");
            rt.AppendLine($"get => ({ptTypeName})properties[{p.Index}];");
            rt.AppendLine($"set =>  _Set({p.Index}, value);");
            rt.AppendLine("}");
        }

        foreach (var c in template.Constants)
        {
            if (c.Inherited)
                continue;

            var ctTypeName = GetTypeName(c.ValueType, templates);
            rt.AppendLine($"public const {ctTypeName} {c.Name} = {c.Value};");
        }


        if (template.Events.Length > 0)
        {
            
            rt.AppendLine("protected override void _EmitEventByIndex(byte index, object args) {");
            rt.AppendLine("switch (index) {");

            var eventsList = new StringBuilder();

            foreach (var e in template.Events)
            {
                var etTypeName = GetTypeName(e.ArgumentType, templates);
                rt.AppendLine($"case {e.Index}: {e.Name}?.Invoke(({etTypeName})args); break;");
                eventsList.AppendLine($"public event ResourceEventHandler<{etTypeName}> {e.Name};");
            }

            rt.AppendLine("}}");

            rt.AppendLine(eventsList.ToString());

        }

        rt.AppendLine("\r\n}\r\n}");

        return rt.ToString();
    }

}