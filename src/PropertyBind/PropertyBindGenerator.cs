using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using static PropertyBind.EmitHelper;

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
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
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
		var code = new StringBuilder();
		var collectionPropertyNames = new List<string>();

		foreach (var attr in source.Attributes)
		{

			ExtractAttribute(attr, out var collectionProperyName, out var bindPropertyName);
			if (string.IsNullOrEmpty(collectionProperyName)) continue;
			if (string.IsNullOrEmpty(bindPropertyName)) continue;

			if (TryExtract(source, collectionProperyName, out var propType, out var genericType) == false) continue;

			collectionPropertyNames.Add(collectionProperyName);

			// Generate Code
			var text = GenerateInitializeCode(
							source.TargetSymbol.Name,
							collectionProperyName,
							propType,
							genericType,
							bindPropertyName
						);
			code.AppendLine(text);
		}

		code.Append(GenerateConstructorCode(source.TargetSymbol.Name, collectionPropertyNames));

		AddSource(context, source.TargetSymbol, code.ToString());
	}

	static bool TryExtract(GeneratorAttributeSyntaxContext source, string collectionProperyName, out INamedTypeSymbol propertyType, out ITypeSymbol genericType)
	{
		propertyType = null!;
		genericType = null!;
		var property = ((INamedTypeSymbol)source.TargetSymbol).GetMembers().Where(x => x.Name == collectionProperyName).FirstOrDefault();
		if (property is null) return false;
		if (property is IPropertySymbol p && p.Type is INamedTypeSymbol n && n.IsGenericType)
		{
			propertyType = n;
			genericType = n.TypeArguments[0];
			return true;
		}
		return false;
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

	static string GenerateInitializeCode(string className, string collectionProperyName, ITypeSymbol collectionType, ITypeSymbol genericType, string bindPropertyName)
	{
		var genericTypeFullName = genericType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		// If it is an interface, initialize it with ObservableCollection<T>.
		var collectionTypeFullName = collectionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		if (collectionType.TypeKind == TypeKind.Interface) collectionTypeFullName = $"ObservableCollection<{genericTypeFullName}>";

		var code = new StringBuilder();
		code.AppendLine($$"""
	public partial class {{className}}
	{
		private {{collectionTypeFullName}} __Create{{collectionProperyName}}()
		{
			var lst = new {{collectionTypeFullName}}();
			lst.CollectionChanged += __{{collectionProperyName}}_CollectionChanged;
			return lst;		
		}

		private void __{{collectionProperyName}}_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				if (e.NewItems == null) return;
				foreach ({{genericTypeFullName}} item in e.NewItems)
				{
					item.{{bindPropertyName}} = this;
				}
			}
		}
	}
""");
		return code.ToString();
	}

	static string GenerateConstructorCode(string className, List<string> collectionProperyNames)
	{
		var code = new StringBuilder();
		code.AppendLine($$"""
	public partial class {{className}}
	{
		public {{className}}()
		{
{{ForEachLine("            ", collectionProperyNames, (x) => $"{x} = __Create{x}();")}}
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
#nullable enable

using System.Collections.ObjectModel;
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
