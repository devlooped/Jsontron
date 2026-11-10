using System.Text.Json;
using Json.Schema;

namespace Jsontron.Keywords;

/// <summary>
/// Handles the <c>rules</c> keyword (array of Schematron-like context / assert / report rules).
/// </summary>
public sealed class RulesKeyword : IKeywordHandler
{
    public static RulesKeyword Instance { get; } = new();

    public string Name => "rules";

    RulesKeyword()
    {
    }

    public object? ValidateKeywordValue(JsonElement value)
        => RuleSetCompiler.ParseRules(value);

    public void BuildSubschemas(KeywordData keyword, BuildContext context)
    {
        var rules = (IReadOnlyList<RuleSpec>)keyword.Value!;

        // Merge sibling assert / report sugar into a single context="." rule.
        IReadOnlyList<AssertSpec> asserts = [];
        if (context.LocalSchema.TryGetProperty("assert", out var assertEl))
            asserts = RuleSetCompiler.ParseAssert(assertEl);

        IReadOnlyList<AssertSpec> reports = [];
        if (context.LocalSchema.TryGetProperty("report", out var reportEl))
            reports = RuleSetCompiler.ParseReport(reportEl);

        if (asserts.Count > 0 || reports.Count > 0)
            rules = RuleSetCompiler.MergeRootSugar(rules, asserts, reports);

        IReadOnlyList<LetSpec> lets = [];
        if (context.LocalSchema.TryGetProperty("let", out var letEl))
            lets = RuleSetCompiler.ParseLet(letEl);

        keyword.Value = RuleSetCompiler.Compile(rules, lets);
    }

    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
    {
        var ruleSet = (CompiledRuleSet)keyword.Value!;
        return JqEvaluation.Evaluate(Name, ruleSet, context);
    }
}
