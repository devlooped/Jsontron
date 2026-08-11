using System.Text;
using System.Text.Json;
using Devlooped;
using Json.Schema;

namespace Jsontron;

static class JqEvaluation
{
    /// <summary>
    /// Evaluates a compiled rule set against the current evaluation context.
    /// Binds <c>$root</c> to the document root.
    /// </summary>
    public static KeywordEvaluation Evaluate(string keyword, CompiledRuleSet ruleSet, EvaluationContext context)
    {
        if (ruleSet.IsDelegated || ruleSet.Rules.Count == 0)
            return KeywordEvaluation.Ignore;

        List<string>? failures = null;
        Dictionary<string, JsonElement> variables = new() { ["root"] = context.InstanceRoot };

        try
        {
            foreach (var rule in ruleSet.Rules)
            {
                foreach (var node in rule.Context.Evaluate(context.Instance, variables))
                {
                    foreach (var assert in rule.Asserts)
                    {
                        if (IsTruthy(assert.Test.Evaluate(node, variables)))
                            continue;

                        failures ??= [];
                        failures.Add(EvaluateMessage(assert.Message, node, variables));
                    }
                }
            }
        }
        catch (JqException ex)
        {
            return new KeywordEvaluation
            {
                Keyword = keyword,
                IsValid = false,
                Error = ex.Message
            };
        }

        if (failures is null)
        {
            return new KeywordEvaluation
            {
                Keyword = keyword,
                IsValid = true
            };
        }

        return new KeywordEvaluation
        {
            Keyword = keyword,
            IsValid = false,
            Error = string.Join("; ", failures)
        };
    }

    /// <summary>
    /// jq truthiness: empty stream or any falsey value fails the assert.
    /// </summary>
    public static bool IsTruthy(IEnumerable<JsonElement> results)
    {
        var any = false;
        foreach (var result in results)
        {
            any = true;
            if (result.ValueKind is JsonValueKind.Null or JsonValueKind.False)
                return false;
        }

        return any;
    }

    public static string EvaluateMessage(
        JqExpression message,
        JsonElement node,
        IReadOnlyDictionary<string, JsonElement> variables)
    {
        try
        {
            foreach (var result in message.Evaluate(node, variables))
            {
                return result.ValueKind switch
                {
                    JsonValueKind.String => result.GetString() ?? "",
                    JsonValueKind.Null => "",
                    _ => result.GetRawText()
                };
            }
        }
        catch (JqException)
        {
            // Fall through to empty message on evaluation failure.
        }

        return "";
    }

    /// <summary>
    /// Prepares a message string for jq parsing. If the message does not already start with
    /// a double quote, wraps it as a jq string literal (implicit quotes).
    /// </summary>
    public static string PrepareMessageExpression(string message)
    {
        var trimmed = message.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '"')
            return message;

        // Wrap as a jq string. Preserve \ so jq interpolation (\(.x)) works;
        // only escape quotes and control characters.
        var sb = new StringBuilder(message.Length + 2);
        sb.Append('"');
        foreach (var c in message)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    public static JqExpression ParseExpression(string expression, string role)
    {
        try
        {
            return Jq.Parse(expression);
        }
        catch (JqException ex)
        {
            throw new JsonSchemaException($"Invalid jq {role}: {ex.Message}");
        }
    }

    public static JqExpression ParseMessage(string message)
        => ParseExpression(PrepareMessageExpression(message), "message");
}
