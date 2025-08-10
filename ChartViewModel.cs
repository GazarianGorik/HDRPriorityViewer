using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;

namespace WwiseHDRTool;

public class ChartViewModel : INotifyPropertyChanged
{
    private ObservableCollection<ISeries> _series;
    private ScatterSeries<ErrorPoint> _mainSeries;

    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    public ObservableCollection<ISeries> Series
    {
        get => _series;
        set
        {
            _series = value;
            OnPropertyChanged();
        }
    }

    public ChartViewModel()
    {
        _mainSeries = new ScatterSeries<ErrorPoint>
        {
            Values = new ObservableCollection<ErrorPoint>(),
            Fill = new SolidColorPaint(SKColors.Red) { ZIndex = 10 },
            Stroke = new SolidColorPaint(SKColors.Black) { StrokeThickness = 1.5f },
            ErrorPaint = new SolidColorPaint(LightenColor(SKColors.Red, 0.1f, 0.6f)) { StrokeThickness = 5 },
            GeometrySize = 15,
            YToolTipLabelFormatter = point =>
            {
                if (point.Context.DataSource is ErrorPoint errorPoint)
                {
                    var myMeta = errorPoint.MetaData as PointMetaData;
                    return myMeta?.Name.Replace("\r", "").Replace("\n", "") ?? "Unknown";
                }
                return "Unknown";
            },
            ZIndex = 10
        };

        // Abonnement à l'événement clic
        _mainSeries.ChartPointPointerDown += async (chart, point) =>
        {
            Console.WriteLine($"Point cliqué !");
            await OnClickPointOpenWwiseElement(point);
        };

        Series = new ObservableCollection<ISeries>
        {
            _mainSeries
        };
    }

    public async Task OnClickPointOpenWwiseElement(ChartPoint point)
    {
        if (!MainWindow.Instance.IsCtrlDown())
            return;

        var chart = point.Context.Chart;

        // Valeurs du point cliqué (en unités de données)
        var xClicked = point.Coordinate.PrimaryValue;
        var yClicked = point.Coordinate.SecondaryValue;

        // Seuil de proximité en unités de données (à ajuster)
        double xThreshold = 0.1; // ex : 0.1 unité sur X
        double yThreshold = 1.0; // ex : 1 unité sur Y

        List<ErrorPoint> pointsNearClicked = new();

        foreach (var series in Series)
        {
            foreach (ErrorPoint p in series.Values)
            {
                double xDiff = Math.Abs(p.Coordinate.PrimaryValue - xClicked);
                double yDiff = Math.Abs(p.Coordinate.SecondaryValue- yClicked);

                if (xDiff < xThreshold && yDiff < yThreshold)
                {
                    pointsNearClicked.Add(p);
                }
            }
        }

        Console.WriteLine($"Points proches du clic : {pointsNearClicked.Count}");

        // Si plusieurs points se chevauchent, on bloque l'action
        if (pointsNearClicked.Count > 1)
        {
            MainWindow.Instance.DispatcherQueue.TryEnqueue(async () => {
                await MainWindow.Instance.ShowMessageAsync("Warning", $"Can't open multiple Wwise objects at once. Please zoom in to open only one.");
            });
            Console.WriteLine("Plusieurs points proches détectés, clic ignoré.");
            return;
        }

        // Sinon on ouvre l’élément Wwise
        if (point.Context.DataSource is ErrorPoint errorPoint)
        {
            var meta = errorPoint.MetaData as PointMetaData;
            string name = meta?.Name ?? "Unknown";
            string wwiseID = meta?.WwiseID ?? "Unknown";

            Console.WriteLine($"Point cliqué : {name} ({wwiseID})");

            await WaapiBridge.FocusWwiseWindow();
            await WaapiBridge.FindObjectInProjectExplorer(wwiseID);
            await WaapiBridge.InspectWwiseObject(wwiseID);
        }
    }

    private readonly Dictionary<SKColor, ScatterSeries<ErrorPoint>> _seriesByColor
    = new Dictionary<SKColor, ScatterSeries<ErrorPoint>>(new SKColorEqualityComparer());

    public void AddPointWithVerticalError(
    string name,
    int index,
    float volume,
    float VolumeRTPCMinValue,
    float VolumeRTPCMaxValue,
    double pointOffset,
    float maxLabelOffset,
    SKColor color,
    string wwiseID)
    {
        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
        {
            float xOffsetStep = 0.04f;
            double totalXOffset = 0 + pointOffset * xOffsetStep;

            // Si on n’a pas encore une série pour cette couleur, on la crée
            if (!_seriesByColor.TryGetValue(color, out var series))
            {
                series = new ScatterSeries<ErrorPoint>
                {
                    Values = new ObservableCollection<ErrorPoint>(),
                    Fill = new SolidColorPaint(color) { ZIndex = 10 },
                    Stroke = new SolidColorPaint(SKColors.Black) { StrokeThickness = 1.5f },
                    ErrorPaint = new SolidColorPaint(LightenColor(color, 0.1f, 0.6f)) { StrokeThickness = 2 },
                    GeometrySize = 15,
                    YToolTipLabelFormatter = point =>
                    {
                        if (point.Context.DataSource is ErrorPoint errorPoint)
                        {
                            var myMeta = errorPoint.MetaData as PointMetaData;
                            return myMeta?.Name ?? "Unknown";
                        }
                        return "Unknown";
                    },
                    ZIndex = 10
                };

                _seriesByColor[color] = series;
                series.ChartPointPointerDown += async (chart, point) =>
                {
                    Console.WriteLine($"Point cliqué !");
                    await OnClickPointOpenWwiseElement(point);
                };
                Series.Add(series);
            }

            // Ajoute le point à la bonne série
            ((ObservableCollection<ErrorPoint>)series.Values).Add(
                new ErrorPoint(totalXOffset, volume, 0, 0, VolumeRTPCMinValue, VolumeRTPCMaxValue)
                {
                    MetaData = new PointMetaData
                    {
                        Name = $"{name} : {volume}dB ({VolumeRTPCMinValue} | {VolumeRTPCMaxValue})",
                        WwiseID = wwiseID
                    }
                });
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    SKColor LightenColor(SKColor color, float vanished = 0.3f, float transparency = 0f, bool invert = false)
    {
        // Clamp des valeurs pour éviter les débordements
        vanished = Math.Clamp(vanished, 0f, 1f);
        transparency = Math.Clamp(Math.Abs(1 - transparency), 0f, 1f);

        // Inversion des couleurs si demandé
        if (invert)
        {
            color = new SKColor(
                (byte)(255 - color.Red),
                (byte)(255 - color.Green),
                (byte)(255 - color.Blue),
                color.Alpha // On garde l'alpha original ici
            );
        }

        // Application de l'éclaircissement
        byte r = (byte)(color.Red + (255 - color.Red) * vanished);
        byte g = (byte)(color.Green + (255 - color.Green) * vanished);
        byte b = (byte)(color.Blue + (255 - color.Blue) * vanished);
        byte a = (byte)(255 * transparency);

        return new SKColor(r, g, b, a);
    }
}

public class NoTooltip : IChartTooltip, IDisposable
{
    // Cette méthode est appelée pour afficher le tooltip
    public void Show(IEnumerable<ChartPoint> points, Chart chart)
    {
        // Ne rien faire, donc tooltip invisible
    }

    // Cette méthode est appelée pour cacher le tooltip
    public void Hide(Chart chart)
    {
        // Ne rien faire
    }

    // Dispose si besoin (ici rien à nettoyer)
    public void Dispose()
    {
    }

    // On doit aussi implémenter cette propriété (pas obligatoire, mais recommandée)
    public TooltipFindingStrategy TooltipFindingStrategy { get; set; } = TooltipFindingStrategy.Automatic;
}
public class PointMetaData : ChartEntityMetaData
{
    public string Name { get; set; }
    public string WwiseID { get; set; }
}
public class SKColorEqualityComparer : IEqualityComparer<SKColor>
{
    public bool Equals(SKColor x, SKColor y) =>
        x.Red == y.Red && x.Green == y.Green && x.Blue == y.Blue && x.Alpha == y.Alpha;

    public int GetHashCode(SKColor obj) =>
        HashCode.Combine(obj.Red, obj.Green, obj.Blue, obj.Alpha);
}