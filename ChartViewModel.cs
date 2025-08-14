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
using System.Linq;
using System.Runtime.CompilerServices;

namespace WwiseHDRTool;

public partial class ChartViewModel : INotifyPropertyChanged
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

        Series = new ObservableCollection<ISeries> { };
    }

    public Dictionary<ParentData, ScatterSeries<ErrorPoint>> _seriesByParentData = new Dictionary<ParentData, ScatterSeries<ErrorPoint>>(new ParentDataEqualityComparer());

    public void AddPointWithVerticalError(string name, int index, float volume, float VolumeRTPCMinValue, float VolumeRTPCMaxValue, double pointOffset, ParentData parentData, string wwiseID)
    {
        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
        {
            float xOffsetStep = 0.04f;
            double totalXOffset = 0 + pointOffset * xOffsetStep;

            // If we don't have a series for this color yet, create it
            if (!_seriesByParentData.TryGetValue(parentData, out var series))
            {
                series = new ScatterSeries<ErrorPoint>
                {
                    Name = parentData.Name,
                    Values = new ObservableCollection<ErrorPoint>(),
                    Fill = AppSettings.chartPointFill(parentData.Color),
                    Stroke = AppSettings.chartPointStroke,
                    ErrorPaint = AppSettings.chartPointError(parentData.Color),
                    GeometrySize = AppSettings.chartPointSize,

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

                _seriesByParentData[parentData] = series;
                Series.Add(series);
                MainWindow.Instance.MainViewModel.AddCategorieFilterButton(parentData);
            }

            ErrorPoint pointToAdd = new ErrorPoint(totalXOffset, volume, 0, 0, VolumeRTPCMinValue, VolumeRTPCMaxValue)
            {
                MetaData = new PointMetaData
                {
                    Name = $"{name} : {volume}dB ({VolumeRTPCMinValue} | {VolumeRTPCMaxValue})",
                    WwiseID = wwiseID,
                    OwnerSerie = series
                }
            };

            // Add the point to the correct series
            ((ObservableCollection<ErrorPoint>)series.Values).Add(pointToAdd);
            WwiseCache.chartDefaultPoints.Add(pointToAdd);
        });
    }


    // Add in your class a dictionary to manage highlights by name
    private readonly Dictionary<string, ScatterSeries<ErrorPoint>> _highlightSeriesByName = new();

    public void HighlightPointByName(string pointName)
    {
        Console.WriteLine($"[Info] Highlighting {pointName}...");

        // If we already highlighted this pointName, do nothing to avoid duplicates
        if (_highlightSeriesByName.ContainsKey(pointName))
        {
            Console.WriteLine($"[Info] Point '{pointName}' already highlighted.");
            return;
        }

        var pointsToHighlight = new List<ErrorPoint>();

        // Go through all series (including highlights already added)
        var baseSeries = Series.ToList();

        SKColor matchedPointSerrieColor = SKColors.Red;

        foreach (var s in baseSeries)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (var pt in scatterSeries.Values)
                {
                    // Fetch point name without other data
                    string ptNameWithoutOtherData = "";
                    PointMetaData md = new PointMetaData();

                    if (pt.MetaData is PointMetaData pointMetaData)
                    {
                        md = pointMetaData;
                        ptNameWithoutOtherData = md.Name.Split(':')[0].Trim(); ;
                    }

                    // If the point name without other data matches the pointName we want to highlight
                    if (!String.IsNullOrEmpty(ptNameWithoutOtherData) && ptNameWithoutOtherData == pointName)
                    {
                        if (scatterSeries.Fill is SolidColorPaint solidColor)
                        {
                            matchedPointSerrieColor = solidColor.Color;
                        }

                        // Create a new ErrorPoint with the same coordinates and metadata
                        pointsToHighlight.Add(new ErrorPoint(pt.X ?? 0, pt.Y ?? 0, 0, 0, 0, 0)
                        {
                            MetaData = new PointMetaData
                            {
                                Name = md.Name,
                                WwiseID = md.WwiseID
                            }
                        });
                    }
                }
            }
        }

        // If we found points to highlight, create a new series and add it to the chart
        if (pointsToHighlight.Count > 0)
        {
            // If it's the first highlight, we dim the default points
            if (pointsToHighlight.Count == 1)
                DimDefaultChartPoints();

            var highlightSeries = new ScatterSeries<ErrorPoint>
            {
                Values = new ObservableCollection<ErrorPoint>(pointsToHighlight),
                Fill = new SolidColorPaint(Utility.OpaqueColor(matchedPointSerrieColor)),
                Stroke = AppSettings.chartPointHighlightedStroke,
                GeometrySize = 20,
                IsHoverable = false,
                ZIndex = 50,
                DataLabelsSize = AppSettings.chartPointHighlightedDataLabelsSize,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsFormatter = cp =>
                {
                    if (cp.Context.DataSource is ErrorPoint ep &&
                        ep.MetaData is PointMetaData md)
                        return md.Name.Split(':')[0];
                    return "";
                },
                XToolTipLabelFormatter = _ => "",
                YToolTipLabelFormatter = _ => "",
            };

            Series.Add(highlightSeries);

            // Store the highlight series by pointName to avoid duplicates
            _highlightSeriesByName[pointName] = highlightSeries;

            Console.WriteLine($"[Info] Point '{pointName}' highlighted with {pointsToHighlight.Count} points.");
        }
        else
        {
            Console.WriteLine($"[Info] No point found with the name '{pointName}'.");
        }
    }

    private void DimDefaultChartPoints()
    {
        foreach (var s in Series.ToList())
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                if (scatterSeries.Fill is SolidColorPaint solidColor)
                {
                    // If it's the first highlight, make original series a bit transparent
                    if (_highlightSeriesByName.Count == 0)
                    {
                        scatterSeries.Fill = AppSettings.chartPointFillDimed(solidColor.Color);
                        scatterSeries.ErrorPaint = AppSettings.chartPointErrorDimed(solidColor.Color);
                    }
                }
            }
        }
    }

    private void UnDimDefaultChartPoints()
    {
        foreach (var s in Series.ToList())
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                if (scatterSeries.Fill is SolidColorPaint solidColor)
                {
                    // If it's the first highlight, make original series a bit transparent
                    if (_highlightSeriesByName.Count == 0)
                    {
                        scatterSeries.Fill = AppSettings.chartPointFill(Utility.OpaqueColor(solidColor.Color));
                        scatterSeries.ErrorPaint = AppSettings.chartPointError(Utility.OpaqueColor(solidColor.Color));
                    }
                }
            }
        }
    }

    public void DehighlightPointByName(string pointName)
    {
        Console.WriteLine($"[Info] Dehighlighting {pointName}...");

        // Look for highlight series associated with pointName
        if (_highlightSeriesByName.TryGetValue(pointName, out var highlightSeries))
        {
            // Remove the series from the chart and collection
            Series.Remove(highlightSeries);
            _highlightSeriesByName.Remove(pointName);

            Console.WriteLine($"[Info] Highlight removed for point '{pointName}'.");

            // If there are no more highlights, restore original colors
            if (_highlightSeriesByName.Count == 0)
            {
                UnDimDefaultChartPoints();
            }
        }
        else
        {
            Console.WriteLine($"[Info] No highlight found for point '{pointName}'.");
        }
    }

    // Add in your class a dictionary to manage clickable points by name
    private ScatterSeries<ErrorPoint> _clickableSerieByName = new();

    public void MakeClickablePointByName(ErrorPoint point)
    {
        string pointName = (point.MetaData as PointMetaData).Name;

        UnmakeClickablePointByName();

        Console.WriteLine($"[Info] Making clickable '{pointName}'...");

        var pointsToHighlight = new List<ErrorPoint>();

        // Go through all series (including highlights already added)
        var baseSeries = Series.ToList();

        foreach (var s in baseSeries)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (var pt in scatterSeries.Values)
                {
                    PointMetaData md = new PointMetaData();

                    if (pt == point)
                    {
                        SKColor color = SKColors.Red;

                        if (scatterSeries.Fill is SolidColorPaint solidColorPaint)
                        {
                            color = solidColorPaint.Color;
                            // You can use color here
                            Console.WriteLine($"Color: {color}");
                        }

                        pointsToHighlight.Add(new ErrorPoint(pt.X ?? 0, pt.Y ?? 0, 0, 0, 0, 0)
                        {
                            MetaData = new PointMetaData
                            {
                                Name = md.Name,
                                WwiseID = md.WwiseID,
                                SerieColor = color
                            }
                        });
                    }
                }
            }
        }

        if (pointsToHighlight.Count > 0)
        {
            var clickableSeries = new ScatterSeries<ErrorPoint>
            {
                AnimationsSpeed = TimeSpan.FromMilliseconds(100),
                Values = new ObservableCollection<ErrorPoint>(pointsToHighlight),
                Fill = AppSettings.chartPointClickableFill((pointsToHighlight[0]?.MetaData as PointMetaData).SerieColor),
                GeometrySize = AppSettings.chartPointClickabeSize,
                IsHoverable = false,
                ZIndex = 50,
                XToolTipLabelFormatter = _ => "",
                YToolTipLabelFormatter = _ => "",
            };

            Series.Add(clickableSeries);

            // Store the series by name to avoid duplicates
            _clickableSerieByName = clickableSeries;

            Console.WriteLine($"[Info] Point '{pointName}' Clickable with {pointsToHighlight.Count} points.");
        }
        else
        {
            Console.WriteLine($"[Info] No point found with the name '{pointName}'.");
        }
    }


    public void UnmakeClickablePointByName()
    {
        Console.WriteLine($"[Info] UnmakeClickable...");

        // Look for clickable series associated with the pointName
        if (_clickableSerieByName != null)
        {
            // Remove the series from the chart and collection
            Series.Remove(_clickableSerieByName);
            _clickableSerieByName = null;

            Console.WriteLine($"[Info] Clickable removed for point.");
        }
        else
        {
            Console.WriteLine($"[Info] No Clickable found for point.");
        }
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

    // --- New method to call to reposition points (without overlap)
    public void RepositionPointsWithoutOverlap()
    {
        // Simplified example, adapt according to your original logic:
        var yMinMaxList = new List<(double?, double?)>();
        int xOffsetDirection = 1;

        var points = GetAllPoints();

        foreach (var point in points)
        {
            var yMinMax = (point.YErrorI + point.Y, point.YErrorJ + point.Y);

            int occurrence = 0;
            foreach (var range in yMinMaxList)
            {
                if (yMinMax.Item1 <= range.Item2 || yMinMax.Item2 >= range.Item1)
                {
                    occurrence++;
                }
            }

            yMinMaxList.Add(yMinMax);

            float xOffset = occurrence * xOffsetDirection;

            // Update point position in the chart
            point.X = xOffset;
        }
    }

    public IEnumerable<ErrorPoint> GetAllPoints()
    {
        foreach (var series in Series)
        {
            if (series is ScatterSeries<ErrorPoint> errorSeries)
            {
                if (series.IsVisible)
                {
                    foreach (var point in errorSeries.Values)
                    {
                        yield return point;
                    }
                }
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

}

public class NoTooltip : IChartTooltip, IDisposable
{
    // This method is called to display the tooltip
    public void Show(IEnumerable<ChartPoint> points, Chart chart)
    {
        // Do nothing, tooltip invisible
    }

    // This method is called to hide the tooltip
    public void Hide(Chart chart)
    {
        // Do nothing
    }

    // Dispose if necessary (nothing to clean here)
    public void Dispose()
    {
    }

    // We must also implement this property (not mandatory, but recommended)
    public FindingStrategy TooltipFindingStrategy { get; set; } = FindingStrategy.Automatic;
}

public class PointMetaData : ChartEntityMetaData
{
    public string Name { get; set; }
    public string WwiseID { get; set; }
    public SKColor SerieColor { get; set; }
    public ScatterSeries<ErrorPoint> OwnerSerie { get; set; }
}

public class ParentDataEqualityComparer : IEqualityComparer<ParentData>
{
    public bool Equals(ParentData x, ParentData y)
    {
        if (ReferenceEquals(x, y)) return true;    // same object
        if (x is null || y is null) return false; // null safety

        return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
               x.Color.Red == y.Color.Red &&
               x.Color.Green == y.Color.Green &&
               x.Color.Blue == y.Color.Blue &&
               x.Color.Alpha == y.Color.Alpha;
    }

    public int GetHashCode(ParentData obj)
    {
        if (obj is null) return 0;
        return HashCode.Combine(
            obj.Name?.ToLowerInvariant(),  // case-insensitive hash
            obj.Color.Red,
            obj.Color.Green,
            obj.Color.Blue,
            obj.Color.Alpha
        );
    }
}
