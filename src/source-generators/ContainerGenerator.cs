using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

[Generator]
public class ContainerGenerator : ISourceGenerator
{

    private class FindClassesWithAttributeVisitor : CSharpSyntaxRewriter
    {
        private readonly INamedTypeSymbol _attributeSyntax;
        private List<ClassDeclarationSyntax> _classes = new List<ClassDeclarationSyntax>();
        public IEnumerable<ClassDeclarationSyntax> Classes => _classes;

        public FindClassesWithAttributeVisitor(INamedTypeSymbol attributeSyntax)
        {
            _attributeSyntax = attributeSyntax;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

            var any = node.AttributeLists
                .SelectMany(x => x.Attributes)
                .Select(x => x.Name)
                .OfType<GenericNameSyntax>()
                .Any(x => x.Identifier.Text == _attributeSyntax.Name);
                if (any)
            {
                _classes.Add(node);
            }
            //if (node.AttributeLists.Any(x=> x == _attributeSyntax))
            //{
            //    _classes.Add(node);
            //}
            string className = node.Identifier.ValueText;
            

            return node;
        }
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Find the main method
        var mainMethod = context.Compilation.GetEntryPoint(context.CancellationToken);

        var attributeName = "ServiceAttribute`1";
        INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName(attributeName);

        var visitor = new FindClassesWithAttributeVisitor(attributeSymbol);

        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            visitor.Visit(tree.GetRoot());
        }

        var classes = visitor.Classes.ToArray();


        var variables = new StringBuilder();
        var getServices = new StringBuilder();
        var create = new StringBuilder();

        foreach (var @class in classes) 
        {
            var semanticModel = context.Compilation.GetSemanticModel(@class.SyntaxTree);
            

            var nodes = @class.DescendantNodes();

            var attributes = @class.AttributeLists.SelectMany(x => x.Attributes).ToArray();
            var attribute = attributes.FirstOrDefault();
            var serviceContract = (attribute.Name as GenericNameSyntax)?.TypeArgumentList.Arguments.FirstOrDefault();
            // What about multiple contracts for the same implementation ?


            var serviceClass = @class.Identifier;

            var ctor = @class.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            var dependencies = ctor?.ParameterList.Parameters.ToArray() ?? Enumerable.Empty<ParameterSyntax>();
            
            // only add it as variable if its a singleton or scoped variable?
            variables.AppendLine($"private {serviceContract}? _{serviceContract} = null;");

            // add the get the method
            getServices.AppendLine($@"if (serviceType == typeof({serviceContract}))
{{
    return Create{serviceContract}();
}}");

            // Add the Create method for the contract
            create.AppendLine($@"
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private {serviceContract} Create{serviceContract}()
{{");

            if (dependencies.Any())
            {
                // Create each parameter/dependency using there own create method
                foreach (var parameter in dependencies)
                {
                    var parameterType = ((IdentifierNameSyntax)parameter.Type);
                    create.AppendLine($@"var {parameter.Identifier.Text} = Create{parameterType.Identifier.Text}();");
                }

                // return new insatnce og the serviceClass
                var parameters = string.Join(", ", dependencies.Select(x => x.Identifier.Text));
                create.AppendLine($@"return new {serviceClass}({parameters});");
            }
            else
            {
                // If no parameters then just create object
                create.AppendLine($@"return new {serviceClass}();");
            }

            create.AppendLine($@"}}");

            //var typeInfo = semanticModel.GetTypeInfo(serviceClass.Parent);
            //var members = typeInfo.Type.GetMembers();
            //var ctor = members.FirstOrDefault(x => x.Name == ".ctor") as IMethodSymbol;


            //var attribute = nodes.FirstOrDefault(x => x.Text == attributeSymbol.Name);

            //var serviceContract = (attribute.Parent as GenericNameSyntax)?.TypeArgumentList.Arguments.FirstOrDefault();
        }
        

        


        // Build up the source code
        string source = $@"// <auto-generated/>
using System;
using System.Runtime.CompilerServices;

#nullable enable

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

            if (serviceType == typeof(IServiceProvider))
            {{
                return this;
            }}

            if (serviceType == typeof(IContainer))
            {{
                return this;
            }}

            return null;
        }}

        {create.ToString()}
    
    }}
}}";
        

        // Lets try to pretty print the code
        var indent = 0;
        var rebuilder = new StringBuilder();
        foreach(var line in source.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("}"))
            {
                indent--;
            }

            for (int i = 0; i < indent; i++) 
            {
                rebuilder.Append("    ");
            }

            rebuilder.AppendLine(trimmed);

            if (trimmed.StartsWith("{"))
            {
                indent++;
            }
        }

        source = rebuilder.ToString();
        
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

