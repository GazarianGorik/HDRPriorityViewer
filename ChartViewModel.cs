using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WwiseHDRTool;

public class ChartViewModel : INotifyPropertyChanged
{
    private ObservableCollection<ISeries> _series;
    private ScatterSeries<ObservablePoint> _borderSerie;

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
        _borderSerie = new ScatterSeries<ObservablePoint>
        {
            GeometrySize = 0,
            YToolTipLabelFormatter = null,
        };

        Series = new ObservableCollection<ISeries>{};
    }

    private readonly Dictionary<SKColor, ScatterSeries<ErrorPoint>> _seriesByColor = new Dictionary<SKColor, ScatterSeries<ErrorPoint>>(new SKColorEqualityComparer());

    public void AddPointWithVerticalError(string name, int index, float volume, float VolumeRTPCMinValue, float VolumeRTPCMaxValue, double pointOffset, SKColor color, string wwiseID)
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

    public void UpdateBorders()
    {
        Console.WriteLine("[Info] Updating chart borders...");
        double maxY = double.MinValue;
        double maxX = double.MinValue;
        double minX = double.MaxValue;

        foreach (var s in Series)
        {
            if (s.Values is IEnumerable<ErrorPoint> points)
            {
                foreach (var p in points)
                {
                    double yVal = p.Y ?? double.MinValue;
                    double xVal = p.X ?? double.MinValue;

                    if (yVal > maxY)
                        maxY = yVal;

                    if (xVal > maxX)
                        maxX = xVal;

                    if (xVal < minX)
                        minX = xVal;

                    if (p.YErrorJ > maxY)
                        maxY = p.YErrorJ;
                }
            }
        }

        float padding = 0.05f; // 5% padding

        minX = minX - padding;
        maxX = maxX + padding;
        maxY = maxY + padding;


        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
        {
            _borderSerie.Values = new ObservableCollection<ObservablePoint>
            {
                new ObservablePoint(maxX, maxY),
                new ObservablePoint(minX, maxY)
            };
            Series.Add(_borderSerie);
        });
        Console.WriteLine("[Info] Chart borders updated!");
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