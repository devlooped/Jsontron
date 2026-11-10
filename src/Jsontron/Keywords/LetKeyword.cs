using System.Text.Json;
using Json.Schema;

namespace Jsontron.Keywords;

/// <summary>
/// Handles the schema-level <c>let</c> keyword. Bindings have no evaluation of their own;
/// sibling <c>rules</c> / <c>assert</c> / <c>report</c> read the <c>let</c> value at build time
/// and include the bindings in the compiled rule set.
/// </summary>
public sealed class LetKeyword : IKeywordHandler
{
    public static LetKeyword Instance { get; } = new();

    public string Name => "let";

    LetKeyword()
    {
    }

    public object? ValidateKeywordValue(JsonElement value)
        => RuleSetCompiler.ParseLet(value);

    public void BuildSubschemas(KeywordData keyword, BuildContext context)
    {
        // Compile the bindings so bad jq is caught at build time, then mark as delegated:
        // actual binding happens inside the sibling keyword that owns evaluation.
        var lets = (IReadOnlyList<LetSpec>)keyword.Value!;
        _ = RuleSetCompiler.CompileLets(lets);
        keyword.Value = CompiledRuleSet.Delegated;
    }

    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
        => KeywordEvaluation.Ignore;
}
