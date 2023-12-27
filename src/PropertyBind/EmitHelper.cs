using Microsoft.CodeAnalysis;

namespace PropertyBind;

internal static class EmitHelper
{
	public static string ForEachLine<T>(string indent, IEnumerable<T> values, Func<T, string> lineSelector)
	{
		return string.Join(Environment.NewLine, values.Select(x => indent + lineSelector(x)));
	}
}
