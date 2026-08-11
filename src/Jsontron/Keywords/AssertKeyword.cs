using System.Text.Json;
using Json.Schema;

namespace Jsontron.Keywords;

/// <summary>
/// Handles the <c>assert</c> keyword (sugar for a rule with <c>context: "."</c>).
/// </summary>
public sealed class AssertKeyword : IKeywordHandler
{
    public static AssertKeyword Instance { get; } = new();

    public string Name => "assert";

    AssertKeyword()
    {
    }

    public object? ValidateKeywordValue(JsonElement value)
        => RuleSetCompiler.ParseAssert(value);

    public void BuildSubschemas(KeywordData keyword, BuildContext context)
    {
        // When sibling "rules" is present, merge/evaluation is owned by RulesKeyword.
        if (context.LocalSchema.TryGetProperty("rules", out _))
        {
            keyword.Value = CompiledRuleSet.Delegated;
            return;
        }

        var asserts = (IReadOnlyList<AssertSpec>)keyword.Value!;
        keyword.Value = RuleSetCompiler.CompileAssertOnly(asserts);
    }

    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
    {
        var ruleSet = (CompiledRuleSet)keyword.Value!;
        return JqEvaluation.Evaluate(Name, ruleSet, context);
    }
}
