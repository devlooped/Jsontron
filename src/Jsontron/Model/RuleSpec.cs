namespace Jsontron;

/// <summary>
/// Uncompiled rule specification.
/// </summary>
readonly record struct RuleSpec(string Context, IReadOnlyList<AssertSpec> Asserts);
