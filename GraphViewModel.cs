using CSharpMarkup.WinUI;
using CSharpMarkup.WinUI.LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.SkiaSharpView.WinUI;
using LiveChartsCore.VisualElements;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Windows.Foundation.Collections;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WwiseHDRTool;

public class GraphViewModel : INotifyPropertyChanged
{
    private ObservableCollection<ISeries> _series;

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

    public GraphViewModel()
    {
        var values0 = new ErrorValue[]
        {
            // (Y, Y+- error) 
            new(-96, 0, 0),
            new(10, 0, 0),
            new(10, 0, 0),
            new(10, 0, 0),
        };

        Series = new ObservableCollection<ISeries>
        {
            new ScatterSeries<ErrorValue>
            {
                Values = values0,
                ErrorPaint = new SolidColorPaint(SKColors.IndianRed),
                Stroke = null,
                GeometrySize = 0,
                DataLabelsPaint = new SolidColorPaint(new SKColor(200, 200, 200)),
                DataLabelsSize = 0,
                DataLabelsPosition = DataLabelsPosition.Top
            }
        };
    }


    public void AddStackedColumnSeries(string name, int value)
    {
        Series.Add(CreateStackedColumnSeries(name, value));
    }

    public void AddScatterSeries(string name, ObservablePoint[] errorValues, int labelPosOffset)
    {
        Series.Add(CreateScatterSeries(name, errorValues, labelPosOffset));
    }
    public void AddErrorLineSeries(string name, ObservablePoint[] values)
    {
        try
        {
            Series.Add(CreateErrorLineSeries(name, values));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void ClearSeries()
    {
        Series.Clear();
    }

    public void AddPointWithVerticalError(string name, int index, double volume, double VolumeRTPCMinValue, double VolumeRTPCMaxValue, double labelPosOffset, double pointOffset, int totalColor, float maxLabelOffset)
    {
        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
        {
            SKColor color = SKColorFromHue((360f / totalColor) * index);

            float xOffsetStep = 0.04f;
            double totalXOffset = 0 + pointOffset * xOffsetStep;

            // 1) le point central
            var point = new LineSeries<ErrorPoint>
            {
                Values = new[] { new ErrorPoint(totalXOffset, volume, 0, 0, VolumeRTPCMinValue, VolumeRTPCMaxValue) },
                Name = name,
                GeometryFill = new SolidColorPaint(color) { ZIndex = 10},
                GeometryStroke = new SolidColorPaint(SKColors.Black) { StrokeThickness = 1.5f },
                ErrorPaint = new SolidColorPaint(LightenColor(color, 0.1f, 0.6f)) { StrokeThickness = 5},
                GeometrySize = 15,
                YToolTipLabelFormatter = (chartPoint) => $"{volume}dB {VolumeRTPCMinValue} | {VolumeRTPCMaxValue}",
                ZIndex = 10
            };

            // 1b) le point central

            float nameStartWithSOffset = name.StartsWith('S') ? -0.011f : 0f;

            var label = new ScatterSeries<ObservablePoint>
            {
                Values = new[] { new ObservablePoint(-0.6f, volume) },
                Stroke = null,
                GeometrySize = 0,
                ZIndex = 5,
                DataLabelsPaint = new SolidColorPaint(LightenColor(color, 0.4f))
                {
                    SKTypeface = SKTypeface.FromFamilyName("Consolas"),
                    SKFontStyle = SKFontStyle.Normal
                },
                DataLabelsSize = 16,
                DataLabelsTranslate = new LvcPoint(nameStartWithSOffset, labelPosOffset),
                DataLabelsPosition = DataLabelsPosition.Right,
                DataLabelsFormatter = point => $"{name}",
            };

            // 2) la tige d'erreur 
            /*var errorLine = new LineSeries<ObservablePoint>
            {
                Values = new[]
                {
                new ObservablePoint(totalXOffset, VolumeRTPCMinValue),
                new ObservablePoint(totalXOffset, VolumeRTPCMaxValue)
                },
                GeometrySize = 0,                     // pas de marker
                LineSmoothness = 0,                   // droite non lissée
                Stroke = new SolidColorPaint(LightenColor(color, 0.3f, 0.7f)) { ZIndex = 0, StrokeThickness = 3 },
                Fill = null,
                YToolTipLabelFormatter = null,
                XToolTipLabelFormatter = null,
                ZIndex = 0
            };*/

            try
            {
                //Series.Add(errorLine);
                Series.Add(point);
                //Series.Add(label);
                //ISeries[] whitListedSeries = [errorLine];
                //MainWindow.Instance.SetChartTooltipFilter(whitListedSeries);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });
    }


    private ScatterSeries<ObservablePoint> CreateScatterSeries(string name, ObservablePoint[] values, int labelPosOffset)
    {
        return new ScatterSeries<ObservablePoint>
        {
            Values = values,
            ErrorPaint = new SolidColorPaint(SKColors.IndianRed),
            Name = name,
            Stroke = null,
            GeometrySize = 10,
            DataLabelsPaint = new SolidColorPaint(new SKColor(200, 200, 200)),
            DataLabelsSize = 16,
            DataLabelsTranslate = new LvcPoint(0, labelPosOffset),
            ZIndex = 1,
            DataLabelsPosition = DataLabelsPosition.Right,
            DataLabelsFormatter = point => $"{name}",
            ClippingMode = ClipMode.XY,
        };
    }

    private LineSeries<ObservablePoint> CreateErrorLineSeries(string name, ObservablePoint[] values)
    {
        return new LineSeries<ObservablePoint>
        {
            Values = values,
            ZIndex = 0,
        };
    }

    private ISeries CreateStackedColumnSeries(string name, int value)
    {
        return new StackedColumnSeries<int>
        {
            Name = name,
            Values = [value],
            Stroke = null,
            DataLabelsPaint = new SolidColorPaint(new SKColor(45, 45, 45)),
            DataLabelsSize = 14,
            DataLabelsPosition = DataLabelsPosition.Middle,
            DataLabelsFormatter = point => $"{name} : {point.Coordinate.PrimaryValue:N}",
            YToolTipLabelFormatter =
                            p => $"{p.Coordinate.PrimaryValue:N} ({p.StackedValue!.Share:P})"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    SKColor SKColorFromHue(float hue)
    {
        float saturation = 0.8f;
        float lightness = 0.6f;

        float c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        float x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        float m = lightness - c / 2;

        float r1 = 0, g1 = 0, b1 = 0;

        if (hue < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (hue < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (hue < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (hue < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (hue < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        byte r = (byte)((r1 + m) * 255);
        byte g = (byte)((g1 + m) * 255);
        byte b = (byte)((b1 + m) * 255);

        return new SKColor(r, g, b);
    }

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