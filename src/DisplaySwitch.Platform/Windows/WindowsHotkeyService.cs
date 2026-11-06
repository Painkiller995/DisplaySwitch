using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Platform.Windows;

/// <summary>
/// Global shortcuts through <c>RegisterHotKey</c>, running on a dedicated message-loop thread.
/// </summary>
/// <remarks>
/// Hotkeys registered without a window are delivered to the registering thread's message queue,
/// so a lightweight thread that only pumps messages is enough: no hidden window and no coupling to
/// the UI framework's message loop. Registration calls are marshaled onto that thread.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsHotkeyService : IGlobalHotkeyService
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<Action> _pendingWork = new();
    private readonly Dictionary<int, HotkeyGesture> _registered = [];
    private readonly Thread _thread;
    private readonly TaskCompletionSource<uint> _threadId = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    public WindowsHotkeyService(ILogger<WindowsHotkeyService> logger)
    {
        _logger = logger;
        _thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "DisplaySwitch hotkeys" };
        _thread.Start();
    }

    public bool IsSupported => true;

    public event EventHandler<HotkeyGesture>? Pressed;

    public async Task<IReadOnlySet<HotkeyGesture>> RegisterAsync(IReadOnlyCollection<HotkeyGesture> gestures)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(gestures);

        var completion = new TaskCompletionSource<IReadOnlySet<HotkeyGesture>>(TaskCreationOptions.RunContinuationsAsynchronously);
        await PostAsync(() =>
        {
            try
            {
                completion.SetResult(ReplaceRegistrations(gestures));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return await completion.Task;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_threadId.Task.IsCompletedSuccessfully)
        {
            User32.PostThreadMessage(_threadId.Task.Result, User32.WmQuit, 0, 0);
            _thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    private async Task PostAsync(Action work)
    {
        var threadId = await _threadId.Task;
        _pendingWork.Enqueue(work);
        if (!User32.PostThreadMessage(threadId, User32.WmApp, 0, 0))
        {
            throw new InvalidOperationException($"The hotkey thread is not accepting messages (Win32 error {Marshal.GetLastPInvokeError()}).");
        }
    }

    private void RunMessageLoop()
    {
        // Touching the queue forces Windows to create it before anyone can post to this thread.
        User32.PeekMessage(out _, 0, 0, 0, User32.PmNoRemove);
        _threadId.SetResult(Kernel32.GetCurrentThreadId());

        while (User32.GetMessage(out var message, 0, 0, 0) > 0)
        {
            switch (message.Message)
            {
                case User32.WmApp:
                    while (_pendingWork.TryDequeue(out var work))
                    {
                        work();
                    }

                    break;

                case User32.WmHotkey when _registered.TryGetValue((int)message.WParam, out var gesture):
                    Pressed?.Invoke(this, gesture);
                    break;
            }
        }

        ReplaceRegistrations([]);
    }

    private HashSet<HotkeyGesture> ReplaceRegistrations(IReadOnlyCollection<HotkeyGesture> gestures)
    {
        foreach (var id in _registered.Keys)
        {
            User32.UnregisterHotKey(0, id);
        }

        _registered.Clear();
        var failed = new HashSet<HotkeyGesture>();
        var nextId = 1;
        foreach (var gesture in gestures.Distinct())
        {
            if (!gesture.IsValid || !User32.RegisterHotKey(0, nextId, ToNativeModifiers(gesture.Modifiers), ToVirtualKey(gesture.Key)))
            {
                LogRegistrationFailed(gesture.ToString(), Marshal.GetLastPInvokeError());
                failed.Add(gesture);
                continue;
            }

            _registered[nextId++] = gesture;
        }

        return failed;
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers) =>
        User32.ModNoRepeat
        | (modifiers.HasFlag(HotkeyModifiers.Control) ? User32.ModControl : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Alt) ? User32.ModAlt : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Shift) ? User32.ModShift : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Meta) ? User32.ModWin : 0);

    /// <summary>Virtual-key codes: '0'–'9' and 'A'–'Z' equal their ASCII values, VK_F1 is 0x70.</summary>
    private static uint ToVirtualKey(HotkeyKey key) => key switch
    {
        >= HotkeyKey.D0 and <= HotkeyKey.D9 => '0' + (uint)(key - HotkeyKey.D0),
        >= HotkeyKey.A and <= HotkeyKey.Z => 'A' + (uint)(key - HotkeyKey.A),
        >= HotkeyKey.F1 and <= HotkeyKey.F12 => 0x70 + (uint)(key - HotkeyKey.F1),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported hotkey key."),
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not register global shortcut {Gesture} (Win32 error {Error})")]
    private partial void LogRegistrationFailed(string gesture, int error);
}
