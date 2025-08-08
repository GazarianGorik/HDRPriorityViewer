using CSharpMarkup.WinUI.LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WwiseHDRTool
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } // Permet d'accéder à MainWindow depuis ailleurs
        public MainViewModel MainViewModel { get; } = new MainViewModel();
        public GraphViewModel GraphViewModel { get; } = new GraphViewModel();
        private const string EventsFolderKey = "EventsFolderPath";
        public IntPtr WindowHandle => WindowNative.GetWindowHandle(this);
        public static DispatcherQueue MainDispatcherQueue { get; private set; }

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
                MaxLimit =-2,
                MinLimit = 100
            };
            var yAxis = new Axis
            {
                Name = "dB",
                SeparatorsPaint = new SolidColorPaint(SKColors.LightYellow)
                {
                    StrokeThickness = 0.4f
                }
            };

            chart.XAxes = new List<Axis> { xAxis};
            chart.YAxes = new List<Axis> { yAxis };

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
                ProjectDataFetcher.ListSoundObjectsRoutedToHDR().Wait();
            }).Start();
        }


        private ContentDialog _dialog;
        private string _message;

        private async Task ShowMessageAsync(string title, string message)
        {
            _message = message;

            var copyButton = new Button
            {
                Content = "Copy Error",
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 100
            };
            copyButton.Click += CopyButton_Click;

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            stackPanel.Children.Add(copyButton);

            _dialog = new ContentDialog
            {
                Title = title,
                Content = stackPanel,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await _dialog.ShowAsync();
        }
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_message);
            Clipboard.SetContent(dataPackage);
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