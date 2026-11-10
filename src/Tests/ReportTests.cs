using Json.Schema;

namespace Jsontron.Tests;

public class ReportTests : JsontronFixture
{
    [Fact]
    public void String_report_fails_when_test_is_truthy()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": ".value < 0"
            }
            """,
            """{ "value": -1 }""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void String_report_passes_when_test_is_falsey()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": ".value < 0"
            }
            """,
            """{ "value": 1 }""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Object_report_uses_custom_message()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": {
                "test": ".value < 0",
                "message": "value must not be negative"
              }
            }
            """,
            """{ "value": -5 }""");

        Assert.False(result.IsValid);
        Assert.Contains("value must not be negative", CollectErrors(result));
    }

    [Fact]
    public void Array_report_appends_to_single_root_context_rule()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": [
                ".a < 0",
                { "test": ".b < 0", "message": "b must not be negative" }
              ]
            }
            """,
            """{ "a": 1, "b": -1 }""");

        Assert.False(result.IsValid);
        Assert.Contains("b must not be negative", CollectErrors(result));
    }

    [Fact]
    public void Empty_stream_test_does_not_trigger_report()
    {
        // jq-truthy requires at least one non-false, non-null result; empty stream is falsey,
        // so a report with an empty-stream test must NOT fire.
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": ".missing[]?"
            }
            """,
            """{ "value": 1 }""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rule_with_reports_only()
    {
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "context": ".items[]",
                  "reports": [
                    { "test": ".qty < 0", "message": "item \\(.id) has negative qty" }
                  ]
                }
              ]
            }
            """,
            """{ "items": [ { "id": "a", "qty": 1 }, { "id": "b", "qty": -3 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("item b has negative qty", CollectErrors(result));
    }

    [Fact]
    public void Rule_with_both_asserts_and_reports()
    {
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "context": ".items[]",
                  "asserts": [
                    { "test": ".id | type == \"string\"", "message": "id must be a string" }
                  ],
                  "reports": [
                    { "test": ".qty < 0", "message": "qty must not be negative" }
                  ]
                }
              ]
            }
            """,
            """{ "items": [ { "id": "a", "qty": -1 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("qty must not be negative", CollectErrors(result));
    }

    [Fact]
    public void Top_level_report_merges_into_root_context_rule_alongside_rules()
    {
        var result = Evaluate(
            """
            {
              "report": {
                "test": ".forbidden == true",
                "message": "forbidden"
              },
              "rules": [
                {
                  "context": ".",
                  "asserts": [
                    { "test": ".value > 0", "message": "value must be positive" }
                  ]
                }
              ]
            }
            """,
            """{ "forbidden": true, "value": -1 }""");

        Assert.False(result.IsValid);
        var errors = CollectErrors(result);
        Assert.Contains("forbidden", errors);
        Assert.Contains("value must be positive", errors);
    }

    [Fact]
    public void Top_level_assert_and_report_merge_into_synthetic_root_rule()
    {
        var result = Evaluate(
            """
            {
              "assert": { "test": ".ok == true", "message": "not ok" },
              "report": { "test": ".forbidden == true", "message": "forbidden" }
            }
            """,
            """{ "ok": false, "forbidden": true }""");

        Assert.False(result.IsValid);
        var errors = CollectErrors(result);
        Assert.Contains("not ok", errors);
        Assert.Contains("forbidden", errors);
    }

    [Fact]
    public void Report_sugar_invalid_shape_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "report": 42
            }
            """));
    }

    [Fact]
    public void Rule_without_asserts_or_reports_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "rules": [
                {
                  "context": "."
                }
              ]
            }
            """));
    }
}
