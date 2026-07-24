using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;

namespace Inputor.App.Services;

public static class InputAttributionService
{
    public static readonly TimeSpan RecentTypingWindow = TimeSpan.FromSeconds(2);

    public static AttributionDecision Evaluate(bool isEditableTarget, ActivityKind activityKind)
    {
        if (!isEditableTarget)
        {
            return AttributionDecision.RejectNotEditable;
        }

        return activityKind switch
        {
            ActivityKind.Unavailable => AttributionDecision.CountUsingFallback,
            ActivityKind.Typing => AttributionDecision.Count,
            ActivityKind.PasteShortcut => AttributionDecision.RejectPaste,
            _ => AttributionDecision.RejectNoRecentTyping
        };
    }

    public static bool IsEditableTarget(AutomationElement element)
    {
        try
        {
            var controlType = element.Properties.ControlType.ValueOrDefault;
            var isReadOnly = TryGetIsReadOnly(element);

            if (controlType == ControlType.Edit)
            {
                return isReadOnly is not true;
            }

            if (controlType != ControlType.Document)
            {
                return false;
            }

            var hasKeyboardFocus = element.Properties.HasKeyboardFocus.TryGetValue(out var hasFocus)
                && hasFocus;
            var isKeyboardFocusable = element.Properties.IsKeyboardFocusable.TryGetValue(out var isFocusable)
                && isFocusable;
            return hasKeyboardFocus && isKeyboardFocusable && isReadOnly is false;
        }
        catch
        {
            return false;
        }
    }

    private static bool? TryGetIsReadOnly(AutomationElement element)
    {
        if (element.Patterns.Value.IsSupported
            && element.Patterns.Value.Pattern.IsReadOnly.TryGetValue(out var valueIsReadOnly))
        {
            return valueIsReadOnly;
        }

        if (element.Patterns.LegacyIAccessible.IsSupported
            && element.Patterns.LegacyIAccessible.Pattern.State.TryGetValue(out var state))
        {
            return (state & AccessibilityState.STATE_SYSTEM_READONLY) != 0;
        }

        return null;
    }

    public enum ActivityKind
    {
        None,
        Typing,
        PasteShortcut,
        Unavailable
    }

    public enum AttributionDecision
    {
        Count,
        CountUsingFallback,
        RejectNotEditable,
        RejectNoRecentTyping,
        RejectPaste
    }
}
