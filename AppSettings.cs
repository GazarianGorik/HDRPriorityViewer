using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    public static class AppSettings
    {
        // Wwise audio objects point settings
        public static readonly SolidColorPaint chartPointStroke = new SolidColorPaint(SKColors.Black) { StrokeThickness = 1.5f };
        public static readonly float chartPointSize = 15;
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
        public static readonly SolidColorPaint chartPointHighlightedStroke = new SolidColorPaint(Utility.LightenColor(SKColors.LightGoldenrodYellow, 0, 0.1f)) { StrokeThickness = 2 };
        public static readonly int chartPointHighlightedDataLabelsSize = 18;

        // Clickable point settings        
        public static SolidColorPaint chartPointClickableFill(SKColor serieColor)
        {
            return new SolidColorPaint(Utility.LightenColor(serieColor, 0.4f, 0));
        }
        public static readonly float chartPointClickabeSize = 30;
    }
}
