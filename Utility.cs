using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    public static class Utility
    {
        public static SKColor LightenColor(SKColor color, float vanished = 0.3f, float transparency = 0f, bool invert = false)
        {
            // Clamp des valeurs pour éviter les débordements
            vanished = Math.Clamp(vanished, 0f, 1f);
            transparency = Math.Clamp(Math.Abs(1 - transparency), 0f, 1f);

            // Inversion des couleurs si demandé
            if (invert)
            {
                color = new SKColor(
                    (byte)(255 - color.Red),
                    (byte)(255 - color.Green),
                    (byte)(255 - color.Blue),
                    color.Alpha // On garde l'alpha original ici
                );
            }

            // Application de l'éclaircissement
            byte r = (byte)(color.Red + (255 - color.Red) * vanished);
            byte g = (byte)(color.Green + (255 - color.Green) * vanished);
            byte b = (byte)(color.Blue + (255 - color.Blue) * vanished);
            byte a = (byte)(255 * transparency);

            return new SKColor(r, g, b, a);
        }

        public static SKColor OpaqueColor(SKColor color)
        {
            // Application de l'éclaircissement
            byte r = (byte)(color.Red);
            byte g = (byte)(color.Green);
            byte b = (byte)(color.Blue);
            byte a = (byte)(255);

            return new SKColor(r, g, b, a);
        }
    }
}
