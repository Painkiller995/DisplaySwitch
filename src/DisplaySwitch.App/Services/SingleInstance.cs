using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace DisplaySwitch.App.Services;

/// <summary>Requests a second launch can make of the running instance.</summary>
internal enum InstanceCommand : byte
{
    /// <summary>Show the main window.</summary>
    Activate = (byte)'A',

    /// <summary>Exit gracefully (used by the installer and uninstaller before replacing files).</summary>
    Quit = (byte)'Q',
}

/// <summary>
/// Keeps one instance per user. A second launch passes its request (show, or quit) to the running
/// instance over a per-user named pipe, then exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, string pipeName)
    {
        _mutex = mutex;
        _pipeName = pipeName;
    }

    /// <summary>
    /// Claims the instance. If another instance owns it, sends <paramref name="command"/> to it and
    /// returns <see langword="null"/>.
    /// </summary>
    public static SingleInstance? TryClaim(InstanceCommand command)
    {
        var name = InstanceName();
        var mutex = new Mutex(initiallyOwned: true, $"{name}.lock", out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex, name);
        }

        mutex.Dispose();
        Send(name, command);
        return null;
    }

    /// <summary>Invokes <paramref name="onCommand"/> (on a background thread) for each request from another launch.</summary>
    public void Listen(Action<InstanceCommand> onCommand)
    {
        ArgumentNullException.ThrowIfNull(onCommand);
        _ = Task.Run(async () =>
        {
            var buffer = new byte[1];
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(_stop.Token);
                    var read = await server.ReadAsync(buffer, _stop.Token);
                    // Older or unknown requests fall back to the safe default: show the window.
                    onCommand(read == 1 && Enum.IsDefined((InstanceCommand)buffer[0]) ? (InstanceCommand)buffer[0] : InstanceCommand.Activate);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // A client that disconnects early is harmless; keep listening.
                }
            }
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static void Send(string name, InstanceCommand command)
    {
        const int ConnectTimeoutMilliseconds = 2000;
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeoutMilliseconds);
            client.WriteByte((byte)command);
            client.Flush();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // The other instance is starting or exiting; nothing more to do.
        }
    }

    /// <summary>Per-user name, hashed so user names never appear in system-wide object names.</summary>
    private static string InstanceName()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName));
        return "DisplaySwitch-" + Convert.ToHexString(hash, 0, 8);
    }
}
