using Devlooped;

namespace Jsontron;

/// <summary>
/// <c>let</c> binding with a pre-parsed jq expression.
/// </summary>
sealed class CompiledLet(string name, JqExpression expression)
{
    public string Name { get; } = name;
    public JqExpression Expression { get; } = expression;
}
