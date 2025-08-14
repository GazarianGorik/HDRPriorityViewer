using CSharpMarkup.WinUI.LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WwiseHDRTool
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } // Allows accessing MainWindow from other classes
        public MainViewModel MainViewModel { get; } = new MainViewModel();
        public IntPtr WindowHandle => WindowNative.GetWindowHandle(this);
        public static Microsoft.UI.Dispatching.DispatcherQueue MainDispatcherQueue { get; private set; }
        IEnumerable<ChartPoint>? chartPointUnderCursor;

        private bool isCtrlDown = false;
        private bool isMenuDown = false;
        private bool isPointClickable = false;

        public MainWindow()
        {
            this.InitializeComponent();

            Instance = this;

            RootGrid.DataContext = MainViewModel;

            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
            this.AppWindow.Closing += AppWindow_Closing;

            MainDispatcherQueue = this.DispatcherQueue;

            var xAxis = new Axis
            {
                IsVisible = false
            };
            var yAxis = new Axis
            {
                Name = "HDR Priority",
                NameTextSize = 14,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightYellow)
                {
                    StrokeThickness = 0.4f
                },
                NamePaint = new SolidColorPaint(SKColors.LightYellow, 1),
                LabelsPaint = new SolidColorPaint(SKColors.LightYellow, 1)
            };
            chart.XAxes = new List<Axis> { xAxis };
            chart.YAxes = new List<Axis> { yAxis };

            chart.ZoomMode = ZoomAndPanMode.Both;

            chart.HoveredPointsChanged += HoveredPointsChanged;
            chart.PointerPressed += ChartPointerPressed;

            RootGrid.KeyDown += KeyDown;
            RootGrid.KeyUp += KeyUp;
            RootGrid.PointerPressed += RootGrid_PointerPressed;

            this.Activated += UpdateAppFocused;

            LiveCharts.DefaultSettings.MaxTooltipsAndLegendsLabelsWidth = 1000;
        }

        private async void ChartPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Console.WriteLine($"Point clicked!");

            // Only open Wwise element if Ctrl is pressed
            if (!isCtrlDown || chartPointUnderCursor == null || chartPointUnderCursor.Count() == 0)
                return;

            if (chartPointUnderCursor.Count() == 1)
            {
                var point = chartPointUnderCursor.Single();

                if (point.Context.DataSource is ErrorPoint errorPoint)
                {
                    var meta = errorPoint.MetaData as PointMetaData;
                    string name = meta?.Name ?? "Unknown";
                    string wwiseID = meta?.WwiseID ?? "Unknown";

                    Console.WriteLine($"Point clicked: {name} ({wwiseID})");

                    await WaapiBridge.FocusWwiseWindow();
                    await WaapiBridge.FindObjectInProjectExplorer(wwiseID);
                    await WaapiBridge.InspectWwiseObject(wwiseID);
                }
            }
            else
            {
                MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
                {
                    MainWindow.Instance.ShowMessageAsync("Warning", $"Can't open multiple Wwise objects at once. Please zoom in to select only one.");
                });
            }
        }

        private IEnumerable<ChartPoint>? lastNewItems = null;

        private void HoveredPointsChanged(IChartView chart, IEnumerable<ChartPoint>? newItems, IEnumerable<ChartPoint>? oldItems)
        {
            chartPointUnderCursor = newItems?.ToList();

            Console.WriteLine($"Hovered points changed: {chartPointUnderCursor?.Count() ?? 0} points under cursor.");

            // Always try to update if Ctrl is pressed
            UpdateClickablePoint();
        }

        private void UpdateClickablePoint()
        {
            if (chartPointUnderCursor == null || !chartPointUnderCursor.Any())
            {
                if (isPointClickable)
                {
                    MainViewModel.ChartViewModel.UnmakeClickablePointByName();
                    isPointClickable = false;
                    lastNewItems = null;
                }
                return;
            }

            if (isCtrlDown)
            {
                var chartPoint = chartPointUnderCursor.FirstOrDefault();
                if (chartPoint?.Context.DataSource is ErrorPoint ep)
                {
                    var pointName = (ep.MetaData as PointMetaData)?.Name?.Split(':')[0]?.Trim();
                    if (!string.IsNullOrEmpty(pointName))
                    {
                        // Only redo if the point has changed
                        if (!isPointClickable || !IsSameAsLast(chartPointUnderCursor))
                        {
                            MainViewModel.ChartViewModel.MakeClickablePointByName(ep);
                            lastNewItems = chartPointUnderCursor.ToList();
                            isPointClickable = true;
                        }
                    }
                }
            }
            else
            {
                if (isPointClickable)
                {
                    MainViewModel.ChartViewModel.UnmakeClickablePointByName();
                    isPointClickable = false;
                    lastNewItems = null;
                }
            }
        }

        private bool IsSameAsLast(IEnumerable<ChartPoint> newPoints)
        {
            if (lastNewItems == null) return false;

            var newCoords = newPoints
                .Select(p => (p.Coordinate.PrimaryValue, p.Coordinate.SecondaryValue))
                .OrderBy(t => t.PrimaryValue)
                .ThenBy(t => t.SecondaryValue)
                .ToList();

            var lastCoords = lastNewItems
                .Select(p => (p.Coordinate.PrimaryValue, p.Coordinate.SecondaryValue))
                .OrderBy(t => t.PrimaryValue)
                .ThenBy(t => t.SecondaryValue)
                .ToList();

            const double tolerance = 0.0001;

            return newCoords.Count == lastCoords.Count &&
                   newCoords.Zip(lastCoords, (a, b) =>
                       Math.Abs(a.PrimaryValue - b.PrimaryValue) < tolerance &&
                       Math.Abs(a.SecondaryValue - b.SecondaryValue) < tolerance
                   ).All(equal => equal);
        }

        private void UpdateAppFocused(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            // Window lost focus
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                chart.ZoomMode = ZoomAndPanMode.Both;
                isMenuDown = false;
                isCtrlDown = false;

                chartPointUnderCursor = null;
            }
            else // Window gained focus
            {
            }
        }

        private void KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control && !isCtrlDown)
            {
                isCtrlDown = true;
                chart.ZoomMode = ZoomAndPanMode.Y;
                UpdateClickablePoint();
            }

            if (e.Key == VirtualKey.Menu && !isMenuDown)
            {
                isMenuDown = true;
                chart.ZoomMode = ZoomAndPanMode.X;
            }
        }

        private void KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control && isCtrlDown)
            {
                isCtrlDown = false;
                chart.ZoomMode = isMenuDown ? ZoomAndPanMode.X : ZoomAndPanMode.Both;
                UpdateClickablePoint();
            }

            if (e.Key == VirtualKey.Menu && isMenuDown)
            {
                isMenuDown = false;
                chart.ZoomMode = isCtrlDown ? ZoomAndPanMode.Y : ZoomAndPanMode.Both;
            }
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            new Thread(() =>
            {
                try
                {
                    WaapiBridge.Disconnect().Wait();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }).Start();
        }

        private ContentDialog _dialog;
        private string _message;
        private bool isDialogOpen = false;

        public async Task ShowMessageAsync(string title, string message)
        {
            if (isDialogOpen)
                return;

            isDialogOpen = true;
            _message = message;

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            _dialog = new ContentDialog
            {
                Title = title,
                Content = stackPanel,
                PrimaryButtonText = "Copy",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await _dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                isDialogOpen = false;
                // Copy to clipboard
                var dataPackage = new DataPackage();
                dataPackage.SetText(message);
                Clipboard.SetContent(dataPackage);
            }
            else
            {
                isDialogOpen = false;
            }
        }

        public CartesianChart GetChart()
        {
            return chart;
        }
        public bool IsCtrlDown()
        {
            return isCtrlDown;
        }

        private void SearchField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SearchItemViewModel item)
            {
                if (e.Key == VirtualKey.Enter)
                {
                    MainViewModel.ValidateSearchItem(item);
                }
            }
        }

        private void SehField_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SearchItemViewModel item)
            {
                MainViewModel.ValidateSearchItem(item);
            }
        }

        private SearchItemViewModel _lastFocusedItem;

        private void SearchField_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SearchItemViewModel item)
            {
                _lastFocusedItem = item;
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (SuggestionsPopup.IsOpen)
                SuggestionsPopup.IsOpen = false;
        }

        private void SearchSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox lb && lb.SelectedItem is string selected && _lastFocusedItem != null)
            {
                // Put the suggestion in the active search field
                _lastFocusedItem.Text = selected;

                // Validate the item (which may create a new field if needed)
                MainViewModel.ValidateSearchItem(_lastFocusedItem);

                // Close the popup
                lb.SelectedItem = null;
                SuggestionsPopup.IsOpen = false;
            }
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SearchItemViewModel item)
            {
                MainViewModel.UpdateSuggestions(item);

                if (MainViewModel.SearchSuggestions.Count > 0)
                {
                    tb.DispatcherQueue.TryEnqueue(() =>
                    {
                        var transform = tb.TransformToVisual(RootGrid);
                        var position = transform.TransformPoint(new Windows.Foundation.Point(0, tb.ActualHeight));

                        var padding = RootGrid.Padding;
                        SuggestionsPopup.HorizontalOffset = position.X - padding.Left;
                        SuggestionsPopup.VerticalOffset = position.Y - padding.Top;

                        if (SuggestionsPopup.Child is FrameworkElement popupChild)
                            popupChild.Width = tb.ActualWidth;

                        SuggestionsPopup.IsOpen = true;
                    });
                }
                else
                {
                    SuggestionsPopup.IsOpen = false;
                }
            }
        }
    }
}
