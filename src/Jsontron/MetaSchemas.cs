using System.Text;
using System.Text.Json;
using Json.Schema;
using Jsontron.Keywords;
using SchemaDialect = Json.Schema.Dialect;

namespace Jsontron;

/// <summary>
/// Registers Jsontron keywords on the default dialect and loads the meta-schema.
/// </summary>
public static class MetaSchemas
{
    /// <summary>
    /// Meta-schema <c>$id</c> / SchemaStore URI for Jsontron 0.1.
    /// </summary>
    public static readonly Uri JsontronId = new("https://www.schemastore.org/jsontron-0.1.json");

    /// <summary>
    /// The Jsontron vocabulary meta-schema (keyword syntax).
    /// </summary>
    public static JsonSchema Jsontron { get; private set; } = null!;

    static int registered;

    /// <summary>
    /// Registers Jsontron keywords on <see cref="SchemaDialect.Default"/> and
    /// loads the meta-schema into the schema registry.
    /// </summary>
    /// <remarks>
    /// After this call, <see cref="JsonSchema.FromText(string)"/> and other build APIs
    /// that use <see cref="BuildOptions.Default"/> understand <c>rules</c> and <c>assert</c>
    /// without further configuration.
    /// </remarks>
    public static void Register(BuildOptions? buildOptions = null)
    {
        buildOptions ??= BuildOptions.Default;

        if (Interlocked.Exchange(ref registered, 1) == 0)
        {
            var current = SchemaDialect.Default;
            var extended = current.With(
                [RulesKeyword.Instance, AssertKeyword.Instance],
                id: current.Id,
                allowUnknownKeywords: true);

            // BuildOptions.Default captures Dialect at construction time; refresh both.
            SchemaDialect.Default = extended;
            BuildOptions.Default.Dialect = extended;
        }

        // Ensure the caller's options (often BuildOptions.Default) use the extended dialect.
        buildOptions.Dialect = SchemaDialect.Default;

        buildOptions.DialectRegistry.Register(Dialect.Jsontron);
        buildOptions.VocabularyRegistry.Register(Vocabulary.Jsontron);

        Jsontron ??= LoadMetaSchema(buildOptions);
        buildOptions.SchemaRegistry.Register(JsontronId, Jsontron);
    }

    static JsonSchema LoadMetaSchema(BuildOptions buildOptions)
    {
        var assembly = typeof(MetaSchemas).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("jsontron-0.1.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded meta-schema jsontron-0.1.json not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        return JsonSchema.Build(JsonDocument.Parse(text).RootElement, buildOptions);
    }
}
