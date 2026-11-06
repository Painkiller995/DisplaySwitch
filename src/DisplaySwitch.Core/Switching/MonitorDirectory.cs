using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Core.Profiles;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Core.Switching;

/// <summary>Remembers capabilities strings between sessions. They are static per monitor model and slow to read.</summary>
public interface ICapabilitiesCache
{
    string? Find(string monitorKey);

    void Store(string monitorKey, string capabilities);
}

/// <summary>Discovers attached monitors and owns their controllers.</summary>
public sealed partial class MonitorDirectory : IDisposable
{
    private readonly IDisplayBackend _backend;
    private readonly ICapabilitiesCache _capabilitiesCache;
    private readonly DdcTiming _timing;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public MonitorDirectory(IDisplayBackend backend, ICapabilitiesCache capabilitiesCache, DdcTiming timing, ILoggerFactory loggerFactory)
    {
        _backend = backend;
        _capabilitiesCache = capabilitiesCache;
        _timing = timing;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MonitorDirectory>();
    }

    public IReadOnlyList<MonitorController> Monitors { get; private set; } = [];

    /// <summary>A platform-level problem from the most recent discovery, written for end users.</summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// Rediscovers monitors. Concurrent calls share one discovery. Previous controllers are disposed
    /// after the new set is published, so in-flight operations on them finish safely.
    /// </summary>
    /// <param name="forceCapabilities">Ignore cached capabilities and query every monitor again.</param>
    /// <param name="cancellationToken">Cancels discovery.</param>
    public async Task RefreshAsync(bool forceCapabilities, CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var discovery = await Task.Run(_backend.Discover, cancellationToken);
            var controllers = new List<MonitorController>(discovery.Monitors.Count);
            try
            {
                foreach (var monitor in discovery.Monitors)
                {
                    controllers.Add(await CreateControllerAsync(monitor, forceCapabilities, cancellationToken));
                }
            }
            catch
            {
                DisposeAll(controllers);
                foreach (var monitor in discovery.Monitors.Skip(controllers.Count))
                {
                    monitor.Dispose();
                }

                throw;
            }

            var previous = Monitors;
            Monitors = controllers;
            Problem = discovery.Problem;
            LogDiscovered(controllers.Count, Problem);
            _ = Task.Run(() => DisposeAll(previous), CancellationToken.None);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public void Dispose()
    {
        DisposeAll(Monitors);
        Monitors = [];
        _refreshGate.Dispose();
    }

    private async Task<MonitorController> CreateControllerAsync(IDdcMonitor monitor, bool forceCapabilities, CancellationToken cancellationToken)
    {
        var channel = new MonitorChannel(monitor, _timing, _loggerFactory.CreateLogger<MonitorChannel>());
        var identity = monitor.Identity;
        var controllerLogger = _loggerFactory.CreateLogger<MonitorController>();
        if (!channel.SupportsDdc)
        {
            return new MonitorController(channel, MonitorSupport.NoDdc, MonitorCapabilities.Empty, [], null, _timing, controllerLogger);
        }

        var reported = await channel.TryReadInputAsync(cancellationToken);
        var capabilities = await LoadCapabilitiesAsync(channel, identity.Key, forceCapabilities, cancellationToken);
        var support = reported is not null || capabilities.SupportsInputSelect ? MonitorSupport.Ready
            : capabilities.VcpFeatures.Count > 0 ? MonitorSupport.NoInputControl
            : MonitorSupport.NoDdc;

        var inputs = support == MonitorSupport.Ready ? DetectInputs(identity, capabilities, reported) : [];
        return new MonitorController(channel, support, capabilities, inputs, reported, _timing, controllerLogger);
    }

    private async Task<MonitorCapabilities> LoadCapabilitiesAsync(MonitorChannel channel, string key, bool force, CancellationToken cancellationToken)
    {
        var raw = force ? null : _capabilitiesCache.Find(key);
        if (raw is null)
        {
            raw = await channel.TryReadCapabilitiesAsync(cancellationToken);
            if (raw is not null)
            {
                _capabilitiesCache.Store(key, raw);
            }
        }

        return CapabilitiesParser.Parse(raw);
    }

    /// <summary>
    /// Picks the default input list: a verified built-in profile first, because capabilities strings
    /// are often wrong. Then the advertised inputs, and finally the currently reported input so at
    /// least the active input is always listed.
    /// </summary>
    internal static IReadOnlyList<InputSource> DetectInputs(MonitorIdentity identity, MonitorCapabilities capabilities, InputCode? reported)
    {
        if (MonitorProfiles.Find(identity.HardwareId) is { } profile)
        {
            return profile.Inputs;
        }

        var inputs = capabilities.AdvertisedInputs.Select(MccsInputCatalog.Describe).ToList();
        if (reported is { } active && !inputs.Any(i => i.IsReportedAs(active)))
        {
            inputs.Add(MccsInputCatalog.Describe(active));
        }

        return inputs;
    }

    private static void DisposeAll(IEnumerable<MonitorController> controllers)
    {
        foreach (var controller in controllers)
        {
            controller.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered {Count} monitor(s). Platform problem: {Problem}")]
    private partial void LogDiscovered(int count, string? problem);
}
