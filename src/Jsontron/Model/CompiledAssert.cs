using Devlooped;

namespace Jsontron;

/// <summary>
/// Assert with pre-parsed jq expressions for the test and message.
/// </summary>
/// <remarks>
/// When <paramref name="negate"/> is <see langword="true"/>, this represents a
/// <c>report</c>: it fails when the test IS jq-truthy (Schematron report semantics).
/// Otherwise it fails when the test is NOT truthy (Schematron assert semantics).
/// </remarks>
sealed class CompiledAssert(
    JqExpression test,
    JqExpression message,
    bool negate = false,
    string? id = null,
    string? flag = null)
{
    public JqExpression Test { get; } = test;
    public JqExpression Message { get; } = message;
    public bool Negate { get; } = negate;
    public string? Id { get; } = id;
    public string? Flag { get; } = flag;
}
