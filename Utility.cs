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
using SkiaSharp;
using Windows.UI;

namespace HDRPriorityGraph
{
    public static class Utility
    {
        public static Color ToWinUIColor(this SKColor skColor)
        {
            return Color.FromArgb(
                skColor.Alpha,
                skColor.Red,
                skColor.Green,
                skColor.Blue
            );
        }

        public static SKColor ToSKColor(this Color color)
        {
            return new SKColor(
                color.R,
                color.G,
                color.B,
                color.A
            );
        }

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

        public static SKColor DimColor(SKColor color, float factor = 0.3f, float transparency = 0f, bool invert = false)
        {
            // Clamp values to avoid overflow
            factor = Math.Clamp(factor, 0f, 1f);
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

            // Apply dimming (move channels toward 0)
            byte r = (byte)(color.Red * (1 - factor));
            byte g = (byte)(color.Green * (1 - factor));
            byte b = (byte)(color.Blue * (1 - factor));
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

        /// <summary>
        /// Generates a pastel color palette using HSV/HSL.
        /// Hue varies, Saturation is low (~0.4), Value is high (~0.9).
        /// </summary>
        public static List<SKColor> GeneratePastelPalette(int count)
        {
            var colors = new List<SKColor>();
            for (int i = 0; i < count; i++)
            {
                float hue = (360f / count) * i;  // réparti sur le cercle chromatique
                float saturation = 0.6f;         // pastel → faible saturation
                float value = 0.9f;              // clair → forte luminosité

                colors.Add(HsvToColor(hue, saturation, value));
            }
            return colors;
        }

        /// <summary>
        /// Convert HSV into SKColor
        /// </summary>
        public static SKColor HsvToColor(float h, float s, float v)
        {
            int hi = (int)(h / 60) % 6;
            float f = h / 60 - (int)(h / 60);

            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);

            float r = 0, g = 0, b = 0;
            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }

            return new SKColor(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255)
            );
        }
    }
}
