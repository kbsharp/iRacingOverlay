using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IRacingOverlay.App.Controls;
using IRacingOverlay.App.Services;
using IRacingOverlay.App.ViewModels;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Core.Theme;
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
        ["standings", "relative", "relative-traffic", "fuel", "fuel-pit-exit", "fuel-save", "radar",
         "radar-danger", "radar-unresolved", "delta", "track-map", "track-map-learning", "settings"];

    [STAThread]
    private static int Main(string[] args)
    {
        if (!TryParseArgs(args, out var targets, out var outDir, out var colorBlind, out var grips, out var error))
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

        // --colorblind renders every widget in the colour-blind palette, so a
        // styling pass can compare it to the default (and run the PNGs through a
        // CVD simulation) rather than guess. Applied to the same live brushes the
        // running app swaps, so what renders is what ships.
        if (colorBlind)
        {
            PaletteService.Apply(app.Resources, PaletteVariant.ColorBlindFriendly);
        }

        var windows = BuildWindows(targets);
        if (windows.Count == 0)
        {
            Console.Error.WriteLine("Nothing to render.");
            return 1;
        }

        foreach (var (name, window) in windows)
        {
            // Resize grips are invisible until their widget is hovered, and nothing
            // hovers anything in a headless render - so --grips forces them on when
            // the corner treatment itself is what's being reviewed. A local value,
            // which outranks the style's resting Opacity setter.
            if (grips && window.Content is DependencyObject content)
            {
                RevealResizeGrips(content);
            }

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
        string[] args,
        out List<string> targets,
        out string outDir,
        out bool colorBlind,
        out bool grips,
        out string error)
    {
        targets = [];
        outDir = Path.Combine(Environment.CurrentDirectory, "out");
        colorBlind = false;
        grips = false;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "--colorblind" or "--colour-blind")
            {
                colorBlind = true;
                continue;
            }

            if (arg == "--grips")
            {
                grips = true;
                continue;
            }

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
        TrackMapViewModel? trackMap = null;

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

        if (targets.Contains("track-map"))
        {
            // Needs the same history the radar does: there is no circuit to draw
            // until a lap of the player's own heading has been learned.
            trackMap = new TrackMapViewModel("Demo");
        }

        var live = new OverlayViewModelBase?[] { standings, relative, fuel, radar, delta, trackMap }
            .Where(vm => vm is not null)
            .Select(vm => vm!)
            .ToList();

        if (live.Count > 0)
        {
            WarmUp(live, radar, trackMap, needsLaps: fuel is not null || delta is not null);
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

        if (trackMap is not null)
        {
            results.Add(("track-map", new IRacingOverlay.App.TrackMapWindow { DataContext = trackMap }));
        }

        if (targets.Contains("track-map-learning"))
        {
            results.Add(("track-map-learning", RenderTrackMapLearning()));
        }

        if (targets.Contains("radar-danger"))
        {
            results.Add(("radar-danger", RenderRadarDanger()));
        }

        if (targets.Contains("radar-unresolved"))
        {
            results.Add(("radar-unresolved", RenderRadarUnresolved()));
        }

        if (targets.Contains("fuel-pit-exit"))
        {
            results.Add(("fuel-pit-exit", RenderFuelPitExit()));
        }

        if (targets.Contains("fuel-save"))
        {
            results.Add(("fuel-save", RenderFuelSave()));
        }

        if (targets.Contains("relative-traffic"))
        {
            results.Add(("relative-traffic", RenderRelativeTraffic()));
        }

        return results;
    }

    /// <summary>
    /// Renders the relative with its multiclass traffic strip showing a
    /// multi-lap forecast.
    ///
    /// The plain "relative" target does show the strip, but only ever its "this
    /// lap" case: the demo runs a tight pack, so the nearest faster car is always
    /// right on the player's gearbox. The forecast that earns its keep - a car a
    /// couple of laps out, meeting the player in a sector it hasn't reached yet -
    /// needs a gap the demo doesn't produce.
    ///
    /// So this stages the missing input the same way the pit-exit render does:
    /// a real warmed demo frame, then one faster-class car placed a measured
    /// couple of seconds back with the rest of that class moved clear ahead, so
    /// the staged one is the threat shown. Everything downstream - the
    /// forecaster, the sector mapping, the bindings - is the real thing.
    /// </summary>
    private static Window RenderRelativeTraffic()
    {
        var vm = new RelativeViewModel("Demo");
        using var source = new SimulatedTelemetrySource();

        SessionMetadata? metadata = null;
        TelemetrySnapshot? latest = null;
        source.SessionMetadataReceived += (_, m) =>
        {
            metadata = m;
            vm.ApplySessionMetadata(m);
        };
        source.TelemetryReceived += (_, s) =>
        {
            latest = s;
            vm.ApplyTelemetry(s);
        };
        vm.SetConnectionState(true);
        source.Start();

        var started = DateTime.UtcNow;
        while ((latest is null || metadata is null) && DateTime.UtcNow - started < WarmupCap)
        {
            Thread.Sleep(50);
        }

        source.Stop();

        if (latest is not { } frame || metadata is null)
        {
            Console.Error.WriteLine("Warning: relative-traffic never received a demo frame.");
            return new IRacingOverlay.App.RelativeWindow { DataContext = vm };
        }

        vm.ApplyTelemetry(StageIncomingTraffic(frame, metadata));

        if (!vm.HasTraffic)
        {
            Console.Error.WriteLine(
                "Warning: relative-traffic expected a forecast, got none - "
                + "the demo may have no faster class to stage.");
        }

        return new IRacingOverlay.App.RelativeWindow { DataContext = vm };
    }

    /// <summary>
    /// Repositions the field so exactly one faster-class car is closing from a
    /// couple of laps back: it is placed a measured 1.6 laps behind (so it reads
    /// "next lap" and meets the player in a sector up the road), and every other
    /// faster-class car is pushed clear ahead so it can't be the nearer threat.
    /// Same-class and slower cars are left where the demo put them, so the pack
    /// around the player still looks natural.
    /// </summary>
    private static TelemetrySnapshot StageIncomingTraffic(
        TelemetrySnapshot frame, SessionMetadata metadata)
    {
        if (!metadata.DriversByCarIdx.TryGetValue(frame.PlayerCarIdx, out var playerDriver))
        {
            return frame;
        }

        var playerPace = playerDriver.ClassEstLapTimeSeconds;
        var playerClass = playerDriver.ClassShortName;

        double playerPct = 0.3, playerEst = 0;
        foreach (var car in frame.Cars)
        {
            if (car.CarIdx == frame.PlayerCarIdx)
            {
                playerPct = car.LapDistPct;
                playerEst = car.EstTimeSeconds;
            }
        }

        // The nearest-idx faster class is the one we bring into shot; the rest of
        // that class step aside.
        var chosen = -1;
        foreach (var car in frame.Cars.OrderBy(c => c.CarIdx))
        {
            if (car.CarIdx != frame.PlayerCarIdx
                && metadata.DriversByCarIdx.TryGetValue(car.CarIdx, out var d)
                && d.ClassShortName != playerClass
                && d.ClassEstLapTimeSeconds > 0
                && d.ClassEstLapTimeSeconds < playerPace)
            {
                chosen = car.CarIdx;
                break;
            }
        }

        const double targetLaps = 1.6;
        var cars = new List<CarTelemetry>(frame.Cars.Count);
        foreach (var car in frame.Cars)
        {
            if (car.CarIdx == frame.PlayerCarIdx
                || !metadata.DriversByCarIdx.TryGetValue(car.CarIdx, out var driver)
                || driver.ClassShortName == playerClass
                || driver.ClassEstLapTimeSeconds <= 0
                || driver.ClassEstLapTimeSeconds >= playerPace)
            {
                cars.Add(car); // player, same-class, or slower - leave it be
                continue;
            }

            var pace = driver.ClassEstLapTimeSeconds;

            if (car.CarIdx == chosen)
            {
                var gap = targetLaps * (playerPace - pace);
                var est = playerEst - gap;
                if (est < 0)
                {
                    est += pace; // just behind, across the line
                }

                cars.Add(car with
                {
                    LapDistPct = (float)(est / pace),
                    EstTimeSeconds = (float)est,
                    OnPitRoad = false,
                    Surface = CarTrackSurface.OnTrack,
                });
            }
            else
            {
                // Level on track but a few seconds up the road: same lap fraction
                // (so no start/finish wrap can flip the sign) with a positive
                // EstTime offset, which the forecaster reads as ahead and ignores.
                // Staggered so the stepped-aside cars don't stack on one delta.
                cars.Add(car with
                {
                    LapDistPct = (float)playerPct,
                    EstTimeSeconds = (float)(playerEst + 3 + (car.CarIdx * 0.5)),
                    OnPitRoad = false,
                    Surface = CarTrackSurface.OnTrack,
                });
            }
        }

        return frame with { Cars = cars };
    }

    /// <summary>
    /// Renders the fuel widget with its pit-exit projection showing.
    ///
    /// The plain "fuel" target can't produce it, and correctly so: the demo field
    /// parks one car in its box for the whole session and never cycles anyone
    /// through the lane, so <c>PitLossTracker</c> never sees a pit-road crossing
    /// and never learns what a stop costs - which means the strip stays hidden
    /// rather than guessing. That is the behaviour, not a gap in the demo.
    ///
    /// So this stages the missing input the only honest way: it takes a real warmed
    /// -up demo frame and replays it with three cars actually crossing in and back
    /// out of the lane, each losing about half a minute. Everything downstream is
    /// the real tracker, the real projector and the real bindings - only the pit
    /// stops are staged, because the demo has none to offer.
    /// </summary>
    private static Window RenderFuelPitExit()
    {
        var vm = new FuelViewModel(new FuelCalculator(), new LapTimeTracker(), "Demo");
        vm.ApplySettings(new OverlaySettings());

        using var source = new SimulatedTelemetrySource();

        TelemetrySnapshot? latest = null;
        source.SessionMetadataReceived += (_, m) => vm.ApplySessionMetadata(m);
        source.TelemetryReceived += (_, s) =>
        {
            latest = s;
            vm.ApplyTelemetry(s);
        };
        vm.SetConnectionState(true);
        source.Start();

        // Same warm-up the fuel widget always needs: the burn figures come off a
        // lap-over-lap calculator, so without laps the panel is placeholders.
        var started = DateTime.UtcNow;
        while (!vm.HasStrategy && DateTime.UtcNow - started < WarmupCap)
        {
            Thread.Sleep(100);
        }

        source.Stop();

        if (latest is not { } frame)
        {
            Console.Error.WriteLine("Warning: fuel-pit-exit never received a demo frame.");
            return new IRacingOverlay.App.FuelWindow { DataContext = vm };
        }

        // Three stops, because that is the tracker's threshold for trusting the
        // figure - one stop is anyone's bad day.
        foreach (var carIdx in PitStopCarIndexes(frame))
        {
            var before = FindF2(frame, carIdx);

            vm.ApplyTelemetry(WithCar(frame, carIdx, inLane: false, f2: before));
            vm.ApplyTelemetry(WithCar(frame, carIdx, inLane: true, f2: before));
            vm.ApplyTelemetry(WithCar(frame, carIdx, inLane: false, f2: before + 29));
        }

        // A last clean frame so the projection is computed against a settled field.
        vm.ApplyTelemetry(frame);

        if (!vm.HasPitExit)
        {
            Console.Error.WriteLine(
                "Warning: fuel-pit-exit expected a projection, got none - "
                + "the staged stops were not learned from.");
        }

        return new IRacingOverlay.App.FuelWindow { DataContext = vm };
    }

    /// <summary>
    /// Renders the fuel widget with its push-or-save strip showing.
    ///
    /// The demo can't produce this one either, and for two reasons that are both
    /// the feature working. It runs a comfortable timed race, so there is never a
    /// save to make; and every demo lap takes exactly the same time whatever it
    /// burns, so the save-cost tracker correctly concludes it has learned nothing.
    ///
    /// So the stint is staged: a real warmed-up demo frame supplies the session,
    /// the roster and the tank, then ten laps are replayed through it where burn
    /// and lap time move together the way they do in a car - lean laps slower,
    /// thirsty laps quicker - and the tank is left short of the finish. Three pit
    /// -road crossings supply the other half of the comparison. Everything
    /// downstream is the real tracker, planner, formatter and bindings.
    /// </summary>
    private static Window RenderFuelSave()
    {
        // 1.5s of lap time for every litre per lap, across a 2.2-2.7 L range.
        const double secondsPerLiter = 1.5;
        const double leanestBurn = 2.2;
        const double lapSecondsAtLeanest = 90;
        const int lapsRemaining = 14;

        var vm = new FuelViewModel(new FuelCalculator(), new LapTimeTracker(), "Demo");
        vm.ApplySettings(new OverlaySettings());

        if (CaptureDemoFrame(vm) is not { } frame)
        {
            Console.Error.WriteLine("Warning: fuel-save never received a demo frame.");
            return new IRacingOverlay.App.FuelWindow { DataContext = vm };
        }

        // Alternating rich and lean laps, so the burn range the fit needs is one a
        // driver would actually produce rather than a ramp.
        double[] burns = [2.5, 2.2, 2.6, 2.3, 2.7, 2.4, 2.5, 2.3, 2.6, 2.4];

        // Land on a tank that is short: 14 laps at ~2.45 L needs ~34 L, and 29.4 L
        // leaves a 2.1 L/lap save target - a real lift-and-coast, and well inside
        // the range these laps were driven over.
        var fuel = 29.4 + burns.Sum();
        var time = 0.0;
        var lap = 1;

        // The stop cost, staged the same way the pit-exit render does it - three
        // crossings, because that is the tracker's threshold.
        var atStart = Racing(frame, lap, fuel, time, lapsRemaining);
        foreach (var carIdx in PitStopCarIndexes(atStart))
        {
            var before = FindF2(atStart, carIdx);

            vm.ApplyTelemetry(WithCar(atStart, carIdx, inLane: false, f2: before));
            vm.ApplyTelemetry(WithCar(atStart, carIdx, inLane: true, f2: before));
            vm.ApplyTelemetry(WithCar(atStart, carIdx, inLane: false, f2: before + 29));
        }

        foreach (var burn in burns)
        {
            vm.ApplyTelemetry(Racing(frame, lap, fuel, time, lapsRemaining));

            lap++;
            time += lapSecondsAtLeanest - secondsPerLiter * (burn - leanestBurn);
            fuel -= burn;

            vm.ApplyTelemetry(Racing(frame, lap, fuel, time, lapsRemaining));
        }

        if (!vm.HasFuelSave)
        {
            Console.Error.WriteLine(
                "Warning: fuel-save expected a priced tradeoff, got none - "
                + "the staged stint taught the tracker nothing.");
        }

        return new IRacingOverlay.App.FuelWindow { DataContext = vm };
    }

    /// <summary>One staged racing frame: the demo's field and session, with the
    /// player's lap, tank and clock set, and a lap-limited race length so the
    /// strategy doesn't depend on the demo's four-minute countdown.</summary>
    private static TelemetrySnapshot Racing(
        TelemetrySnapshot frame, int lap, double fuel, double time, int lapsRemaining) =>
        frame with
        {
            Lap = lap,
            FuelLevelLiters = (float)fuel,
            SessionTimeSeconds = time,
            SessionLapsRemain = lapsRemaining,
        };

    /// <summary>Runs the demo just long enough to capture a frame and the session
    /// metadata (roster, classes, tank capacity), feeding both to the view model.</summary>
    private static TelemetrySnapshot? CaptureDemoFrame(FuelViewModel vm)
    {
        using var source = new SimulatedTelemetrySource();

        SessionMetadata? metadata = null;
        TelemetrySnapshot? latest = null;
        source.SessionMetadataReceived += (_, m) => metadata = m;
        source.TelemetryReceived += (_, s) => latest = s;
        vm.SetConnectionState(true);
        source.Start();

        var started = DateTime.UtcNow;
        while ((latest is null || metadata is null) && DateTime.UtcNow - started < WarmupCap)
        {
            Thread.Sleep(50);
        }

        source.Stop();

        if (metadata is not null)
        {
            vm.ApplySessionMetadata(metadata);
        }

        return latest;
    }

    /// <summary>Three cars that aren't the player and aren't already in the lane.</summary>
    private static List<int> PitStopCarIndexes(TelemetrySnapshot frame) =>
        frame.Cars
            .Where(c => c.CarIdx != frame.PlayerCarIdx
                && !c.OnPitRoad
                && c.Surface == CarTrackSurface.OnTrack)
            .Take(3)
            .Select(c => c.CarIdx)
            .ToList();

    private static float FindF2(TelemetrySnapshot frame, int carIdx)
    {
        foreach (var car in frame.Cars)
        {
            if (car.CarIdx == carIdx)
            {
                return Math.Max(car.F2TimeSeconds, 1f);
            }
        }

        return 1f;
    }

    /// <summary>The same frame with one car moved in or out of the pit lane.</summary>
    private static TelemetrySnapshot WithCar(
        TelemetrySnapshot frame, int carIdx, bool inLane, float f2)
    {
        var cars = new List<CarTelemetry>(frame.Cars.Count);
        foreach (var car in frame.Cars)
        {
            cars.Add(car.CarIdx == carIdx
                ? car with
                {
                    OnPitRoad = inLane,
                    Surface = inLane ? CarTrackSurface.InPitStall : CarTrackSurface.OnTrack,
                    F2TimeSeconds = f2,
                }
                : car);
        }

        return frame with { Cars = cars };
    }

    /// <summary>
    /// Drives one demo session, fanning every frame out to all the view models,
    /// until they've each seen enough: the radar has learned the track, and the
    /// lap-driven widgets (fuel's burn average, delta's reference lap) have laps
    /// behind them.
    /// </summary>
    private static void WarmUp(
        List<OverlayViewModelBase> viewModels,
        RadarViewModel? radar,
        TrackMapViewModel? trackMap,
        bool needsLaps)
    {
        using var source = new SimulatedTelemetrySource();

        source.SessionMetadataReceived += (_, m) =>
        {
            foreach (var vm in viewModels)
            {
                vm.ApplySessionMetadata(m);
            }
        };

        var frames = 0;
        source.TelemetryReceived += (_, s) =>
        {
            Interlocked.Increment(ref frames);
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
            var mapReady = trackMap is null || trackMap.HasOutline;

            // At least one frame, always. Widgets that need neither laps nor a
            // track map (standings, relative) otherwise satisfied both checks on
            // the first pass and were rendered before any telemetry arrived -
            // an empty panel that looks like a styling result rather than a bug.
            if (Volatile.Read(ref frames) > 0 && fuelReady && radarReady && mapReady)
            {
                break;
            }

            Thread.Sleep(100);
        }

        source.Stop();

        if (Volatile.Read(ref frames) == 0)
        {
            Console.Error.WriteLine(
                "Warning: no telemetry arrived during warm-up; widgets will render empty.");
        }

        if (radar is not null && !radar.ShowRadar)
        {
            Console.Error.WriteLine(
                "Warning: radar never mapped the track; rendering whatever state it reached.");
        }

        if (trackMap is not null && !trackMap.HasOutline)
        {
            Console.Error.WriteLine(
                "Warning: track-map never learned the circuit; rendering whatever state it reached.");
        }
    }

    /// <summary>
    /// Renders the track map's first-lap state: no circuit yet, so it shows how
    /// much of the lap it has learned instead. Half a demo lap of samples produces
    /// it directly - this is the widget's real behaviour on every out-lap, not a
    /// staged one.
    /// </summary>
    private static Window RenderTrackMapLearning()
    {
        var vm = new TrackMapViewModel("Demo");
        using var source = new SimulatedTelemetrySource();

        source.SessionMetadataReceived += (_, m) => vm.ApplySessionMetadata(m);
        source.TelemetryReceived += (_, s) => vm.ApplyTelemetry(s);
        vm.SetConnectionState(true);
        source.Start();

        // Demo laps are 15s; a few seconds is a believable fraction of an out-lap.
        Thread.Sleep(4000);
        source.Stop();

        if (vm.HasOutline)
        {
            Console.Error.WriteLine(
                "Warning: track-map-learning expected a part-learned track, got a finished one.");
        }

        return new IRacingOverlay.App.TrackMapWindow { DataContext = vm };
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

    /// <summary>
    /// Renders the positional radar's other honest state: the track is mapped, but
    /// the cars level with the player are stacked on the centreline, where the walk
    /// can't say which side they are. The demo field runs nose-to-tail, so cycling
    /// the spotter to CarLeft while the map is ready produces exactly that - dimmed
    /// blips plus a left glow, rather than a confident placement or an empty mirror.
    /// </summary>
    private static Window RenderRadarUnresolved()
    {
        var vm = new RadarViewModel("Demo");
        using var source = new SimulatedTelemetrySource();

        source.SessionMetadataReceived += (_, m) => vm.ApplySessionMetadata(m);
        source.TelemetryReceived += (_, s) => vm.ApplyTelemetry(s);
        vm.SetConnectionState(true);
        source.Start();

        var started = DateTime.UtcNow;
        while (!vm.ShowRadar && DateTime.UtcNow - started < WarmupCap)
        {
            Thread.Sleep(100);
        }

        var state = CarLeftRight.Off;
        for (var i = 0; i < 50 && state != CarLeftRight.CarLeft; i++)
        {
            state = source.CycleCarLeftRight();
        }

        Thread.Sleep(500);
        source.Stop();

        if (!vm.ShowRadar || vm.LeftDanger <= 0)
        {
            Console.Error.WriteLine(
                $"Warning: radar-unresolved expected a mapped radar with a left glow, got "
                + $"mapped={vm.MapReady} left={vm.LeftDanger:0.00}.");
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

    /// <summary>Walks the logical tree and shows every resize grip it finds.</summary>
    private static void RevealResizeGrips(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is WidgetResizeGrip grip)
            {
                grip.Opacity = 1;
            }

            RevealResizeGrips(child);
        }
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
