using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Core.Switching;
using DisplaySwitch.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplaySwitch.Core.Tests;

public class MonitorControllerTests
{
    private static readonly InputSource DisplayPort = new() { Code = new InputCode(0x0F), PortName = "DisplayPort", ReadAliases = [new InputCode(0x03)] };
    private static readonly InputSource Hdmi1 = new() { Code = new InputCode(0x11), PortName = "HDMI 1" };
    private static readonly InputSource Hdmi2 = new() { Code = new InputCode(0x12), PortName = "HDMI 2" };
    private static readonly IReadOnlyList<InputSource> AllInputs = [DisplayPort, Hdmi1, Hdmi2];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verified_when_monitor_reports_the_new_input()
    {
        var fake = new FakeDdcMonitor(0x0F);
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(Hdmi1, AllInputs, Token);

        Assert.Equal(SwitchOutcome.Verified, result.Outcome);
        Assert.True(result.IsConfirmedSuccess);
        Assert.Equal([0x11], fake.Writes);
    }

    [Fact]
    public async Task Read_aliases_count_as_the_input()
    {
        var fake = new FakeDdcMonitor(0x11);
        fake.ReportedAfterWrite[0x0F] = 0x03;
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(DisplayPort, AllInputs, Token);

        Assert.Equal(SwitchOutcome.Verified, result.Outcome);
        Assert.Equal(new InputCode(0x03), result.ReportedInput);
    }

    [Fact]
    public async Task Skips_write_when_already_on_the_input()
    {
        var fake = new FakeDdcMonitor(0x03);
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(DisplayPort, AllInputs, Token);

        Assert.Equal(SwitchOutcome.AlreadyActive, result.Outcome);
        Assert.Empty(fake.Writes);
    }

    [Fact]
    public async Task Rejects_codes_that_are_not_configured()
    {
        var fake = new FakeDdcMonitor();
        using var controller = Create(fake);
        var arbitrary = new InputSource { Code = new InputCode(0x99), PortName = "Unknown" };

        var result = await controller.SwitchAsync(arbitrary, AllInputs, Token);

        Assert.Equal(SwitchOutcome.Rejected, result.Outcome);
        Assert.Empty(fake.Writes);
    }

    [Fact]
    public async Task Reports_not_confirmed_when_monitor_ignores_the_command()
    {
        var fake = new FakeDdcMonitor(0x03);
        fake.IgnoredCodes.Add(0x11);
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(Hdmi1, AllInputs, Token);

        Assert.Equal(SwitchOutcome.NotConfirmed, result.Outcome);
        Assert.False(result.IsConfirmedSuccess);
        Assert.Equal(new InputCode(0x03), result.ReportedInput);
    }

    [Fact]
    public async Task Reports_unconfirmed_when_monitor_goes_silent_after_switching()
    {
        var fake = new FakeDdcMonitor(0x0F) { StopRespondingAfterWrite = true };
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(Hdmi1, AllInputs, Token);

        Assert.Equal(SwitchOutcome.SentWithoutConfirmation, result.Outcome);
        Assert.False(result.IsConfirmedSuccess);
    }

    [Fact]
    public async Task Reports_failure_when_monitor_rejects_the_write()
    {
        var fake = new FakeDdcMonitor(0x0F) { RejectWrites = true };
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(Hdmi1, AllInputs, Token);

        Assert.Equal(SwitchOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Retries_transient_errors()
    {
        var fake = new FakeDdcMonitor(0x0F);
        fake.UpcomingFailures.Enqueue(DdcError.InvalidReply);
        fake.UpcomingFailures.Enqueue(DdcError.NoResponse);
        using var controller = Create(fake);

        var result = await controller.SwitchAsync(Hdmi1, AllInputs, Token);

        Assert.Equal(SwitchOutcome.Verified, result.Outcome);
    }

    [Fact]
    public async Task Rapid_requests_settle_on_the_last_one()
    {
        var fake = new FakeDdcMonitor(0x0F) { CallDuration = TimeSpan.FromMilliseconds(15) };
        using var controller = Create(fake);

        var first = controller.SwitchAsync(Hdmi1, AllInputs, Token);
        var second = controller.SwitchAsync(Hdmi2, AllInputs, Token);
        var results = await Task.WhenAll(first, second);

        Assert.Equal(SwitchOutcome.Superseded, results[0].Outcome);
        Assert.Equal(SwitchOutcome.Verified, results[1].Outcome);
        Assert.Equal(0x12, fake.CurrentInput);
        Assert.Equal(1, fake.MaxObservedConcurrency);
    }

    [Fact]
    public async Task Preview_always_returns_to_the_original_input()
    {
        var fake = new FakeDdcMonitor(0x0F);
        using var controller = Create(fake);

        var returned = await controller.PreviewInputAsync(new InputCode(0x1B), DisplayPort, TimeSpan.FromMilliseconds(5), Token);

        Assert.True(returned);
        Assert.Equal([0x1B, 0x0F], fake.Writes);
    }

    [Fact]
    public async Task Switching_a_retired_monitor_fails_cleanly()
    {
        var controller = Create(new FakeDdcMonitor(0x0F));
        controller.Dispose();

        var result = await controller.SwitchAsync(Hdmi1, AllInputs, Token);

        Assert.Equal(SwitchOutcome.Failed, result.Outcome);
        Assert.Contains("reconnected", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static MonitorController Create(FakeDdcMonitor fake) =>
        new(
            new MonitorChannel(fake, TestTiming.Fast, NullLogger.Instance),
            MonitorSupport.Ready,
            MonitorCapabilities.Empty,
            AllInputs,
            null,
            TestTiming.Fast,
            NullLogger.Instance);
}

public class MonitorChannelTests
{
    [Fact]
    public async Task A_hung_call_times_out_without_overlapping_the_next_call()
    {
        var fake = new FakeDdcMonitor { CallDuration = TimeSpan.FromMilliseconds(300) };
        var timing = TestTiming.Fast with { CommandTimeout = TimeSpan.FromMilliseconds(50), MaxAttempts = 2 };
        using var channel = new MonitorChannel(fake, timing, NullLogger.Instance);

        var reading = await channel.TryReadInputAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading);
        Assert.Equal(1, fake.MaxObservedConcurrency);
    }

    [Fact]
    public async Task Never_writes_the_reserved_zero_code()
    {
        using var channel = new MonitorChannel(new FakeDdcMonitor(), TestTiming.Fast, NullLogger.Instance);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => channel.WriteInputAsync(default, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_zero_reading_is_retried_as_garbled()
    {
        using var channel = new MonitorChannel(new FakeDdcMonitor(0x00), TestTiming.Fast, NullLogger.Instance);

        Assert.Null(await channel.TryReadInputAsync(TestContext.Current.CancellationToken));
    }
}

public class MonitorDirectoryTests
{
    [Fact]
    public async Task Discovers_controllable_monitor_and_caches_capabilities()
    {
        var fake = new FakeDdcMonitor(0x0F) { Capabilities = "(vcp(60(0F 11)))" };
        var cache = new MemoryCache();
        using var directory = new MonitorDirectory(new FakeDisplayBackend(fake), cache, TestTiming.Fast, NullLoggerFactory.Instance);

        await directory.RefreshAsync(forceCapabilities: false, TestContext.Current.CancellationToken);

        var monitor = Assert.Single(directory.Monitors);
        Assert.Equal(MonitorSupport.Ready, monitor.Support);
        Assert.Equal(["DisplayPort 1", "HDMI 1"], monitor.DetectedInputs.Select(i => i.PortName));
        Assert.Equal("(vcp(60(0F 11)))", cache.Find("TST0001"));
    }

    [Fact]
    public async Task Marks_silent_monitor_as_without_ddc()
    {
        var fake = new FakeDdcMonitor();
        for (var i = 0; i < 20; i++)
        {
            fake.UpcomingFailures.Enqueue(DdcError.NoResponse);
        }

        using var directory = new MonitorDirectory(new FakeDisplayBackend(fake), new MemoryCache(), TestTiming.Fast, NullLoggerFactory.Instance);

        await directory.RefreshAsync(false, TestContext.Current.CancellationToken);

        Assert.Equal(MonitorSupport.NoDdc, Assert.Single(directory.Monitors).Support);
    }

    [Fact]
    public void Built_in_profile_overrides_misleading_capabilities()
    {
        var identity = new MonitorIdentity("SAM0F21", "SAM0F21", "C49J89x", ConnectionKind.DisplayPort, 3840, 1080);
        var capabilities = CapabilitiesParser.Parse("(vcp(60(01 03)))");

        var inputs = MonitorDirectory.DetectInputs(identity, capabilities, new InputCode(0x03));

        Assert.Equal(["DisplayPort", "HDMI"], inputs.Select(i => i.PortName));
        Assert.Equal(new InputCode(0x03), inputs[0].Code);
        Assert.Equal(new InputCode(0x01), inputs[1].Code);
    }

    [Fact]
    public void Always_lists_the_currently_reported_input()
    {
        var identity = new MonitorIdentity("ABC0001", "ABC0001", "Monitor", ConnectionKind.Hdmi, 0, 0);

        var inputs = MonitorDirectory.DetectInputs(identity, MonitorCapabilities.Empty, new InputCode(0x11));

        Assert.Equal("HDMI 1", Assert.Single(inputs).PortName);
    }

    private sealed class MemoryCache : ICapabilitiesCache
    {
        private readonly Dictionary<string, string> _entries = [];

        public string? Find(string monitorKey) => _entries.GetValueOrDefault(monitorKey);

        public void Store(string monitorKey, string capabilities) => _entries[monitorKey] = capabilities;
    }
}
