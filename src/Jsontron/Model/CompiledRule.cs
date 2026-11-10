using Devlooped;

namespace Jsontron;

/// <summary>
/// Rule with a pre-parsed context expression, compiled let bindings, and compiled asserts
/// (which may include reports, distinguished by <see cref="CompiledAssert.Negate"/>).
/// </summary>
sealed class CompiledRule(
    JqExpression context,
    IReadOnlyList<CompiledLet> lets,
    IReadOnlyList<CompiledAssert> asserts)
{
    public JqExpression Context { get; } = context;
    public IReadOnlyList<CompiledLet> Lets { get; } = lets;
    public IReadOnlyList<CompiledAssert> Asserts { get; } = asserts;
}
