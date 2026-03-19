namespace Inputor.App.Services;

public static class BulkLoadDetectionService
{
    public static bool LooksLikeBulkContentLoad(
        int delta,
        string? insertedTextSegment,
        string controlTypeName,
        bool isPaste)
    {
        if (isPaste || delta <= 0 || string.IsNullOrWhiteSpace(insertedTextSegment))
        {
            return false;
        }

        var segment = Normalize(insertedTextSegment);
        if (segment.Length == 0)
        {
            return false;
        }

        var whitespaceCount = segment.Count(char.IsWhiteSpace);
        var hasLineBreak = segment.Contains('\n') || segment.Contains('\r');
        var isDocumentLike = string.Equals(controlTypeName, "Document", StringComparison.OrdinalIgnoreCase);

        if (isDocumentLike && delta >= 12 && (hasLineBreak || whitespaceCount >= 2 || segment.Length >= 24))
        {
            return true;
        }

        if (delta >= 40 && (hasLineBreak || whitespaceCount >= 4 || segment.Length >= 64))
        {
            return true;
        }

        return false;
    }

    private static string Normalize(string text)
    {
        return text.Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
