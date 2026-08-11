using Devlooped;

namespace Jsontron;

/// <summary>
/// Rule with a pre-parsed context expression and compiled asserts.
/// </summary>
sealed class CompiledRule(JqExpression context, IReadOnlyList<CompiledAssert> asserts)
{
    public JqExpression Context { get; } = context;
    public IReadOnlyList<CompiledAssert> Asserts { get; } = asserts;
}
