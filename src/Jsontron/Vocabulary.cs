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
    public static readonly Uri Id = new("https://www.schemastore.org/jsontron-0.2.json");

    /// <summary>
    /// The Jsontron vocabulary (rules + assert + report + let keywords).
    /// </summary>
    public static readonly Json.Schema.Vocabulary Jsontron = new(
        Id,
        RulesKeyword.Instance,
        AssertKeyword.Instance,
        ReportKeyword.Instance,
        LetKeyword.Instance);
}
