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

    // How long to wait in native Chinese IME mode before releasing pending input as English.
    // Uses last-activity time rather than composition-start time, so continuous typing
    // never triggers this; only a genuine idle pause of this duration does.
    private static readonly TimeSpan NativeImePendingConfirmationDelay = TimeSpan.FromSeconds(5);

    // Time window within which a text-restore after a sudden clear is treated as
    // a browser/app lifecycle event rather than genuine user input.
    private static readonly TimeSpan TransientClearWindow = TimeSpan.FromSeconds(5);

    // Minimum committed character count before a sudden drop to near-zero is
    // considered a potential transient clear (not just the user deleting a short word).
    private const int TransientClearSourceThreshold = 5;

    // Minimum single-step delta required to identify a restore event inside the
    // transient-clear window. This distinguishes a bulk restore (browser puts back
    // the old text in one shot) from the user typing character by character.
    private const int TransientClearRestoreThreshold = 4;

    private string? _lastSnapshotKey;
    private string _lastRawText = string.Empty;

    // Count of committed supported characters (total minus pending Latin letters).
    // Every delta is computed relative to this baseline.
    private int _lastCommittedCount;

    // Non-null while a Latin-letter IME preedit is in flight.
    private PendingComposition? _pending;

    // Non-null after a sudden text clear on the same snapshot key. Used to detect
    // whether the following large positive delta is a browser/app restoring old
    // content rather than the user typing new characters.
    private TransientClear? _transientClear;

    public DeltaResult ProcessSnapshot(
        string snapshotKey,
        string rawText,
        DateTime utcNow,
        bool isNativeImeInputMode,
        bool includeTextComparison)
    {
        if (_lastSnapshotKey != snapshotKey)
        {
            var flushedDelta = _pending?.LetterCount ?? 0;
            Reset(snapshotKey, rawText);
            return flushedDelta > 0
                ? new DeltaResult(flushedDelta, false, null, null)
                : DeltaResult.NoChange(false);
        }

        var textComparison = includeTextComparison ? BuildTextComparison(_lastRawText, rawText) : null;
        var insertedTextSegment = GetCurrentChangedSegment(_lastRawText, rawText, textComparison);

        if (_lastRawText == rawText)
        {
            return HandleUnchangedText(utcNow, isNativeImeInputMode);
        }

        if (_pending is not null && IsCompositionCommit(_lastRawText, rawText))
        {
            return HandleCompositionCommit(rawText, insertedTextSegment, textComparison, utcNow);
        }

        if (_pending is not null && IsEnglishDeletionOnly(_lastRawText, rawText))
        {
            return HandleCompositionDeletion(rawText, insertedTextSegment, textComparison, utcNow);
        }

        if (TryDetectNewCompositionLetters(_lastRawText, rawText, isNativeImeInputMode, out var addedLetterCount))
        {
            return HandleCompositionExtension(rawText, addedLetterCount, utcNow, insertedTextSegment, textComparison);
        }

        if (_pending is not null && isNativeImeInputMode && IsOnlySpacesAdded(_lastRawText, rawText))
        {
            return HandlePinyinSeparator(rawText, insertedTextSegment, textComparison, utcNow);
        }

        if (_pending is not null && isNativeImeInputMode && IsOnlySpacesRemoved(_lastRawText, rawText))
        {
            return HandlePinyinSeparator(rawText, insertedTextSegment, textComparison, utcNow);
        }

        return HandleNormalEdit(rawText, utcNow, insertedTextSegment, textComparison);
    }

    public void Reset()
    {
        _pending = null;
        _transientClear = null;
        _lastSnapshotKey = null;
        _lastRawText = string.Empty;
        _lastCommittedCount = 0;
    }

    private DeltaResult HandleUnchangedText(DateTime utcNow, bool isNativeImeInputMode)
    {
        if (_pending is not null)
        {
            var delay = isNativeImeInputMode ? NativeImePendingConfirmationDelay : PendingConfirmationDelay;
            if (utcNow - _pending.LastUpdatedUtc >= delay)
            {
                var released = _pending.LetterCount;
                _pending = null;
                _lastCommittedCount += released;
                return new DeltaResult(released, false, null, null);
            }
        }

        return DeltaResult.NoChange(_pending is not null);
    }

    private DeltaResult HandleCompositionCommit(
        string rawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison,
        DateTime utcNow)
    {
        _transientClear = null;
        var trailingPreedit = CountTrailingPreeditSupportedChars(rawText);
        var committed = CharacterCountService.CountSupportedCharacters(rawText) - trailingPreedit;
        var delta = Math.Max(0, committed - _lastCommittedCount);
        _lastRawText = rawText;
        _lastCommittedCount = committed;
        _pending = trailingPreedit > 0 ? new PendingComposition(rawText, trailingPreedit, utcNow) : null;
        return new DeltaResult(delta, trailingPreedit > 0, insertedTextSegment, textComparison);
    }

    private static int CountTrailingPreeditSupportedChars(string rawText)
    {
        var count = 0;
        for (var i = rawText.Length - 1; i >= 0; i--)
        {
            var ch = rawText[i];
            if (CharacterCountService.IsEnglishLetter(ch) || ch == '\'')
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count;
    }

    private DeltaResult HandleCompositionDeletion(
        string rawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison,
        DateTime utcNow)
    {
        _transientClear = null;
        var deletedLetters = CharacterCountService.CountEnglishLetters(_lastRawText)
                           - CharacterCountService.CountEnglishLetters(rawText);
        var newLetterCount = Math.Max(0, _pending!.LetterCount - deletedLetters);

        _pending = newLetterCount > 0
            ? _pending with { LetterCount = newLetterCount, RawText = rawText, LastUpdatedUtc = utcNow }
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
        if (_transientClear is not null
            && utcNow - _transientClear.DetectedUtc <= TransientClearWindow
            && addedLetterCount >= TransientClearRestoreThreshold)
        {
            _transientClear = null;
            _pending = null;
            _lastRawText = rawText;
            _lastCommittedCount = CharacterCountService.CountSupportedCharacters(rawText);
            return DeltaResult.NoChange(false);
        }

        _transientClear = null;

        if (_pending is not null
            && rawText.Contains(_pending.RawText, StringComparison.Ordinal))
        {
            _pending = _pending with { LetterCount = _pending.LetterCount + addedLetterCount, RawText = rawText, LastUpdatedUtc = utcNow };
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
        DateTime utcNow,
        string? insertedTextSegment,
        DebugTextComparison? textComparison)
    {
        _pending = null;
        var committed = CharacterCountService.CountSupportedCharacters(rawText);
        var delta = committed - _lastCommittedCount;

        if (_transientClear is not null)
        {
            if (utcNow - _transientClear.DetectedUtc <= TransientClearWindow
                && delta >= TransientClearRestoreThreshold)
            {
                _transientClear = null;
                _lastRawText = rawText;
                _lastCommittedCount = committed;
                return DeltaResult.NoChange(false);
            }

            _transientClear = null;
        }

        if (_lastCommittedCount >= TransientClearSourceThreshold && committed <= 1 && delta < 0)
        {
            _transientClear = new TransientClear(_lastCommittedCount, utcNow);
        }

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

    private DeltaResult HandlePinyinSeparator(
        string rawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison,
        DateTime utcNow)
    {
        _transientClear = null;
        if (_pending is not null)
        {
            var addedSupportedSeparators = CountAddedPinyinSeparatorSupportedChars(_lastRawText, rawText);
            _pending = _pending with
            {
                RawText = rawText,
                LetterCount = _pending.LetterCount + addedSupportedSeparators,
                LastUpdatedUtc = utcNow,
            };
        }

        _lastRawText = rawText;
        return DeltaResult.NoChange(true);
    }

    private static int CountAddedPinyinSeparatorSupportedChars(string previousRawText, string currentRawText)
    {
        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);

        var addedLength = currentRawText.Length - prefixLength - suffixLength;
        var removedLength = previousRawText.Length - prefixLength - suffixLength;

        var addedSegment = addedLength > 0 ? currentRawText.Substring(prefixLength, addedLength) : string.Empty;
        var removedSegment = removedLength > 0 ? previousRawText.Substring(prefixLength, removedLength) : string.Empty;

        return CharacterCountService.CountSupportedCharacters(addedSegment)
             - CharacterCountService.CountSupportedCharacters(removedSegment);
    }

    private static bool IsOnlySpacesAdded(string previousRawText, string currentRawText)
    {
        if (currentRawText.Length <= previousRawText.Length)
        {
            return false;
        }

        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);
        var previousChangedLength = previousRawText.Length - prefixLength - suffixLength;
        if (previousChangedLength != 0)
        {
            return false;
        }

        var currentChangedLength = currentRawText.Length - prefixLength - suffixLength;
        var changedSegment = currentRawText.Substring(prefixLength, currentChangedLength);
        return changedSegment.Length > 0 && changedSegment.All(IsPinyinSeparator);
    }

    private static bool IsOnlySpacesRemoved(string previousRawText, string currentRawText)
    {
        if (currentRawText.Length >= previousRawText.Length)
        {
            return false;
        }

        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);
        var currentChangedLength = currentRawText.Length - prefixLength - suffixLength;
        if (currentChangedLength != 0)
        {
            return false;
        }

        var previousChangedLength = previousRawText.Length - prefixLength - suffixLength;
        var removedSegment = previousRawText.Substring(prefixLength, previousChangedLength);
        return removedSegment.Length > 0 && removedSegment.All(IsPinyinSeparator);
    }

    private static bool IsPinyinSeparator(char ch) => ch == ' ' || ch == '\'';

    private static bool TryDetectNewCompositionLetters(
        string previousRawText,
        string currentRawText,
        bool isNativeImeInputMode,
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
        if (changedSegment.Any(ch => !CharacterCountService.IsEnglishLetter(ch) && !(isNativeImeInputMode && IsPinyinSeparator(ch))))
        {
            return false;
        }

        newLetterCount = CharacterCountService.CountSupportedCharacters(changedSegment);
        return newLetterCount > 0;
    }

    private void Reset(string snapshotKey, string rawText)
    {
        _pending = null;
        _transientClear = null;
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

    private sealed record PendingComposition(string RawText, int LetterCount, DateTime LastUpdatedUtc);

    private sealed record TransientClear(int PreClearCommittedCount, DateTime DetectedUtc);
}
