using System.Text.Json;
using Json.Schema;

namespace Jsontron.Keywords;

/// <summary>
/// Handles the <c>report</c> keyword — sugar for a rule with <c>context: "."</c> whose asserts
/// FAIL when the test is jq-truthy (opposite polarity of <c>assert</c>, mirroring Schematron
/// <c>report</c>).
/// </summary>
public sealed class ReportKeyword : IKeywordHandler
{
    public static ReportKeyword Instance { get; } = new();

    public string Name => "report";

    ReportKeyword()
    {
    }

    public object? ValidateKeywordValue(JsonElement value)
        => RuleSetCompiler.ParseReport(value);

    public void BuildSubschemas(KeywordData keyword, BuildContext context)
    {
        // When sibling "rules" or "assert" is present, evaluation is owned by that keyword.
        if (context.LocalSchema.TryGetProperty("rules", out _) ||
            context.LocalSchema.TryGetProperty("assert", out _))
        {
            keyword.Value = CompiledRuleSet.Delegated;
            return;
        }

        var reports = (IReadOnlyList<AssertSpec>)keyword.Value!;

        IReadOnlyList<LetSpec> lets = [];
        if (context.LocalSchema.TryGetProperty("let", out var letEl))
            lets = RuleSetCompiler.ParseLet(letEl);

        keyword.Value = RuleSetCompiler.CompileSugarOnly([], reports, lets);
    }

    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
    {
        var ruleSet = (CompiledRuleSet)keyword.Value!;
        return JqEvaluation.Evaluate(Name, ruleSet, context);
    }
}
