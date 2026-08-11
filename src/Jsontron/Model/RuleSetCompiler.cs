using System.Text.Json;
using Json.Schema;

namespace Jsontron;

/// <summary>
/// Parses <c>rules</c> / <c>assert</c> keyword values and compiles them into cached jq expressions.
/// </summary>
static class RuleSetCompiler
{
    public const string RootContext = ".";

    public static IReadOnlyList<AssertSpec> ParseAssert(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => [ParseAssertItem(value)],
            JsonValueKind.Object => [ParseAssertItem(value)],
            JsonValueKind.Array => ParseAssertArray(value),
            _ => throw new JsonSchemaException(
                $"'assert' value must be a string, object, or array, found {value.ValueKind}.")
        };
    }

    static IReadOnlyList<AssertSpec> ParseAssertArray(JsonElement array)
    {
        List<AssertSpec> asserts = [];
        foreach (var item in array.EnumerateArray())
            asserts.Add(ParseAssertItem(item));

        if (asserts.Count == 0)
            throw new JsonSchemaException("'assert' array must not be empty.");

        return asserts;
    }

    static AssertSpec ParseAssertItem(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.String)
        {
            var expr = value.GetString()
                ?? throw new JsonSchemaException("'assert' string must not be null.");
            if (string.IsNullOrWhiteSpace(expr))
                throw new JsonSchemaException("'assert' string must not be empty.");

            // Shorthand: test and message source are the same expression text.
            return new AssertSpec(expr, expr);
        }

        if (value.ValueKind is not JsonValueKind.Object)
            throw new JsonSchemaException(
                $"'assert' items must be strings or objects, found {value.ValueKind}.");

        if (!value.TryGetProperty("test", out var testEl) || testEl.ValueKind is not JsonValueKind.String)
            throw new JsonSchemaException("'assert' object requires a string 'test' property.");

        var test = testEl.GetString();
        if (string.IsNullOrWhiteSpace(test))
            throw new JsonSchemaException("'assert' test must not be empty.");

        string message;
        if (value.TryGetProperty("message", out var messageEl))
        {
            if (messageEl.ValueKind is not JsonValueKind.String)
                throw new JsonSchemaException("'assert' message must be a string.");

            message = messageEl.GetString()
                ?? throw new JsonSchemaException("'assert' message must not be null.");
        }
        else
        {
            message = test;
        }

        return new AssertSpec(test, message);
    }

    public static IReadOnlyList<RuleSpec> ParseRules(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Array)
            throw new JsonSchemaException($"'rules' value must be an array, found {value.ValueKind}.");

        List<RuleSpec> rules = [];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            rules.Add(ParseRule(item, index));
            index++;
        }

        if (rules.Count == 0)
            throw new JsonSchemaException("'rules' array must not be empty.");

        return rules;
    }

    static RuleSpec ParseRule(JsonElement value, int index)
    {
        if (value.ValueKind is not JsonValueKind.Object)
            throw new JsonSchemaException($"'rules[{index}]' must be an object, found {value.ValueKind}.");

        if (!value.TryGetProperty("context", out var contextEl) || contextEl.ValueKind is not JsonValueKind.String)
            throw new JsonSchemaException($"'rules[{index}]' requires a string 'context' property.");

        var context = contextEl.GetString();
        if (string.IsNullOrWhiteSpace(context))
            throw new JsonSchemaException($"'rules[{index}].context' must not be empty.");

        if (!value.TryGetProperty("asserts", out var assertsEl) || assertsEl.ValueKind is not JsonValueKind.Array)
            throw new JsonSchemaException($"'rules[{index}]' requires an 'asserts' array.");

        List<AssertSpec> asserts = [];
        var assertIndex = 0;
        foreach (var assertEl in assertsEl.EnumerateArray())
        {
            if (assertEl.ValueKind is not JsonValueKind.Object)
                throw new JsonSchemaException(
                    $"'rules[{index}].asserts[{assertIndex}]' must be an object, found {assertEl.ValueKind}.");

            if (!assertEl.TryGetProperty("test", out var testEl) || testEl.ValueKind is not JsonValueKind.String)
                throw new JsonSchemaException(
                    $"'rules[{index}].asserts[{assertIndex}]' requires a string 'test' property.");

            var test = testEl.GetString();
            if (string.IsNullOrWhiteSpace(test))
                throw new JsonSchemaException(
                    $"'rules[{index}].asserts[{assertIndex}].test' must not be empty.");

            string message;
            if (assertEl.TryGetProperty("message", out var messageEl))
            {
                if (messageEl.ValueKind is not JsonValueKind.String)
                    throw new JsonSchemaException(
                        $"'rules[{index}].asserts[{assertIndex}].message' must be a string.");

                message = messageEl.GetString()
                    ?? throw new JsonSchemaException(
                        $"'rules[{index}].asserts[{assertIndex}].message' must not be null.");
            }
            else
            {
                message = test;
            }

            asserts.Add(new AssertSpec(test, message));
            assertIndex++;
        }

        if (asserts.Count == 0)
            throw new JsonSchemaException($"'rules[{index}].asserts' must not be empty.");

        return new RuleSpec(context, asserts);
    }

    /// <summary>
    /// Appends sugar asserts into a single <c>context: "."</c> rule (creating it if needed).
    /// </summary>
    public static IReadOnlyList<RuleSpec> MergeRootAsserts(
        IReadOnlyList<RuleSpec> rules,
        IReadOnlyList<AssertSpec> asserts)
    {
        if (asserts.Count == 0)
            return rules;

        List<RuleSpec> merged = [.. rules];
        var rootIndex = -1;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Context == RootContext)
            {
                rootIndex = i;
                break;
            }
        }

        if (rootIndex >= 0)
        {
            var existing = merged[rootIndex];
            List<AssertSpec> combined = [.. existing.Asserts, .. asserts];
            merged[rootIndex] = existing with { Asserts = combined };
        }
        else
        {
            merged.Add(new RuleSpec(RootContext, asserts));
        }

        return merged;
    }

    public static CompiledRuleSet Compile(IReadOnlyList<RuleSpec> rules)
    {
        List<CompiledRule> compiled = new(rules.Count);
        foreach (var rule in rules)
        {
            var context = JqEvaluation.ParseExpression(rule.Context, "context");
            List<CompiledAssert> asserts = new(rule.Asserts.Count);
            foreach (var assert in rule.Asserts)
            {
                var test = JqEvaluation.ParseExpression(assert.Test, "test");
                var message = JqEvaluation.ParseMessage(assert.Message);
                asserts.Add(new CompiledAssert(test, message));
            }

            compiled.Add(new CompiledRule(context, asserts));
        }

        return new CompiledRuleSet(compiled);
    }

    public static CompiledRuleSet CompileAssertOnly(IReadOnlyList<AssertSpec> asserts)
        => Compile([new RuleSpec(RootContext, asserts)]);
}
