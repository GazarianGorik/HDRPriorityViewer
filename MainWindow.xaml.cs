using CSharpMarkup.WinUI.LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WwiseHDRTool
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } // Permet d'accéder à MainWindow depuis ailleurs
        public MainViewModel MainViewModel { get; } = new MainViewModel();
        public ChartViewModel ChartViewModel { get; } = new ChartViewModel();
        private const string EventsFolderKey = "EventsFolderPath";
        public IntPtr WindowHandle => WindowNative.GetWindowHandle(this);
        public static Microsoft.UI.Dispatching.DispatcherQueue MainDispatcherQueue { get; private set; }
        IEnumerable<ChartPoint>? chartPointUnderCursor;

        private bool isCtrlDown = false;
        private bool isMenuDown = false;

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
            this.AppWindow.Closing += AppWindow_Closing;
            RootGrid.DataContext = this;
            MainDispatcherQueue = this.DispatcherQueue;

            var xAxis = new Axis
            {
                /*MaxLimit =-2,
                MinLimit = 100*/
                IsVisible = false
            };
            var yAxis = new Axis
            {
                Name = "Priority",
                NameTextSize = 14,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightYellow)
                {
                    StrokeThickness = 0.4f
                },
                NamePaint = new SolidColorPaint(SKColors.LightYellow, 1),
                LabelsPaint = new SolidColorPaint(SKColors.LightYellow, 1)
            };
            chart.XAxes = new List<Axis> { xAxis};
            chart.YAxes = new List<Axis> { yAxis };

            chart.ZoomMode = ZoomAndPanMode.Both;

            chart.HoveredPointsChanged += HoveredPointsChanged;
            chart.PointerPressed += ChartPointerPressed;

            RootGrid.KeyDown += KeyDown;
            RootGrid.KeyUp += KeyUp;
            this.Activated += UpdateAppFocused;

            LiveCharts.DefaultSettings.MaxTooltipsAndLegendsLabelsWidth = 1000;
        }

        private async void ChartPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Console.WriteLine($"Point clicked!");

            // Only open Wwise element if Ctrl is pressed
            if (!isCtrlDown)
                return;

            if (chartPointUnderCursor.Count() == 1)
            {
                var point = chartPointUnderCursor.Single();

                if (point.Context.DataSource is ErrorPoint errorPoint)
                {
                    var meta = errorPoint.MetaData as PointMetaData;
                    string name = meta?.Name ?? "Unknown";
                    string wwiseID = meta?.WwiseID ?? "Unknown";

                    Console.WriteLine($"Point cliqué : {name} ({wwiseID})");

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

        private void HoveredPointsChanged(IChartView chart, IEnumerable<ChartPoint>? newItems, IEnumerable<ChartPoint>? oldItems)
        {
            if (newItems == null || !newItems.Any())
            {
                chartPointUnderCursor = null;
                Console.WriteLine("No points under cursor");
                return;
            }
            chartPointUnderCursor = newItems;
            Console.WriteLine($"Points under cursor: {newItems.Count()}");
        }

        private void UpdateAppFocused(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            // UNFOCUSED
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                Console.WriteLine("All key NOT pressed");
                chart.ZoomMode = ZoomAndPanMode.Both;
                isMenuDown = false;
                isCtrlDown = false;

                chartPointUnderCursor = null;
                Console.WriteLine("No points under cursor");
            }
            else // FOCUSED
            {
            }
        }

        private void KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var key = e.Key;

            if (key == VirtualKey.Control)
            {
                //First press
                if (!isCtrlDown)
                {
                    chart.ZoomMode = ZoomAndPanMode.Y;
                    Console.WriteLine("Control key pressed");
                }

                //Spam
                isCtrlDown = true;
            }
            if (key == VirtualKey.Menu)
            {
                //First press
                if (!isMenuDown)
                {
                    chart.ZoomMode = ZoomAndPanMode.X;
                    Console.WriteLine("Menu key pressed");
                }

                //Spam
                isMenuDown = true;
            }
        }

        private void KeyUp(object sender, KeyRoutedEventArgs e)
        {
            var key = e.Key;

            if (key == VirtualKey.Control)
            {
                //First press
                if (isCtrlDown)
                {
                    Console.WriteLine("Control key NOT pressed");

                    if (isMenuDown)
                        chart.ZoomMode = ZoomAndPanMode.X;
                    else
                        chart.ZoomMode = ZoomAndPanMode.Both;
                }

                //Spam
                isCtrlDown = false;
            }
            if (key == VirtualKey.Menu)
            {
                //First press
                if (isMenuDown)
                {
                    Console.WriteLine("Menu key NOT pressed");

                    if (isCtrlDown)
                        chart.ZoomMode = ZoomAndPanMode.Y;
                    else
                        chart.ZoomMode = ZoomAndPanMode.Both;
                }

                //Spam
                isMenuDown = false;
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

        private async void Button_ConnectToWwise(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await WaapiBridge.ConnectToWwise();
                });
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Erreur", ex.Message);
            }
        }


        private void Button_ListSoundObjectsRoutedToHDR(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                ChartBridge.ListSoundObjectsRoutedToHDR().Wait();
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
                // Copier dans le presse-papier
                var dataPackage = new DataPackage();
                dataPackage.SetText(message);
                Clipboard.SetContent(dataPackage);
            }
            else
            {
                isDialogOpen = false;
            }
        }

        public void SetChartTooltipFilter(ISeries[] whiteListedSeries)
        {
            var defaultTooltip = chart.Tooltip;
            chart.Tooltip = new FilteredTooltip(defaultTooltip, whiteListedSeries);
        }

        public CartesianChart GetChart()
        {
            return chart;
        }
        public bool IsCtrlDown()
        {
            return isCtrlDown;
        }
    }
}

public class FilteredTooltip : IChartTooltip
{
    private readonly IChartTooltip _innerTooltip;
    private readonly ISeries[] _excludedSeries;

    public FilteredTooltip(IChartTooltip innerTooltip, params ISeries[] excludedSeries)
    {
        _innerTooltip = innerTooltip ?? throw new ArgumentNullException(nameof(innerTooltip));
        _excludedSeries = excludedSeries ?? Array.Empty<ISeries>();
    }

    public TooltipFindingStrategy TooltipFindingStrategy { get; set; } = TooltipFindingStrategy.Automatic;

    public void Show(IEnumerable<ChartPoint> points, Chart chart)
    {
        if (points.Any(p => _excludedSeries.Contains(p.Context.Series)))
        {
            Hide(chart);
        }
        else
        {
            _innerTooltip.Show(points, chart);
        }
    }

    public void Hide(Chart chart)
    {
        _innerTooltip.Hide(chart);
    }
}