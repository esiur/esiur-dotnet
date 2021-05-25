using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
using Esiur.Data;
using System.IO;
using Esiur.Core;

namespace Esiur.Proxy
{
    [Generator]
    public class ResourceGenerator : ISourceGenerator
    {


        private static Regex urlRegex = new Regex(@"^(?:([\S]*)://([^/]*)/?)");

        private KeyList<string, ResourceTemplate[]> cache = new();
       // private List<string> inProgress = new();

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register receiver

            context.RegisterForSyntaxNotifications(() => new ResourceGeneratorReceiver());
        }

        string GetTypeName(TemplateDataType templateDataType, ResourceTemplate[] templates)
        {

            if (templateDataType.Type == DataType.Resource)
                return templates.First(x => x.ClassId == templateDataType.TypeGuid).ClassName;
            else if (templateDataType.Type == DataType.ResourceArray)
                return templates.First(x => x.ClassId == templateDataType.TypeGuid).ClassName + "[]";

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

        void ReportError(GeneratorExecutionContext context, string title, string msg, string category)
        {
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("MySG001", title, msg, category, DiagnosticSeverity.Error, true), Location.None));
        }

        string GenerateClass(ResourceTemplate template, ResourceTemplate[] templates)
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
                    eventsList.AppendLine($"public event ResourceEventHanlder<{etTypeName}> {e.Name};");
                }

                rt.AppendLine("}}");

                rt.AppendLine(eventsList.ToString());

            }

            rt.AppendLine("\r\n}\r\n}");

            return rt.ToString();
        }


        void GenerateModel(GeneratorExecutionContext context, ResourceTemplate[] templates)
        {
            foreach (var tmp in templates)
            {
                var source = GenerateClass(tmp, templates);
                //File.WriteAllText($@"C:\gen\{tmp.ClassName}.cs", source);
                context.AddSource(tmp.ClassName + "_esiur.cs", source);
            }

            // generate info class

            var gen = "using System; \r\n namespace Esiur { public static class Generated { public static Type[] Types {get;} = new Type[]{ " +
                    string.Join(",", templates.Select(x => $"typeof({x.ClassName})"))
                + " }; \r\n } \r\n}";

            //File.WriteAllText($@"C:\gen\Esiur.Generated.cs", gen);

            context.AddSource("Esiur.Generated.cs", gen);

        }

        public void Execute(GeneratorExecutionContext context)
        {

            if (!(context.SyntaxContextReceiver is ResourceGeneratorReceiver receiver))
                return;

            //if (receiver.Imports.Count > 0 && !Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}

            foreach (var path in receiver.Imports)
            {
                if (!urlRegex.IsMatch(path))
                    continue;


                //File.WriteAllLines("C:\\gen\\ref.log", context.Compilation.ReferencedAssemblyNames.Select(x => x.ToString()));

                if (cache.Contains(path))
                {
                    GenerateModel(context, cache[path]);
                    continue;
                }

                // Syncronization
                //if (inProgress.Contains(path))
                //  continue;

                //inProgress.Add(path);

                var url = urlRegex.Split(path);


                try
                {
                    var con = Warehouse.Get<DistributedConnection>(url[1] + "://" + url[2]).Wait(20000);
                    var templates = con.GetLinkTemplates(url[3]).Wait(60000);

                    cache[path] = templates;

                    // make sources
                    GenerateModel(context, templates);

                }
                catch (Exception ex)
                {
                    ReportError(context, ex.Source, ex.Message, "Esiur"); 
                    System.IO.File.AppendAllText("c:\\gen\\error.log", ex.ToString() + "\r\n");
                }

                //inProgress.Remove(path);
            }


            //#if DEBUG

            //#endif

            //var toImplement = receiver.Classes.Where(x => x.Fields.Length > 0);

            foreach (var ci in receiver.Classes.Values)
            {
                try
                {

                    var code = @$"using Esiur.Resource; 
using Esiur.Core; 
namespace { ci.ClassSymbol.ContainingNamespace.ToDisplayString() } {{
";

                    if (ci.HasInterface)
                        code += $"public partial class {ci.Name} {{";
                    else
                    {
                        code += @$"public partial class {ci.Name} : IResource {{
public Instance Instance {{ get; set; }}
public event DestroyedEvent OnDestroy;
public virtual void Destroy() {{ OnDestroy?.Invoke(this); }}
";

                        if (!ci.HasTrigger)
                            code += "public AsyncReply<bool> Trigger(ResourceTrigger trigger) => new AsyncReply<bool>(true);\r\n";
                    }

                    foreach (var f in ci.Fields)
                    {
                        var fn = f.Name;
                        var pn = fn.Substring(0, 1).ToUpper() + fn.Substring(1);

                        // copy attributes 
                        var attrs = string.Join(" ", f.GetAttributes().Select(x => $"[{x.ToString()}]"));
                        code += $"{attrs} public {f.Type} {pn} {{ get => {fn}; set {{ {fn} = value; Instance?.Modified(); }} }}\r\n";
                    }

                    code += "}}\r\n";

                    //System.IO.File.WriteAllText("c:\\gen\\" + ci.Name + "_esiur.cs", code);
                    context.AddSource(ci.Name + "_esiur.cs", code);

                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("c:\\gen\\error.log", ci.Name + " " + ex.ToString() + "\r\n");
                }
            }
        }
    }
}
