using System.Text.Json;
using Json.Schema;

namespace Jsontron.Tests;

public abstract class JsontronFixture
{
    static JsontronFixture() => MetaSchemas.Register();

    protected static JsonSchema Schema(string json) => JsonSchema.FromText(json);

    protected static JsonElement Instance(string json) => JsonDocument.Parse(json).RootElement;

    protected static EvaluationResults Evaluate(string schemaJson, string instanceJson)
        => Schema(schemaJson).Evaluate(
            Instance(instanceJson),
            new EvaluationOptions { OutputFormat = OutputFormat.List });

    protected static string CollectErrors(EvaluationResults result)
    {
        List<string> errors = [];
        CollectErrors(result, errors);
        return string.Join("; ", errors);
    }

    static void CollectErrors(EvaluationResults result, List<string> errors)
    {
        if (result.Errors is not null)
            errors.AddRange(result.Errors.Values);

        if (result.Details is null)
            return;

        foreach (var detail in result.Details)
            CollectErrors(detail, errors);
    }
}
