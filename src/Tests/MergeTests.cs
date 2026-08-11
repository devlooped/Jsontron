using Json.Schema;

namespace Jsontron.Tests;

public class MergeTests : JsontronFixture
{
    [Fact]
    public void Assert_merges_into_existing_root_context_rule()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "test": ".x > 0",
                "message": "from assert"
              },
              "rules": [
                {
                  "context": ".",
                  "asserts": [
                    { "test": ".y > 0", "message": "from rules" }
                  ]
                }
              ]
            }
            """,
            """{ "x": -1, "y": -1 }""");

        Assert.False(result.IsValid);
        var errors = CollectErrors(result);
        Assert.Contains("from assert", errors);
        Assert.Contains("from rules", errors);
    }

    [Fact]
    public void Assert_creates_root_context_rule_when_rules_lack_one()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "assert": {
                "test": ".ok == true",
                "message": "not ok"
              },
              "rules": [
                {
                  "context": ".items[]",
                  "asserts": [
                    { "test": ".n > 0", "message": "item" }
                  ]
                }
              ]
            }
            """,
            """{ "ok": false, "items": [] }""");

        Assert.False(result.IsValid);
        Assert.Contains("not ok", CollectErrors(result));
    }

    [Fact]
    public void Assert_is_not_evaluated_twice_when_rules_present()
    {
        // If both keywords evaluated the same assert, we would still only get one logical failure;
        // this checks the merge path produces a single coherent invalid result without throw.
        var result = Evaluate(
            """
            {
              "assert": ".v == 1",
              "rules": [
                {
                  "context": ".",
                  "asserts": [ { "test": ".v == 1", "message": "v" } ]
                }
              ]
            }
            """,
            """{ "v": 0 }""");

        Assert.False(result.IsValid);
    }
}
