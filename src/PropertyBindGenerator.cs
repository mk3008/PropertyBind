using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PropertyBind;

[Generator(LanguageNames.CSharp)]
public partial class PropertyBindGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(EmitAttributes);

		var source = context.SyntaxProvider.ForAttributeWithMetadataName(
				 "PropertyBind.GeneratePropertyBindAttribute",
				 static (node, token) => node is ClassDeclarationSyntax,
				 static (context, token) => context);

		context.RegisterSourceOutput(source, Emit);
	}

	static void EmitAttributes(IncrementalGeneratorPostInitializationContext context)
	{
		context.AddSource("GeneratePropertyBindAttribute.cs", """
namespace PropertyBind
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class GeneratePropertyBindAttribute : Attribute
    {
        public string ObservableCollectionPropertyName { get; } 
        public string BindPropertyName { get; } 
        public GeneratePropertyBindAttribute(string observableCollectionPropertyName, string bindPropertyName)
        {
            this.ObservableCollectionPropertyName = observableCollectionPropertyName;
			this.BindPropertyName = bindPropertyName;
        }
    }
}
""");
	}

	static void Emit(SourceProductionContext context, GeneratorAttributeSyntaxContext source)
	{
		var attr = source.Attributes[0]; // allowMultiple:false
		ExtractAttribute(attr, out var collectionProperyName, out var bindPropertyName);
		if (string.IsNullOrEmpty(collectionProperyName)) return;
		if (string.IsNullOrEmpty(bindPropertyName)) return;

		ExtractGenericType(source, collectionProperyName, out var genericType);
		if (genericType == null) return;

		// Generate Code
		var code = GenerateCode(
			source.TargetSymbol.Name,
			collectionProperyName,
			genericType.Name,
			bindPropertyName
		);
		AddSource(context, source.TargetSymbol, code);
	}

	static void ExtractGenericType(GeneratorAttributeSyntaxContext source, string collectionProperyName, out ITypeSymbol? symbol)
	{
		symbol = null;
		var property = ((INamedTypeSymbol)source.TargetSymbol).GetMembers().Where(x => x.Name == collectionProperyName).FirstOrDefault();
		if (property is null) return;
		if (property is IPropertySymbol p && p.Type is INamedTypeSymbol n && n.IsGenericType)
		{
			symbol = n.TypeArguments[0];
		}
	}

	static void ExtractAttribute(AttributeData attributeData, out string observableCollectionPropertyName, out string bindPropertyName)
	{
		// Extract attribute parameter
		// ParentPropertyBindAttribute(
		//     string observableCollectionPropertyName,
		//     string bindPropertyName
		// )

		observableCollectionPropertyName = (string)attributeData.ConstructorArguments[0].Value!;
		bindPropertyName = (string)attributeData.ConstructorArguments[1].Value!;
	}

	static string GenerateCode(string className, string collectionProperyName, string genericTypeName, string bindPropertyName)
	{
		var code = new StringBuilder();
		code.AppendLine($$"""
public partial class {{className}}
{
	public {{className}}()
	{
		{{collectionProperyName}}.CollectionChanged += {{collectionProperyName}}_CollectionChanged;
	}

	private void {{collectionProperyName}}_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			if (e.NewItems == null) return;
			foreach ({{genericTypeName}} item in e.NewItems)
			{
				item.{{bindPropertyName}} = this;
			}
		}
	}
}
""");
		return code.ToString();
	}

	static void AddSource(SourceProductionContext context, ISymbol targetSymbol, string code, string fileExtension = ".g.cs")
	{
		var fullType = targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
		  .Replace("global::", "")
		  .Replace("<", "_")
		  .Replace(">", "_");

		var sb = new StringBuilder();

		sb.AppendLine("""
using System.Collections.Specialized;
""");

		var ns = targetSymbol.ContainingNamespace;
		if (!ns.IsGlobalNamespace)
		{
			sb.AppendLine($"namespace {ns} {{");
		}
		sb.AppendLine();

		sb.AppendLine(code);

		if (!ns.IsGlobalNamespace)
		{
			sb.AppendLine($"}}");
		}

		var sourceCode = sb.ToString();
		context.AddSource($"{fullType}{fileExtension}", sourceCode);
	}
}
