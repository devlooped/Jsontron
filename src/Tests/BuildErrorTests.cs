using Json.Schema;

namespace Jsontron.Tests;

public class BuildErrorTests : JsontronFixture
{
    [Fact]
    public void Invalid_assert_shape_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "assert": 42
            }
            """));
    }

    [Fact]
    public void Invalid_jq_test_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "assert": {
                "test": "(((",
                "message": "x"
              }
            }
            """));
    }

    [Fact]
    public void Rules_without_context_throws_at_build()
    {
        Assert.ThrowsAny<Exception>(() => Schema(
            """
            {
              "rules": [
                {
                  "asserts": [ { "test": "true" } ]
                }
              ]
            }
            """));
    }
}
