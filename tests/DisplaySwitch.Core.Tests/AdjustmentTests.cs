using DisplaySwitch.Core.Adjustments;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Switching;
using DisplaySwitch.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplaySwitch.Core.Tests;

public class PictureAdjustmentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Loads_supported_features_and_skips_unsupported_ones()
    {
        var fake = new FakeDdcMonitor();
        fake.Features[VcpCodes.Brightness] = (30, 100);
        var adjustments = Create(fake);

        await adjustments.LoadAsync(Token);

        Assert.True(adjustments.IsLoaded);
        Assert.Equal(new AdjustableFeature(VcpCodes.Brightness, 30, 100), adjustments.Brightness);
        Assert.Null(adjustments.ColorTemperature);
    }

    [Fact]
    public async Task Probes_only_advertised_features_when_capabilities_are_known()
    {
        var fake = new FakeDdcMonitor();
        fake.Features[VcpCodes.Brightness] = (30, 100);
        fake.Features[VcpCodes.ColorTemperatureRequest] = (40, 170);
        var adjustments = Create(fake, CapabilitiesParser.Parse("(vcp(10 60(0F)))"));

        await adjustments.LoadAsync(Token);

        Assert.NotNull(adjustments.Brightness);
        Assert.Null(adjustments.ColorTemperature);
    }

    [Fact]
    public async Task Converts_color_temperature_to_kelvin_only_when_the_step_is_reported()
    {
        var withStep = new FakeDdcMonitor();
        withStep.Features[VcpCodes.ColorTemperatureRequest] = (40, 170);
        withStep.Features[VcpCodes.ColorTemperatureIncrement] = (50, 50);
        var withoutStep = new FakeDdcMonitor();
        withoutStep.Features[VcpCodes.ColorTemperatureRequest] = (40, 170);
        var known = Create(withStep);
        var unknown = Create(withoutStep);

        await known.LoadAsync(Token);
        await unknown.LoadAsync(Token);

        Assert.Equal(5000, known.ToKelvin(40));
        Assert.Equal(11500, known.ToKelvin(170));
        Assert.Null(unknown.ToKelvin(40));
    }

    [Fact]
    public async Task Ignores_features_with_no_range()
    {
        var fake = new FakeDdcMonitor();
        fake.Features[VcpCodes.Brightness] = (0, 0);
        var adjustments = Create(fake);

        await adjustments.LoadAsync(Token);

        Assert.Null(adjustments.Brightness);
    }

    [Fact]
    public async Task Applied_change_is_verified_by_read_back()
    {
        var fake = new FakeDdcMonitor();
        fake.Features[VcpCodes.Brightness] = (30, 100);
        var adjustments = await LoadedAsync(fake);

        var result = await adjustments.SetAsync(adjustments.Brightness!, 75);

        Assert.Equal(AdjustmentOutcome.Applied, result.Outcome);
        Assert.Equal(75, result.Feature.Value);
        Assert.Equal(75, adjustments.Brightness!.Value);
    }

    [Fact]
    public async Task Values_are_clamped_to_the_reported_range()
    {
        var fake = new FakeDdcMonitor();
        fake.Features[VcpCodes.Brightness] = (30, 100);
        var adjustments = await LoadedAsync(fake);

        await adjustments.SetAsync(adjustments.Brightness!, 250);
        await adjustments.SetAsync(adjustments.Brightness!, -5);

        Assert.Equal([(VcpCodes.Brightness, (ushort)100), (VcpCodes.Brightness, (ushort)0)], fake.FeatureWrites);
    }

    [Fact]
    public async Task Ignored_change_is_reported_as_not_applied()
    {
        var adjustments = await LoadedAsync(FakeDdcMonitor.SamsungC49J89x());

        var result = await adjustments.SetAsync(adjustments.ColorTemperature!, 60);

        Assert.Equal(AdjustmentOutcome.NotApplied, result.Outcome);
        Assert.Equal(40, result.Feature.Value);
    }

    [Fact]
    public async Task Rapid_changes_write_the_latest_value_without_overlapping()
    {
        var fake = new FakeDdcMonitor { CallDuration = TimeSpan.FromMilliseconds(20) };
        fake.Features[VcpCodes.Brightness] = (0, 100);
        var adjustments = await LoadedAsync(fake);
        var feature = adjustments.Brightness!;

        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(v => adjustments.SetAsync(feature, v * 5)));

        Assert.Equal(100, fake.Features[VcpCodes.Brightness].Current);
        Assert.True(fake.FeatureWrites.Count < 20, $"Expected coalescing, got {fake.FeatureWrites.Count} writes.");
        Assert.Equal(1, fake.MaxObservedConcurrency);
        Assert.Equal(AdjustmentOutcome.Applied, results[^1].Outcome);
        Assert.All(results[..^1], r => Assert.Equal(AdjustmentOutcome.Superseded, r.Outcome));
    }

    [Fact]
    public async Task Failed_write_reports_failure_and_later_writes_still_work()
    {
        var fake = new FakeDdcMonitor();
        fake.Features[VcpCodes.Brightness] = (30, 100);
        var adjustments = await LoadedAsync(fake);
        fake.RejectWrites = true;

        var failed = await adjustments.SetAsync(adjustments.Brightness!, 80);
        fake.RejectWrites = false;
        var retried = await adjustments.SetAsync(adjustments.Brightness!, 80);

        Assert.Equal(AdjustmentOutcome.Failed, failed.Outcome);
        Assert.Equal(30, failed.Feature.Value);
        Assert.Equal(AdjustmentOutcome.Applied, retried.Outcome);
    }

    private static PictureAdjustments Create(FakeDdcMonitor fake, MonitorCapabilities? capabilities = null) =>
        new(new MonitorChannel(fake, TestTiming.Fast, NullLogger.Instance), capabilities ?? MonitorCapabilities.Empty, NullLogger.Instance);

    private static async Task<PictureAdjustments> LoadedAsync(FakeDdcMonitor fake)
    {
        var adjustments = Create(fake);
        await adjustments.LoadAsync(TestContext.Current.CancellationToken);
        return adjustments;
    }
}
