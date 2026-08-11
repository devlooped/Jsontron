using Json.Schema;

namespace Jsontron.Tests;

public class AssertSugarTests : JsontronFixture
{
    [Fact]
    public void String_assert_passes_when_test_is_truthy()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": ".value > 0"
            }
            """,
            """{ "value": 1 }""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void String_assert_fails_when_test_is_falsey()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": ".value > 0"
            }
            """,
            """{ "value": 0 }""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Object_assert_uses_custom_message()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "test": ".value > 0",
                "message": "value must be positive"
              }
            }
            """,
            """{ "value": -1 }""");

        Assert.False(result.IsValid);
        Assert.Contains("value must be positive", CollectErrors(result));
    }

    [Fact]
    public void Array_assert_appends_to_single_root_context_rule()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": [
                ".a > 0",
                { "test": ".b > 0", "message": "b must be positive" }
              ]
            }
            """,
            """{ "a": -1, "b": -1 }""");

        Assert.False(result.IsValid);
        Assert.Contains("b must be positive", CollectErrors(result));
    }

    [Fact]
    public void Message_supports_jq_string_interpolation()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "test": ".value > 0",
                "message": "bad value: \\(.value)"
              }
            }
            """,
            """{ "value": -5 }""");

        Assert.False(result.IsValid);
        Assert.Contains("bad value: -5", CollectErrors(result));
    }
}
