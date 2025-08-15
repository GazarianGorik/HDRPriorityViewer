using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WwiseHDRTool.Views;

namespace WwiseHDRTool;

public partial class MainViewModel : ObservableObject
{
    public ChartViewModel ChartViewModel { get; }

    [ObservableProperty]
    private string searchText = string.Empty;

    public ObservableCollection<string> SearchSuggestions { get; } = new();

    public ObservableCollection<ButtonData> CategorieFilterButtons { get; } = new();

    public ObservableCollection<SearchItemViewModel> SearchItems { get; } = new();

    public IRelayCommand<SearchItemViewModel> RemoveSearchItemCommand { get; }

    public List<string> Searches = new List<string>();

    private string _wwiseVersion;
    public string WwiseVersion
    {
        get => _wwiseVersion;
        set => SetProperty(ref _wwiseVersion, "Version: " + value); // si tu as BaseViewModel avec INotifyPropertyChanged
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
        Log.Info("[Info] Initializing MainViewModel...");
        ChartViewModel = new ChartViewModel();
        RemoveSearchItemCommand = new RelayCommand<SearchItemViewModel>(RemoveSearchItem);
    }

    private void RemoveSearchItem(SearchItemViewModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.PreviousValideText))
        {
            if (Searches.Contains(item.PreviousValideText))
            {
                Searches.Remove(item.PreviousValideText);
            }
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

        // 3. Add to the global list if it’s a new term
        if (!Searches.Contains(trimmedText))
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
            if (SearchItems.Count > 1)
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
            IEnumerable<ErrorPoint> matches = WwiseCache.chartDefaultPoints
                .Where(n => (n.MetaData as PointMetaData).OwnerSerie.IsVisible)
                .Where(n => (n.MetaData as PointMetaData).Name.Split(':')[0].Contains(item.Text, StringComparison.OrdinalIgnoreCase) &&
                    !(n.MetaData as PointMetaData).Name.Split(':')[0].Equals(item.Text, StringComparison.OrdinalIgnoreCase));

            foreach (ErrorPoint? m in matches)
            {
                SearchSuggestions.Add((m.MetaData as PointMetaData).Name.Split(':')[0]);
            }
        }
    }

    [RelayCommand]
    public void HideShowSeries(ButtonData btnData)
    {
        ChartViewModel._seriesByParentData.TryGetValue(btnData.ParentData, out ScatterSeries<ErrorPoint>? selectedSeries);

        if (selectedSeries.IsVisible)
        {
            selectedSeries.IsVisible = false;
            selectedSeries.ErrorPaint = null;
            btnData.Background = new SolidColorBrush(Colors.LightCoral);
        }
        else
        {
            selectedSeries.IsVisible = true;
            selectedSeries.ErrorPaint = new SolidColorPaint(
                Utility.LightenColor(btnData.ParentData.Color, 0.1f, 0.6f)
            )
            { StrokeThickness = 2 };
            btnData.Background = new SolidColorBrush(Colors.LightBlue);
        }

        ChartViewModel.RepositionPointsWithoutOverlap();
    }

    public void AddCategorieFilterButton(ParentData parentData)
    {
        Log.Info($"[Info] Adding dynamic button: {parentData.Name}");

        ButtonData btnData = new ButtonData
        {
            Background = new SolidColorBrush(Colors.LightBlue)
        };

        // Now we can use btnData in the lambda without any problem
        btnData.ParentData = parentData;
        btnData.Command = new RelayCommand(() => HideShowSeries(btnData));

        btnData.Label = $"{char.ToUpper(parentData.Name[0]) + parentData.Name.Substring(1)}";

        CategorieFilterButtons.Add(btnData);
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

    public ICommand Command { get; set; }
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
