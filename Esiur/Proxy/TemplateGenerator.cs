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

namespace Esiur.Proxy
{
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
            rt.AppendLine($"public class {className} : IRecord {{");


            foreach (var p in template.Properties)
            {
                var ptTypeName = GetTypeName(p.ValueType, templates);
                rt.AppendLine($"public {ptTypeName} {p.Name} {{ get; set; }}");
                rt.AppendLine();
            }

            rt.AppendLine("\r\n}\r\n}");

            return rt.ToString();
        }

        static string GetTypeName(TemplateDataType templateDataType, TypeTemplate[] templates)
        {

            if (templateDataType.Type == DataType.Resource)
                return templates.First(x => x.ClassId == templateDataType.TypeGuid && (x.Type == TemplateType.Resource || x.Type == TemplateType.Wrapper )).ClassName;
            else if (templateDataType.Type == DataType.ResourceArray)
                return templates.First(x => x.ClassId == templateDataType.TypeGuid && (x.Type == TemplateType.Resource || x.Type == TemplateType.Wrapper )).ClassName + "[]";
            else if (templateDataType.Type == DataType.Record)
                return templates.First(x => x.ClassId == templateDataType.TypeGuid && x.Type == TemplateType.Record).ClassName;
            else if (templateDataType.Type == DataType.RecordArray)
                return templates.First(x => x.ClassId == templateDataType.TypeGuid && x.Type == TemplateType.Record).ClassName + "[]";

            var name = templateDataType.Type switch
            {
                DataType.Bool => "bool",
                DataType.BoolArray => "bool[]",
                DataType.Char => "char",
                DataType.CharArray => "char[]",
                DataType.DateTime => "DateTime",
                DataType.DateTimeArray => "DateTime[]",
                DataType.Decimal => "decimal",
                DataType.DecimalArray => "decimal[]",
                DataType.Float32 => "float",
                DataType.Float32Array => "float[]",
                DataType.Float64 => "double",
                DataType.Float64Array => "double[]",
                DataType.Int16 => "short",
                DataType.Int16Array => "short[]",
                DataType.Int32 => "int",
                DataType.Int32Array => "int[]",
                DataType.Int64 => "long",
                DataType.Int64Array => "long[]",
                DataType.Int8 => "sbyte",
                DataType.Int8Array => "sbyte[]",
                DataType.String => "string",
                DataType.StringArray => "string[]",
                DataType.Structure => "Structure",
                DataType.StructureArray => "Structure[]",
                DataType.UInt16 => "ushort",
                DataType.UInt16Array => "ushort[]",
                DataType.UInt32 => "uint",
                DataType.UInt32Array => "uint[]",
                DataType.UInt64 => "ulong",
                DataType.UInt64Array => "ulong[]",
                DataType.UInt8 => "byte",
                DataType.UInt8Array => "byte[]",
                DataType.VarArray => "object[]",
                DataType.Void => "object",
                _ => "object"
            };

            return name;
        }

        public static string GetTemplate(string url, string dir = null, string username= null, string password = null)
        {
            try
            {

                if (!urlRegex.IsMatch(url))
                    throw new Exception("Invalid IIP URL");

                var path = urlRegex.Split(url);
                var con = Warehouse.Get<DistributedConnection>(path[1] + "://" + path[2],
                        !string.IsNullOrEmpty( username) && !string.IsNullOrEmpty( password) ? new { Username = username, Password = password } : null
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
                }

                // generate info class

                var typesFile = "using System; \r\n namespace Esiur { public static class Generated { public static Type[] Resources {get;} = new Type[] { " +
                        string.Join(",", templates.Where(x => x.Type == TemplateType.Resource).Select(x => $"typeof({x.ClassName})"))
                    + " }; \r\n public static Type[] Records { get; } = new Type[] { " +
                        string.Join(",", templates.Where(x => x.Type == TemplateType.Record).Select(x => $"typeof({x.ClassName})"))
                    + " }; " +

                    "\r\n } \r\n}";


                File.WriteAllText(tempDir.FullName + Path.DirectorySeparatorChar + "Esiur.Generated.cs", typesFile);

                
                return tempDir.FullName;

            }
            catch(Exception ex)
            {
                //File.WriteAllText("C:\\gen\\gettemplate.err", ex.ToString());
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
            rt.AppendLine($"public class {className} : DistributedResource {{");

            rt.AppendLine($"public {className}(DistributedConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) {{}}");
            rt.AppendLine($"public {className}() {{}}");

            foreach (var f in template.Functions)
            {
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
                var ptTypeName = GetTypeName(p.ValueType, templates);
                rt.AppendLine($"public {ptTypeName} {p.Name} {{");
                rt.AppendLine($"get => ({ptTypeName})properties[{p.Index}];");
                rt.AppendLine($"set =>  _Set({p.Index}, value);");
                rt.AppendLine("}");
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
}
