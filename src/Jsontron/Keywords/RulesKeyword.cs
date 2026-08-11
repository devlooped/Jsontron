using System.Text.Json;
using Json.Schema;

namespace Jsontron.Keywords;

/// <summary>
/// Handles the <c>rules</c> keyword (array of Schematron-like context/assert rules).
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

        // Merge sibling assert sugar into a single context="." rule.
        if (context.LocalSchema.TryGetProperty("assert", out var assertEl))
        {
            var asserts = RuleSetCompiler.ParseAssert(assertEl);
            rules = RuleSetCompiler.MergeRootAsserts(rules, asserts);
        }

        keyword.Value = RuleSetCompiler.Compile(rules);
    }

    public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
    {
        var ruleSet = (CompiledRuleSet)keyword.Value!;
        return JqEvaluation.Evaluate(Name, ruleSet, context);
    }
}
