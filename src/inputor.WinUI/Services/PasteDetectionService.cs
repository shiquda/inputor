namespace Inputor.App.Services;

public static class PasteDetectionService
{
    public static bool LooksLikePaste(string? insertedTextSegment, string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(insertedTextSegment) || string.IsNullOrWhiteSpace(clipboardText))
        {
            return false;
        }

        var inserted = Normalize(insertedTextSegment);
        var clipboard = Normalize(clipboardText);
        if (clipboard.Length == 0)
        {
            return false;
        }

        if (string.Equals(inserted, clipboard, StringComparison.Ordinal))
        {
            return true;
        }

        if (inserted.Length < clipboard.Length || inserted.Length % clipboard.Length != 0)
        {
            return false;
        }

        for (var index = 0; index < inserted.Length; index += clipboard.Length)
        {
            if (!inserted.AsSpan(index, clipboard.Length).SequenceEqual(clipboard.AsSpan()))
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string text)
    {
        return text.Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
