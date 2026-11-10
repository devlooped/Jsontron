using Json.Schema;

namespace Jsontron.Tests;

public class LetTests : JsontronFixture
{
    [Fact]
    public void Schema_level_let_is_visible_in_rule_context()
    {
        var result = Evaluate(
            """
            {
              "let": { "threshold": ".policy.min" },
              "rules": [
                {
                  "context": ".items[] | select(.n < $threshold)",
                  "asserts": [
                    { "test": "false", "message": "value \\(.n) below threshold" }
                  ]
                }
              ]
            }
            """,
            """{ "policy": { "min": 10 }, "items": [ { "n": 5 }, { "n": 20 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("value 5 below threshold", CollectErrors(result));
    }

    [Fact]
    public void Schema_level_let_is_visible_in_assert_sugar()
    {
        var result = Evaluate(
            """
            {
              "let": { "min": ".limits.min" },
              "assert": {
                "test": ".value >= $min",
                "message": "value \\(.value) is below \\($min)"
              }
            }
            """,
            """{ "limits": { "min": 10 }, "value": 5 }""");

        Assert.False(result.IsValid);
        Assert.Contains("value 5 is below 10", CollectErrors(result));
    }

    [Fact]
    public void Schema_level_let_is_visible_in_report_sugar()
    {
        var result = Evaluate(
            """
            {
              "let": { "cap": ".limits.max" },
              "report": {
                "test": ".value > $cap",
                "message": "value \\(.value) exceeds \\($cap)"
              }
            }
            """,
            """{ "limits": { "max": 10 }, "value": 15 }""");

        Assert.False(result.IsValid);
        Assert.Contains("value 15 exceeds 10", CollectErrors(result));
    }

    [Fact]
    public void Let_bindings_are_evaluated_in_order_and_see_earlier_bindings()
    {
        var result = Evaluate(
            """
            {
              "let": {
                "base": ".base",
                "doubled": "$base * 2"
              },
              "assert": {
                "test": ".value == $doubled",
                "message": "expected \\($doubled), got \\(.value)"
              }
            }
            """,
            """{ "base": 7, "value": 10 }""");

        Assert.False(result.IsValid);
        Assert.Contains("expected 14, got 10", CollectErrors(result));
    }

    [Fact]
    public void Let_binds_null_when_expression_yields_empty_stream()
    {
        var result = Evaluate(
            """
            {
              "let": { "missing": ".notThere[]?" },
              "assert": {
                "test": "$missing == null",
                "message": "missing was not null: \\($missing)"
              }
            }
            """,
            """{ "value": 1 }""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rule_level_let_is_visible_to_rule_asserts()
    {
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "context": ".orders[]",
                  "let": { "ship": ".shipments[0] // null" },
                  "asserts": [
                    {
                      "test": "$ship == null or $ship.weight <= 100",
                      "message": "order \\(.id) has heavy shipment \\($ship.weight)"
                    }
                  ]
                }
              ]
            }
            """,
            """
            {
              "orders": [
                { "id": "o1", "shipments": [ { "weight": 50 } ] },
                { "id": "o2", "shipments": [ { "weight": 200 } ] }
              ]
            }
            """);

        Assert.False(result.IsValid);
        Assert.Contains("order o2 has heavy shipment 200", CollectErrors(result));
    }

    [Fact]
    public void Rule_level_let_is_not_visible_to_a_later_rule()
    {
        // Second rule references $local which was only bound inside the first rule.
        // Jq variable references are resolved at evaluation time, so the first rule
        // passes and the second rule fails (either because the variable is unbound and
        // the engine reports an error, or because it resolves to something that makes
        // the test falsey). Either way the schema must not be valid against the instance.
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "context": ".",
                  "let": { "local": ".a" },
                  "asserts": [ { "test": "$local == 1", "message": "first rule saw local" } ]
                },
                {
                  "context": ".",
                  "asserts": [ { "test": "$local == 1", "message": "leak: local must not be visible here" } ]
                }
              ]
            }
            """,
            """{ "a": 1 }""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_level_let_in_rule_level_rule_with_lets_both_scopes_seen()
    {
        var result = Evaluate(
            """
            {
              "let": { "cap": ".cap" },
              "rules": [
                {
                  "context": ".items[]",
                  "let": { "doubled": ".n * 2" },
                  "asserts": [
                    {
                      "test": "$doubled <= $cap",
                      "message": "doubled \\($doubled) exceeds cap \\($cap)"
                    }
                  ]
                }
              ]
            }
            """,
            """{ "cap": 10, "items": [ { "n": 3 }, { "n": 8 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("doubled 16 exceeds cap 10", CollectErrors(result));
    }

    [Fact]
    public void Reserved_root_is_rejected_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "let": { "root": "." },
              "assert": ".x > 0"
            }
            """));
    }

    [Fact]
    public void Invalid_identifier_is_rejected_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "let": { "1bad": "." },
              "assert": ".x > 0"
            }
            """));
    }

    [Fact]
    public void Let_requires_string_expression()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "let": { "x": 42 },
              "assert": ".x > 0"
            }
            """));
    }

    [Fact]
    public void Invalid_let_jq_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "let": { "x": "(((" },
              "assert": ".x > 0"
            }
            """));
    }

    [Fact]
    public void Rule_level_let_with_invalid_identifier_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "rules": [
                {
                  "context": ".",
                  "let": { "root": "." },
                  "asserts": [ { "test": "true" } ]
                }
              ]
            }
            """));
    }
}
