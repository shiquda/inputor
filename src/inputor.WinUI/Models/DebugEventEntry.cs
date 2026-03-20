namespace Inputor.App.Models;

public sealed class DebugEventEntry
{
    public required DateTime Timestamp { get; init; }
    public required string AppName { get; init; }
    public required string StatusMessage { get; init; }
    public required string ControlTypeName { get; init; }
    public required int Delta { get; init; }
    public required int InsertedSegmentLength { get; init; }
    public required int InsertedSupportedCharacterCount { get; init; }
    public required int InsertedChineseCharacterCount { get; init; }
    public required int InsertedEnglishLetterCount { get; init; }
    public required int InsertedOtherSupportedCharacterCount { get; init; }
    public required bool IsPendingComposition { get; init; }
    public required bool IsPaste { get; init; }
    public required bool IsBulkContentLoad { get; init; }
    public required bool IsNativeImeInputMode { get; init; }
    public required bool IsCurrentTargetSupported { get; init; }
    public DebugTextComparison? TextComparison { get; init; }
}
