using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace Esiur.Proxy
{
    [Generator]
    public class ResourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // Register receiver
            context.RegisterForSyntaxNotifications(() => new ResourceGeneratorReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {

            if (!(context.SyntaxContextReceiver is ResourceGeneratorReceiver receiver))
                return;

//#if DEBUG
//            if (!Debugger.IsAttached)
//            {
//                Debugger.Launch();
//            }
//#endif

            //var toImplement = receiver.Classes.Where(x => x.Fields.Length > 0);

            foreach (var ci in receiver.Classes)
            {
                var code = @$"using Esiur.Resource; 
using Esiur.Core; 
namespace { ci.ClassSymbol.ContainingNamespace.ToDisplayString() } {{
";

                if (ci.ImplementInterface)
                    code += $"public partial class {ci.Name} {{";
                else
                {
                    code += @$"public partial class {ci.Name} : IResource {{
public Instance Instance {{ get; set; }}
public event DestroyedEvent OnDestroy;
public virtual void Destroy() {{ OnDestroy?.Invoke(this); }}
";

                    if (!ci.ImplementTrigger)
                        code += "public AsyncReply<bool> Trigger(ResourceTrigger trigger) => new AsyncReply<bool>(true);";
                }

                foreach (var f in ci.Fields)
                {
                    var fn = f.Name;
                    var pn = fn.Substring(0, 1).ToUpper() + fn.Substring(1);
                    code += $@"[Public] public {f.Type} {pn} {{ get => {fn}; set {{ {fn} = value; Instance.Modified(); }} }}";
                }

                code += "}}";

                //System.IO.File.WriteAllText("C:\\www\\class.cs", code);
                context.AddSource(ci.Name + "_esiur.cs", code);
            }
        }
    }
}
