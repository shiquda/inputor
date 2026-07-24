using System.Runtime.InteropServices;
using Inputor.WinUI;

namespace Inputor.App.Services;

public sealed class KeyboardActivityService : IDisposable
{
    private static readonly TimeSpan HookStartupTimeout = TimeSpan.FromSeconds(1);

    private const int WhKeyboardLl = 13;
    private const int HcAction = 0;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmQuit = 0x0012;
    private const uint PmNoRemove = 0x0000;
    private const uint LlkHfInjected = 0x00000010;
    private const uint VkControl = 0x11;
    private const uint VkLControl = 0xA2;
    private const uint VkRControl = 0xA3;
    private const uint VkShift = 0x10;
    private const uint VkLShift = 0xA0;
    private const uint VkRShift = 0xA1;
    private const uint VkMenu = 0x12;
    private const uint VkLMenu = 0xA4;
    private const uint VkRMenu = 0xA5;
    private const uint VkLWin = 0x5B;
    private const uint VkRWin = 0x5C;
    private const uint VkV = 0x56;
    private const uint VkInsert = 0x2D;

    private readonly object _syncRoot = new();
    private readonly ManualResetEventSlim _started = new(false);
    private readonly HookProcedure _hookProcedure;
    private Thread? _hookThread;
    private IntPtr _hookHandle;
    private uint _hookThreadId;
    private bool _isAvailable;
    private bool _isDisposed;
    private bool _stopRequested;
    private bool _isControlDown;
    private bool _isShiftDown;
    private bool _isAltDown;
    private bool _isWindowsDown;
    private uint _lastProcessId;
    private long _lastActivityTick;
    private InputAttributionService.ActivityKind _lastActivityKind;

    public KeyboardActivityService()
    {
        _hookProcedure = HookCallback;
    }

    public bool IsAvailable
    {
        get
        {
            lock (_syncRoot)
            {
                return _isAvailable;
            }
        }
    }

    public bool Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_hookThread is not null)
            {
                return _isAvailable;
            }

            _hookThread = new Thread(HookThreadMain)
            {
                IsBackground = true,
                Name = "inputor-keyboard-hook"
            };
            _hookThread.Start();
        }

        if (!_started.Wait(HookStartupTimeout))
        {
            StartupDiagnostics.Log("KeyboardActivityService hook startup timed out.");
            RequestStop();
            return false;
        }

        return IsAvailable;
    }

    public InputAttributionService.ActivityKind GetRecentActivity(uint processId, TimeSpan maximumAge)
    {
        lock (_syncRoot)
        {
            if (!_isAvailable)
            {
                return InputAttributionService.ActivityKind.Unavailable;
            }

            if (_lastProcessId != processId || _lastActivityTick == 0)
            {
                return InputAttributionService.ActivityKind.None;
            }

            var ageMilliseconds = Environment.TickCount64 - _lastActivityTick;
            if (ageMilliseconds < 0 || ageMilliseconds > maximumAge.TotalMilliseconds)
            {
                return InputAttributionService.ActivityKind.None;
            }

            return _lastActivityKind;
        }
    }

    public void Dispose()
    {
        Thread? hookThread;
        uint hookThreadId;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _stopRequested = true;
            hookThread = _hookThread;
            hookThreadId = _hookThreadId;
        }

        PostQuitMessage(hookThreadId);

        var stopped = hookThread?.Join(TimeSpan.FromSeconds(2)) ?? true;
        if (stopped)
        {
            _started.Dispose();
        }
        else
        {
            StartupDiagnostics.Log("KeyboardActivityService hook thread did not stop within the disposal timeout.");
        }
    }

    private void HookThreadMain()
    {
        try
        {
            var threadId = GetCurrentThreadId();
            _ = PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);

            lock (_syncRoot)
            {
                _hookThreadId = threadId;
                if (_stopRequested)
                {
                    SignalStarted();
                    return;
                }
            }

            var moduleHandle = GetModuleHandle(null);
            var hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProcedure, moduleHandle, 0);
            bool stopRequested;

            lock (_syncRoot)
            {
                _hookHandle = hookHandle;
                stopRequested = _stopRequested;
                _isAvailable = hookHandle != IntPtr.Zero && !stopRequested;
            }

            SignalStarted();
            if (hookHandle == IntPtr.Zero)
            {
                StartupDiagnostics.Log($"KeyboardActivityService hook installation failed with Win32 error {Marshal.GetLastWin32Error()}.");
                return;
            }

            if (stopRequested)
            {
                return;
            }

            int messageResult;
            while ((messageResult = GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
            {
                _ = TranslateMessage(ref message);
                _ = DispatchMessage(ref message);
            }

            if (messageResult < 0)
            {
                StartupDiagnostics.Log($"KeyboardActivityService message loop failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"KeyboardActivityService hook thread failed: {exception}");
            SignalStarted();
        }
        finally
        {
            IntPtr hookHandle;
            lock (_syncRoot)
            {
                hookHandle = _hookHandle;
                _hookHandle = IntPtr.Zero;
                _hookThreadId = 0;
                _isAvailable = false;
            }

            if (hookHandle != IntPtr.Zero)
            {
                if (!UnhookWindowsHookEx(hookHandle))
                {
                    StartupDiagnostics.Log($"KeyboardActivityService hook removal failed with Win32 error {Marshal.GetLastWin32Error()}.");
                }
            }
        }
    }

    private void RequestStop()
    {
        uint hookThreadId;
        lock (_syncRoot)
        {
            _stopRequested = true;
            hookThreadId = _hookThreadId;
        }

        PostQuitMessage(hookThreadId);
    }

    private static void PostQuitMessage(uint hookThreadId)
    {
        if (hookThreadId == 0)
        {
            return;
        }

        if (!PostThreadMessage(hookThreadId, WmQuit, UIntPtr.Zero, IntPtr.Zero))
        {
            StartupDiagnostics.Log($"KeyboardActivityService could not request hook shutdown: Win32 error {Marshal.GetLastWin32Error()}.");
        }
    }

    private void SignalStarted()
    {
        try
        {
            _started.Set();
        }
        catch (ObjectDisposedException)
        {
            StartupDiagnostics.Log("KeyboardActivityService startup signal was already disposed.");
        }
    }

    private IntPtr HookCallback(int code, UIntPtr message, IntPtr data)
    {
        if (code == HcAction)
        {
            try
            {
                var messageId = message.ToUInt32();
                var isKeyDown = messageId is WmKeyDown or WmSysKeyDown;
                var isKeyUp = messageId is WmKeyUp or WmSysKeyUp;
                if (isKeyDown || isKeyUp)
                {
                    var keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(data);
                    if ((keyboardData.Flags & LlkHfInjected) == 0)
                    {
                        ProcessKey(keyboardData.VirtualKeyCode, isKeyDown);
                    }
                }
            }
            catch (Exception exception)
            {
                StartupDiagnostics.Log($"KeyboardActivityService hook callback failed: {exception.Message}");
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    private void ProcessKey(uint virtualKeyCode, bool isKeyDown)
    {
        lock (_syncRoot)
        {
            if (IsControlKey(virtualKeyCode))
            {
                _isControlDown = isKeyDown;
                return;
            }

            if (IsShiftKey(virtualKeyCode))
            {
                _isShiftDown = isKeyDown;
                return;
            }

            if (IsAltKey(virtualKeyCode))
            {
                _isAltDown = isKeyDown;
                return;
            }

            if (IsWindowsKey(virtualKeyCode))
            {
                _isWindowsDown = isKeyDown;
                return;
            }

            if (!isKeyDown)
            {
                return;
            }

            RefreshModifierState();
            var activityKind = IsPasteShortcut(virtualKeyCode)
                ? InputAttributionService.ActivityKind.PasteShortcut
                : !_isControlDown && !_isAltDown && !_isWindowsDown && IsTextProducingKey(virtualKeyCode)
                    ? InputAttributionService.ActivityKind.Typing
                    : InputAttributionService.ActivityKind.None;
            if (activityKind == InputAttributionService.ActivityKind.None)
            {
                return;
            }

            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return;
            }

            _ = GetWindowThreadProcessId(foregroundWindow, out _lastProcessId);
            if (_lastProcessId == 0)
            {
                return;
            }

            _lastActivityTick = Environment.TickCount64;
            _lastActivityKind = activityKind;
        }
    }

    private bool IsPasteShortcut(uint virtualKeyCode)
    {
        return (_isControlDown && virtualKeyCode == VkV)
            || (_isShiftDown && virtualKeyCode == VkInsert);
    }

    private static bool IsControlKey(uint virtualKeyCode)
    {
        return virtualKeyCode is VkControl or VkLControl or VkRControl;
    }

    private static bool IsShiftKey(uint virtualKeyCode)
    {
        return virtualKeyCode is VkShift or VkLShift or VkRShift;
    }

    private static bool IsAltKey(uint virtualKeyCode)
    {
        return virtualKeyCode is VkMenu or VkLMenu or VkRMenu;
    }

    private static bool IsWindowsKey(uint virtualKeyCode)
    {
        return virtualKeyCode is VkLWin or VkRWin;
    }

    private void RefreshModifierState()
    {
        _isControlDown = IsKeyCurrentlyDown(VkControl);
        _isShiftDown = IsKeyCurrentlyDown(VkShift);
        _isAltDown = IsKeyCurrentlyDown(VkMenu);
        _isWindowsDown = IsKeyCurrentlyDown(VkLWin) || IsKeyCurrentlyDown(VkRWin);
    }

    private static bool IsKeyCurrentlyDown(uint virtualKeyCode)
    {
        return (GetAsyncKeyState((int)virtualKeyCode) & 0x8000) != 0;
    }

    private static bool IsTextProducingKey(uint virtualKeyCode)
    {
        return virtualKeyCode is >= 0x30 and <= 0x5A
            or >= 0x60 and <= 0x6F
            or 0x0D
            or 0x20
            or >= 0xBA and <= 0xE2;
    }

    private delegate IntPtr HookProcedure(int code, UIntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Value;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProcedure hookProcedure, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, UIntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out Message message, IntPtr window, uint minimumMessage, uint maximumMessage);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out Message message, IntPtr window, uint minimumMessage, uint maximumMessage, uint removeMessage);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
