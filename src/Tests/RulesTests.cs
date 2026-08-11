using Json.Schema;

namespace Jsontron.Tests;

public class RulesTests : JsontronFixture
{
    const string SampleSchema = """
        {
          "type": "object",
          "required": [ "orders", "policy", "approvedCustomers" ],
          "rules": [
            {
              "context": ".orders[] | select(.total > 1000)",
              "asserts": [
                {
                  "test": ".discount >= ($root.policy.minHighValueDiscount // 0.1)",
                  "message": "High-value orders must respect the root policy discount"
                },
                {
                  "test": ".customer.id | IN($root.approvedCustomers[])",
                  "message": "Customer is not on the approved list"
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void High_value_order_passes_policy_and_approved_customer()
    {
        var result = Evaluate(SampleSchema, """
            {
              "policy": { "minHighValueDiscount": 0.15 },
              "approvedCustomers": [ "c1", "c2" ],
              "orders": [
                { "total": 1500, "discount": 0.2, "customer": { "id": "c1" } },
                { "total": 50, "discount": 0, "customer": { "id": "unknown" } }
              ]
            }
            """);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void High_value_order_fails_discount_policy()
    {
        var result = Evaluate(SampleSchema, """
            {
              "policy": { "minHighValueDiscount": 0.15 },
              "approvedCustomers": [ "c1" ],
              "orders": [
                { "total": 1500, "discount": 0.05, "customer": { "id": "c1" } }
              ]
            }
            """);

        Assert.False(result.IsValid);
        Assert.Contains("High-value orders must respect the root policy discount", CollectErrors(result));
    }

    [Fact]
    public void High_value_order_fails_unapproved_customer()
    {
        var result = Evaluate(SampleSchema, """
            {
              "policy": { "minHighValueDiscount": 0.1 },
              "approvedCustomers": [ "c1" ],
              "orders": [
                { "total": 2000, "discount": 0.2, "customer": { "id": "c99" } }
              ]
            }
            """);

        Assert.False(result.IsValid);
        Assert.Contains("Customer is not on the approved list", CollectErrors(result));
    }

    [Fact]
    public void Empty_context_match_is_vacuously_valid()
    {
        var result = Evaluate(SampleSchema, """
            {
              "policy": { "minHighValueDiscount": 0.5 },
              "approvedCustomers": [],
              "orders": [
                { "total": 10, "discount": 0, "customer": { "id": "x" } }
              ]
            }
            """);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Nested_assert_uses_local_instance_as_dot()
    {
        var result = Evaluate(
            """
            {
              "type": "object",
              "properties": {
                "orders": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "assert": ".total >= 0"
                  }
                }
              }
            }
            """,
            """
            {
              "orders": [
                { "total": 1 },
                { "total": -3 }
              ]
            }
            """);

        Assert.False(result.IsValid);
    }
}
