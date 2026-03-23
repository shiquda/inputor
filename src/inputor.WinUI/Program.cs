using System.Threading;
using System.Runtime.InteropServices;
using Inputor.App.Models;
using Inputor.App.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Inputor.WinUI;

internal static class Program
{
    private static App? _app;

    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.Log("Program.Main entered.");
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            StartupDiagnostics.Log($"AppDomain.CurrentDomain.UnhandledException: {eventArgs.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            StartupDiagnostics.Log($"TaskScheduler.UnobservedTaskException: {eventArgs.Exception}");
        };

        if (TryHandleCli(args))
        {
            StartupDiagnostics.Log("CLI mode handled, exiting Program.Main.");
            return;
        }

        XamlCheckProcessRequirements();
        StartupDiagnostics.Log("XamlCheckProcessRequirements completed.");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        StartupDiagnostics.Log("COM wrappers initialized.");
        try
        {
            Application.Start(_initParams =>
            {
                StartupDiagnostics.Log("Application.Start callback entered.");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App();
                StartupDiagnostics.Log("App instance created and stored.");
            });
            StartupDiagnostics.Log("Application.Start returned.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Program.Main catch: {ex}");
            throw;
        }
    }

    private static bool TryHandleCli(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--count-sample")
        {
            Console.WriteLine(CharacterCountService.CountSupportedCharacters(args[1]));
            return true;
        }

        if (args.Length >= 2 && args[0] == "--simulate-sequence")
        {
            var tracker = new CompositionAwareDeltaTracker();
            var now = DateTime.UtcNow;
            var isNativeImeInputMode = args.Length >= 3 && bool.TryParse(args[2], out var parsedImeMode) && parsedImeMode;
            foreach (var sample in args[1].Split('|', StringSplitOptions.None))
            {
                var result = tracker.ProcessSnapshot("sample", sample, now, isNativeImeInputMode, false);
                Console.WriteLine($"{sample} => delta={result.Delta}, pending={result.IsPendingComposition}");
                now = now.AddMilliseconds(500);
            }

            return true;
        }

        if (args.Length >= 4 && args[0] == "--simulate-paste")
        {
            var tracker = new CompositionAwareDeltaTracker();
            var now = DateTime.UtcNow;
            _ = tracker.ProcessSnapshot("sample", args[1], now, false, false);
            var result = tracker.ProcessSnapshot("sample", args[2], now.AddMilliseconds(500), false, false);
            var isPaste = PasteDetectionService.LooksLikePaste(result.InsertedTextSegment, args[3]);
            Console.WriteLine($"delta={result.Delta}, pending={result.IsPendingComposition}, inserted={result.InsertedTextSegment}, paste={isPaste}");
            return true;
        }

        if (args.Length >= 5 && args[0] == "--simulate-bulk")
        {
            var processName = args.Length >= 6 ? args[5] : "sample";
            var delta = int.Parse(args[1]);
            var inserted = args[2];
            var controlTypeName = args[3];
            var isPaste = bool.Parse(args[4]);
            var textComparison = args.Length >= 8
                ? BuildDebugTextComparison(args[6], args[7])
                : null;
            Console.WriteLine(BulkLoadDetectionService.LooksLikeBulkContentLoad(processName, delta, inserted, textComparison, controlTypeName, isPaste));
            return true;
        }

        if (args.Length >= 1 && args[0] == "--print-storage-paths")
        {
            Console.WriteLine($"channel={AppVariant.ChannelName}");
            Console.WriteLine($"data={AppVariant.GetDataDirectory()}");
            Console.WriteLine($"icons={AppVariant.GetIconCacheDirectory()}");
            Console.WriteLine($"exports={AppVariant.GetExportDirectory()}");
            Console.WriteLine($"backups={AppVariant.GetBackupDirectory()}");
            Console.WriteLine($"autostart={AppVariant.AutoStartEntryName}");
            Console.WriteLine($"startup-log={StartupDiagnostics.GetLogPath()}");
            return true;
        }

        return false;
    }

    private static DebugTextComparison? BuildDebugTextComparison(string previousRawText, string currentRawText)
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
            PreviousText = previousChangedSegment,
            CurrentText = currentChangedSegment,
        };
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
}
