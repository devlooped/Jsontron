using Json.Schema;

namespace Jsontron.Tests;

public class SeverityAndIdTests : JsontronFixture
{
    [Fact]
    public void Assert_id_prefixes_message()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "id": "A-1",
                "test": ".value > 0",
                "message": "value must be positive"
              }
            }
            """,
            """{ "value": -1 }""");

        Assert.False(result.IsValid);
        Assert.Contains("[A-1] value must be positive", CollectErrors(result));
    }

    [Fact]
    public void Report_id_prefixes_message()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": {
                "id": "R-1",
                "test": ".forbidden == true",
                "message": "forbidden"
              }
            }
            """,
            """{ "forbidden": true }""");

        Assert.False(result.IsValid);
        Assert.Contains("[R-1] forbidden", CollectErrors(result));
    }

    [Fact]
    public void Rule_id_and_assert_id_both_prefix_rule_first()
    {
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "id": "orders",
                  "context": ".items[]",
                  "asserts": [
                    { "id": "qty-pos", "test": ".qty > 0", "message": "qty must be positive" }
                  ]
                }
              ]
            }
            """,
            """{ "items": [ { "qty": -1 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("[orders][qty-pos] qty must be positive", CollectErrors(result));
    }

    [Fact]
    public void Flag_prefixes_message_without_id()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "flag": "warning",
                "test": ".value > 0",
                "message": "value should be positive"
              }
            }
            """,
            """{ "value": -1 }""");

        Assert.False(result.IsValid);
        Assert.Contains("[flag=warning] value should be positive", CollectErrors(result));
    }

    [Fact]
    public void Flag_prefixes_after_id()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "id": "A-1",
                "flag": "error",
                "test": ".value > 0",
                "message": "value must be positive"
              }
            }
            """,
            """{ "value": -1 }""");

        Assert.False(result.IsValid);
        Assert.Contains("[A-1][flag=error] value must be positive", CollectErrors(result));
    }

    [Fact]
    public void Role_alias_produces_same_output_as_flag()
    {
        var withFlag = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "flag": "warning",
                "test": ".value > 0",
                "message": "msg"
              }
            }
            """,
            """{ "value": -1 }""");

        var withRole = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "role": "warning",
                "test": ".value > 0",
                "message": "msg"
              }
            }
            """,
            """{ "value": -1 }""");

        Assert.False(withFlag.IsValid);
        Assert.False(withRole.IsValid);
        Assert.Equal(CollectErrors(withFlag), CollectErrors(withRole));
        Assert.Contains("[flag=warning] msg", CollectErrors(withRole));
    }

    [Fact]
    public void Flag_and_role_on_same_entry_rejects_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "type": "object",
              "assert": {
                "flag": "warning",
                "role": "warning",
                "test": ".value > 0",
                "message": "msg"
              }
            }
            """));
    }

    [Fact]
    public void Empty_id_rejects_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "type": "object",
              "assert": {
                "id": "   ",
                "test": ".value > 0"
              }
            }
            """));
    }

    [Fact]
    public void String_sugar_still_works_unchanged()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": ".value > 0"
            }
            """,
            """{ "value": -1 }""");

        Assert.False(result.IsValid);
        var errors = CollectErrors(result);
        Assert.DoesNotContain("[", errors);
    }

    [Fact]
    public void Top_level_assert_sugar_object_form_carries_id()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "id": "top-a",
                "test": ".ok == true",
                "message": "not ok"
              }
            }
            """,
            """{ "ok": false }""");

        Assert.False(result.IsValid);
        Assert.Contains("[top-a] not ok", CollectErrors(result));
    }

    [Fact]
    public void Top_level_report_sugar_object_form_carries_id()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "report": {
                "id": "top-r",
                "test": ".forbidden == true",
                "message": "forbidden"
              }
            }
            """,
            """{ "forbidden": true }""");

        Assert.False(result.IsValid);
        Assert.Contains("[top-r] forbidden", CollectErrors(result));
    }

    [Fact]
    public void Rule_assert_item_flag_prefixes_message()
    {
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "context": ".items[]",
                  "asserts": [
                    { "flag": "warning", "test": ".qty > 0", "message": "qty low" }
                  ]
                }
              ]
            }
            """,
            """{ "items": [ { "qty": -1 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("[flag=warning] qty low", CollectErrors(result));
    }

    [Fact]
    public void Rule_report_item_id_and_flag_prefix_message()
    {
        var result = Evaluate(
            """
            {
              "rules": [
                {
                  "id": "rule-x",
                  "context": ".items[]",
                  "reports": [
                    { "id": "neg", "role": "error", "test": ".qty < 0", "message": "negative qty" }
                  ]
                }
              ]
            }
            """,
            """{ "items": [ { "qty": -1 } ] }""");

        Assert.False(result.IsValid);
        Assert.Contains("[rule-x][neg][flag=error] negative qty", CollectErrors(result));
    }

    [Fact]
    public void Flag_and_role_on_rule_item_rejects_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "rules": [
                {
                  "context": ".",
                  "asserts": [
                    { "flag": "warning", "role": "warning", "test": ".ok == true" }
                  ]
                }
              ]
            }
            """));
    }
}
