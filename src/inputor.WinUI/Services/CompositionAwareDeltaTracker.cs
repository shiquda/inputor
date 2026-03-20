namespace Inputor.App.Services;

using Inputor.App.Models;

/// <summary>
/// Tracks per-snapshot character deltas with awareness of Chinese IME composition.
///
/// During IME composition, Latin-letter input codes (pinyin, Wubi, etc.) appear in the
/// text field transiently and must not be counted as English characters. They are held
/// as "pending" until the user commits them as Chinese characters, or they time out and
/// are released as confirmed English input.
///
/// State machine:
///   Idle      → Composing : changed segment consists entirely of Latin letters
///   Composing → Composing : more Latin letters added, or Latin letters deleted (backspace)
///   Composing → Idle      : Chinese up + English down (commit)
///                         : unchanged text times out (English mode only)
///                         : any other non-Latin change (composition break)
/// </summary>
public sealed class CompositionAwareDeltaTracker
{
    // How long to wait before treating unconfirmed Latin input as committed English.
    // Only applies when the IME is not in native Chinese mode.
    private static readonly TimeSpan PendingConfirmationDelay = TimeSpan.FromMilliseconds(700);

    private string? _lastSnapshotKey;
    private string _lastRawText = string.Empty;

    // Count of committed supported characters (total minus pending Latin letters).
    // Every delta is computed relative to this baseline.
    private int _lastCommittedCount;

    // Non-null while a Latin-letter IME preedit is in flight.
    private PendingComposition? _pending;

    public DeltaResult ProcessSnapshot(
        string snapshotKey,
        string rawText,
        DateTime utcNow,
        bool isNativeImeInputMode,
        bool includeTextComparison)
    {
        if (_lastSnapshotKey != snapshotKey)
        {
            Reset(snapshotKey, rawText);
            return DeltaResult.NoChange(false);
        }

        var textComparison = includeTextComparison ? BuildTextComparison(_lastRawText, rawText) : null;
        var insertedTextSegment = GetCurrentChangedSegment(_lastRawText, rawText, textComparison);

        if (_lastRawText == rawText)
        {
            return HandleUnchangedText(utcNow, isNativeImeInputMode);
        }

        if (_pending is not null && IsCompositionCommit(_lastRawText, rawText))
        {
            return HandleCompositionCommit(rawText, insertedTextSegment, textComparison);
        }

        if (_pending is not null && IsEnglishDeletionOnly(_lastRawText, rawText))
        {
            return HandleCompositionDeletion(rawText, insertedTextSegment, textComparison);
        }

        if (TryDetectNewCompositionLetters(_lastRawText, rawText, out var addedLetterCount))
        {
            return HandleCompositionExtension(rawText, addedLetterCount, utcNow, insertedTextSegment, textComparison);
        }

        return HandleNormalEdit(rawText, insertedTextSegment, textComparison);
    }

    public void Reset()
    {
        _pending = null;
        _lastSnapshotKey = null;
        _lastRawText = string.Empty;
        _lastCommittedCount = 0;
    }

    private DeltaResult HandleUnchangedText(DateTime utcNow, bool isNativeImeInputMode)
    {
        if (_pending is not null
            && !isNativeImeInputMode
            && utcNow - _pending.StartedUtc >= PendingConfirmationDelay)
        {
            // Timeout: release pending letters as confirmed English input.
            var released = _pending.LetterCount;
            _pending = null;
            _lastCommittedCount += released;
            return new DeltaResult(released, false, null, null);
        }

        return DeltaResult.NoChange(_pending is not null);
    }

    private DeltaResult HandleCompositionCommit(
        string rawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison)
    {
        // Count ALL supported characters gained, not only Chinese.
        // _lastCommittedCount is the committed baseline from before composition began.
        var committed = CharacterCountService.CountSupportedCharacters(rawText);
        var delta = Math.Max(0, committed - _lastCommittedCount);
        _pending = null;
        _lastRawText = rawText;
        _lastCommittedCount = committed;
        return new DeltaResult(delta, false, insertedTextSegment, textComparison);
    }

    private DeltaResult HandleCompositionDeletion(
        string rawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison)
    {
        var deletedLetters = CharacterCountService.CountEnglishLetters(_lastRawText)
                           - CharacterCountService.CountEnglishLetters(rawText);
        var newLetterCount = Math.Max(0, _pending!.LetterCount - deletedLetters);

        _pending = newLetterCount > 0
            ? _pending with { LetterCount = newLetterCount, RawText = rawText }
            : null;

        var effectiveCommitted = CharacterCountService.CountSupportedCharacters(rawText) - newLetterCount;
        _lastRawText = rawText;
        _lastCommittedCount = effectiveCommitted;
        return DeltaResult.NoChange(_pending is not null);
    }

    private DeltaResult HandleCompositionExtension(
        string rawText,
        int addedLetterCount,
        DateTime utcNow,
        string? insertedTextSegment,
        DebugTextComparison? textComparison)
    {
        if (_pending is not null
            && rawText.Contains(_pending.RawText, StringComparison.Ordinal))
        {
            _pending = _pending with { LetterCount = _pending.LetterCount + addedLetterCount, RawText = rawText };
        }
        else
        {
            _pending = new PendingComposition(rawText, addedLetterCount, utcNow);
        }

        var effective = CharacterCountService.CountSupportedCharacters(rawText) - _pending.LetterCount;
        var delta = effective - _lastCommittedCount;
        _lastRawText = rawText;
        _lastCommittedCount = effective;
        return new DeltaResult(delta, true, insertedTextSegment, textComparison);
    }

    private DeltaResult HandleNormalEdit(
        string rawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison)
    {
        _pending = null;
        var committed = CharacterCountService.CountSupportedCharacters(rawText);
        var delta = committed - _lastCommittedCount;
        _lastRawText = rawText;
        _lastCommittedCount = committed;
        return new DeltaResult(delta, false, insertedTextSegment, textComparison);
    }

    private static bool IsCompositionCommit(string previousRawText, string currentRawText)
    {
        return CharacterCountService.CountChineseCharacters(currentRawText)
                   > CharacterCountService.CountChineseCharacters(previousRawText)
               && CharacterCountService.CountEnglishLetters(currentRawText)
                   < CharacterCountService.CountEnglishLetters(previousRawText);
    }

    private static bool IsEnglishDeletionOnly(string previousRawText, string currentRawText)
    {
        var prevLetters = CharacterCountService.CountEnglishLetters(previousRawText);
        var currLetters = CharacterCountService.CountEnglishLetters(currentRawText);
        if (currLetters >= prevLetters)
        {
            return false;
        }

        if (CharacterCountService.CountChineseCharacters(currentRawText)
            != CharacterCountService.CountChineseCharacters(previousRawText))
        {
            return false;
        }

        // Non-English supported characters (digits, symbols, punctuation) must not change.
        var prevOther = CharacterCountService.CountSupportedCharacters(previousRawText) - prevLetters;
        var currOther = CharacterCountService.CountSupportedCharacters(currentRawText) - currLetters;
        return currOther == prevOther;
    }

    private static bool TryDetectNewCompositionLetters(
        string previousRawText,
        string currentRawText,
        out int newLetterCount)
    {
        newLetterCount = 0;

        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);
        var currentChangedLength = currentRawText.Length - prefixLength - suffixLength;

        if (currentChangedLength <= 0)
        {
            return false;
        }

        var changedSegment = currentRawText.Substring(prefixLength, currentChangedLength);
        if (changedSegment.Any(ch => !CharacterCountService.IsEnglishLetter(ch)))
        {
            return false;
        }

        newLetterCount = changedSegment.Count(ch => CharacterCountService.IsEnglishLetter(ch));
        return newLetterCount > 0;
    }

    private void Reset(string snapshotKey, string rawText)
    {
        _pending = null;
        _lastSnapshotKey = snapshotKey;
        _lastRawText = rawText;
        _lastCommittedCount = CharacterCountService.CountSupportedCharacters(rawText);
    }

    private static string? GetCurrentChangedSegment(
        string previousRawText,
        string currentRawText,
        DebugTextComparison? textComparison)
    {
        if (previousRawText == currentRawText)
        {
            return null;
        }

        if (textComparison is not null)
        {
            return textComparison.CurrentSegmentLength <= 0
                ? null
                : currentRawText.Substring(textComparison.ChangeStartIndex, textComparison.CurrentSegmentLength);
        }

        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);
        var currentChangedLength = currentRawText.Length - prefixLength - suffixLength;
        if (currentChangedLength <= 0)
        {
            return null;
        }

        return currentRawText.Substring(prefixLength, currentChangedLength);
    }

    private static DebugTextComparison? BuildTextComparison(
        string previousRawText,
        string currentRawText)
    {
        if (previousRawText == currentRawText)
        {
            return null;
        }

        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);
        var previousChangedLength = previousRawText.Length - prefixLength - suffixLength;
        var currentChangedLength = currentRawText.Length - prefixLength - suffixLength;
        var previousChangedSegment = previousChangedLength > 0
            ? previousRawText.Substring(prefixLength, previousChangedLength)
            : string.Empty;
        var currentChangedSegment = currentChangedLength > 0
            ? currentRawText.Substring(prefixLength, currentChangedLength)
            : string.Empty;

        return new DebugTextComparison
        {
            ChangeStartIndex = prefixLength,
            PreviousTextLength = previousRawText.Length,
            CurrentTextLength = currentRawText.Length,
            PreviousSegmentLength = previousChangedLength,
            CurrentSegmentLength = currentChangedLength,
            PreviousSupportedCharacterCount = CharacterCountService.CountSupportedCharacters(previousChangedSegment),
            PreviousChineseCharacterCount = CharacterCountService.CountChineseCharacters(previousChangedSegment),
            PreviousEnglishLetterCount = CharacterCountService.CountEnglishLetters(previousChangedSegment),
            CurrentSupportedCharacterCount = CharacterCountService.CountSupportedCharacters(currentChangedSegment),
            CurrentChineseCharacterCount = CharacterCountService.CountChineseCharacters(currentChangedSegment),
            CurrentEnglishLetterCount = CharacterCountService.CountEnglishLetters(currentChangedSegment),
            PreviousText = TruncateForDisplay(previousChangedSegment),
            CurrentText = TruncateForDisplay(currentChangedSegment)
        };
    }

    private static string TruncateForDisplay(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "-";
        }

        const int DisplayLimit = 80;
        var runes = text.EnumerateRunes().ToList();
        if (runes.Count <= DisplayLimit)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var rune in runes.Take(DisplayLimit))
        {
            builder.Append(rune.ToString());
        }

        builder.Append("…");
        return builder.ToString();
    }

    private static int GetCommonPrefixLength(string left, string right)
    {
        var maxLength = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < maxLength && left[index] == right[index])
        {
            index++;
        }

        return index;
    }

    private static int GetCommonSuffixLength(string left, string right, int prefixLength)
    {
        var leftIndex = left.Length - 1;
        var rightIndex = right.Length - 1;
        var count = 0;

        while (leftIndex >= prefixLength && rightIndex >= prefixLength && left[leftIndex] == right[rightIndex])
        {
            count++;
            leftIndex--;
            rightIndex--;
        }

        return count;
    }

    public readonly record struct DeltaResult(
        int Delta,
        bool IsPendingComposition,
        string? InsertedTextSegment,
        DebugTextComparison? TextComparison)
    {
        public static DeltaResult NoChange(bool isPendingComposition) =>
            new(0, isPendingComposition, null, null);
    }

    private sealed record PendingComposition(string RawText, int LetterCount, DateTime StartedUtc);
}
