using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;

namespace Jsontron;

/// <summary>
/// Parses <c>rules</c> / <c>assert</c> / <c>report</c> / <c>let</c> keyword values and compiles
/// them into cached jq expressions.
/// </summary>
static class RuleSetCompiler
{
    public const string RootContext = ".";

    const string ReservedRoot = "root";

    static readonly Regex IdentifierPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static IReadOnlyList<AssertSpec> ParseAssert(JsonElement value)
        => ParseAssertOrReport(value, "assert");

    public static IReadOnlyList<AssertSpec> ParseReport(JsonElement value)
        => ParseAssertOrReport(value, "report");

    static IReadOnlyList<AssertSpec> ParseAssertOrReport(JsonElement value, string keyword)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => [ParseAssertItem(value, keyword)],
            JsonValueKind.Object => [ParseAssertItem(value, keyword)],
            JsonValueKind.Array => ParseAssertArray(value, keyword),
            _ => throw new JsonSchemaException(
                $"'{keyword}' value must be a string, object, or array, found {value.ValueKind}.")
        };
    }

    static IReadOnlyList<AssertSpec> ParseAssertArray(JsonElement array, string keyword)
    {
        List<AssertSpec> items = [];
        foreach (var item in array.EnumerateArray())
            items.Add(ParseAssertItem(item, keyword));

        if (items.Count == 0)
            throw new JsonSchemaException($"'{keyword}' array must not be empty.");

        return items;
    }

    static AssertSpec ParseAssertItem(JsonElement value, string keyword)
    {
        if (value.ValueKind is JsonValueKind.String)
        {
            var expr = value.GetString()
                ?? throw new JsonSchemaException($"'{keyword}' string must not be null.");
            if (string.IsNullOrWhiteSpace(expr))
                throw new JsonSchemaException($"'{keyword}' string must not be empty.");

            // Shorthand: test and message source are the same expression text.
            return new AssertSpec(expr, expr);
        }

        if (value.ValueKind is not JsonValueKind.Object)
            throw new JsonSchemaException(
                $"'{keyword}' items must be strings or objects, found {value.ValueKind}.");

        if (!value.TryGetProperty("test", out var testEl) || testEl.ValueKind is not JsonValueKind.String)
            throw new JsonSchemaException($"'{keyword}' object requires a string 'test' property.");

        var test = testEl.GetString();
        if (string.IsNullOrWhiteSpace(test))
            throw new JsonSchemaException($"'{keyword}' test must not be empty.");

        string message;
        if (value.TryGetProperty("message", out var messageEl))
        {
            if (messageEl.ValueKind is not JsonValueKind.String)
                throw new JsonSchemaException($"'{keyword}' message must be a string.");

            message = messageEl.GetString()
                ?? throw new JsonSchemaException($"'{keyword}' message must not be null.");
        }
        else
        {
            message = test;
        }

        return new AssertSpec(test, message);
    }

    /// <summary>
    /// Parses a <c>let</c> object into bindings in document order.
    /// </summary>
    public static IReadOnlyList<LetSpec> ParseLet(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Object)
            throw new JsonSchemaException($"'let' value must be an object, found {value.ValueKind}.");

        List<LetSpec> lets = [];
        HashSet<string> seen = [];
        foreach (var property in value.EnumerateObject())
        {
            var name = property.Name;
            ValidateLetName(name);

            if (!seen.Add(name))
                throw new JsonSchemaException($"'let.{name}' is defined more than once.");

            if (property.Value.ValueKind is not JsonValueKind.String)
                throw new JsonSchemaException($"'let.{name}' must be a string jq expression.");

            var expression = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(expression))
                throw new JsonSchemaException($"'let.{name}' must not be empty.");

            lets.Add(new LetSpec(name, expression));
        }

        if (lets.Count == 0)
            throw new JsonSchemaException("'let' must declare at least one binding.");

        return lets;
    }

    static void ValidateLetName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new JsonSchemaException("'let' binding name must not be empty.");

        if (string.Equals(name, ReservedRoot, StringComparison.Ordinal))
            throw new JsonSchemaException("'let' cannot bind the reserved name 'root' ($root is reserved).");

        if (!IdentifierPattern.IsMatch(name))
            throw new JsonSchemaException(
                $"'let' binding name '{name}' must match [A-Za-z_][A-Za-z0-9_]*.");
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

        var hasAsserts = value.TryGetProperty("asserts", out var assertsEl);
        var hasReports = value.TryGetProperty("reports", out var reportsEl);

        if (!hasAsserts && !hasReports)
            throw new JsonSchemaException(
                $"'rules[{index}]' requires an 'asserts' array, a 'reports' array, or both.");

        IReadOnlyList<AssertSpec> asserts = hasAsserts
            ? ParseRuleItems(assertsEl, index, "asserts")
            : [];

        IReadOnlyList<AssertSpec> reports = hasReports
            ? ParseRuleItems(reportsEl, index, "reports")
            : [];

        if (asserts.Count == 0 && reports.Count == 0)
            throw new JsonSchemaException(
                $"'rules[{index}]' must have at least one assert or report.");

        IReadOnlyList<LetSpec> lets = [];
        if (value.TryGetProperty("let", out var letEl))
            lets = ParseLet(letEl);

        return new RuleSpec(context, asserts, reports, lets);
    }

    static IReadOnlyList<AssertSpec> ParseRuleItems(JsonElement array, int ruleIndex, string name)
    {
        if (array.ValueKind is not JsonValueKind.Array)
            throw new JsonSchemaException(
                $"'rules[{ruleIndex}].{name}' must be an array, found {array.ValueKind}.");

        List<AssertSpec> items = [];
        var itemIndex = 0;
        foreach (var el in array.EnumerateArray())
        {
            if (el.ValueKind is not JsonValueKind.Object)
                throw new JsonSchemaException(
                    $"'rules[{ruleIndex}].{name}[{itemIndex}]' must be an object, found {el.ValueKind}.");

            if (!el.TryGetProperty("test", out var testEl) || testEl.ValueKind is not JsonValueKind.String)
                throw new JsonSchemaException(
                    $"'rules[{ruleIndex}].{name}[{itemIndex}]' requires a string 'test' property.");

            var test = testEl.GetString();
            if (string.IsNullOrWhiteSpace(test))
                throw new JsonSchemaException(
                    $"'rules[{ruleIndex}].{name}[{itemIndex}].test' must not be empty.");

            string message;
            if (el.TryGetProperty("message", out var messageEl))
            {
                if (messageEl.ValueKind is not JsonValueKind.String)
                    throw new JsonSchemaException(
                        $"'rules[{ruleIndex}].{name}[{itemIndex}].message' must be a string.");

                message = messageEl.GetString()
                    ?? throw new JsonSchemaException(
                        $"'rules[{ruleIndex}].{name}[{itemIndex}].message' must not be null.");
            }
            else
            {
                message = test;
            }

            items.Add(new AssertSpec(test, message));
            itemIndex++;
        }

        if (items.Count == 0)
            throw new JsonSchemaException($"'rules[{ruleIndex}].{name}' must not be empty.");

        return items;
    }

    /// <summary>
    /// Appends sugar asserts and reports into a single <c>context: "."</c> rule (creating it if needed).
    /// </summary>
    public static IReadOnlyList<RuleSpec> MergeRootSugar(
        IReadOnlyList<RuleSpec> rules,
        IReadOnlyList<AssertSpec> asserts,
        IReadOnlyList<AssertSpec> reports)
    {
        if (asserts.Count == 0 && reports.Count == 0)
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
            List<AssertSpec> combinedAsserts = [.. existing.Asserts, .. asserts];
            List<AssertSpec> combinedReports = [.. existing.Reports, .. reports];
            merged[rootIndex] = existing with
            {
                Asserts = combinedAsserts,
                Reports = combinedReports
            };
        }
        else
        {
            merged.Add(new RuleSpec(RootContext, asserts, reports, []));
        }

        return merged;
    }

    public static CompiledRuleSet Compile(
        IReadOnlyList<RuleSpec> rules,
        IReadOnlyList<LetSpec> schemaLets)
    {
        List<CompiledRule> compiledRules = new(rules.Count);
        foreach (var rule in rules)
        {
            var context = JqEvaluation.ParseExpression(rule.Context, "context");
            List<CompiledAssert> asserts = new(rule.Asserts.Count + rule.Reports.Count);
            foreach (var assert in rule.Asserts)
            {
                var test = JqEvaluation.ParseExpression(assert.Test, "test");
                var message = JqEvaluation.ParseMessage(assert.Message);
                asserts.Add(new CompiledAssert(test, message, negate: false));
            }

            foreach (var report in rule.Reports)
            {
                var test = JqEvaluation.ParseExpression(report.Test, "test");
                var message = JqEvaluation.ParseMessage(report.Message);
                asserts.Add(new CompiledAssert(test, message, negate: true));
            }

            var lets = CompileLets(rule.Lets);
            compiledRules.Add(new CompiledRule(context, lets, asserts));
        }

        return new CompiledRuleSet(compiledRules, CompileLets(schemaLets));
    }

    public static IReadOnlyList<CompiledLet> CompileLets(IReadOnlyList<LetSpec> lets)
    {
        if (lets.Count == 0)
            return [];

        List<CompiledLet> compiled = new(lets.Count);
        foreach (var let in lets)
        {
            var expression = JqEvaluation.ParseExpression(let.Expression, $"let.{let.Name}");
            compiled.Add(new CompiledLet(let.Name, expression));
        }

        return compiled;
    }

    /// <summary>
    /// Compiles sugar-only (<c>assert</c> / <c>report</c>) into a single <c>context: "."</c> rule,
    /// optionally including schema-level let bindings.
    /// </summary>
    public static CompiledRuleSet CompileSugarOnly(
        IReadOnlyList<AssertSpec> asserts,
        IReadOnlyList<AssertSpec> reports,
        IReadOnlyList<LetSpec> schemaLets)
    {
        if (asserts.Count == 0 && reports.Count == 0)
            return new CompiledRuleSet([], CompileLets(schemaLets));

        return Compile([new RuleSpec(RootContext, asserts, reports, [])], schemaLets);
    }
}
