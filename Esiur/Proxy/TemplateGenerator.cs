using Esiur.Data;
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

    //public static string ToLiteral(string valueTextForCompiler)
    //{
    //    return SymbolDisplay.FormatLiteral(valueTextForCompiler, false);
    //}

    static string ToLiteral(string input)
    {
        if (input == null) return "null";

        var literal = new StringBuilder();
        literal.Append("\"");
        foreach (var c in input)
        {
            switch (c)
            {
                case '\"': literal.Append("\\\""); break;
                case '\\': literal.Append(@"\\"); break;
                case '\0': literal.Append(@"\0"); break;
                case '\a': literal.Append(@"\a"); break;
                case '\b': literal.Append(@"\b"); break;
                case '\f': literal.Append(@"\f"); break;
                case '\n': literal.Append(@"\n"); break;
                case '\r': literal.Append(@"\r"); break;
                case '\t': literal.Append(@"\t"); break;
                case '\v': literal.Append(@"\v"); break;
                default:
                    // ASCII printable character
                    if (c >= 0x20 && c <= 0x7e)
                    {
                        literal.Append(c);
                        // As UTF16 escaped character
                    }
                    else
                    {
                        literal.Append(@"\u");
                        literal.Append(((int)c).ToString("x4"));
                    }
                    break;
            }
        }
        literal.Append("\"");
        return literal.ToString();
    }


    internal static string GenerateRecord(TypeTemplate template, TypeTemplate[] templates)
    {
        var cls = template.ClassName.Split('.');

        var nameSpace = string.Join(".", cls.Take(cls.Length - 1));
        var className = cls.Last();


        var rt = new StringBuilder();

        rt.AppendLine("using System;\r\nusing Esiur.Resource;\r\nusing Esiur.Core;\r\nusing Esiur.Data;\r\nusing Esiur.Net.IIP;");
        rt.AppendLine($"namespace {nameSpace} {{");

        if (template.Annotations != null)
        {
            foreach (var ann in template.Annotations)
            {
                rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
            }
        }

        rt.AppendLine($"[ClassId(\"{template.ClassId.Data.ToHex(0, 16, null)}\")]");
        rt.AppendLine($"[Export] public class {className} : IRecord {{");


        foreach (var p in template.Properties)
        {
            var ptTypeName = GetTypeName(p.ValueType, templates);


            if (p.Annotations != null)
            {
                foreach (var ann in p.Annotations)
                {
                    rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
                }
            }


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
        rt.AppendLine($"namespace {nameSpace} {{");

        if (template.Annotations != null)
        {
            foreach (var ann in template.Annotations)
            {
                rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
            }
        }

        rt.AppendLine($"[ClassId(\"{template.ClassId.Data.ToHex(0, 16, null)}\")]");
        rt.AppendLine($"[Export] public enum {className} {{");

        rt.AppendLine(String.Join(",\r\n", template.Constants.Select(x => $"{x.Name}={x.Value}")));

        rt.AppendLine("\r\n}\r\n}");

        return rt.ToString();
    }


    static string GetTypeName(TRU representationType, TypeTemplate[] templates)
    {
        string name;

        if (representationType.Identifier == TRUIdentifier.TypedResource)// == DataType.Resource)
            name = templates.First(x => x.ClassId == representationType.UUID && (x.Type == TemplateType.Resource)).ClassName;
        else if (representationType.Identifier == TRUIdentifier.TypedRecord)
            name = templates.First(x => x.ClassId == representationType.UUID && x.Type == TemplateType.Record).ClassName;
        else if (representationType.Identifier == TRUIdentifier.Enum)
            name = templates.First(x => x.ClassId == representationType.UUID && x.Type == TemplateType.Enum).ClassName;
        else if (representationType.Identifier == TRUIdentifier.TypedList)
            name = GetTypeName(representationType.SubTypes[0], templates) + "[]";
        else if (representationType.Identifier == TRUIdentifier.TypedMap)
            name = "Map<" + GetTypeName(representationType.SubTypes[0], templates)
                    + "," + GetTypeName(representationType.SubTypes[1], templates)
                    + ">";
        else if (representationType.Identifier == TRUIdentifier.Tuple2 ||
                 representationType.Identifier == TRUIdentifier.Tuple3 ||
                 representationType.Identifier == TRUIdentifier.Tuple4 ||
                 representationType.Identifier == TRUIdentifier.Tuple5 ||
                 representationType.Identifier == TRUIdentifier.Tuple6 ||
                 representationType.Identifier == TRUIdentifier.Tuple7)
            name = "(" + String.Join(",", representationType.SubTypes.Select(x => GetTypeName(x, templates)))
                    + ")";
        else
        {

            name = representationType.Identifier switch
            {
                TRUIdentifier.Dynamic => "object",
                TRUIdentifier.Bool => "bool",
                TRUIdentifier.Char => "char",
                TRUIdentifier.DateTime => "DateTime",
                TRUIdentifier.Decimal => "decimal",
                TRUIdentifier.Float32 => "float",
                TRUIdentifier.Float64 => "double",
                TRUIdentifier.Int16 => "short",
                TRUIdentifier.Int32 => "int",
                TRUIdentifier.Int64 => "long",
                TRUIdentifier.Int8 => "sbyte",
                TRUIdentifier.String => "string",
                TRUIdentifier.Map => "Map<object, object>",
                TRUIdentifier.UInt16 => "ushort",
                TRUIdentifier.UInt32 => "uint",
                TRUIdentifier.UInt64 => "ulong",
                TRUIdentifier.UInt8 => "byte",
                TRUIdentifier.List => "object[]",
                TRUIdentifier.Resource => "IResource",
                TRUIdentifier.Record => "IRecord",
                _ => "object"
            };
        }

        return (representationType.Nullable) ? name + "?" : name;
    }

    public static string GetTemplate(string url, string dir = null, bool tempDir = false, string username = null, string password = null, bool asyncSetters = false)
    {
        try
        {

            if (!urlRegex.IsMatch(url))
                throw new Exception("Invalid IIP URL");

            var path = urlRegex.Split(url);
            var con = Warehouse.Default.Get<DistributedConnection>(path[1] + "://" + path[2],
                    !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password) ? new { Username = username, Password = password } : null
                ).Wait(20000);

            if (con == null)
                throw new Exception("Can't connect to server");

            if (string.IsNullOrEmpty(dir))
                dir = path[2].Replace(":", "_");

            var templates = con.GetLinkTemplates(path[3]).Wait(60000);
            // no longer needed
            Warehouse.Default.Remove(con);

            var dstDir = new DirectoryInfo(tempDir ? Path.GetTempPath() + Path.DirectorySeparatorChar
                            + Misc.Global.GenerateCode(20) + Path.DirectorySeparatorChar + dir : dir);

            if (!dstDir.Exists)
                dstDir.Create();
            else
            {
                foreach (FileInfo file in dstDir.GetFiles())
                    file.Delete();
            }

            // make sources
            foreach (var tmp in templates)
            {
                if (tmp.Type == TemplateType.Resource)
                {
                    var source = GenerateClass(tmp, templates, asyncSetters);
                    File.WriteAllText(dstDir.FullName + Path.DirectorySeparatorChar + tmp.ClassName + ".g.cs", source);
                }
                else if (tmp.Type == TemplateType.Record)
                {
                    var source = GenerateRecord(tmp, templates);
                    File.WriteAllText(dstDir.FullName + Path.DirectorySeparatorChar + tmp.ClassName + ".g.cs", source);
                }
                else if (tmp.Type == TemplateType.Enum)
                {
                    var source = GenerateEnum(tmp, templates);
                    File.WriteAllText(dstDir.FullName + Path.DirectorySeparatorChar + tmp.ClassName + ".g.cs", source);
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


            File.WriteAllText(dstDir.FullName + Path.DirectorySeparatorChar + "Esiur.g.cs", typesFile);


            return dstDir.FullName;

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    internal static string GenerateClass(TypeTemplate template, TypeTemplate[] templates, bool asyncSetters)
    {
        var cls = template.ClassName.Split('.');

        var nameSpace = string.Join(".", cls.Take(cls.Length - 1));
        var className = cls.Last();

        var rt = new StringBuilder();

        rt.AppendLine("using System;\r\nusing Esiur.Resource;\r\nusing Esiur.Core;\r\nusing Esiur.Data;\r\nusing Esiur.Net.IIP;");
        rt.AppendLine("#nullable enable");

        rt.AppendLine($"namespace {nameSpace} {{");

        if (template.Annotations != null)
        {
            foreach (var ann in template.Annotations)
            {
                rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
            }
        }


        rt.AppendLine($"[ClassId(\"{template.ClassId.Data.ToHex(0, 16, null)}\")]");

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

            var positionalArgs = f.Arguments.Where((x) => !x.Optional).ToArray();
            var optionalArgs = f.Arguments.Where((x) => x.Optional).ToArray();

            if (f.Annotations != null)
            {
                foreach (var kv in f.Annotations)
                {
                    rt.AppendLine($"[Annotation({ToLiteral(kv.Key)}, {ToLiteral(kv.Value)})]");
                }
            }

            if (f.IsStatic)
            {

                rt.Append($"[Export] public static AsyncReply<{rtTypeName}> {f.Name}(DistributedConnection connection");

                if (positionalArgs.Length > 0)
                    rt.Append(", " +
                        String.Join(", ", positionalArgs.Select((a) => GetTypeName(a.Type, templates) + " " + a.Name)));

                if (optionalArgs.Length > 0)
                    rt.Append(", " +
                        String.Join(", ", optionalArgs.Select((a) => GetTypeName(a.Type.ToNullable(), templates) + " " + a.Name + " = null")));

            }
            else
            {
                rt.Append($"[Export] public AsyncReply<{rtTypeName}> {f.Name}(");

                if (positionalArgs.Length > 0)
                    rt.Append(
                        String.Join(", ", positionalArgs.Select((a) => GetTypeName(a.Type, templates) + " " + a.Name)));

                if (optionalArgs.Length > 0)
                {
                    if (positionalArgs.Length > 0) rt.Append(",");

                    rt.Append(
                        String.Join(", ", optionalArgs.Select((a) => GetTypeName(a.Type.ToNullable(), templates) + " " + a.Name + " = null")));
                }
            }

            rt.AppendLine(") {");

            rt.AppendLine(
               $"var args = new Map<byte, object>(){{{String.Join(", ", positionalArgs.Select((e) => "[" + e.Index + "] = " + e.Name))}}};");

            foreach (var a in optionalArgs)
            {
                rt.AppendLine(
                    $"if ({a.Name} != null) args[{a.Index}] = {a.Name};");
            }


            rt.AppendLine($"var rt = new AsyncReply<{rtTypeName}>();");

            if (f.IsStatic)
                rt.AppendLine($"connection.StaticCall(Guid.Parse(\"{template.ClassId.ToString()}\"), {f.Index}, args)");
            else
                rt.AppendLine($"_Invoke({f.Index}, args)");

            rt.AppendLine($".Then(x => rt.Trigger(({rtTypeName})x))");
            rt.AppendLine($".Error(x => rt.TriggerError(x))");
            rt.AppendLine($".Chunk(x => rt.TriggerChunk(x));");
            rt.AppendLine("return rt; }");
        }


        foreach (var p in template.Properties)
        {
            if (p.Inherited)
                continue;

            if (p.Annotations != null)
            {
                foreach (var ann in p.Annotations)
                {
                    rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
                }
            }

            var ptTypeName = GetTypeName(p.ValueType, templates);
            rt.AppendLine($"[Export] public {ptTypeName} {p.Name} {{");
            rt.AppendLine($"get => ({ptTypeName})properties[{p.Index}];");
            if (asyncSetters)
                rt.AppendLine($"set => _Set({p.Index}, value);");
            else
                rt.AppendLine($"set => _SetSync({p.Index}, value);");
            rt.AppendLine("}");
        }

        foreach (var c in template.Constants)
        {
            if (c.Inherited)
                continue;

            if (c.Annotations != null)
            {
                foreach (var ann in c.Annotations)
                    rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
            }

            var ctTypeName = GetTypeName(c.ValueType, templates);
            rt.AppendLine($"[Export] public const {ctTypeName} {c.Name} = {c.Value};");
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


                if (e.Annotations != null)
                {
                    foreach (var ann in e.Annotations)
                        rt.AppendLine($"[Annotation({ToLiteral(ann.Key)}, {ToLiteral(ann.Value)})]");
                }


                eventsList.AppendLine($"[Export] public event ResourceEventHandler<{etTypeName}> {e.Name};");
            }

            rt.AppendLine("}}");

            rt.AppendLine(eventsList.ToString());

        }

        rt.AppendLine("\r\n}\r\n}");

        return rt.ToString();
    }

}
