using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

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

    public void AddPointWithVerticalError(string name, float volume, float VolumeRTPCMinValue, float VolumeRTPCMaxValue, ParentData parentData, string wwiseID)
    {
        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
        {

            // If we don't have a series for this color yet, create it
            if (!_seriesByParentData.TryGetValue(parentData, out ScatterSeries<ErrorPoint>? series))
            {
                series = new ScatterSeries<ErrorPoint>
                {
                    //Name = parentData.Name,
                    Values = new ObservableCollection<ErrorPoint>(),
                    Fill = AppSettings.chartPointFill(parentData.Color),
                    Stroke = AppSettings.chartPointStroke(),
                    ErrorPaint = AppSettings.chartPointError(parentData.Color),
                    GeometrySize = AppSettings.chartPointSize,

                    YToolTipLabelFormatter = point =>
                    {
                        if (point.Context.DataSource is ErrorPoint errorPoint)
                        {
                            PointMetaData? myMeta = errorPoint.MetaData as PointMetaData;
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

            ErrorPoint pointToAdd = new ErrorPoint(0, volume, 0, 0, VolumeRTPCMinValue, VolumeRTPCMaxValue)
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
        Log.Info($"Highlighting {pointName}...");

        // If we already highlighted this pointName, do nothing to avoid duplicates
        if (_highlightSeriesByName.ContainsKey(pointName))
        {
            Log.Info($"Point '{pointName}' already highlighted.");
            return;
        }

        List<ErrorPoint> pointsToHighlight = new List<ErrorPoint>();

        // Go through all series (including highlights already added)
        List<ISeries> baseSeries = Series.ToList();

        SKColor matchedPointSerrieColor = SKColors.Red;

        foreach (ISeries? s in baseSeries)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (ErrorPoint pt in scatterSeries.Values)
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
                                WwiseID = md.WwiseID,
                                TwinPoint = pt
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
            {
                DimDefaultChartPoints();
            }

            ScatterSeries<ErrorPoint> highlightSeries = new ScatterSeries<ErrorPoint>
            {
                Values = new ObservableCollection<ErrorPoint>(pointsToHighlight),
                Fill = new SolidColorPaint(Utility.OpaqueColor(matchedPointSerrieColor)),
                Stroke = AppSettings.chartPointHighlightedStroke(),
                GeometrySize = 20,
                IsHoverable = false,
                ZIndex = 50,
                DataLabelsSize = AppSettings.chartPointHighlightedDataLabelsSize,
                DataLabelsPaint = AppSettings.chartPointHighlightedLabel(),
                DataLabelsFormatter = cp =>
                {
                    if (cp.Context.DataSource is ErrorPoint ep &&
                        ep.MetaData is PointMetaData md)
                    {
                        return md.Name.Split(':')[0];
                    }

                    return "";
                },
                XToolTipLabelFormatter = _ => "",
                YToolTipLabelFormatter = _ => "",
            };

            Series.Add(highlightSeries);

            // Store the highlight series by pointName to avoid duplicates
            _highlightSeriesByName[pointName] = highlightSeries;

            Log.Info($"Point '{pointName}' highlighted with {pointsToHighlight.Count} points.");
        }
        else
        {
            Log.Info($"No point found with the name '{pointName}'.");
        }
    }

    private void DimDefaultChartPoints()
    {
        foreach (ISeries? s in Series.ToList())
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries && !_highlightSeriesByName.ContainsValue(scatterSeries))
            {
                if (scatterSeries.Fill is SolidColorPaint solidColor)
                {
                    MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                    {
                        scatterSeries.Fill = AppSettings.chartPointFillDimed(solidColor.Color);
                        scatterSeries.ErrorPaint = AppSettings.chartPointErrorDimed(solidColor.Color);
                    });
                }
            }
        }
    }

    private void UnDimDefaultChartPoints()
    {
        foreach (ISeries? s in Series.ToList())
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries && !_highlightSeriesByName.ContainsValue(scatterSeries))
            {
                if (scatterSeries.Fill is SolidColorPaint solidColor)
                {
                    MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                    {
                        scatterSeries.Fill = AppSettings.chartPointFill(Utility.OpaqueColor(solidColor.Color));
                        scatterSeries.ErrorPaint = AppSettings.chartPointError(Utility.OpaqueColor(solidColor.Color));
                    });
                }
            }
        }
    }

    public void DehighlightPointByName(string pointName)
    {
        Log.Info($"Dehighlighting {pointName}...");

        // Look for highlight series associated with pointName
        if (_highlightSeriesByName.TryGetValue(pointName, out ScatterSeries<ErrorPoint>? highlightSeries))
        {
            // Remove the series from the chart and collection
            Series.Remove(highlightSeries);
            _highlightSeriesByName.Remove(pointName);

            Log.Info($"Highlight removed for point '{pointName}'.");

            // If there are no more highlights, restore original colors
            if (_highlightSeriesByName.Count == 0)
            {
                UnDimDefaultChartPoints();
            }
        }
        else
        {
            Log.Info($"No highlight found for point '{pointName}'.");
        }
    }

    // Add in your class a dictionary to manage clickable points by name
    private ScatterSeries<ErrorPoint> _clickableSerieByName = new();

    public void MakeClickablePointByName(ErrorPoint point)
    {
        string pointName = (point.MetaData as PointMetaData).Name;

        UnmakeClickablePointByName();

        Log.Info($"Making clickable '{pointName}'...");

        List<ErrorPoint> pointsToHighlight = new List<ErrorPoint>();

        // Go through all series (including highlights already added)
        List<ISeries> baseSeries = Series.ToList();

        foreach (ISeries? s in baseSeries)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (ErrorPoint pt in scatterSeries.Values)
                {
                    PointMetaData md = new PointMetaData();

                    if (pt == point)
                    {
                        SKColor color = SKColors.Red;

                        if (scatterSeries.Fill is SolidColorPaint solidColorPaint)
                        {
                            color = solidColorPaint.Color;
                            // You can use color here
                            Log.Info($"Color: {color}");
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
            ScatterSeries<ErrorPoint> clickableSeries = new ScatterSeries<ErrorPoint>
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

            Log.Info($"Point '{pointName}' Clickable with {pointsToHighlight.Count} points.");
        }
        else
        {
            Log.Info($"No point found with the name '{pointName}'.");
        }
    }


    public void UnmakeClickablePointByName()
    {
        Log.Info($"UnmakeClickable...");

        // Look for clickable series associated with the pointName
        if (_clickableSerieByName != null)
        {
            // Remove the series from the chart and collection
            Series.Remove(_clickableSerieByName);
            _clickableSerieByName = null;

            Log.Info($"Clickable removed for point.");
        }
        else
        {
            Log.Info($"No Clickable found for point.");
        }
    }

    public void ClearChart()
    {
        Log.Info("Clearing chart...");

        // 1. Remove all series from the chart
        Series.Clear();

        // 2. Clear internal dictionaries
        _seriesByParentData.Clear();
        _highlightSeriesByName.Clear();
        _clickableSerieByName = null;

        // 3. Reset border series
        _borderSerie.Values = new ObservableCollection<ObservablePoint>();

        // 4. Optionally reset Wwise cache if needed
        WwiseCache.chartDefaultPoints.Clear();

        Log.Info("Chart cleared successfully.");
    }


    public void UpdateBorders()
    {
        Log.Info("Updating chart borders...");
        double maxY = double.MinValue;
        double maxX = double.MinValue;
        double minX = double.MaxValue;

        int pointsCount = 0;

        foreach (ISeries s in Series)
        {
            if (s.Values is IEnumerable<ErrorPoint> points)
            {
                foreach (ErrorPoint p in points)
                {
                    double yVal = p.Y ?? double.MinValue;
                    double xVal = p.X ?? double.MinValue;

                    pointsCount++;

                    if (yVal > maxY)
                    {
                        maxY = yVal;
                    }

                    if (xVal > maxX)
                    {
                        maxX = xVal;
                    }

                    if (xVal < minX)
                    {
                        minX = xVal;
                    }

                    if (p.YErrorJ > maxY)
                    {
                        maxY = p.YErrorJ;
                    }
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

            if (!Series.Contains(_borderSerie))
                Series.Add(_borderSerie);
        });
        Log.Info($"Chart borders updated! ({pointsCount})");
    }

    public void UpdateHighlightedPointPoistion()
    {
        bool anyHighlightIsVisible = false;

        foreach (ISeries s in _highlightSeriesByName.Values)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (ErrorPoint pt in s.Values)
                {
                    var twinPoint = (pt.MetaData as PointMetaData).TwinPoint;

                    if ((twinPoint.MetaData as PointMetaData).OwnerSerie.IsVisible == false)
                    {
                        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                        {
                            s.IsVisible = false;
                        });
                        break;
                    }
                    else
                    {
                        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                        {
                            s.IsVisible = true;
                        });
                        anyHighlightIsVisible = true;
                    }

                    var twinPointX = (pt.MetaData as PointMetaData).TwinPoint.X;
                    var twinPointY = (pt.MetaData as PointMetaData).TwinPoint.Y;

                    MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                    {
                        pt.X = twinPointX;
                        pt.Y = twinPointY;
                    });
                }
            }
        }

        if (anyHighlightIsVisible)
        {
            DimDefaultChartPoints();
        }
        else
        {
            UnDimDefaultChartPoints();
        }
    }

    // --- New method to call to reposition points (without overlap)
    public void RepositionPointsWithoutOverlap()
    {
        // Simplified example, adapt according to your original logic:
        List<(double?, double?)> yMinMaxList = new List<(double?, double?)>();
        int xOffsetDirection = 1;

        IEnumerable<ErrorPoint> points = GetAllPoints();

        foreach (ErrorPoint point in points)
        {
            (double?, double?) yMinMax = (point.YErrorI + point.Y, point.YErrorJ + point.Y);

            int occurrence = 0;
            foreach ((double?, double?) range in yMinMaxList)
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

    /* OLD VERSION COULD BE USEFULL LATER (it stacks points with same value, min, max)
    public void RepositionPointsWithoutOverlap()
    {
        // Liste des points déjà placés : on garde value, min, max et l'offset attribué
        var placed = new List<(float value, float min, float max, int offset)>();
        const float EPS = 0.0001f;
        int xOffsetDirection = 1;
        int index = 0;
        IEnumerable<ErrorPoint> points = GetAllPoints();

        foreach (ErrorPoint point in points)
        {
            index++;

            float value = (float)(point.Y);
            float min = Math.Max((float)point.YErrorI, -96f);
            float max = (float)point.YErrorJ;

            // 1) Existe-t-il déjà un point EXACT (value,min,max) ?
            int? existingOffset = placed
                .Where(p => Math.Abs(p.value - value) < EPS
                         && Math.Abs(p.min - min) < EPS
                         && Math.Abs(p.max - max) < EPS)
                .Select(p => (int?)p.offset)
                .FirstOrDefault();

            int chosenOffset;
            if (existingOffset.HasValue)
            {
                // Réutiliser l'offset existant → superposition des identiques
                chosenOffset = existingOffset.Value;
            }
            else
            {
                // 2) Sinon, trouver les offsets déjà utilisés par les points qui se chevauchent verticalement
                var usedOffsets = new HashSet<int>();
                foreach (var p in placed)
                {
                    // test d'intersection correcte : !(current.max < p.min || current.min > p.max)
                    if (!(max < p.min - EPS || min > p.max + EPS))
                    {
                        usedOffsets.Add(p.offset);
                    }
                }

                // 3) choisir le plus petit offset libre (0, 1, 2, ...)
                chosenOffset = 0;
                while (usedOffsets.Contains(chosenOffset))
                    chosenOffset++;
            }

            // On enregistre le point avec son offset
            placed.Add((value, min, max, chosenOffset));

            // Calcul du xOffset effectif envoyé au chart (garde ton xOffsetDirection)
            float xOffset = chosenOffset * xOffsetDirection;

            try
            {
                point.X = xOffset;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to reposition point on graph: {ex}");
            }
        }
    }
    */

    public IEnumerable<ErrorPoint> GetAllPoints()
    {
        foreach (ISeries series in Series)
        {
            if (series is ScatterSeries<ErrorPoint> errorSeries)
            {
                if (series.IsVisible)
                {
                    foreach (ErrorPoint point in errorSeries.Values)
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
    public string Name
    {
        get; set;
    }
    public string WwiseID
    {
        get; set;
    }
    public SKColor SerieColor
    {
        get; set;
    }
    public ScatterSeries<ErrorPoint> OwnerSerie
    {
        get; set;
    }

    // Only used for highlight to easilly update there position when the original point moves
    public ErrorPoint TwinPoint
    {
        get; set;
    }
}

public class ParentDataEqualityComparer : IEqualityComparer<ParentData>
{
    public bool Equals(ParentData x, ParentData y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;    // same object
        }

        if (x is null || y is null)
        {
            return false; // null safety
        }

        return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
               x.Color.Red == y.Color.Red &&
               x.Color.Green == y.Color.Green &&
               x.Color.Blue == y.Color.Blue &&
               x.Color.Alpha == y.Color.Alpha;
    }

    public int GetHashCode(ParentData obj)
    {
        if (obj is null)
        {
            return 0;
        }

        return HashCode.Combine(
            obj.Name?.ToLowerInvariant(),  // case-insensitive hash
            obj.Color.Red,
            obj.Color.Green,
            obj.Color.Blue,
            obj.Color.Alpha
        );
    }
}
