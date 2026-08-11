using Jsontron.Keywords;

namespace Jsontron;

/// <summary>
/// Declares the Jsontron vocabulary and its keywords.
/// </summary>
public static class Vocabulary
{
    /// <summary>
    /// The Jsontron vocabulary identifier.
    /// </summary>
    public static readonly Uri Id = new("https://www.schemastore.org/jsontron-0.1.json");

    /// <summary>
    /// The Jsontron vocabulary (rules + assert keywords).
    /// </summary>
    public static readonly Json.Schema.Vocabulary Jsontron = new(
        Id,
        RulesKeyword.Instance,
        AssertKeyword.Instance);
}
