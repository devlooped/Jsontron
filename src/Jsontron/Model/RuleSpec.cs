namespace Jsontron;

/// <summary>
/// Uncompiled rule specification.
/// </summary>
readonly record struct RuleSpec(
    string Context,
    IReadOnlyList<AssertSpec> Asserts,
    IReadOnlyList<AssertSpec> Reports,
    IReadOnlyList<LetSpec> Lets)
{
    public RuleSpec(string context, IReadOnlyList<AssertSpec> asserts)
        : this(context, asserts, [], [])
    {
    }
}
