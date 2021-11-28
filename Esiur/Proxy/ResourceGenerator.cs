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

        private KeyList<string, TypeTemplate[]> cache = new();
        // private List<string> inProgress = new();

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register receiver

            context.RegisterForSyntaxNotifications(() => new ResourceGeneratorReceiver());
        }

      
        void ReportError(GeneratorExecutionContext context, string title, string msg, string category)
        {
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("MySG001", title, msg, category, DiagnosticSeverity.Error, true), Location.None));
        }

     
        void GenerateModel(GeneratorExecutionContext context, TypeTemplate[] templates)
        {
            foreach (var tmp in templates)
            {
                if (tmp.Type == TemplateType.Resource)
                {
                    var source = TemplateGenerator.GenerateClass(tmp, templates);
                    // File.WriteAllText($@"C:\gen\{tmp.ClassName}.cs", source);
                    context.AddSource(tmp.ClassName + ".Generated.cs", source);
                }
                else if (tmp.Type == TemplateType.Record)
                {
                    var source = TemplateGenerator.GenerateRecord(tmp, templates);
                    // File.WriteAllText($@"C:\gen\{tmp.ClassName}.cs", source);
                    context.AddSource(tmp.ClassName + ".Generated.cs", source);
                }
            }

            // generate info class


            var typesFile = "using System; \r\n namespace Esiur { public static class Generated { public static Type[] Resources {get;} = new Type[] { " +
                                string.Join(",", templates.Where(x => x.Type == TemplateType.Resource).Select(x => $"typeof({x.ClassName})"))
                            + " }; \r\n public static Type[] Records { get; } = new Type[] { " +
                                string.Join(",", templates.Where(x => x.Type == TemplateType.Record).Select(x => $"typeof({x.ClassName})"))
                            + " }; " +

                            "\r\n } \r\n}";

            //File.WriteAllText($@"C:\gen\Esiur.Generated.cs", gen);

            context.AddSource("Esiur.Generated.cs", typesFile);

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
                if (!TemplateGenerator.urlRegex.IsMatch(path))
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

                var url = TemplateGenerator.urlRegex.Split(path);


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
                    //System.IO.File.AppendAllText("c:\\gen\\error.log", ex.ToString() + "\r\n");
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

                    //Debugger.Launch();

                    foreach (var f in ci.Fields)
                    {
                        var givenName = f.GetAttributes().Where(x=>x.AttributeClass.Name == "PublicAttribute").FirstOrDefault()?.ConstructorArguments.FirstOrDefault().Value;

                        var fn = f.Name;
                        var pn = givenName ?? fn.Substring(0, 1).ToUpper() + fn.Substring(1);

                        //System.IO.File.AppendAllText("c:\\gen\\fields.txt", fn + " -> " + pn + "\r\n");

                        // copy attributes 
                        var attrs = string.Join(" ", f.GetAttributes().Select(x => $"[{x.ToString()}]"));
                        code += $"{attrs} public {f.Type} {pn} {{ get => {fn}; set {{ {fn} = value; Instance?.Modified(); }} }}\r\n";
                    }

                    code += "}}\r\n";

                    //System.IO.File.WriteAllText("c:\\gen\\" + ci.Name + "_esiur.cs", code);
                    context.AddSource(ci.Name + ".Generated.cs", code);

                }
                catch (Exception ex)
                {
                    //System.IO.File.AppendAllText("c:\\gen\\error.log", ci.Name + " " + ex.ToString() + "\r\n");
                }
            }
        }
    }
}
