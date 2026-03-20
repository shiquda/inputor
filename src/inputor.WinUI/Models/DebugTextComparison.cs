namespace Inputor.App.Models;

public sealed class DebugTextComparison
{
    public required int ChangeStartIndex { get; init; }
    public required int PreviousTextLength { get; init; }
    public required int CurrentTextLength { get; init; }
    public required int PreviousSegmentLength { get; init; }
    public required int CurrentSegmentLength { get; init; }
    public required int PreviousSupportedCharacterCount { get; init; }
    public required int PreviousChineseCharacterCount { get; init; }
    public required int PreviousEnglishLetterCount { get; init; }
    public required int CurrentSupportedCharacterCount { get; init; }
    public required int CurrentChineseCharacterCount { get; init; }
    public required int CurrentEnglishLetterCount { get; init; }
    public required string PreviousPreviewMask { get; init; }
    public required string CurrentPreviewMask { get; init; }
}
