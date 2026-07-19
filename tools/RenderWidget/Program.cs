using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IRacingOverlay.App.ViewModels;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Infrastructure.Telemetry;
// App has UseWindowsForms on, so these names are ambiguous across the two SDKs'
// global usings - same workaround the App project itself needs (see CLAUDE.md).
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;

namespace IRacingOverlay.Tools.RenderWidget;

/// <summary>
/// Renders widget windows offscreen to PNGs, driven by the demo telemetry
/// source, so a headless session can review a styling change instead of
/// guessing at it. Screen-capturing the running app doesn't work: the widgets
/// are ShowInTaskbar="False" / WindowStyle="None" and have no taskbar entry.
///
///   dotnet run --project tools/RenderWidget                    # every widget -> out/
///   dotnet run --project tools/RenderWidget -- fuel relative   # just these two
///   dotnet run --project tools/RenderWidget -- --out img fuel  # somewhere else
///
/// Rendering everything costs barely more than rendering one: all the view models
/// are fed from a single demo session rather than one run each.
/// </summary>
internal static class Program
{
    private const double Scale = 2.0; // 192 DPI - small badge/caption text is unreadable at 1x.

    /// <summary>Demo laps are 15s; the fuel burn average needs a few of them.</summary>
    private static readonly TimeSpan FuelWarmup = TimeSpan.FromSeconds(35);

    private static readonly TimeSpan WarmupCap = TimeSpan.FromSeconds(75);

    /// <summary>
    /// Every renderable target. "radar-danger" is a second view of the radar
    /// rather than a separate widget - see <see cref="RenderRadarDanger"/>.
    /// </summary>
    private static readonly string[] AllTargets =
        ["standings", "relative", "fuel", "radar", "radar-danger", "delta", "settings"];

    [STAThread]
    private static int Main(string[] args)
    {
        if (!TryParseArgs(args, out var targets, out var outDir, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine($"Known targets: {string.Join(", ", AllTargets)}");
            return 1;
        }

        // Constructing App loads App.xaml's resources (fonts, brushes, badge
        // styles) via InitializeComponent. It ALSO queues App.OnStartup on this
        // dispatcher - so never pump the dispatcher below (no Show(), no
        // Dispatcher.Invoke). Pumping runs the real composition root, which
        // constructs UpdateService and dies on "No VelopackLocator has been set".
        // Layout is driven manually instead; StaticResource lookups still resolve
        // because the resources live on Application.Current.Resources.
        var app = new IRacingOverlay.App.App();
        app.InitializeComponent();

        var windows = BuildWindows(targets);
        if (windows.Count == 0)
        {
            Console.Error.WriteLine("Nothing to render.");
            return 1;
        }

        foreach (var (name, window) in windows)
        {
            var path = Path.Combine(outDir, $"{name}.png");
            // The borderless widgets size themselves to their content, so they
            // render at their natural size. A window that declares a Width/Height
            // (the settings window) is rendered at that size instead - measured
            // unconstrained, its columns sprawl to the width of the longest hint
            // string and the PNG says nothing about how it actually looks.
            RenderToPng((FrameworkElement)window.Content, path,
                new Size(window.Width, window.Height));
            Console.WriteLine($"Wrote {path}");
        }

        return 0;
    }

    private static bool TryParseArgs(
        string[] args, out List<string> targets, out string outDir, out string error)
    {
        targets = [];
        outDir = Path.Combine(Environment.CurrentDirectory, "out");
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "--out" or "-o")
            {
                if (i + 1 >= args.Length)
                {
                    error = "--out needs a directory.";
                    return false;
                }

                outDir = Path.GetFullPath(args[++i]);
                continue;
            }

            if (arg == "--all")
            {
                targets.AddRange(AllTargets);
                continue;
            }

            var name = arg.ToLowerInvariant();
            if (!AllTargets.Contains(name))
            {
                error = $"Unknown target '{arg}'.";
                return false;
            }

            targets.Add(name);
        }

        // No target named: render the lot. Rendering everything is the common
        // case (a theme or typography change touches all of them) and costs
        // barely more than one, so it's the default rather than an opt-in.
        if (targets.Count == 0)
        {
            targets.AddRange(AllTargets);
        }

        targets = targets.Distinct().ToList();
        outDir = Path.GetFullPath(outDir);
        return true;
    }

    /// <summary>
    /// Builds every requested window, sharing one demo session across all the
    /// telemetry-driven view models. They're warmed together because the slow
    /// part is wall-clock demo laps, not the rendering.
    /// </summary>
    private static List<(string Name, Window Window)> BuildWindows(List<string> targets)
    {
        var results = new List<(string, Window)>();

        // The settings window isn't telemetry-driven, so it's built outside the
        // demo session entirely.
        var telemetryTargets = targets.Where(t => t != "settings").ToList();

        if (telemetryTargets.Count > 0)
        {
            results.AddRange(RenderTelemetryWidgets(telemetryTargets));
        }

        if (targets.Contains("settings"))
        {
            results.Add(("settings", BuildSettingsWindow()));
        }

        return results;
    }

    private static List<(string Name, Window Window)> RenderTelemetryWidgets(List<string> targets)
    {
        var results = new List<(string, Window)>();

        StandingsViewModel? standings = null;
        RelativeViewModel? relative = null;
        FuelViewModel? fuel = null;
        RadarViewModel? radar = null;
        DeltaViewModel? delta = null;

        if (targets.Contains("standings"))
        {
            standings = new StandingsViewModel("Demo");
        }

        if (targets.Contains("relative"))
        {
            relative = new RelativeViewModel("Demo");
        }

        if (targets.Contains("fuel"))
        {
            // The burn figures come off a lap-over-lap calculator, so a single
            // snapshot renders a panel of placeholders and tells you nothing
            // about the layout - hence the warmup below.
            fuel = new FuelViewModel(new FuelCalculator(), new LapTimeTracker(), "Demo");
            fuel.ApplySettings(new OverlaySettings());
        }

        if (targets.Contains("radar"))
        {
            // Unlike the others the radar needs *history*: it shows nothing until it
            // has learned the track from a lap of the player's own heading.
            radar = new RadarViewModel("Demo");
        }

        if (targets.Contains("delta"))
        {
            // Like the fuel widget, this needs laps rather than a frame: there is
            // no reference lap - and so no delta at all - until the player has
            // completed one, which the shared warm-up below covers.
            delta = new DeltaViewModel("Demo");
        }

        var live = new OverlayViewModelBase?[] { standings, relative, fuel, radar, delta }
            .Where(vm => vm is not null)
            .Select(vm => vm!)
            .ToList();

        if (live.Count > 0)
        {
            WarmUp(live, radar, needsLaps: fuel is not null || delta is not null);
        }

        if (standings is not null)
        {
            results.Add(("standings", new IRacingOverlay.App.StandingsWindow { DataContext = standings }));
        }

        if (relative is not null)
        {
            results.Add(("relative", new IRacingOverlay.App.RelativeWindow { DataContext = relative }));
        }

        if (fuel is not null)
        {
            results.Add(("fuel", new IRacingOverlay.App.FuelWindow { DataContext = fuel }));
        }

        if (radar is not null)
        {
            results.Add(("radar", new IRacingOverlay.App.RadarWindow { DataContext = radar }));
        }

        if (delta is not null)
        {
            results.Add(("delta", new IRacingOverlay.App.DeltaWindow { DataContext = delta }));
        }

        if (targets.Contains("radar-danger"))
        {
            results.Add(("radar-danger", RenderRadarDanger()));
        }

        return results;
    }

    /// <summary>
    /// Drives one demo session, fanning every frame out to all the view models,
    /// until they've each seen enough: the radar has learned the track, and the
    /// lap-driven widgets (fuel's burn average, delta's reference lap) have laps
    /// behind them.
    /// </summary>
    private static void WarmUp(
        List<OverlayViewModelBase> viewModels, RadarViewModel? radar, bool needsLaps)
    {
        using var source = new SimulatedTelemetrySource();

        source.SessionMetadataReceived += (_, m) =>
        {
            foreach (var vm in viewModels)
            {
                vm.ApplySessionMetadata(m);
            }
        };

        source.TelemetryReceived += (_, s) =>
        {
            foreach (var vm in viewModels)
            {
                vm.ApplyTelemetry(s);
            }
        };

        foreach (var vm in viewModels)
        {
            vm.SetConnectionState(true);
        }

        source.Start();

        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < WarmupCap)
        {
            var elapsed = DateTime.UtcNow - started;
            var fuelReady = !needsLaps || elapsed >= FuelWarmup;
            var radarReady = radar is null || radar.ShowRadar;

            if (fuelReady && radarReady)
            {
                break;
            }

            Thread.Sleep(100);
        }

        source.Stop();

        if (radar is not null && !radar.ShowRadar)
        {
            Console.Error.WriteLine(
                "Warning: radar never mapped the track; rendering whatever state it reached.");
        }
    }

    /// <summary>
    /// Renders the radar's red proximity glow at full strength.
    ///
    /// The glow can't be produced from demo traffic: the demo field runs
    /// nose-to-tail at zero lateral offset, which <c>RadarDanger</c> correctly
    /// reads as queued traffic rather than a side-by-side. So this drives the
    /// *spotter fallback* instead - iRacing's coarse CarLeftRight signal, which
    /// the view model maps to a full-strength glow before the track is mapped.
    /// The glow ellipses live outside both the positional and fallback subtrees
    /// in RadarWindow.xaml, so this is the real thing rendered by the real
    /// binding, not a mock.
    ///
    /// What this does NOT show is the *graded* glow the positional path
    /// produces (a car drifting away fading out). That needs real side-by-side
    /// geometry, which means the sim.
    /// </summary>
    private static Window RenderRadarDanger()
    {
        var vm = new RadarViewModel("Demo");
        using var source = new SimulatedTelemetrySource();

        source.SessionMetadataReceived += (_, m) => vm.ApplySessionMetadata(m);
        source.TelemetryReceived += (_, s) => vm.ApplyTelemetry(s);
        vm.SetConnectionState(true);
        source.Start();

        // Cycle the spotter signal to CarLeftRight (Clear -> CarLeft -> CarRight
        // -> CarLeftRight) so both glows light. Deliberately kept short: this
        // state only exists before the track map is ready.
        var state = CarLeftRight.Off;
        for (var i = 0; i < 50 && state != CarLeftRight.CarLeftRight; i++)
        {
            state = source.CycleCarLeftRight();
        }

        Thread.Sleep(500);
        source.Stop();

        if (vm.LeftDanger <= 0 || vm.RightDanger <= 0)
        {
            Console.Error.WriteLine(
                $"Warning: radar-danger expected both glows lit, got "
                + $"left={vm.LeftDanger:0.00} right={vm.RightDanger:0.00}.");
        }

        return new IRacingOverlay.App.RadarWindow { DataContext = vm };
    }

    private static Window BuildSettingsWindow()
    {
        // The settings window isn't telemetry-driven, but it does need a real
        // SettingsService - which reads the user's actual settings.json. That's
        // read-only here: nothing in this harness calls a setter, so no save is
        // ever scheduled.
        // isInstalled: false - read the source-build settings file, never the one
        // an installed copy uses for real racing.
        var settings = new IRacingOverlay.App.Services.SettingsService(isInstalled: false);
        var widgets = WidgetIds.All
            .Select(id => (id, DisplayName: id.Replace("Window", string.Empty)))
            .ToList();

        return new IRacingOverlay.App.SettingsWindow
        {
            DataContext = new SettingsViewModel(settings, widgets),
        };
    }

    private static void RenderToPng(FrameworkElement content, string outPath, Size declared)
    {
        // A NaN dimension means the window declared none, so that axis measures
        // against infinity and the content decides its own size.
        var available = new Size(
            double.IsNaN(declared.Width) ? double.PositiveInfinity : declared.Width,
            double.IsNaN(declared.Height) ? double.PositiveInfinity : declared.Height);

        content.Measure(available);
        content.Arrange(new Rect(new Size(
            double.IsInfinity(available.Width) ? content.DesiredSize.Width : available.Width,
            double.IsInfinity(available.Height) ? content.DesiredSize.Height : available.Height)));
        content.UpdateLayout();

        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);

        // Draw over an opaque rect first: the panel material is near-opaque, and
        // against undefined transparency its contrast can't be judged fairly.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x30, 0x34, 0x3A)), null,
                new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, width, height));
        }

        // RenderTargetBitmap uses greyscale antialiasing exactly as the live
        // AllowsTransparency windows do, so text weight comes out faithful.
        var bitmap = new RenderTargetBitmap(
            (int)(width * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var stream = File.Create(outPath);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }
}
