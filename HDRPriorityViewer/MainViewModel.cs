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
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace HDRPriorityViewer;

public partial class MainViewModel : ObservableObject
{
    public ChartViewModel ChartViewModel
    {
        get;
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    public ObservableCollection<string> SearchSuggestions { get; } = new();

    public ObservableCollection<ButtonData> CategorieFilterButtons { get; } = new();

    public ObservableCollection<SearchItemViewModel> SearchItems { get; } = new();

    public IRelayCommand<SearchItemViewModel> RemoveSearchItemCommand
    {
        get;
    }

    public List<string> Searches = new List<string>();

    private string _wwiseVersion;
    public string WwiseVersion
    {
        get => _wwiseVersion;
        set => SetProperty(ref _wwiseVersion, "Version: " + value);
    }

    private string _wwiseProjectName;
    public string WwiseProjectName
    {
        get => _wwiseProjectName;
        set => SetProperty(ref _wwiseProjectName, "Project: " + value);
    }

    private string _totalChartPoints;
    public string TotalChartPoints
    {
        get => _totalChartPoints;
        set => SetProperty(ref _totalChartPoints, "Scanned: " + value);
    }

    public MainViewModel()
    {
        Log.Info("Initializing MainViewModel...");
        ChartViewModel = new ChartViewModel();
        RemoveSearchItemCommand = new RelayCommand<SearchItemViewModel>(RemoveSearchItem);
    }

    private void RemoveSearchItem(SearchItemViewModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.PreviousValideText))
        {
            var key = NormalizeKey(item.PreviousValideText);
            var idx = Searches.FindIndex(s => NormalizeKey(s) == key);
            if (idx >= 0) Searches.RemoveAt(idx);

            ChartViewModel.DehighlightPointByName(item.PreviousValideText);
        }

        if (SearchItems.Contains(item))
        {
            SearchItems.Remove(item);
        }
    }

    private void AddSearchItem(SearchItemViewModel item)
    {
        // 1. Dehighlight if this SearchItem already had a point
        if (!string.IsNullOrEmpty(item.PreviousValideText))
        {
            ChartViewModel.DehighlightPointByName(item.PreviousValideText);
        }

        string trimmedText = item.Text.Trim();

        // 2. Always highlight the new point for this SearchItem
        ChartViewModel.HighlightPointByName(trimmedText);
        item.PreviousValideText = trimmedText;

        // 3. Add to the global list if it’s a new term (comparaison normalisée)
        if (!Searches.Any(s => NormalizeKey(s) == NormalizeKey(trimmedText)))
        {
            if (item == SearchItems.First())
            {
                SearchItems.Insert(0, new SearchItemViewModel());
            }
            Searches.Add(trimmedText);
        }
    }


    public void ValidateSearchItem(SearchItemViewModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.Text))
        {
            AddSearchItem(item);
        }
        else
        {
            if (SearchItems.IndexOf(item) != 0) // Do not remove default empty search bar
            {
                RemoveSearchItem(item);
            }
        }
    }

    public void UpdateSuggestions(SearchItemViewModel item)
    {
        SearchSuggestions.Clear();

        if (!string.IsNullOrWhiteSpace(item.Text))
        {
            var queryWords = NormalizeKey(item.Text)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matches = WwiseCache.chartAudioObjectsPoints
                .Where(n => (n.MetaData as PointMetaData).OwnerSerie.IsVisible)
                .Where(n =>
                {
                    var name = (n.MetaData as PointMetaData).Name.Split(':')[0];
                    var normalized = NormalizeKey(name);
                    var nameWords = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return queryWords.All(q => nameWords.Any(nw => nw.StartsWith(q, StringComparison.Ordinal)));
                });

            // Prepare normalized sets for O(1) tests
            var normalizedSearched = new HashSet<string>(Searches.Select(NormalizeKey));
            var normalizedListed = new HashSet<string>(SearchSuggestions.Select(NormalizeKey));

            foreach (var m in matches)
            {
                var suggestionToAdd = (m.MetaData as PointMetaData).Name.Split(':')[0];
                var key = NormalizeKey(suggestionToAdd);

                // Not already listed (same name, different case, spaces, _...) AND not already searched
                if (!normalizedListed.Contains(key) && !normalizedSearched.Contains(key))
                {
                    SearchSuggestions.Add(suggestionToAdd);
                    normalizedListed.Add(key);
                }
            }
        }
    }

    private static string NormalizeKey(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        // 1) _ -> space  2) Trim  3) Collapse spaces  4) Invariant lowercase
        var t = s.Replace('_', ' ').Trim();
        t = string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return t.ToLowerInvariant();
    }

    [RelayCommand]
    public void HideShowSeries(ButtonData btnData)
    {
        ChartViewModel._seriesByParentData.TryGetValue(btnData.ParentData, out ScatterSeries<ErrorPoint>? selectedSeries);

        if (selectedSeries.IsVisible)
        {
            selectedSeries.IsVisible = false;
            selectedSeries.ErrorPaint = null;
            btnData.Foreground = AppSettings.DisabledFiltersButtonForegroundColor(btnData.ParentData);
            btnData.Background = AppSettings.DisabledFiltersButtonBackgroundColor(btnData.ParentData);
            btnData.HoverBackground = AppSettings.DisabledFiltersButtonHoverBackgroundColor(btnData.ParentData);

        }
        else
        {
            selectedSeries.IsVisible = true;
            selectedSeries.ErrorPaint = new SolidColorPaint(
                Utility.LightenColor(btnData.ParentData.Color, 0.1f, 0.6f)
            )
            {
                StrokeThickness = 2
            };
            btnData.Foreground = AppSettings.EnabledFiltersButtonForegroundColor(btnData.ParentData);
            btnData.Background = AppSettings.EnabledFiltersButtonBackgroundColor(btnData.ParentData);
            btnData.HoverBackground = AppSettings.EnabledFiltersButtonHoverBackgroundColor(btnData.ParentData);
        }

        ChartViewModel.RepositionPointsWithoutOverlap();
        ChartViewModel.UpdateHighlightedPointPoistion();
    }

    public void AddCategorieFilterButton(ParentData parentData)
    {
        Log.Info($"Adding dynamic button: {parentData.Name}");

        ButtonData btnData = new ButtonData
        {
            Foreground = AppSettings.EnabledFiltersButtonForegroundColor(parentData),
            Background = AppSettings.EnabledFiltersButtonBackgroundColor(parentData),
            HoverBackground = AppSettings.EnabledFiltersButtonHoverBackgroundColor(parentData)
        };

        btnData.ParentData = parentData;
        btnData.Command = new RelayCommand(() => HideShowSeries(btnData));

        btnData.Label = $"{char.ToUpper(parentData.Name[0]) + parentData.Name.Substring(1)}";

        CategorieFilterButtons.Add(btnData);
    }

    public void ApplyDefaultFilter()
    {
        // Deactive every filters exept the first one
        for (var i = 0; i < CategorieFilterButtons.Count; i++)
        {
            if(i > 0)
                HideShowSeries(CategorieFilterButtons[i]);
        }
    }
}

public class ButtonData : ObservableObject
{
    private ParentData _parentData;
    public ParentData ParentData
    {
        get => _parentData;
        set => SetProperty(ref _parentData, value);
    }

    private string _label;
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private Brush _background;
    public Brush Background
    {
        get => _background;
        set => SetProperty(ref _background, value);
    }

    private Brush _foreground;
    public Brush Foreground
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    private Brush _hoverBackground;
    public Brush HoverBackground
    {
        get => _hoverBackground;
        set => SetProperty(ref _hoverBackground, value);
    }

    public ICommand Command
    {
        get; set;
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            return v == Visibility.Visible;
        }

        return false;
    }
}

public class SearchItemViewModel : ObservableObject
{
    private string _text;
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    private string _previousValideText;
    public string PreviousValideText
    {
        get => _previousValideText;
        set => SetProperty(ref _previousValideText, value);
    }
}
