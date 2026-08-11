using Devlooped;

namespace Jsontron;

/// <summary>
/// Assert with pre-parsed jq expressions for the test and message.
/// </summary>
sealed class CompiledAssert(JqExpression test, JqExpression message)
{
    public JqExpression Test { get; } = test;
    public JqExpression Message { get; } = message;
}
