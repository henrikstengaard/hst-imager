namespace Hst.Imager.Core.Commands;

using System;
using System.Text.RegularExpressions;

public class PatternMatcher
{
    private readonly string pattern;
    private readonly Regex regex;

    public PatternMatcher(string pattern)
    {
        this.pattern = pattern;
        this.regex = pattern.IndexOf("*", StringComparison.Ordinal) >= 0 ? CreateRegex(pattern) : null;
    }

    private static Regex CreateRegex(string pattern)
    {
        if (pattern.IndexOf("**", StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException("Pattern can not contain multiple wildcards after each other (**)",
                nameof(pattern));
        }

        return new Regex($"^{pattern.Replace(".", "\\.").Replace("*", ".*")}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public bool IsMatch(string fileName)
    {
        if (this.regex == null)
        {
            return fileName.Length == this.pattern.Length &&
                   fileName.IndexOf(this.pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return this.regex.IsMatch(fileName);
    }
}