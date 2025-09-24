/******************************************************************************
#                                                                             #
#   Copyright (c) 2025 Gorik Gazarian                                         #
#                                                                             #
#   This software is licensed under the PolyForm Internal Use License 1.0.0.  #
#   You may obtain a copy of the License at                                   #
#   https://polyformproject.org/licenses/internal-use/1.0.0                   #
#   and in the LICENSE file in this repository.                               #
#                                                                             #
#   You may use, copy, and modify this software for internal purposes,        #
#   including internal commercial use, but you may not redistribute it        #
#   or sell it without a separate license.                                    #
#                                                                             #
******************************************************************************/

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

namespace HDRPriorityViewer;

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

        _borderSerie.IsHoverable = false;
        Series = new ObservableCollection<ISeries> { };
    }

    public Dictionary<ParentData, ScatterSeries<ErrorPoint>> _seriesByParentData = new Dictionary<ParentData, ScatterSeries<ErrorPoint>>();

    public void AddPointWithVerticalError(string name, float volume, float VolumeRTPCMinValue, float VolumeRTPCMaxValue, ParentData parentData, string wwiseID, string eventName)
    {
        MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
        {
            // If we don't have a series for this color yet, create it
            if (!_seriesByParentData.TryGetValue(parentData, out var series))
            {
                Log.Info($"{parentData.ID} is unique!");

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
                            string tooltipLabel = $"Event: {myMeta.EventName}{Environment.NewLine}" +
                                                  $"Object: {myMeta.AudioObjectName}{Environment.NewLine}" +
                                                  $"Priority: {Math.Round(errorPoint.Y.Value)}dB " +
                                                  $"({Math.Round(errorPoint.YErrorI)}dB | " +
                                                  $"{Math.Round(errorPoint.YErrorJ)}dB)";
                            return tooltipLabel;
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
                    AudioObjectName = name,
                    EventName = eventName,
                    WwiseID = wwiseID,
                    OwnerSerie = series
                }
            };

            // Add the point to the correct series
            ((ObservableCollection<ErrorPoint>)series.Values).Add(pointToAdd);
            WwiseCache.chartAudioObjectsPoints.Add(pointToAdd);
        });
    }


    // Add in your class a dictionary to manage highlights by name
    private Dictionary<string, List<ISeries>> _highlightSeriesByName = new();

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

        // Dictionnaire SKColor -> liste de points à surligner
        var pointsByColor = new Dictionary<SKColor, List<ErrorPoint>>();

        foreach (ISeries? s in baseSeries)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (ErrorPoint pt in scatterSeries.Values)
                {
                    string ptAudioObjectName = "";
                    PointMetaData md = new PointMetaData();

                    if (pt.MetaData is PointMetaData pointMetaData)
                    {
                        md = pointMetaData;
                        ptAudioObjectName = md.AudioObjectName;
                    }

                    if (!string.IsNullOrEmpty(ptAudioObjectName) && ptAudioObjectName == pointName)
                    {
                        SKColor ptColor = SKColors.Red; // fallback
                        if (scatterSeries.Fill is SolidColorPaint solidColor)
                            ptColor = solidColor.Color;

                        var newPoint = new ErrorPoint(pt.X ?? 0, pt.Y ?? 0, 0, 0, 0, 0)
                        {
                            MetaData = new PointMetaData
                            {
                                WwiseID = md.WwiseID,
                                AudioObjectName = md.AudioObjectName,
                                TwinPoint = pt
                            }
                        };

                        if (!pointsByColor.ContainsKey(ptColor))
                            pointsByColor[ptColor] = new List<ErrorPoint>();

                        pointsByColor[ptColor].Add(newPoint);
                    }
                }
            }
        }

        if (pointsByColor.Count > 0)
        {
            int totalPoints = pointsByColor.Values.Sum(list => list.Count);

            // If this is the first highlight, dim the points by default
            if (totalPoints == 1)
                DimDefaultChartPoints();

            foreach (var kvp in pointsByColor)
            {
                SKColor color = kvp.Key;
                List<ErrorPoint> pts = kvp.Value;

                ScatterSeries<ErrorPoint> highlightSeries = new ScatterSeries<ErrorPoint>
                {
                    Values = new ObservableCollection<ErrorPoint>(pts),
                    Fill = new SolidColorPaint(Utility.OpaqueColor(color)),
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
                            return md.AudioObjectName;
                        }
                        return "";
                    },
                    XToolTipLabelFormatter = _ => "",
                    YToolTipLabelFormatter = _ => "",
                };

                Series.Add(highlightSeries);

                if (!_highlightSeriesByName.ContainsKey(pointName))
                    _highlightSeriesByName[pointName] = new List<ISeries>();

                _highlightSeriesByName[pointName].Add(highlightSeries);
            }

            Log.Info($"Point '{pointName}' highlighted with {totalPoints} points.");
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
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                // Check that this series is not already highlighted
                bool isHighlighted = _highlightSeriesByName.Values
                    .SelectMany(list => list) // Flatten all series lists
                    .Contains(scatterSeries);

                if (!isHighlighted)
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
    }

    private void UnDimDefaultChartPoints()
    {
        foreach (ISeries? s in Series.ToList())
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                bool isHighlighted = _highlightSeriesByName.Values
                    .SelectMany(list => list)
                    .Contains(scatterSeries);

                if (!isHighlighted)
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
    }

    public void DehighlightPointByName(string pointName)
    {
        Log.Info($"Dehighlighting {pointName}...");

        // Retrieve the list of highlighted series for this pointName
        if (_highlightSeriesByName.TryGetValue(pointName, out List<ISeries>? highlightSeriesList))
        {
            // Supprime chaque série highlight du chart
            foreach (var highlightSeries in highlightSeriesList)
            {
                Series.Remove(highlightSeries);
            }

            // Remove the entry from the dictionary
            _highlightSeriesByName.Remove(pointName);

            Log.Info($"Highlight removed for point '{pointName}'.");

            // If no highlights remain, restore the original chart colors
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
        string pointAudioObjectName = (point.MetaData as PointMetaData).AudioObjectName;

        UnmakeClickablePointByName();

        Log.Info($"Making clickable '{pointAudioObjectName}'...");

        List<ErrorPoint> clickablePoints = new List<ErrorPoint>();

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

                            Log.Info($"Color: {color}");
                        }

                        clickablePoints.Add(new ErrorPoint(pt.X ?? 0, pt.Y ?? 0, 0, 0, 0, 0)
                        {
                            MetaData = new PointMetaData
                            {
                                AudioObjectName = md.AudioObjectName,
                                WwiseID = md.WwiseID,
                                SerieColor = color
                            }
                        });
                    }
                }
            }
        }

        if (clickablePoints.Count > 0)
        {
            ScatterSeries<ErrorPoint> clickableSeries = new ScatterSeries<ErrorPoint>
            {
                AnimationsSpeed = TimeSpan.FromMilliseconds(100),
                Values = new ObservableCollection<ErrorPoint>(clickablePoints),
                Fill = AppSettings.chartPointClickableFill((clickablePoints[0]?.MetaData as PointMetaData).SerieColor),
                GeometrySize = AppSettings.chartPointClickabeSize,
                IsHoverable = false,
                ZIndex = 50,
                XToolTipLabelFormatter = _ => "",
                YToolTipLabelFormatter = _ => "",
            };

            Series.Add(clickableSeries);

            // Store the series by name to avoid duplicates
            _clickableSerieByName = clickableSeries;

            Log.Info($"Point '{pointAudioObjectName}' Clickable with {clickablePoints.Count} points.");
        }
        else
        {
            Log.Info($"No point found with the name '{pointAudioObjectName}'.");
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
        WwiseCache.chartAudioObjectsPoints.Clear();

        Log.Info("Chart cleared successfully.");
    }


    public void UpdateBorders()
    {
        double maxY = 0;
        double maxX = 5;
        double minX = 0;

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

        foreach (ISeries s in _highlightSeriesByName.Values.SelectMany(list => list))
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (ErrorPoint pt in scatterSeries.Values)
                {
                    var twinPoint = (pt.MetaData as PointMetaData)?.TwinPoint;
                    if (twinPoint == null) continue;

                    bool ownerVisible = (twinPoint.MetaData as PointMetaData)?.OwnerSerie?.IsVisible ?? true;

                    MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                    {
                        scatterSeries.IsVisible = ownerVisible;
                    });

                    if (!ownerVisible) break;

                    var twinPointX = twinPoint.X;
                    var twinPointY = twinPoint.Y;

                    MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                    {
                        pt.X = twinPointX;
                        pt.Y = twinPointY;
                    });

                    anyHighlightIsVisible = true;
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
    public string AudioObjectName
    {
        get; set;
    }
    public string EventName
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
