using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Esiur.Proxy;
public class ResourceGeneratorReceiver : ISyntaxContextReceiver
{

    public Dictionary<string, ResourceGeneratorClassInfo> Classes { get; } = new();

    public List<string> Imports { get; } = new();

    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {

        if (context.Node is ClassDeclarationSyntax)
        {
            var cds = context.Node as ClassDeclarationSyntax;
            var cls = context.SemanticModel.GetDeclaredSymbol(cds) as ITypeSymbol;
            var attrs = cls.GetAttributes();

            var imports = attrs.Where(a => a.AttributeClass.ToDisplayString() == "Esiur.Resource.ImportAttribute");

            foreach (var import in imports)
            {
                // Debugger.Launch();

                var urls = import.ConstructorArguments.Select(x => x.Value.ToString());//.ToString();

                foreach(var url in urls)
                    if (!Imports.Contains(url))
                        Imports.Add(url);
            }

            if (attrs.Any(a => a.AttributeClass.ToDisplayString() == "Esiur.Resource.ResourceAttribute"))
            {


                var hasTrigger = cds.Members
                    .Where(x => x is MethodDeclarationSyntax)
                    .Select(x => context.SemanticModel.GetDeclaredSymbol(x) as IMethodSymbol)
                    .Any(x => x.Name == "Trigger"
                            && x.Parameters.Length == 1
                            && x.Parameters[0].Type.ToDisplayString() == "Esiur.Resource.ResourceTrigger");

                var fields = cds.Members.Where(x => x is FieldDeclarationSyntax)
                                        .Select(x => context.SemanticModel.GetDeclaredSymbol((x as FieldDeclarationSyntax).Declaration.Variables.First()) as IFieldSymbol)
                                        .Where(x => !x.IsConst)
                                        .Where(x => x.GetAttributes().Any(a => a.AttributeClass.ToDisplayString() == "Esiur.Resource.PublicAttribute"))
                                        .ToArray();

                //if (!Debugger.IsAttached)
                //{
                //    if (cls.Name == "User")
                //        Debugger.Launch();
                //}



                // get fields

                var fullName = cls.ContainingAssembly + "." + cls.Name;

                // Partial class check
                if (Classes.ContainsKey(fullName))
                {
                    // append fields
                    var c = Classes[fullName];
                    c.Fields.AddRange(fields);
                    if (!c.HasInterface)
                        c.HasInterface = cls.Interfaces.Any(x => x.ToDisplayString() == "Esiur.Resource.IResource");
                    if (!c.HasTrigger)
                        c.HasTrigger = hasTrigger;
                }
                else
                {
                    Classes.Add(fullName, new ResourceGeneratorClassInfo()
                    {
                        Name = cls.Name,
                        ClassDeclaration = cds,
                        ClassSymbol = cls,
                        Fields = fields.ToList(),
                        HasInterface = cls.Interfaces.Any(x => x.ToDisplayString() == "Esiur.Resource.IResource"),
                        HasTrigger = hasTrigger
                    });
                }


                return;
            }
        }
    }
}

