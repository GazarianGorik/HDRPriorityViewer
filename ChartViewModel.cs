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

            // Si on n’a pas encore une série pour cette couleur, on la crée
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

            // Ajoute le point à la bonne série
            ((ObservableCollection<ErrorPoint>)series.Values).Add(pointToAdd);
            WwiseCache.chartDefaultPoints.Add(pointToAdd);
        });
    }


    // Ajoute dans ta classe un dictionnaire pour gérer les highlights par nom
    private readonly Dictionary<string, ScatterSeries<ErrorPoint>> _highlightSeriesByName = new();

    public void HighlightPointByName(string pointName)
    {
        Console.WriteLine($"[Info] Highlighting {pointName}...");

        // Si on a déjà highlight ce pointName, on ne fait rien pour éviter doublons
        if (_highlightSeriesByName.ContainsKey(pointName))
        {
            Console.WriteLine($"[Info] Point '{pointName}' already highlighted.");
            return;
        }

        var pointsToHighlight = new List<ErrorPoint>();

        // Parcourir toutes les séries (y compris les highlights déjà ajoutés)
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
            // If its the firs highlight, we dim the default points
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

            // Stock the highlight series by pointName to avoid duplicates
            _highlightSeriesByName[pointName] = highlightSeries;

            Console.WriteLine($"[Info] Point '{pointName}' highlighted with {pointsToHighlight.Count} points.");
        }
        else
        {
            Console.WriteLine($"[Info] Aucun point trouvé avec le nom '{pointName}'.");
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
                    // If it's the first highlight, make original serie a bit transparent
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
                    // If it's the first highlight, make original serie a bit transparent
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

        // On va chercher les séries highlight associées au pointName
        if (_highlightSeriesByName.TryGetValue(pointName, out var highlightSeries))
        {
            // Retirer la série du chart et de la collection
            Series.Remove(highlightSeries);
            _highlightSeriesByName.Remove(pointName);

            Console.WriteLine($"[Info] Highlight removed for point '{pointName}'.");

            // Si plus de highlights, on restaure les couleurs d'origine
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

    // Ajoute dans ta classe un dictionnaire pour gérer les highlights par nom
    private ScatterSeries<ErrorPoint> _clickableSerieByName = new();

    public void MakeClickablePointByName(ErrorPoint point)
    {
        string pointName = (point.MetaData as PointMetaData).Name;

        UnmakeClickablePointByName();

        Console.WriteLine($"[Info] Making clickable '{pointName}'...");

        var pointsToHighlight = new List<ErrorPoint>();

        // Parcourir toutes les séries (y compris les highlights déjà ajoutés)
        var baseSeries = Series.ToList();

        foreach (var s in baseSeries)
        {
            if (s is ScatterSeries<ErrorPoint> scatterSeries)
            {
                foreach (var pt in scatterSeries.Values)
                {
                    string ptNameWithoutOtherData = "";
                    PointMetaData md = new PointMetaData();

                    if (pt == point)
                    {
                        SKColor color = SKColors.Red;

                        if (scatterSeries.Fill is SolidColorPaint solidColorPaint)
                        {
                            color = solidColorPaint.Color;
                            // Tu peux utiliser color ici
                            Console.WriteLine($"Couleur: {color}");
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

            // Stocker la série par nom pour éviter doublons
            _clickableSerieByName = clickableSeries;

            Console.WriteLine($"[Info] Point '{pointName}' Clickable with {pointsToHighlight.Count} points.");
        }
        else
        {
            Console.WriteLine($"[Info] Aucun point trouvé avec le nom '{pointName}'.");
        }
    }


    public void UnmakeClickablePointByName()
    {
        Console.WriteLine($"[Info] UnmakeClickable...");

        // On va chercher les séries highlight associées au pointName
        if (_clickableSerieByName != null)
        {
            // Retirer la série du chart et de la collection
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

    // --- Nouvelle méthode à appeler pour repositionner les points (sans chevauchement)
    public void RepositionPointsWithoutOverlap()
    {
        // Exemple simplifié, à adapter selon ta logique d'origine :
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

            // Update point position dans le graphique
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
    public SKColor SerieColor { get; set; }
    public ScatterSeries<ErrorPoint> OwnerSerie { get; set; }
}

public class ParentDataEqualityComparer : IEqualityComparer<ParentData>
{
    public bool Equals(ParentData x, ParentData y)
    {
        if (ReferenceEquals(x, y)) return true;    // même objet
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