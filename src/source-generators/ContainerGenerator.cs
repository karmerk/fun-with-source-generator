﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Linq;
using System.Text;

[Generator]
public class ContainerGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        // Find the main method
        var mainMethod = context.Compilation.GetEntryPoint(context.CancellationToken);

        var attributeName = "ServiceAttribute`1";

        var attributeSymbol = context.Compilation.GetTypeByMetadataName(attributeName);

        var classWithAttributes = context.Compilation.SyntaxTrees.Where(st => st.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Any(p => p.DescendantNodes().OfType<AttributeSyntax>().Any()));
        
        var variables = new StringBuilder();
        var getServices = new StringBuilder();

        var create = new StringBuilder();

        foreach (SyntaxTree tree in classWithAttributes)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);

            foreach (var declaredClass in tree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(cd => cd.DescendantNodes().OfType<AttributeSyntax>().Any()))
            {
                var nodes = declaredClass
                .DescendantNodes()
                .OfType<AttributeSyntax>()
                .FirstOrDefault(a => a.DescendantTokens().Any(dt => dt.IsKind(SyntaxKind.IdentifierToken) && semanticModel.GetTypeInfo(dt.Parent).Type.Name == attributeSymbol.Name))
                ?.DescendantTokens()
                ?.Where(dt => dt.IsKind(SyntaxKind.IdentifierToken))
                ?.ToList();

                if (nodes?.Count != 2)
                {
                    continue;
                }

                var attribute = nodes.FirstOrDefault(x => x.Text == attributeSymbol.Name);
                
                var serviceContract = (attribute.Parent as GenericNameSyntax)?.TypeArgumentList.Arguments.FirstOrDefault();
                //var serviceClass = nodes.FirstOrDefault(x => x != attribute).Parent;
                var serviceClass = nodes.FirstOrDefault(x => x != attribute).Parent;




                var typeInfo = semanticModel.GetTypeInfo(serviceClass);
                var members = typeInfo.Type.GetMembers();
                var ctor = members.FirstOrDefault(x => x.Name == ".ctor") as IMethodSymbol;



                // only add it as variable if its a singleton or scoped variable?
                variables.AppendLine($"private {serviceContract}? _{serviceClass} = null;");

                // add the get the method
                getServices.AppendLine($@"if (serviceType == typeof({serviceContract}))
{{
    return Create{serviceContract}();
}}");

                // Add the Create method for the contract
                create.AppendLine($@"private {serviceContract} Create{serviceContract}()
{{");

                if (ctor.Parameters.Any())
                {
                    // Create each parameter/dependency using there own create method
                    foreach (var parameter in ctor.Parameters)
                    {
                        create.AppendLine($@"var {parameter.Name} = Create{parameter.Type.Name}();");
                    }

                    // return new insatnce og the serviceClass
                    var parameters = string.Join(", ", ctor.Parameters.Select(x => x.Name));
                    create.AppendLine($@"return new {serviceClass}({parameters});");
                }
                else
                {
                    // If no parameters then just create object
                    create.AppendLine($@"return new {serviceClass}();");
                }

                create.AppendLine($@"}}");
            }


        }


        // Build up the source code
        string source = $@"// <auto-generated/>
using System;

public partial class Container
{{
    static Container()
    {{
        Instance = new ContainerImpl();
    }}

    private class ContainerImpl : IContainer
    {{
        {variables.ToString()}
    

    public object? GetService(Type serviceType)
    {{
        {getServices.ToString()}

        return null;
    }}

    {create.ToString()}
    
}}
}}
";
        var typeName = "Container";

        // Add the source code to the compilation
        context.AddSource($"{typeName}.g.cs", source);
    }

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        if (!Debugger.IsAttached)
        {
            Debugger.Launch();
        }
#endif 
        // No initialization required for this one
    }
}

