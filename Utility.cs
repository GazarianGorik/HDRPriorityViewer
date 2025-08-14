using SkiaSharp;
using System;

namespace WwiseHDRTool
{
    public static class Utility
    {
        public static SKColor LightenColor(SKColor color, float vanished = 0.3f, float transparency = 0f, bool invert = false)
        {
            // Clamp values to avoid overflow
            vanished = Math.Clamp(vanished, 0f, 1f);
            transparency = Math.Clamp(Math.Abs(1 - transparency), 0f, 1f);

            // Invert colors if requested
            if (invert)
            {
                color = new SKColor(
                    (byte)(255 - color.Red),
                    (byte)(255 - color.Green),
                    (byte)(255 - color.Blue),
                    color.Alpha // Keep original alpha here
                );
            }

            // Apply lightening
            byte r = (byte)(color.Red + ((255 - color.Red) * vanished));
            byte g = (byte)(color.Green + ((255 - color.Green) * vanished));
            byte b = (byte)(color.Blue + ((255 - color.Blue) * vanished));
            byte a = (byte)(255 * transparency);

            return new SKColor(r, g, b, a);
        }

        public static SKColor OpaqueColor(SKColor color)
        {
            // Force full opacity
            byte r = color.Red;
            byte g = color.Green;
            byte b = color.Blue;
            byte a = 255;

            return new SKColor(r, g, b, a);
        }
    }
}
