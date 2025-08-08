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
    public class AudioBus
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Path { get; set; }
        public bool IsHDR { get; set; }
        public bool HDRChild { get; set; }
    }

    public class WwiseEvent
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Path { get; set; }
    }

    public class WwiseAction
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? TargetId { get; set; }
        public string? TargetName { get; set; }
    }

    internal static class ProjectDataFetcher
    {
        private static string eventsWWUFolderPath = "";
        private static string audioObjWWUFolderPath = "";

        // Caches & stores
        private static readonly ConcurrentDictionary<string, string?> outputBusCache = new();
        private static readonly ConcurrentDictionary<string, int?> volumeCache = new();
        private static readonly ConcurrentDictionary<string, (float min, float max)?> volumeRangeCache = new();

        public static void SetProjectFolderPathes(string _eventsWWUFolderPath, string _audioObjWWUFolderPath)
        {
            eventsWWUFolderPath = _eventsWWUFolderPath;
            audioObjWWUFolderPath = _audioObjWWUFolderPath;

            Console.WriteLine($"[Info] Working with the following folders : \n{eventsWWUFolderPath}\n{audioObjWWUFolderPath}\n");
        }

        /// <summary>
        /// Parse events' actions from WWU files (events workunits).
        /// This remains single-pass over events WWU files.
        /// </summary>
        public static List<WwiseAction> ParseEventActionsFromWorkUnits()
        {
            var actionsWithTargets = new List<WwiseAction>();
            if (string.IsNullOrEmpty(eventsWWUFolderPath) || !Directory.Exists(eventsWWUFolderPath))
            {
                Console.WriteLine($"[Warning] Events WWU folder path is not set or doesn't exist: {eventsWWUFolderPath}");
                return actionsWithTargets;
            }

            var wwuFiles = Directory.GetFiles(eventsWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
            Console.WriteLine($"[Info] Found {wwuFiles.Length} .wwu event files.");

            foreach (var wwuFile in wwuFiles)
            {
                try
                {
                    var doc = XDocument.Load(wwuFile);
                    var events = doc.Descendants("Event");

                    foreach (var evt in events)
                    {
                        var eventPath = evt.Attribute("Name")?.Value ?? "";

                        foreach (var action in evt.Descendants("Action"))
                        {
                            var actionId = action.Attribute("ID")?.Value;
                            var actionName = action.Attribute("Name")?.Value ?? "";

                            var targetRef = action.Descendants("Reference")
                                .FirstOrDefault(r => r.Attribute("Name")?.Value == "Target");

                            var objectRef = targetRef?.Element("ObjectRef");

                            if (objectRef != null)
                            {
                                actionsWithTargets.Add(new WwiseAction
                                {
                                    Id = actionId,
                                    Name = actionName,
                                    Path = eventPath,
                                    TargetId = objectRef.Attribute("ID")?.Value,
                                    TargetName = objectRef.Attribute("Name")?.Value
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed parsing WWU '{wwuFile}': {ex.Message}");
                }
            }

            Console.WriteLine($"[Info] Extracted {actionsWithTargets.Count} actions with targets.");
            return actionsWithTargets;
        }

        /// <summary>
        /// Main optimized flow:
        /// - Get buses & events (WAAPI)
        /// - Parse actions once from event workunits
        /// - Batch request OutputBus & Volume for unique TargetIds
        /// - Preload volume RTPC ranges once from audio objects WWU
        /// - Use caches to avoid per-action WAAPI / IO calls
        /// </summary>
        public static async Task ListSoundObjectsRoutedToHDR()
        {
            try
            {
                Console.WriteLine("[Info] Requesting Audio Buses...");
                var allBusses = await GetAudioBuses();
                if (allBusses == null || allBusses.Count == 0)
                {
                    Console.WriteLine("[Warning] No AudioBus found.");
                    return;
                }

                var allRelevantBusIds = allBusses
                    .Where(bus => bus.IsHDR || bus.HDRChild)
                    .Select(bus => bus.Id)
                    .Where(id => id != null)
                    .ToHashSet()!;

                Console.WriteLine("[Info] Requesting Events...");
                var allEvents = await GetEvents();
                if (allEvents == null || allEvents.Count == 0)
                {
                    Console.WriteLine("[Warning] No Event found.");
                    return;
                }
                Console.WriteLine($"[Info] {allEvents.Count} Events retrieved.");

                Console.WriteLine("[Info] Parsing Actions and Targets from WWU files...");
                var allActionsWithTargets = ParseEventActionsFromWorkUnits();
                if (allActionsWithTargets.Count == 0)
                {
                    Console.WriteLine("[Info] No actions with targets found in events WWU.");
                    return;
                }

                // --- Collect unique target ids to batch WAAPI + preload volume ranges
                var uniqueTargetIds = allActionsWithTargets
                    .Select(a => a.TargetId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => id!)
                    .Distinct()
                    .ToList();

                Console.WriteLine($"[Info] {uniqueTargetIds.Count} unique TargetIds to query (batch).");

                // Preload volume RTPC ranges from audio objects WWU (scan once)
                Console.WriteLine("[Info] Preloading volume RTPC ranges from audio object WWU files...");
                PreloadVolumeRanges(); // sync preloading — fast enough; if too slow you can make it Task.Run()

                // Batch WAAPI calls
                Console.WriteLine("[Info] Batch requesting OutputBus for targets...");
                await BatchGetTargetOutputBus(uniqueTargetIds);

                Console.WriteLine("[Info] Batch requesting Volume for targets...");
                await BatchGetVolumes(uniqueTargetIds);

                // Filter actions whose target output bus is HDR or HDR child
                var routedActions = new List<(WwiseAction action, string outputBusId)>();
                foreach (var action in allActionsWithTargets)
                {
                    if (string.IsNullOrEmpty(action.TargetId))
                        continue;

                    if (outputBusCache.TryGetValue(action.TargetId!, out var busId) && !string.IsNullOrEmpty(busId)
                        && allRelevantBusIds.Contains(busId))
                    {
                        routedActions.Add((action, busId!));
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"[Info] Found {routedActions.Count} actions routed to HDR buses via their targets.");

                foreach (var (action, busId) in routedActions.Take(200)) // preview only first 200 in logs to avoid flooding console
                {
                    Console.WriteLine($"Action: {action.Name} (ID: {action.Id}) => Target: {action.TargetName} ({action.TargetId}) => OutputBus: {busId}");
                }
                if (routedActions.Count > 200)
                    Console.WriteLine($"[Info] ... (omitted remaining {routedActions.Count - 200} lines)");

                Console.WriteLine();

                // Group routed actions by Event for efficient processing
                var eventsByName = allEvents.ToDictionary(e => e.Name ?? "", e => e);
                var eventsWithActions = routedActions
                    .GroupBy(t => t.action.Path ?? "")
                    .Select(g => (evt: eventsByName.TryGetValue(g.Key, out var e) ? e : new WwiseEvent { Name = g.Key }, actions: g.ToList()))
                    .Where(x => x.actions.Count > 0)
                    .ToList();

                Console.WriteLine($"[Info] {eventsWithActions.Count} Events have Actions routed to HDR.");

                // Prepare for graph plotting (same logic as before)
                var volumeOffsets = new Dictionary<int, int>();
                float yOffsetStep = 0.5f;
                int xOffsetStep = 4;
                int xOffsetCount = 0;
                int index = 0;
                int totalColor = routedActions.Count;

                float maxXOffset = float.MinValue;
                int tempXOffsetCount = 0;
                int tempXOffsetDirection = 1;

                foreach (var (_, actionsList) in eventsWithActions)
                {
                    foreach (var _ in actionsList)
                    {
                        tempXOffsetCount++;
                        tempXOffsetDirection *= -1;
                        float tempXOffset = tempXOffsetCount * tempXOffsetDirection;
                        if (tempXOffset > maxXOffset)
                            maxXOffset = tempXOffset;
                    }
                }

                // Iterate events and use caches (no more WAAPI or IO per action)
                foreach (var (evt, actionsList) in eventsWithActions)
                {
                    Console.WriteLine($"Event: {evt.Name} (ID: {evt.Id}) has {actionsList.Count} HDR routed action(s).");
                    foreach (var (action, busId) in actionsList)
                    {
                        index++;

                        Console.WriteLine($"  => Action: {action.Name} (ID: {action.Id}) => OutputBus: {busId}");

                        // Use cached volume and range
                        int volume = 0;
                        if (volumeCache.TryGetValue(action.TargetId!, out var volVal) && volVal.HasValue)
                            volume = volVal.Value;

                        var volumeRange = (volumeRangeCache.TryGetValue(action.TargetId!, out var vr) ? vr : null);

                        int occurrence = volumeOffsets.TryGetValue(volume, out var existingCount) ? existingCount : 0;
                        volumeOffsets[volume] = occurrence + 1;

                        float yOffset = occurrence * yOffsetStep;
                        float volumeMin = 0, volumeMax = 0;
                        float xOffset = 0;

                        if (volumeRange != null)
                        {
                            xOffsetCount += xOffsetStep;
                            xOffset = xOffsetCount;
                            volumeMin = volumeRange.Value.min;
                            volumeMax = volumeRange.Value.max;
                        }
                        else
                        {
                            xOffset = occurrence * xOffsetStep;
                        }

                        Console.WriteLine($"    Volume: {volume} | Range: [{volumeMin}, {volumeMax}]");

                        // Keep original plotting call
                        try
                        {
                            MainWindow.Instance.GraphViewModel.AddPointWithVerticalError(
                                action.TargetName,
                                index,
                                volume,
                                volumeMin,
                                volumeMax,
                                yOffset,
                                xOffset,
                                totalColor,
                                maxXOffset
                            );
                        }
                        catch (Exception ex)
                        {
                            // Ensure UI exceptions don't break the whole processing
                            Console.WriteLine($"[Warning] Failed to add point to graph: {ex.Message}");
                        }
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Error] Final error: " + ex.Message);
            }
        }

        #region WAAPI Helpers (unchanged behaviour but batched + cached)

        public static async Task<List<AudioBus>> GetAudioBuses()
        {
            var buses = new List<AudioBus>();

            var query = new JObject(
                new JProperty("from", new JObject(
                    new JProperty("ofType", new JArray("Bus"))
                ))
            );

            var options = new JObject(
                new JProperty("return", new JArray("id", "name", "path", "HdrEnable"))
            );

            try
            {
                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                var hdrMap = result["return"]?
                    .ToDictionary(
                        bus => bus["path"]?.ToString() ?? "",
                        bus => bus["HdrEnable"]?.Value<bool>() ?? false
                    ) ?? new Dictionary<string, bool>();

                foreach (var bus in result["return"]!)
                {
                    var path = bus["path"]?.ToString() ?? "";
                    var isHDR = bus["HdrEnable"]?.Value<bool>() ?? false;

                    bool isHDRChild = false;
                    var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = parts.Length - 1; i > 0; i--)
                    {
                        var parentPath = "\\" + string.Join("\\", parts.Take(i));
                        if (hdrMap.TryGetValue(parentPath, out bool parentIsHDR) && parentIsHDR)
                        {
                            isHDRChild = true;
                            break;
                        }
                    }

                    buses.Add(new AudioBus
                    {
                        Name = bus["name"]?.ToString(),
                        Id = bus["id"]?.ToString(),
                        Path = path,
                        IsHDR = isHDR,
                        HDRChild = isHDRChild && !isHDR
                    });
                }

                return buses;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[Error] Failed to retrieve AudioBuses: {e.Message}");
                return buses;
            }
        }

        public static async Task<List<WwiseEvent>> GetEvents()
        {
            var events = new List<WwiseEvent>();

            var query = new JObject(
                new JProperty("from", new JObject(
                    new JProperty("ofType", new JArray("Event"))
                ))
            );

            var options = new JObject(
                new JProperty("return", new JArray("id", "name", "path"))
            );

            try
            {
                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (var evt in result["return"]!)
                {
                    events.Add(new WwiseEvent
                    {
                        Name = evt["name"]?.ToString(),
                        Id = evt["id"]?.ToString(),
                        Path = evt["path"]?.ToString(),
                    });
                }

                return events;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[Error] Failed to retrieve Events: {e.Message}");
                return events;
            }
        }

        #endregion

        #region Batched & Cached WAAPI Calls

        /// <summary>
        /// Batch fetch OutputBus for a list of target IDs and cache results in outputBusCache.
        /// </summary>
        public static async Task BatchGetTargetOutputBus(IEnumerable<string> targetIds)
        {
            var idsToFetch = targetIds.Where(id => !outputBusCache.ContainsKey(id)).Distinct().ToList();
            if (idsToFetch.Count == 0) return;

            try
            {
                var query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(idsToFetch))
                    ))
                );

                var options = new JObject(
                    new JProperty("return", new JArray("id", "OutputBus"))
                );

                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (var obj in result["return"]!)
                {
                    var id = obj["id"]?.ToString();
                    var busId = obj["OutputBus"]?["id"]?.ToString();
                    if (id != null)
                        outputBusCache[id] = busId;
                }

                // For any ids not returned by WAAPI, set null to avoid refetching later
                foreach (var id in idsToFetch)
                {
                    outputBusCache.TryAdd(id, outputBusCache.ContainsKey(id) ? outputBusCache[id] : null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] BatchGetTargetOutputBus failed: {ex.Message}");
                // Ensure we don't re-request infinitely: mark them as null
                foreach (var id in idsToFetch) outputBusCache.TryAdd(id, null);
            }
        }

        /// <summary>
        /// Batch fetch Volume for a list of target IDs and cache results in volumeCache.
        /// </summary>
        public static async Task BatchGetVolumes(IEnumerable<string> targetIds)
        {
            var idsToFetch = targetIds.Where(id => !volumeCache.ContainsKey(id)).Distinct().ToList();
            if (idsToFetch.Count == 0) return;

            try
            {
                var query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(idsToFetch))
                    ))
                );

                var options = new JObject(
                    new JProperty("return", new JArray("id", "Volume"))
                );

                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (var obj in result["return"]!)
                {
                    var id = obj["id"]?.ToString();
                    var volStr = obj["Volume"]?.ToString();
                    if (id != null)
                    {
                        if (int.TryParse(volStr, out var v))
                            volumeCache[id] = v;
                        else
                            volumeCache[id] = null;
                    }
                }

                // Mark missing ids with null to avoid refetch
                foreach (var id in idsToFetch)
                {
                    volumeCache.TryAdd(id, volumeCache.ContainsKey(id) ? volumeCache[id] : null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] BatchGetVolumes failed: {ex.Message}");
                foreach (var id in idsToFetch) volumeCache.TryAdd(id, null);
            }
        }

        #endregion

        #region RTPC Preloading (parse audio object WWU once)

        /// <summary>
        /// Scans audio object WWU files once and fills volumeRangeCache with RTPC min/max for each object ID found.
        /// Uses ConcurrentDictionary for safety.
        /// </summary>
        public static void PreloadVolumeRanges()
        {
            try
            {
                if (string.IsNullOrEmpty(audioObjWWUFolderPath) || !Directory.Exists(audioObjWWUFolderPath))
                {
                    Console.WriteLine($"[Warning] audioObjWWUFolderPath is not set or doesn't exist: {audioObjWWUFolderPath}");
                    return;
                }

                var wwuFiles = Directory.GetFiles(audioObjWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
                Console.WriteLine($"[Info] {wwuFiles.Length} .wwu files found in {audioObjWWUFolderPath}");

                foreach (var file in wwuFiles)
                {
                    try
                    {
                        var doc = XDocument.Load(file);
                        var objects = doc.Descendants()
                            .Where(e => e.Name == "ActorMixer" || e.Name == "Sound" || e.Name == "RandomSequenceContainer" || e.Name == "BlendContainer");

                        foreach (var obj in objects)
                        {
                            var id = obj.Attribute("ID")?.Value;
                            if (string.IsNullOrEmpty(id)) continue;

                            var rtpcs = obj.Descendants("RTPC")
                                .Where(rtpc =>
                                    rtpc.Element("PropertyList")?.Elements("Property")
                                        .Any(p => p.Attribute("Name")?.Value == "PropertyName" &&
                                                  p.Attribute("Value")?.Value == "Volume") == true);

                            if (!rtpcs.Any()) continue;

                            var yValues = rtpcs
                                .SelectMany(rtpc => rtpc.Descendants("Point")
                                    .Select(p => p.Element("YPos")?.Value)
                                    .Where(y => !string.IsNullOrWhiteSpace(y))
                                    .Select(y =>
                                    {
                                        if (float.TryParse(y.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                                            return v;
                                        return float.NaN;
                                    })
                                    .Where(v => !float.IsNaN(v)))
                                .ToList();

                            if (yValues.Count > 0)
                            {
                                var min = yValues.Min();
                                var max = yValues.Max();
                                volumeRangeCache[id] = (min, max);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Warning] Failed parsing audioObj WWU '{file}': {ex.Message}");
                    }
                }

                Console.WriteLine($"[Info] Preloaded RTPC ranges for {volumeRangeCache.Count} audio objects.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] PreloadVolumeRanges failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Backwards-compatible helper: returns cached range or null.
        /// </summary>
        public static (float min, float max)? ParseVolumeRTPCMinMaxFromWwu(string targetId, string audioObjWWUFolderPathParam)
        {
            if (string.IsNullOrEmpty(targetId))
                return null;

            if (volumeRangeCache.TryGetValue(targetId, out var range))
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

        #endregion

        #region Legacy single-id WAAPI helpers (kept for compatibility, but not used in optimized flow)

        public static async Task<int?> GetVolume(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return null;

            // Prefer cached value
            if (volumeCache.TryGetValue(targetId, out var cached) && cached.HasValue)
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
                    volumeCache[targetId] = vol;
                    return vol;
                }

                volumeCache[targetId] = null;
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Warning] Failed to get volume for {targetId}: {e.Message}");
                volumeCache[targetId] = null;
                return null;
            }
        }

        public static async Task<string?> GetTargetOutputBus(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return null;

            // Prefer cache
            if (outputBusCache.TryGetValue(targetId, out var cachedBus) && !string.IsNullOrEmpty(cachedBus))
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

                outputBusCache[targetId] = busId;
                Console.WriteLine($"[Info] OutputBus ID for target {targetId}: {busId}");
                return busId;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Warning] Failed to get OutputBus for target {targetId}: {e.Message}");
                outputBusCache[targetId] = null;
                return null;
            }
        }

        #endregion
    }
}
