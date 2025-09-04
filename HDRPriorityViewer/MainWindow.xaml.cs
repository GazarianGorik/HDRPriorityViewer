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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpMarkup.WinUI.LiveChartsCore.SkiaSharpView;
using HDRPriorityViewer.Views;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HDRPriorityViewer
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType,
                                       int cxDesired, int cyDesired, uint fuLoad);

        const int WM_SETICON = 0x80;
        const int ICON_SMALL = 0;
        const int ICON_BIG = 1;
        const uint IMAGE_ICON = 1;
        const uint LR_LOADFROMFILE = 0x00000010;

        public static MainWindow Instance
        {
            get; private set;
        } // Allows accessing MainWindow from other classes
        public MainViewModel MainViewModel { get; } = new MainViewModel();
        public IntPtr WindowHandle => WindowNative.GetWindowHandle(this);
        public static Microsoft.UI.Dispatching.DispatcherQueue MainDispatcherQueue
        {
            get; private set;
        }
        IEnumerable<ChartPoint>? chartPointUnderCursor;
        public LoadingDialog? loadingDialog;

        private bool _isCtrlDown = false;
        private bool _isMenuDown = false;
        private bool _isPointClickable = false;

        private bool _isChartUpToDate = false;

        public MainWindow()
        {
            this.InitializeComponent();

            Instance = this;

            RootGrid.DataContext = MainViewModel;

            LoadAppIcon();

            this.AppWindow.Closing += AppWindow_Closing;

            // Maximize window
            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            MainDispatcherQueue = this.DispatcherQueue;

            var xAxis = new Axis
            {
                IsVisible = false
            };

            var yAxis = new Axis
            {
                Name = "HDR Priority",
                NameTextSize = 14,
            };
            Chart.XAxes = new List<Axis> { xAxis };
            Chart.YAxes = new List<Axis> { yAxis };

            SetChartAxisColors();

            RootGrid.ActualThemeChanged += (s, e) =>
            {
                SetChartAxisColors();
            };

            Chart.ZoomMode = ZoomAndPanMode.Both;

            Chart.HoveredPointsChanged += HoveredPointsChanged;
            Chart.PointerPressed += ChartPointerPressed;

            RootGrid.KeyDown += KeyDown;
            RootGrid.KeyUp += KeyUp;
            RootGrid.PointerPressed += RootGrid_PointerPressed;

            this.Activated += UpdateAppFocused;

            LiveCharts.DefaultSettings.MaxTooltipsAndLegendsLabelsWidth = 1000;
        }

        private void LoadAppIcon()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Charge ton icône custom
            var hIcon = LoadImage(IntPtr.Zero, "HDRPriorityViewer_Icon.ico", IMAGE_ICON, 256, 256, LR_LOADFROMFILE);

            // Applique pour la fenêtre (grande et petite icône)
            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIcon);
            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIcon);
        }

        private void SetChartAxisColors()
        {
            var separatorColor = Utility.ToSKColor(((SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"]).Color);
            var namePaintColor = Utility.ToSKColor(((SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"]).Color);
            var labelsPaintColor = Utility.ToSKColor(((SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"]).Color);

            Chart.YAxes.FirstOrDefault().SeparatorsPaint = new SolidColorPaint(separatorColor, 0.4f);
            Chart.YAxes.FirstOrDefault().NamePaint = new SolidColorPaint(namePaintColor, 1);
            Chart.YAxes.FirstOrDefault().LabelsPaint = new SolidColorPaint(labelsPaintColor, 1);
        }

        public ElementTheme GetCurrentTheme()
        {
            var root = (Application.Current as App).MainWindow.Content as FrameworkElement;
            return root.ActualTheme;
        }

        private async void ChartPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Log.Info($"Point clicked!");

            // Only open Wwise element if Ctrl is pressed
            if (!_isCtrlDown || chartPointUnderCursor == null || chartPointUnderCursor.Count() == 0)
            {
                return;
            }

            if (chartPointUnderCursor.Count() == 1)
            {
                var point = chartPointUnderCursor.Single();

                if (point.Context.DataSource is ErrorPoint errorPoint)
                {
                    var meta = errorPoint.MetaData as PointMetaData;
                    var name = meta?.Name ?? "Unknown";
                    var wwiseID = meta?.WwiseID ?? "Unknown";

                    Log.Info($"Point clicked: {name} ({wwiseID})");

                    await WaapiBridge.FocusWwiseWindow();
                    await WaapiBridge.FindObjectInProjectExplorer(wwiseID);
                    await WaapiBridge.InspectWwiseObject(wwiseID);
                }
            }
            else
            {
                MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
                {
                    Log.Error($"Can't open multiple Wwise objects at once. Please zoom in to select only one.");
                });
            }
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            AppUtility.ResetCacheAndUI();

            if (!WaapiBridge.ConnectedToWwise)
            {
                await WwiseConnexionProcess();
            }

            if (!WaapiBridge.ConnectedToWwise)
            {
                AnalyzeButton.IsEnabled = true;
                Log.Error("Failed to connect to Wwise.\r\nPlease ensure that your Wwise project is running, Waapi is correctly set, and User Preference window is closed.");
                return;
            }

            if (await WaapiBridge.IsWwiseProjectDirty() ?? true)
            {
                await OpenUnsavedWwiseProjectPopup();
            }

            await AnalyzeProcess();

            UpdateUIAfterAnalyze();

            _isChartUpToDate = true;
            SetChartUpdatedState();
        }

        IAsyncOperation<ContentDialogResult> _connectDialogTask;

        private void OpenLoadingDialog(string loadingText, string detailsText)
        {
            loadingDialog = new LoadingDialog
            {
                XamlRoot = MainWindow.Instance.Content.XamlRoot
            };
            loadingDialog.SetLoadingText(loadingText);
            loadingDialog.SetDetailsText(detailsText);
            _connectDialogTask = loadingDialog.ShowAsync();
        }

        private async Task CloseLoadingDialog()
        {
            if (loadingDialog != null)
            {
                loadingDialog.Hide();
            }

            await _connectDialogTask;
        }


        public async Task OpenUnsavedWwiseProjectPopup()
        {

            if (await EnqueueDialogAsync("Wwise project changes detected!",
            "You may want to first save your Wwise project to analyze the current state of your Wwise session.\nClick OK once you saved it.",
            false,
            "Ok",
            "Cancel") == ContentDialogResult.Secondary)
            {
                AnalyzeButton.IsEnabled = true;
                return;
            }
        }

        private async Task WwiseConnexionProcess()
        {
            AnalyzeButton.IsEnabled = false;

            // --- DIALOG 1: Connecting ---
            OpenLoadingDialog("Connecting to Wwise...", "");

            // Connexion (tâche lourde)
            await Task.Run(async () =>
            {
                Log.Info("Attempting to connect to Wwise...");
                await WaapiBridge.ConnectToWwise();
            });

            // Close dialog
            await CloseLoadingDialog();
        }

        private async Task AnalyzeProcess()
        {
            FirstAnalyzePanel.Visibility = Visibility.Collapsed;

            // --- DIALOG 2: Analysing ---
            OpenLoadingDialog("Analysing Wwise project...", "");

            await ProjectAnalyzer.AnalyzeProjectAsync();

            await CloseLoadingDialog();
        }

        private void UpdateUIAfterAnalyze()
        {
            // --- MAJ UI ---
            MainViewModel.SearchItems.Add(new SearchItemViewModel());
            Stats.Visibility = Visibility.Visible;
            Chart.IsEnabled = true;

            MainWindow.MainDispatcherQueue.TryEnqueue(() =>
            {
                MainViewModel.TotalChartPoints = $"{MainViewModel.ChartViewModel.GetAllPoints().Count()}";
            });
        }

        private IEnumerable<ChartPoint>? _lastHoveredPoints = null;

        private void HoveredPointsChanged(IChartView chart, IEnumerable<ChartPoint>? newItems, IEnumerable<ChartPoint>? oldItems)
        {
            chartPointUnderCursor = newItems?.ToList();

            //Log.Info($"Hovered points changed: {chartPointUnderCursor?.Count() ?? 0} points under cursor.");

            // Always try to update if Ctrl is pressed
            UpdateClickablePoint();
        }

        private void UpdateClickablePoint()
        {
            if (chartPointUnderCursor == null || !chartPointUnderCursor.Any())
            {
                if (_isPointClickable)
                {
                    MainViewModel.ChartViewModel.UnmakeClickablePointByName();
                    _isPointClickable = false;
                    _lastHoveredPoints = null;
                }
                return;
            }

            if (_isCtrlDown)
            {
                var chartPoint = chartPointUnderCursor.FirstOrDefault();
                if (chartPoint?.Context.DataSource is ErrorPoint ep)
                {
                    var pointName = (ep.MetaData as PointMetaData)?.Name?.Split(':')[0]?.Trim();
                    if (!string.IsNullOrEmpty(pointName))
                    {
                        // Only redo if the point has changed
                        if (!_isPointClickable || !IsSameAsLast(chartPointUnderCursor))
                        {
                            MainViewModel.ChartViewModel.MakeClickablePointByName(ep);
                            _lastHoveredPoints = chartPointUnderCursor.ToList();
                            _isPointClickable = true;
                        }
                    }
                }
            }
            else
            {
                if (_isPointClickable)
                {
                    MainViewModel.ChartViewModel.UnmakeClickablePointByName();
                    _isPointClickable = false;
                    _lastHoveredPoints = null;
                }
            }
        }

        private bool IsSameAsLast(IEnumerable<ChartPoint> newPoints)
        {
            if (_lastHoveredPoints == null)
            {
                return false;
            }

            var newCoords = newPoints
                .Select(p => (p.Coordinate.PrimaryValue, p.Coordinate.SecondaryValue))
                .OrderBy(t => t.PrimaryValue)
                .ThenBy(t => t.SecondaryValue)
                .ToList();

            var lastCoords = _lastHoveredPoints
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

        private async void UpdateAppFocused(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            // Window lost focus
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                Chart.ZoomMode = ZoomAndPanMode.Both;
                _isMenuDown = false;
                _isCtrlDown = false;

                chartPointUnderCursor = null;
            }
            else // Window gained focus
            {
                await SetWwiseProjectSavedState();
                SetChartUpdatedState();
            }
        }

        public async Task SetWwiseProjectSavedState()
        {
            if (WaapiBridge.ConnectedToWwise)
            {
                if (await WaapiBridge.IsWwiseProjectDirty() ?? true)
                {
                    WwiseProjectSavedState.Text = "Unsaved";
                    WwiseProjectSavedState.Foreground = AppSettings.WwiseProjectUnsavedTextColor();
                    _isChartUpToDate = false;
                }
                else
                {
                    WwiseProjectSavedState.Text = "Saved";
                    WwiseProjectSavedState.Foreground = AppSettings.WwiseProjectSavedTextColor();
                }
            }
            else
            {
                WwiseProjectSavedState.Text = "Not connected";
                WwiseProjectSavedState.Foreground = AppSettings.WwiseProjectNotConnectedTextColor();
            }
        }

        public void SetChartUpdatedState()
        {
            if (WaapiBridge.ConnectedToWwise)
            {
                if (_isChartUpToDate)
                {
                    ChartUpdatedState.Text = "(Updated)";
                    ChartUpdatedState.Foreground = AppSettings.WwiseProjectSavedTextColor();
                }
                else
                {
                    ChartUpdatedState.Text = "(Outdated!)";
                    ChartUpdatedState.Foreground = AppSettings.WwiseProjectUnsavedTextColor();
                }
            }
            else
            {
                ChartUpdatedState.Text = "";
                ChartUpdatedState.Foreground = AppSettings.WwiseProjectNotConnectedTextColor();
            }
        }

        private void KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control && !_isCtrlDown)
            {
                _isCtrlDown = true;
                Chart.ZoomMode = ZoomAndPanMode.Y;
                UpdateClickablePoint();
            }

            if (e.Key == VirtualKey.Menu && !_isMenuDown)
            {
                _isMenuDown = true;
                Chart.ZoomMode = ZoomAndPanMode.X;
            }
        }

        private void KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control && _isCtrlDown)
            {
                _isCtrlDown = false;
                Chart.ZoomMode = _isMenuDown ? ZoomAndPanMode.X : ZoomAndPanMode.Both;
                UpdateClickablePoint();
            }

            if (e.Key == VirtualKey.Menu && _isMenuDown)
            {
                _isMenuDown = false;
                Chart.ZoomMode = _isCtrlDown ? ZoomAndPanMode.Y : ZoomAndPanMode.Both;
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
                    Log.Error(e);
                }
            }).Start();
        }

        private readonly ConcurrentQueue<(string Title, string Message, string CloseText, bool allowCopy, string? SecondaryText,
            TaskCompletionSource<ContentDialogResult> Tcs, Style CloseButtonStyle, Style SecondaryButtonStyle)> _dialogQueue = new();
        private bool _isDialogOpen = false;

        public Task<ContentDialogResult> EnqueueDialogAsync(
            string title,
            string message,
            bool allowCopy,
            string closeButtonText = "OK",
            string? secondaryButtonText = null,
            Style closeButtonStyle = null,
            Style secondaryButtonStyle = null)
        {
            var tcs = new TaskCompletionSource<ContentDialogResult>();
            _dialogQueue.Enqueue((title, message, closeButtonText, allowCopy, secondaryButtonText, tcs, closeButtonStyle, secondaryButtonStyle));
            _ = ProcessDialogQueueAsync();
            return tcs.Task;
        }

        private async Task ProcessDialogQueueAsync()
        {
            if (_isDialogOpen) return;
            _isDialogOpen = true;


            DispatcherQueue.TryEnqueue(() =>
            {
                if (loadingDialog != null && loadingDialog.Visibility == Visibility.Visible)
                {
                    loadingDialog.Hide();
                    loadingDialog = null;
                }
            });


            while (_dialogQueue.TryDequeue(out var item))
            {
                try
                {
                    // Ensure execution on the UI thread
                    var result = await DispatcherQueue.EnqueueAsync(async () =>
                    {
                        var dialog = new ContentDialog
                        {
                            Title = item.Title,
                            Content = new TextBlock { Text = item.Message, TextWrapping = TextWrapping.Wrap },
                            CloseButtonText = item.CloseText,
                            XamlRoot = this.Content.XamlRoot
                        };

                        if (item.allowCopy)
                        {
                            dialog.PrimaryButtonText = "Copy";
                            dialog.PrimaryButtonClick += (sender, args) =>
                            {
                                args.Cancel = true;

                                dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["CopyButtonCopied"];
                                dialog.PrimaryButtonText = "Copied";

                                CopyDialogMessageToClipboard(item.Message);
                            };
                        }

                        if (item.CloseButtonStyle != null)
                            dialog.CloseButtonStyle = item.CloseButtonStyle;

                        if (item.SecondaryButtonStyle != null)
                            dialog.SecondaryButtonStyle = item.SecondaryButtonStyle;

                        if (!string.IsNullOrWhiteSpace(item.SecondaryText))
                            dialog.SecondaryButtonText = item.SecondaryText;

                        return await dialog.ShowAsync();
                    });

                    item.Tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    item.Tcs.SetException(ex);
                }
            }

            _isDialogOpen = false;
        }

        private void CopyDialogMessageToClipboard(String message)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(message);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public CartesianChart GetChart()
        {
            return Chart;
        }
        public bool IsCtrlDown()
        {
            return _isCtrlDown;
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

        private void SearchField_LostFocus(object sender, RoutedEventArgs e)
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
            CloseSuggestionPopup();
        }

        private void SearchSuggestionLostFocus(object sender, RoutedEventArgs e)
        {
            CloseSuggestionPopup();
        }

        void CloseSuggestionPopup()
        {
            if (SuggestionsPopup.IsOpen)
            {
                SuggestionsPopup.IsOpen = false;
            }
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
                SuggestionsPopup.IsOpen = false;
                lb.SelectedItem = null;
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
                        {
                            popupChild.Width = tb.ActualWidth;
                        }

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
