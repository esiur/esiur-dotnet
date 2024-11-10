using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

namespace Esiur.Proxy;

[Generator]
[System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1036:Specify analyzer banned API enforcement setting", Justification = "<Pending>")]
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
                var source = TemplateGenerator.GenerateClass(tmp, templates, false);
                context.AddSource(tmp.ClassName + ".Generated.cs", source);
            }
            else if (tmp.Type == TemplateType.Record)
            {
                var source = TemplateGenerator.GenerateRecord(tmp, templates);
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

        context.AddSource("Esiur.Generated.cs", typesFile);

    }



    public static string SuggestExportName(string fieldName)
    {
        if (Char.IsUpper(fieldName[0]))
            return fieldName.Substring(0, 1).ToLower() + fieldName.Substring(1);
        else
            return fieldName.Substring(0, 1).ToUpper() + fieldName.Substring(1);
    }

    public static string FormatAttribute(AttributeData attribute)
    {
        if (!(attribute.AttributeClass is object))
            throw new Exception("AttributeClass not found");

        var className = attribute.AttributeClass.ToDisplayString();

        if (!attribute.ConstructorArguments.Any() & !attribute.ConstructorArguments.Any())
            return $"[{className}]";

        var strBuilder = new StringBuilder();

        strBuilder.Append("[");
        strBuilder.Append(className);
        strBuilder.Append("(");

        strBuilder.Append(String.Join(", ", attribute.ConstructorArguments.Select(ca => FormatConstant(ca))));

        strBuilder.Append(String.Join(", ", attribute.NamedArguments.Select(na => $"{na.Key} = {FormatConstant(na.Value)}")));

        strBuilder.Append(")]");

        return strBuilder.ToString();
    }

    public static string FormatConstant(TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Array)
            return $"new {constant.Type.ToDisplayString()} {{{string.Join(", ", constant.Values.Select(v => FormatConstant(v)))}}}";
        else
            return constant.ToCSharpString();
    }


    public void Execute(GeneratorExecutionContext context)
    {

        try
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

#nullable enable

namespace {ci.ClassSymbol.ContainingNamespace.ToDisplayString()} {{
";

                    if (ci.IsInterfaceImplemented(receiver.Classes))
                        code += $"public partial class {ci.Name} {{\r\n";
                    else
                    {
                        code +=
    @$" public partial class {ci.Name} : IResource {{
    public virtual Instance? Instance {{ get; set; }}
    public virtual event DestroyedEvent? OnDestroy;

    public virtual void Destroy() {{ OnDestroy?.Invoke(this); }}
";

                        if (!ci.HasTrigger)
                            code +=
    "\tpublic virtual AsyncReply<bool> Trigger(ResourceTrigger trigger) => new AsyncReply<bool>(true);\r\n\r\n";
                    }

                    //Debugger.Launch();

                    foreach (var f in ci.Fields)
                    {
                        var givenName = f.GetAttributes().Where(x => x.AttributeClass.Name == "ExportAttribute").FirstOrDefault()?.ConstructorArguments.FirstOrDefault().Value as string;

                        var fn = f.Name;
                        var pn = string.IsNullOrEmpty(givenName) ? SuggestExportName(fn) : givenName;

                        // copy attributes 
                        //Debugger.Launch();

                        var attrs = string.Join("\r\n\t", f.GetAttributes().Select(x => FormatAttribute(x)));

                        //Debugger.Launch();
                        if (f.Type.Name.StartsWith("ResourceEventHandler") || f.Type.Name.StartsWith("CustomResourceEventHandler"))
                        {
                            code += $"\t{attrs}\r\n\t public event {f.Type} {pn};\r\n";
                        }
                        else
                        {
                            code += $"\t{attrs}\r\n\t public {f.Type} {pn} {{ \r\n\t\t get => {fn}; \r\n\t\t set {{ \r\n\t\t this.{fn} = value; \r\n\t\t Instance?.Modified(); \r\n\t\t}}\r\n\t}}\r\n";
                        }
                    }

                    code += "}}\r\n";

                    context.AddSource(ci.Name + ".g.cs", code);

                }
                catch (Exception ex)
                {
                    context.AddSource(ci.Name + ".Error.g.cs", $"/*\r\n{ex}\r\n*/");
                }
            }
        }
        catch (Exception ex)
        {

            context.AddSource("Error.g.cs", $"/*\r\n{ex}\r\n*/");
        }
    }
}
