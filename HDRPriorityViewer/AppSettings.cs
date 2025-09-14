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

using System.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace HDRPriorityViewer;

public static class AppSettings
{
    public static readonly int totalChartPointsWarningTreshold = 1000;

    // Wwise audio objects point settings
    public static readonly float chartPointSize = 15;
    public static SolidColorPaint chartPointStroke()
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorPaint(SKColors.Black) { StrokeThickness = 1.5f };
        else
            return new SolidColorPaint(SKColors.White) { StrokeThickness = 1.5f };

    }
    public static SolidColorPaint chartPointFill(SKColor parentDatacolor)
    {
        return new SolidColorPaint(parentDatacolor) { ZIndex = 10 };
    }
    public static SolidColorPaint chartPointFillDimed(SKColor currentColor)
    {
        return new SolidColorPaint(Utility.LightenColor(currentColor, 0f, 0.8f)) { ZIndex = 10 };
    }
    public static SolidColorPaint chartPointError(SKColor parentDatacolor)
    {
        return new SolidColorPaint(Utility.LightenColor(parentDatacolor, 0.1f, 0.6f)) { StrokeThickness = 2 };
    }
    public static SolidColorPaint chartPointErrorDimed(SKColor currentColor)
    {
        return new SolidColorPaint(Utility.LightenColor(currentColor, 0.1f, 0.9f)) { StrokeThickness = 2 };
    }

    // Highlighted point settings
    public static SolidColorPaint chartPointHighlightedStroke()
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorPaint(SKColors.White) { StrokeThickness = 2 };
        else
            return new SolidColorPaint(SKColors.Black) { StrokeThickness = 2 };

    }
    public static readonly int chartPointHighlightedDataLabelsSize = 20;
    public static SolidColorPaint chartPointHighlightedLabel()
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorPaint(SKColors.White);
        else
            return new SolidColorPaint(SKColors.Black);

    }

    // Clickable point settings        
    public static SolidColorPaint chartPointClickableFill(SKColor serieColor)
    {
        return new SolidColorPaint(Utility.LightenColor(serieColor, 0.4f, 0));
    }
    public static readonly float chartPointClickabeSize = 30;

    //Filters buttons
    public static SolidColorBrush EnabledFiltersButtonForegroundColor(ParentData parentData)
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(parentData.Color, 0.2f, 0.1f)));
        else
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.DimColor(parentData.Color, 0.2f, 0.1f)));
    }
    public static SolidColorBrush EnabledFiltersButtonBackgroundColor(ParentData parentData)
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(parentData.Color, 0.1f, 0.93f)));
        else
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.DimColor(parentData.Color, 0.1f, 0.93f)));
    }
    public static SolidColorBrush EnabledFiltersButtonHoverBackgroundColor(ParentData parentData)
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(parentData.Color, 0.2f, 0.7f)));
        else
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.DimColor(parentData.Color, 0.2f, 0.7f)));
    }

    public static SolidColorBrush DisabledFiltersButtonForegroundColor(ParentData parentData)
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(parentData.Color, 0, 0.5f)));
        else
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.DimColor(parentData.Color, 0, 0.5f)));
    }
    public static SolidColorBrush DisabledFiltersButtonBackgroundColor(ParentData parentData)
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(SKColors.Black, 0.1f, 0.5f)));
        else
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(SKColors.White, 0.1f, 0.5f)));
    }
    public static SolidColorBrush DisabledFiltersButtonHoverBackgroundColor(ParentData parentData)
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.LightenColor(parentData.Color, 0, 0.5f)));
        else
            return new SolidColorBrush(Utility.ToWinUIColor(Utility.DimColor(parentData.Color, 0, 0.5f)));
    }

    // WwiseProjectSaveState
    public static SolidColorBrush WwiseProjectUnsavedTextColor()
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Colors.OrangeRed);
        else
            return new SolidColorBrush(Colors.OrangeRed);
    }
    public static SolidColorBrush WwiseProjectSavedTextColor()
    {
        if (MainWindow.Instance.GetCurrentTheme() == ElementTheme.Dark)
            return new SolidColorBrush(Colors.Green);
        else
            return new SolidColorBrush(Colors.Green);
    }
    public static SolidColorBrush WwiseProjectNotConnectedTextColor()
    {
        return (SolidColorBrush)Application.Current.Resources["ApplicationSecondaryForegroundThemeBrush"];
    }
}
