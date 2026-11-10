namespace Jsontron;

/// <summary>
/// Compiled rules ready for evaluation, or a marker that evaluation is delegated to another keyword.
/// </summary>
sealed class CompiledRuleSet
{
    /// <summary>
    /// Marker used by a keyword whose evaluation is owned by a sibling keyword.
    /// </summary>
    public static CompiledRuleSet Delegated { get; } = new([], []);

    public CompiledRuleSet(IReadOnlyList<CompiledRule> rules, IReadOnlyList<CompiledLet> lets)
    {
        Rules = rules;
        Lets = lets;
    }

    public CompiledRuleSet(IReadOnlyList<CompiledRule> rules) : this(rules, [])
    {
    }

    public IReadOnlyList<CompiledRule> Rules { get; }

    /// <summary>
    /// Schema-level <c>let</c> bindings evaluated once with <c>.</c> at this schema location
    /// before any rule runs; in scope for every rule's context, lets, tests, and messages.
    /// </summary>
    public IReadOnlyList<CompiledLet> Lets { get; }

    public bool IsDelegated => ReferenceEquals(this, Delegated);
}
