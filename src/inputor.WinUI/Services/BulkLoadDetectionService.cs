namespace Inputor.App.Services;

using Inputor.App.Models;

public static class BulkLoadDetectionService
{
    public static bool LooksLikeBulkContentLoad(
        string processName,
        int delta,
        string? insertedTextSegment,
        DebugTextComparison? textComparison,
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

        if (LooksLikeTradingViewDocumentRefresh(processName, isDocumentLike, segment, textComparison))
        {
            return true;
        }

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

    private static bool LooksLikeTradingViewDocumentRefresh(
        string processName,
        bool isDocumentLike,
        string segment,
        DebugTextComparison? textComparison)
    {
        if (!isDocumentLike
            || !string.Equals(processName, "TradingView", StringComparison.OrdinalIgnoreCase)
            || textComparison is null)
        {
            return false;
        }

        var isLargeDocument = textComparison.PreviousTextLength >= 80 || textComparison.CurrentTextLength >= 80;
        if (!isLargeDocument)
        {
            return false;
        }

        var replacesExistingContent = textComparison.PreviousSegmentLength > 0;
        if (replacesExistingContent)
        {
            return true;
        }

        var hasRichUiMarkers = segment.Any(char.IsWhiteSpace)
            || segment.Any(char.IsPunctuation)
            || segment.Any(char.IsDigit);

        return textComparison.CurrentSupportedCharacterCount >= 2 && hasRichUiMarkers;
    }
}
