using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IRacingOverlay.App.ViewModels;
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
/// Renders a widget window offscreen to a PNG, driven by the demo telemetry
/// source, so a headless session can review a styling change instead of
/// guessing at it. Screen-capturing the running app doesn't work: the widgets
/// are ShowInTaskbar="False" / WindowStyle="None" and have no taskbar entry.
///
///   dotnet run --project tools/RenderWidget                      # standings
///   dotnet run --project tools/RenderWidget -- relative out.png
/// </summary>
internal static class Program
{
    private const double Scale = 2.0; // 192 DPI - small badge/caption text is unreadable at 1x.

    [STAThread]
    private static int Main(string[] args)
    {
        var widget = args.Length > 0 ? args[0].ToLowerInvariant() : "standings";
        var outPath = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.Combine(Environment.CurrentDirectory, $"{widget}.png");

        // Constructing App loads App.xaml's resources (fonts, brushes, badge
        // styles) via InitializeComponent. It ALSO queues App.OnStartup on this
        // dispatcher - so never pump the dispatcher below (no Show(), no
        // Dispatcher.Invoke). Pumping runs the real composition root, which
        // constructs UpdateService and dies on "No VelopackLocator has been set".
        // Layout is driven manually instead; StaticResource lookups still resolve
        // because the resources live on Application.Current.Resources.
        var app = new IRacingOverlay.App.App();
        app.InitializeComponent();

        if (!TryCaptureDemoFrame(out var snapshot, out var metadata))
        {
            Console.Error.WriteLine("Failed to capture a frame from the demo telemetry source.");
            return 1;
        }

        var window = BuildWindow(widget, snapshot!, metadata!);
        if (window is null)
        {
            Console.Error.WriteLine($"Unknown widget '{widget}'. Known: standings, relative, settings.");
            return 1;
        }

        RenderToPng((FrameworkElement)window.Content, outPath);
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }

    /// <summary>Runs the demo source until it has produced one telemetry frame and a roster.</summary>
    private static bool TryCaptureDemoFrame(out TelemetrySnapshot? snapshot, out SessionMetadata? metadata)
    {
        TelemetrySnapshot? snap = null;
        SessionMetadata? meta = null;

        using var source = new SimulatedTelemetrySource();
        source.TelemetryReceived += (_, s) => snap = s;
        source.SessionMetadataReceived += (_, m) => meta = m;
        source.Start();

        for (var i = 0; i < 400 && (snap is null || meta is null); i++)
        {
            Thread.Sleep(10);
        }

        source.Stop();
        snapshot = snap;
        metadata = meta;
        return snap is not null && meta is not null;
    }

    // Add a case here to render another widget - each is just "view model, feed
    // it the frame, hand it to its window".
    private static Window? BuildWindow(string widget, TelemetrySnapshot snapshot, SessionMetadata metadata)
    {
        switch (widget)
        {
            case "standings":
            {
                var vm = new StandingsViewModel("Demo");
                vm.ApplySessionMetadata(metadata);
                vm.SetConnectionState(true);
                vm.ApplyTelemetry(snapshot);
                return new IRacingOverlay.App.StandingsWindow { DataContext = vm };
            }

            case "relative":
            {
                var vm = new RelativeViewModel("Demo");
                vm.ApplySessionMetadata(metadata);
                vm.SetConnectionState(true);
                vm.ApplyTelemetry(snapshot);
                return new IRacingOverlay.App.RelativeWindow { DataContext = vm };
            }

            case "settings":
            {
                // The settings window isn't telemetry-driven, but it does need a
                // real SettingsService - which reads the user's actual
                // settings.json. That's read-only here: nothing in this harness
                // calls a setter, so no save is ever scheduled.
                var settings = new IRacingOverlay.App.Services.SettingsService();
                var widgets = WidgetIds.All
                    .Select(id => (id, DisplayName: id.Replace("Window", string.Empty)))
                    .ToList();

                return new IRacingOverlay.App.SettingsWindow
                {
                    DataContext = new SettingsViewModel(settings, widgets),
                };
            }

            default:
                return null;
        }
    }

    private static void RenderToPng(FrameworkElement content, string outPath)
    {
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        content.Arrange(new Rect(content.DesiredSize));
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
