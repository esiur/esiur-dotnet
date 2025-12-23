// ================================
// FILE: ResourceIncrementalGenerator.cs
// Replaces: ResourceGenerator.cs + ResourceGeneratorReceiver.cs
// ================================
using Esiur.Core;
using Esiur.Data;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Resource.Template;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Esiur.Proxy
{
    [Generator(LanguageNames.CSharp)]
    public sealed class ResourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1) Discover candidate classes via a cheap syntax filter
            var perClass = context.SyntaxProvider.CreateSyntaxProvider(
                 (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                 (ctx, _) => AnalyzeClass(ctx)
            )
            .Where( x => x is not null)!;

            // 2) Aggregate import URLs (distinct)
            var importUrls = perClass
                .SelectMany((x, y) => x.Value.ImportUrls)
                .Collect()
                .Select( (urls, y) => urls.Distinct(StringComparer.Ordinal).ToImmutableArray());

            // 3) Aggregate class infos and merge partials by stable key
            var mergedResources = perClass
                .Select( (x, y) => x.Value.ClassInfo)
                .Where( ci => ci is not null)!
                .Select( (ci, y) => ci!)
                .Collect()
                .Select( (list, y) => MergePartials(list));

            // 4) Generate: A) remote templates (from ImportAttribute URLs)
            context.RegisterSourceOutput(importUrls,  (spc, urls) =>
            {
                if (urls.Length == 0) return;
                foreach (var path in urls)
                {
                    try
                    {
                        if (!TemplateGenerator.urlRegex.IsMatch(path))
                            continue;

                        var parts = TemplateGenerator.urlRegex.Split(path);
                        var con = Warehouse.Default.Get<DistributedConnection>($"{parts[1]}://{parts[2]}").Wait(20000);
                        var templates = con.GetLinkTemplates(parts[3]).Wait(60000);

                        EmitTemplates(spc, templates);
                    }
                    catch (Exception ex)
                    {
                        Report(spc, "Esiur", ex.Message, DiagnosticSeverity.Error);
                    }
                }
            });

            // 4) Generate: B) per resource partials (properties/events, base IResource impl if needed)
            context.RegisterSourceOutput(mergedResources, static (spc, classes) =>
            {
                foreach (var ci in classes)
                {
                    try
                    {
                        var code = @$"using Esiur.Resource; 
using Esiur.Core;

#nullable enable

namespace {ci.ClassSymbol.ContainingNamespace.ToDisplayString()} {{
";

                        if (IsInterfaceImplemented(ci, classes))
                            code += $"public partial class {ci.Name} {{\r\n";
                        else
                        {
                            code +=
$@" public partial class {ci.Name} : IResource {{
    public virtual Instance? Instance {{ get; set; }}
    public virtual event DestroyedEvent? OnDestroy;

    public virtual void Destroy() {{ OnDestroy?.Invoke(this); }}
";

                            if (!ci.HasTrigger)
                                code += "\tpublic virtual AsyncReply<bool> Trigger(ResourceTrigger trigger) => new AsyncReply<bool>(true);\r\n\r\n";
                        }

                        foreach (var f in ci.Fields)
                        {
                            var givenName = f.GetAttributes().FirstOrDefault(x => x.AttributeClass?.Name == "ExportAttribute")?.ConstructorArguments.FirstOrDefault().Value as string;

                            var fn = f.Name;
                            var pn = string.IsNullOrEmpty(givenName) ? SuggestExportName(fn) : givenName;

                            var attrs = string.Join("\r\n\t", f.GetAttributes().Select(x => FormatAttribute(x)));

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

                        spc.AddSource(ci.Name + ".g.cs", code);
                    }
                    catch (Exception ex)
                    {
                        spc.AddSource(ci.Name + ".Error.g.cs", $"/*\r\n{ex}\r\n*/");
                    }
                }
            });
        }


        // === Analysis ===
        private static PerClass? AnalyzeClass(GeneratorSyntaxContext ctx)
        {
            var cds = (ClassDeclarationSyntax)ctx.Node;
            var cls = ctx.SemanticModel.GetDeclaredSymbol(cds) as ITypeSymbol;
            if (cls is null) return null;

            var attrs = cls.GetAttributes();

            // Collect ImportAttribute URLs
            var importUrls = attrs
                .Where(a => a.AttributeClass?.ToDisplayString() == "Esiur.Resource.ImportAttribute")
                .SelectMany(a => a.ConstructorArguments.Select(x => x.Value?.ToString()))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToImmutableArray();

            // If class has ResourceAttribute, gather details
            var hasResource = attrs.Any(a => a.AttributeClass?.ToDisplayString() == "Esiur.Resource.ResourceAttribute");
            ResourceClassInfo? classInfo = null;
            if (hasResource)
            {
                bool hasTrigger = cds.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(m => ctx.SemanticModel.GetDeclaredSymbol(m) as IMethodSymbol)
                    .Where(s => s is not null)
                    .Any(s => s!.Name == "Trigger" && s.Parameters.Length == 1 && s.Parameters[0].Type.ToDisplayString() == "Esiur.Resource.ResourceTrigger");

                var exportedFields = cds.Members
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables.Select(v => (f, v)))
                    .Select(t => ctx.SemanticModel.GetDeclaredSymbol(t.v) as IFieldSymbol)
                    .Where(f => f is not null && !f!.IsConst)
                    .Where(f => f!.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Esiur.Resource.ExportAttribute"))
                    .Cast<IFieldSymbol>()
                    .ToList();

                bool hasInterface = cls.AllInterfaces.Any(x => x.ToDisplayString() == "Esiur.Resource.IResource");

                var key = $"{cls.ContainingAssembly.Name}:{cls.ContainingNamespace.ToDisplayString()}.{cls.Name}";

                classInfo = new ResourceClassInfo
                (
                    key,
                    cls.Name,
                    cds,
                    cls,
                    exportedFields,
                    hasInterface,
                    hasTrigger
                );
            }

            return new PerClass(classInfo, importUrls);
        }

        private static ImmutableArray<ResourceClassInfo> MergePartials(ImmutableArray<ResourceClassInfo> list)
        {
            var byKey = new Dictionary<string, ResourceClassInfo>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                if (byKey.TryGetValue(item.Key, out var existing))
                {
                    // merge fields + flags
                    var mergedFields = existing.Fields.Concat(item.Fields).ToList();
                    byKey[item.Key] = existing with
                    {
                        Fields = mergedFields,
                        HasInterface = existing.HasInterface || item.HasInterface,
                        HasTrigger = existing.HasTrigger || item.HasTrigger
                    };
                }
                else
                {
                    byKey[item.Key] = item with { Fields = item.Fields.ToList() };
                }
            }
            return byKey.Values.ToImmutableArray();
        }

        // Determine if the base already implements IResource (either directly or via another generated part)
        private static bool IsInterfaceImplemented(ResourceClassInfo ci, ImmutableArray<ResourceClassInfo> merged)
        {
            if (ci.HasInterface) return true;
            var baseType = ci.ClassSymbol.BaseType;
            if (baseType is null) return false;
            var baseKey = $"{baseType.ContainingAssembly.Name}:{baseType.ContainingNamespace.ToDisplayString()}.{baseType.Name}";
            return merged.Any(x => x.Key == baseKey);
        }

        // === Emission helpers (ported from your original generator) ===
        private static void EmitTemplates(SourceProductionContext spc, TypeTemplate[] templates)
        {
            foreach (var tmp in templates)
            {
                if (tmp.Type == TemplateType.Resource)
                {
                    var source = TemplateGenerator.GenerateClass(tmp, templates, false);
                    spc.AddSource(tmp.ClassName + ".g.cs", source);
                }
                else if (tmp.Type == TemplateType.Record)
                {
                    var source = TemplateGenerator.GenerateRecord(tmp, templates);
                    spc.AddSource(tmp.ClassName + ".g.cs", source);
                }
            }

            var typesFile = "using System; \r\n namespace Esiur { public static class Generated { public static Type[] Resources {get;} = new Type[] { " +
                                string.Join(",", templates.Where(x => x.Type == TemplateType.Resource).Select(x => $"typeof({x.ClassName})"))
                            + " }; \r\n public static Type[] Records { get; } = new Type[] { " +
                                string.Join(",", templates.Where(x => x.Type == TemplateType.Record).Select(x => $"typeof({x.ClassName})"))
                            + " }; " +

                            "\r\n } \r\n}";

            spc.AddSource("Esiur.g.cs", typesFile);
        }

        private static void Report(SourceProductionContext ctx, string title, string message, DiagnosticSeverity severity)
        {
            var descriptor = new DiagnosticDescriptor("ESIUR001", title, message, "Esiur", severity, true);
            ctx.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
        }

        // === Formatting helpers from your original code ===
        private static string SuggestExportName(string fieldName)
        {
            if (char.IsUpper(fieldName[0]))
                return fieldName.Substring(0, 1).ToLowerInvariant() + fieldName.Substring(1);
            else
                return char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
        }

        private static string FormatAttribute(AttributeData attribute)
        {
            if (attribute.AttributeClass is null)
                throw new Exception("AttributeClass not found");

            var className = attribute.AttributeClass.ToDisplayString();
            if (!attribute.ConstructorArguments.Any() & !attribute.NamedArguments.Any())
                return $"[{className}]";

            var sb = new StringBuilder();
            sb.Append('[').Append(className).Append('(');
            if (attribute.ConstructorArguments.Any())
                sb.Append(string.Join(", ", attribute.ConstructorArguments.Select(FormatConstant)));
            if (attribute.NamedArguments.Any())
            {
                if (attribute.ConstructorArguments.Any()) sb.Append(", ");
                sb.Append(string.Join(", ", attribute.NamedArguments.Select(na => $"{na.Key} = {FormatConstant(na.Value)}")));
            }
            sb.Append(")] ");
            return sb.ToString();
        }

        private static string FormatConstant(TypedConstant constant)
        {
            if (constant.Kind == TypedConstantKind.Array)
                return $"new {constant.Type?.ToDisplayString()} {{{string.Join(", ", constant.Values.Select(FormatConstant))}}}";
            return constant.ToCSharpString();
        }

        // === Data carriers for the pipeline ===
        private readonly record struct PerClass {
            public PerClass(ResourceClassInfo?  classInfo, ImmutableArray<string> importUrls)
            {
                this.ImportUrls = importUrls;
                this.ClassInfo = classInfo;
            }

            public readonly ImmutableArray<string> ImportUrls;
            public readonly ResourceClassInfo? ClassInfo;
        }

        private sealed record ResourceClassInfo {

            public ResourceClassInfo(string key, string name , 
                ClassDeclarationSyntax classDeclaration, 
                ITypeSymbol classSymbol, List<IFieldSymbol> fileds, bool hasInterface, bool hasTrigger)
            {
                Key = key;
                Name = name;
                ClassDeclaration = classDeclaration;
                ClassSymbol = classSymbol;
                Fields = fileds;
                HasInterface = hasInterface;
                HasTrigger = hasTrigger;
            }

            public string Key;
            public string Name;
            public ClassDeclarationSyntax ClassDeclaration;
            public ITypeSymbol ClassSymbol;
            public List<IFieldSymbol> Fields;
            public bool HasInterface;
            public bool HasTrigger;
        }
    }
}