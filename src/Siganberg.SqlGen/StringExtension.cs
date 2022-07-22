namespace Siganberg.SqlGen;

public static class StringExtension
{
    public static string StripBracket(this string value)
    {
        return value.Replace("]", "").Replace("[", "");
    }
}