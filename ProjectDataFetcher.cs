using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;

namespace WwiseHDRTool
{
    internal static class ProjectDataFetcher
    {
        public static (float min, float max)? ParseVolumeRTPCMinMaxFromWwu(string targetId, string audioObjWWUFolderPathParam)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            if (WwiseCache.volumeRangeCache.TryGetValue(targetId, out (float min, float max)? range))
            {
                return range;
            }

            // Fallback: if not preloaded for some reason, run a quick per-id scan (less efficient)
            try
            {
                string[] wwuFiles = Directory.GetFiles(audioObjWWUFolderPathParam, "*.wwu", SearchOption.AllDirectories);
                float globalMin = float.MaxValue;
                float globalMax = float.MinValue;
                bool found = false;

                foreach (string file in wwuFiles)
                {
                    XDocument doc = XDocument.Load(file);

                    IEnumerable<XElement> objectsWithRtpc = doc.Descendants()
                        .Where(e => e.Name == "ActorMixer" || e.Name == "Sound" || e.Name == "RandomSequenceContainer" || e.Name == "BlendContainer")
                        .Where(obj => obj.Attribute("ID")?.Value?.Equals(targetId, StringComparison.OrdinalIgnoreCase) == true);

                    foreach (XElement? obj in objectsWithRtpc)
                    {
                        IEnumerable<XElement> rtpcs = obj.Descendants("RTPC")
                            .Where(rtpc =>
                                rtpc.Element("PropertyList")?.Elements("Property")
                                    .Any(p => p.Attribute("Name")?.Value == "PropertyName" &&
                                              p.Attribute("Value")?.Value == "Volume") == true
                            );

                        foreach (XElement? rtpc in rtpcs)
                        {
                            List<float> yPositions = rtpc
                                .Descendants("Point")
                                .Select(p => p.Element("YPos")?.Value)
                                .Where(y => !string.IsNullOrWhiteSpace(y))
                                .Select(y =>
                                {
                                    if (float.TryParse(y.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
                                    {
                                        return value;
                                    }
                                    else
                                    {
                                        return float.NaN;
                                    }
                                })
                                .Where(v => !float.IsNaN(v))
                                .ToList();

                            if (yPositions.Count > 0)
                            {
                                found = true;
                                float min = yPositions.Min();
                                float max = yPositions.Max();

                                if (min < globalMin)
                                {
                                    globalMin = min;
                                }

                                if (max > globalMax)
                                {
                                    globalMax = max;
                                }
                            }
                        }
                    }
                }

                if (!found)
                {
                    return null;
                }

                return (globalMin, globalMax);
            }
            catch
            {
                return null;
            }
        }

        #region Legacy single-id WAAPI helpers (kept for compatibility, but not used in optimized flow)

        public static async Task<float?> GetVolume(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            // Prefer cached value
            if (WwiseCache.volumeCache.TryGetValue(targetId, out float? cached) && cached.HasValue)
            {
                return cached;
            }

            try
            {
                JObject query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(targetId))
                    ))
                );

                JObject options = new JObject(
                    new JProperty("return", new JArray("Volume"))
                );

                JObject result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                string? volume = result["return"]?.First?["Volume"]?.ToString();

                if (int.TryParse(volume, out int vol))
                {
                    WwiseCache.volumeCache[targetId] = vol;
                    return vol;
                }

                WwiseCache.volumeCache[targetId] = null;
                return null;
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to get volume for {targetId}: {e.ToString()}");
                WwiseCache.volumeCache[targetId] = null;
                return null;
            }
        }

        public static async Task<string?> GetTargetOutputBus(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            // Prefer cache
            if (WwiseCache.outputBusCache.TryGetValue(targetId, out string? cachedBus) && !string.IsNullOrEmpty(cachedBus))
            {
                return cachedBus;
            }

            try
            {
                JObject query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(targetId))
                    ))
                );
                JObject options = new JObject(
                    new JProperty("return", new JArray("OutputBus"))
                );

                JObject result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                string? busId = result["return"]?.First?["OutputBus"]?["id"]?.ToString();

                WwiseCache.outputBusCache[targetId] = busId;
                Log.Info($"[Info] OutputBus ID for target {targetId}: {busId}");
                return busId;
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to get OutputBus for target {targetId}: {e.ToString()}");
                WwiseCache.outputBusCache[targetId] = null;
                return null;
            }
        }

        #endregion
    }
}
