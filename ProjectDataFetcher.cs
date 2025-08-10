using AK.Wwise.Waapi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WwiseHDRTool
{
    internal static class ProjectDataFetcher
    {
        public static (float min, float max)? ParseVolumeRTPCMinMaxFromWwu(string targetId, string audioObjWWUFolderPathParam)
        {
            if (string.IsNullOrEmpty(targetId))
                return null;

            if (WwiseCache.volumeRangeCache.TryGetValue(targetId, out var range))
                return range;

            // Fallback: if not preloaded for some reason, run a quick per-id scan (less efficient)
            try
            {
                var wwuFiles = Directory.GetFiles(audioObjWWUFolderPathParam, "*.wwu", SearchOption.AllDirectories);
                float globalMin = float.MaxValue;
                float globalMax = float.MinValue;
                bool found = false;

                foreach (var file in wwuFiles)
                {
                    var doc = XDocument.Load(file);

                    var objectsWithRtpc = doc.Descendants()
                        .Where(e => e.Name == "ActorMixer" || e.Name == "Sound" || e.Name == "RandomSequenceContainer" || e.Name == "BlendContainer")
                        .Where(obj => obj.Attribute("ID")?.Value?.Equals(targetId, StringComparison.OrdinalIgnoreCase) == true);

                    foreach (var obj in objectsWithRtpc)
                    {
                        var rtpcs = obj.Descendants("RTPC")
                            .Where(rtpc =>
                                rtpc.Element("PropertyList")?.Elements("Property")
                                    .Any(p => p.Attribute("Name")?.Value == "PropertyName" &&
                                              p.Attribute("Value")?.Value == "Volume") == true
                            );

                        foreach (var rtpc in rtpcs)
                        {
                            var yPositions = rtpc
                                .Descendants("Point")
                                .Select(p => p.Element("YPos")?.Value)
                                .Where(y => !string.IsNullOrWhiteSpace(y))
                                .Select(y =>
                                {
                                    if (float.TryParse(y.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
                                        return value;
                                    else
                                        return float.NaN;
                                })
                                .Where(v => !float.IsNaN(v))
                                .ToList();

                            if (yPositions.Count > 0)
                            {
                                found = true;
                                float min = yPositions.Min();
                                float max = yPositions.Max();

                                if (min < globalMin) globalMin = min;
                                if (max > globalMax) globalMax = max;
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
                return null;

            // Prefer cached value
            if (WwiseCache.volumeCache.TryGetValue(targetId, out var cached) && cached.HasValue)
                return cached;

            try
            {
                var query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(targetId))
                    ))
                );

                var options = new JObject(
                    new JProperty("return", new JArray("Volume"))
                );

                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                var volume = result["return"]?.First?["Volume"]?.ToString();

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
                Console.WriteLine($"[Warning] Failed to get volume for {targetId}: {e.Message}");
                WwiseCache.volumeCache[targetId] = null;
                return null;
            }
        }

        public static async Task<string?> GetTargetOutputBus(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return null;

            // Prefer cache
            if (WwiseCache.outputBusCache.TryGetValue(targetId, out var cachedBus) && !string.IsNullOrEmpty(cachedBus))
                return cachedBus;

            try
            {
                var query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(targetId))
                    ))
                );
                var options = new JObject(
                    new JProperty("return", new JArray("OutputBus"))
                );

                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                var busId = result["return"]?.First?["OutputBus"]?["id"]?.ToString();

                WwiseCache.outputBusCache[targetId] = busId;
                Console.WriteLine($"[Info] OutputBus ID for target {targetId}: {busId}");
                return busId;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Warning] Failed to get OutputBus for target {targetId}: {e.Message}");
                WwiseCache.outputBusCache[targetId] = null;
                return null;
            }
        }

        #endregion
    }
}
