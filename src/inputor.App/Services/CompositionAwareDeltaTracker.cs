namespace Inputor.App.Services;

public sealed class CompositionAwareDeltaTracker
{
    private static readonly TimeSpan PendingConfirmationDelay = TimeSpan.FromMilliseconds(700);

    private PendingEnglishSegment? _pendingSegment;
    private string? _lastSnapshotKey;
    private string _lastRawText = string.Empty;
    private int _lastCommittedCount;

    public DeltaResult ProcessSnapshot(string snapshotKey, string rawText, DateTime utcNow)
    {
        if (_lastSnapshotKey != snapshotKey)
        {
            Reset(snapshotKey, rawText);
            return DeltaResult.NoChange(false);
        }

        var insertedTextSegment = GetCurrentChangedSegment(_lastRawText, rawText);

        if (_pendingSegment is not null
            && TryProcessCompositionCommit(rawText, insertedTextSegment, out var compositionCommitResult))
        {
            return compositionCommitResult;
        }

        UpdatePendingSegment(rawText, utcNow);

        var effectiveCommittedCount = CharacterCountService.CountSupportedCharacters(rawText)
            - (_pendingSegment?.LetterCount ?? 0);
        var delta = effectiveCommittedCount - _lastCommittedCount;

        _lastRawText = rawText;
        _lastCommittedCount = effectiveCommittedCount;

        return new DeltaResult(delta, _pendingSegment is not null, insertedTextSegment);
    }

    private bool TryProcessCompositionCommit(string currentRawText, string? insertedTextSegment, out DeltaResult result)
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

        _pendingSegment = null;
        _lastRawText = currentRawText;
        _lastCommittedCount = CharacterCountService.CountSupportedCharacters(currentRawText);
        result = new DeltaResult(chineseIncrease, false, insertedTextSegment);
        return true;
    }

    public void Reset()
    {
        _pendingSegment = null;
        _lastSnapshotKey = null;
        _lastRawText = string.Empty;
        _lastCommittedCount = 0;
    }

    private void Reset(string snapshotKey, string rawText)
    {
        _pendingSegment = null;
        _lastSnapshotKey = snapshotKey;
        _lastRawText = rawText;
        _lastCommittedCount = CharacterCountService.CountSupportedCharacters(rawText);
    }

    private void UpdatePendingSegment(string currentRawText, DateTime utcNow)
    {
        if (_pendingSegment is not null)
        {
            if (currentRawText == _pendingSegment.RawText)
            {
                if (utcNow - _pendingSegment.FirstSeenUtc >= PendingConfirmationDelay)
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

        if (TryDetectPendingEnglishSegment(_lastRawText, currentRawText, out var pendingLetterCount))
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

    private static bool TryDetectPendingEnglishSegment(string previousRawText, string currentRawText, out int pendingLetterCount)
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
        if (currentChangedSegment.Any(ch => !CharacterCountService.IsEnglishLetter(ch)))
        {
            return false;
        }

        pendingLetterCount = currentChangedSegment.Count(ch => CharacterCountService.IsEnglishLetter(ch));
        return pendingLetterCount > 0;
    }

    private static string? GetCurrentChangedSegment(string previousRawText, string currentRawText)
    {
        if (previousRawText == currentRawText)
        {
            return null;
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

    public readonly record struct DeltaResult(int Delta, bool IsPendingComposition, string? InsertedTextSegment)
    {
        public static DeltaResult NoChange(bool isPendingComposition) => new(0, isPendingComposition, null);
    }

    private sealed record PendingEnglishSegment(string RawText, int LetterCount, DateTime FirstSeenUtc);
}
