namespace Jsontron;

/// <summary>
/// Uncompiled <c>let</c> binding (variable name and jq expression source).
/// </summary>
readonly record struct LetSpec(string Name, string Expression);
