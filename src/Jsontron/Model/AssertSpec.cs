namespace Jsontron;

/// <summary>
/// Uncompiled assert specification (test and message source text).
/// </summary>
readonly record struct AssertSpec(string Test, string Message);
