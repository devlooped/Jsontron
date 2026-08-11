namespace Jsontron;

/// <summary>
/// Compiled rules ready for evaluation, or a marker that evaluation is delegated to <c>rules</c>.
/// </summary>
sealed class CompiledRuleSet
{
    /// <summary>
    /// When present as a sibling of <c>assert</c>, the assert keyword ignores evaluation.
    /// </summary>
    public static CompiledRuleSet Delegated { get; } = new([]);

    public CompiledRuleSet(IReadOnlyList<CompiledRule> rules) => Rules = rules;

    public IReadOnlyList<CompiledRule> Rules { get; }

    public bool IsDelegated => ReferenceEquals(this, Delegated);
}
