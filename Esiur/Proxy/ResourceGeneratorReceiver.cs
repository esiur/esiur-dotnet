using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Esiur.Proxy
{
    public class ResourceGeneratorReceiver : ISyntaxContextReceiver
    {

        public List<GenerationInfo> Classes { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {

            if (context.Node is ClassDeclarationSyntax)
            {
                var cds = context.Node as ClassDeclarationSyntax;
                var cls = context.SemanticModel.GetDeclaredSymbol(cds) as ITypeSymbol;

                if (cls.GetAttributes().Any(a => a.AttributeClass.ToDisplayString() == "Esiur.Resource.ResourceAttribute"))
                {
                    //if (!Debugger.IsAttached)
                    //{
                    //    Debugger.Launch();
                    //}

                    var hasTrigger = cds.Members
                        .Where(x => x is MethodDeclarationSyntax)
                        .Select(x => context.SemanticModel.GetDeclaredSymbol(x) as IMethodSymbol)
                        .Any(x => x.Name == "Trigger"
                                && x.Parameters.Length == 1
                                && x.Parameters[0].Type.ToDisplayString() == "Esiur.Resource.ResourceTrigger");

                    var fields = cds.Members.Where(x => x is FieldDeclarationSyntax)
                                            .Select(x => context.SemanticModel.GetDeclaredSymbol((x as FieldDeclarationSyntax).Declaration.Variables.First()) as IFieldSymbol)
                                            .Where(x => x.GetAttributes().Any(a => a.AttributeClass.ToDisplayString() == "Esiur.Resource.PublicAttribute"))
                                            .ToArray();


                    // get fields
                    Classes.Add(new GenerationInfo()
                    {
                        Name = cls.Name,
                        ClassDeclaration = cds,
                        ClassSymbol = cls,
                        Fields = fields,
                        ImplementInterface = cls.Interfaces.Any(x => x.ToDisplayString() == "Esiur.Resource.IResource"),
                        ImplementTrigger = hasTrigger
                    });

                    return;
                }
            }
        }
    }

}
