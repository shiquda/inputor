using Microsoft.UI.Xaml.Controls;

namespace Inputor.WinUI;

internal sealed class UserFeedbackEventArgs : EventArgs
{
    public required InfoBarSeverity Severity { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public string? ActionLabel { get; init; }

    public Action? Action { get; init; }
}
