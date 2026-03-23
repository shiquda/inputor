namespace Inputor.App.Services;

using Inputor.App.Models;

public sealed class CompositionAwareDeltaTracker
{
    private static readonly TimeSpan PendingConfirmationDelay = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan NativeImePendingConfirmationDelay = TimeSpan.FromSeconds(5);

    private PendingEnglishSegment? _pendingSegment;
    private string? _lastSnapshotKey;
    private string _lastRawText = string.Empty;
    private int _lastCommittedCount;

    public DeltaResult ProcessSnapshot(string snapshotKey, string rawText, DateTime utcNow, bool isNativeImeInputMode, bool includeTextComparison)
    {
        if (_lastSnapshotKey != snapshotKey)
        {
            var flushedDelta = _pendingSegment?.LetterCount ?? 0;
            Reset(snapshotKey, rawText);
            return flushedDelta > 0
                ? new DeltaResult(flushedDelta, false, null, null)
                : DeltaResult.NoChange(false);
        }

        var textComparison = includeTextComparison ? BuildTextComparison(_lastRawText, rawText) : null;
        var insertedTextSegment = GetCurrentChangedSegment(_lastRawText, rawText, textComparison);

        if (_pendingSegment is not null
            && TryProcessCompositionCommit(rawText, insertedTextSegment, textComparison, utcNow, isNativeImeInputMode, out var compositionCommitResult))
        {
            return compositionCommitResult;
        }

        if (_pendingSegment is not null && IsEnglishDeletionOnly(_lastRawText, rawText))
        {
            return HandleCompositionDeletion(rawText, insertedTextSegment, textComparison, utcNow);
        }

        UpdatePendingSegment(rawText, utcNow, isNativeImeInputMode);

        var effectiveCommittedCount = CharacterCountService.CountSupportedCharacters(rawText)
            - (_pendingSegment?.LetterCount ?? 0);
        var delta = effectiveCommittedCount - _lastCommittedCount;

        _lastRawText = rawText;
        _lastCommittedCount = effectiveCommittedCount;

        return new DeltaResult(delta, _pendingSegment is not null, insertedTextSegment, textComparison);
    }

    public void Reset()
    {
        _pendingSegment = null;
        _lastSnapshotKey = null;
        _lastRawText = string.Empty;
        _lastCommittedCount = 0;
    }

    private bool TryProcessCompositionCommit(
        string currentRawText,
        string? insertedTextSegment,
        DebugTextComparison? textComparison,
        DateTime utcNow,
        bool isNativeImeInputMode,
        out DeltaResult result)
    {
        result = DeltaResult.NoChange(false);

        var previousEnglish = CharacterCountService.CountEnglishLetters(_lastRawText);
        var currentEnglish = CharacterCountService.CountEnglishLetters(currentRawText);
        var previousChinese = CharacterCountService.CountChineseCharacters(_lastRawText);
        var currentChinese = CharacterCountService.CountChineseCharacters(currentRawText);
        var chineseIncrease = currentChinese - previousChinese;
        var englishRemoved = previousEnglish - currentEnglish;

        if (chineseIncrease <= 0 || englishRemoved <= 0)
        {
            return false;
        }

        var trailingPreeditCount = CountTrailingPreeditSupportedChars(currentRawText, isNativeImeInputMode);
        _pendingSegment = trailingPreeditCount > 0
            ? new PendingEnglishSegment(currentRawText, trailingPreeditCount, utcNow)
            : null;
        _lastRawText = currentRawText;
        _lastCommittedCount = CharacterCountService.CountSupportedCharacters(currentRawText) - trailingPreeditCount;
        result = new DeltaResult(chineseIncrease, _pendingSegment is not null, insertedTextSegment, textComparison);
        return true;
    }

    private void Reset(string snapshotKey, string rawText)
    {
        _pendingSegment = null;
        _lastSnapshotKey = snapshotKey;
        _lastRawText = rawText;
        _lastCommittedCount = CharacterCountService.CountSupportedCharacters(rawText);
    }

    private void UpdatePendingSegment(string currentRawText, DateTime utcNow, bool isNativeImeInputMode)
    {
        if (_pendingSegment is not null)
        {
            if (currentRawText == _pendingSegment.RawText)
            {
                var delay = isNativeImeInputMode ? NativeImePendingConfirmationDelay : PendingConfirmationDelay;
                if (utcNow - _pendingSegment.LastUpdatedUtc >= delay)
                {
                    _pendingSegment = null;
                }

                return;
            }

            if (LooksLikeCompositionCommitted(_lastRawText, currentRawText))
            {
                _pendingSegment = null;
                return;
            }
        }

        if (TryDetectPendingEnglishSegment(_lastRawText, currentRawText, isNativeImeInputMode, out var pendingLetterCount))
        {
            if (_pendingSegment is not null
                && currentRawText.Contains(_pendingSegment.RawText, StringComparison.Ordinal))
            {
                pendingLetterCount += _pendingSegment.LetterCount;
            }

            _pendingSegment = new PendingEnglishSegment(currentRawText, pendingLetterCount, utcNow);
        }
        else
        {
            _pendingSegment = null;
        }
    }

    private static bool LooksLikeCompositionCommitted(string previousRawText, string currentRawText)
    {
        var previousEnglish = CharacterCountService.CountEnglishLetters(previousRawText);
        var currentEnglish = CharacterCountService.CountEnglishLetters(currentRawText);
        var previousChinese = CharacterCountService.CountChineseCharacters(previousRawText);
        var currentChinese = CharacterCountService.CountChineseCharacters(currentRawText);

        return currentChinese > previousChinese && currentEnglish < previousEnglish;
    }

    private DeltaResult HandleCompositionDeletion(string rawText, string? insertedTextSegment, DebugTextComparison? textComparison, DateTime utcNow)
    {
        var deletedLetters = CharacterCountService.CountEnglishLetters(_lastRawText)
                           - CharacterCountService.CountEnglishLetters(rawText);
        var newLetterCount = Math.Max(0, _pendingSegment!.LetterCount - deletedLetters);

        _pendingSegment = newLetterCount > 0
            ? _pendingSegment with { LetterCount = newLetterCount, RawText = rawText, LastUpdatedUtc = utcNow }
            : null;

        var effectiveCommitted = CharacterCountService.CountSupportedCharacters(rawText) - newLetterCount;
        _lastRawText = rawText;
        _lastCommittedCount = effectiveCommitted;
        return DeltaResult.NoChange(_pendingSegment is not null);
    }

    private static bool IsEnglishDeletionOnly(string previousRawText, string currentRawText)
    {
        var previousLetters = CharacterCountService.CountEnglishLetters(previousRawText);
        var currentLetters = CharacterCountService.CountEnglishLetters(currentRawText);
        if (currentLetters >= previousLetters)
        {
            return false;
        }

        if (CharacterCountService.CountChineseCharacters(currentRawText)
            != CharacterCountService.CountChineseCharacters(previousRawText))
        {
            return false;
        }

        var previousOther = CharacterCountService.CountSupportedCharacters(previousRawText) - previousLetters;
        var currentOther = CharacterCountService.CountSupportedCharacters(currentRawText) - currentLetters;
        return currentOther == previousOther;
    }

    private static int CountTrailingPreeditSupportedChars(string rawText, bool isNativeImeInputMode)
    {
        var count = 0;
        for (var i = rawText.Length - 1; i >= 0; i--)
        {
            var ch = rawText[i];
            if (CharacterCountService.IsEnglishLetter(ch) || (isNativeImeInputMode && IsPinyinSeparator(ch)))
            {
                count += CharacterCountService.CountSupportedCharacters(ch.ToString());
                continue;
            }

            break;
        }

        return count;
    }

    private static bool TryDetectPendingEnglishSegment(string previousRawText, string currentRawText, bool isNativeImeInputMode, out int pendingLetterCount)
    {
        pendingLetterCount = 0;

        if (previousRawText == currentRawText)
        {
            return false;
        }

        var prefixLength = GetCommonPrefixLength(previousRawText, currentRawText);
        var suffixLength = GetCommonSuffixLength(previousRawText, currentRawText, prefixLength);

        var currentChangedStart = prefixLength;
        var currentChangedLength = currentRawText.Length - prefixLength - suffixLength;
        if (currentChangedLength <= 0)
        {
            return false;
        }

        var currentChangedSegment = currentRawText.Substring(currentChangedStart, currentChangedLength);
        if (currentChangedSegment.Any(ch => !CharacterCountService.IsEnglishLetter(ch) && !(isNativeImeInputMode && IsPinyinSeparator(ch))))
        {
            return false;
        }

        pendingLetterCount = CharacterCountService.CountSupportedCharacters(currentChangedSegment);
        return pendingLetterCount > 0;
    }

    private static bool IsPinyinSeparator(char ch) => ch == ' ' || ch == '\'';

    private static string? GetCurrentChangedSegment(string previousRawText, string currentRawText, DebugTextComparison? textComparison)
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

    private static DebugTextComparison? BuildTextComparison(string previousRawText, string currentRawText)
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

    public readonly record struct DeltaResult(int Delta, bool IsPendingComposition, string? InsertedTextSegment, DebugTextComparison? TextComparison)
    {
        public static DeltaResult NoChange(bool isPendingComposition) => new(0, isPendingComposition, null, null);
    }

    private sealed record PendingEnglishSegment(string RawText, int LetterCount, DateTime LastUpdatedUtc);
}
