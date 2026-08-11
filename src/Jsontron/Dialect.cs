using Json.Schema;
using Jsontron.Keywords;
using SchemaDialect = Json.Schema.Dialect;

namespace Jsontron;

/// <summary>
/// Jsontron dialect helpers. Call <see cref="MetaSchemas.Register"/> to apply
/// keywords to <see cref="SchemaDialect.Default"/>.
/// </summary>
public static class Dialect
{
    /// <summary>
    /// Dialect identifier matching the SchemaStore meta-schema.
    /// </summary>
    public static readonly Uri Id = Vocabulary.Id;

    /// <summary>
    /// A dialect based on draft 2020-12 with Jsontron keywords, identified by
    /// <see cref="Id"/> for <c>$schema</c> / registry use.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="MetaSchemas.Register"/> which also updates
    /// <see cref="SchemaDialect.Default"/> so ordinary builds pick up the keywords.
    /// </remarks>
    public static SchemaDialect Jsontron { get; } =
        SchemaDialect.Draft202012.With(
            [RulesKeyword.Instance, AssertKeyword.Instance],
            id: Id,
            allowUnknownKeywords: true);
}
