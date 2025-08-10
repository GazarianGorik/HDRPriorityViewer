using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    internal class ChartBridge
    {
        public static async Task ListSoundObjectsRoutedToHDR()
        {
            try
            {
                Console.WriteLine("[Info] Requesting Audio Buses...");
                var allBusses = await WaapiBridge.GetAudioBuses();
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
                var allEvents = await WaapiBridge.GetEvents();
                if (allEvents == null || allEvents.Count == 0)
                {
                    Console.WriteLine("[Warning] No Event found.");
                    return;
                }
                Console.WriteLine($"[Info] {allEvents.Count} Events retrieved.");

                Console.WriteLine("[Info] Parsing Actions and Targets from WWU files...");
                var allActionsWithTargets = WWUParser.ParseEventActionsFromWorkUnits();
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
                WWUParser.PreloadVolumeRanges(); // sync preloading — fast enough; if too slow you can make it Task.Run()

                // Batch WAAPI calls
                Console.WriteLine("[Info] Batch requesting OutputBus for targets...");
                await WaapiBridge.BatchGetTargetOutputBus(uniqueTargetIds);

                Console.WriteLine("[Info] Batch requesting Volume for targets...");
                await WaapiBridge.BatchGetVolumes(uniqueTargetIds);

                // Filter actions whose target output bus is HDR or HDR child
                var routedActions = new List<(WwiseAction action, string outputBusId)>();
                foreach (var action in allActionsWithTargets)
                {
                    if (string.IsNullOrEmpty(action.TargetId))
                        continue;

                    if (WwiseCache.outputBusCache.TryGetValue(action.TargetId!, out var busId) && !string.IsNullOrEmpty(busId)
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

                // Préparation pour le tracé
                var volumeOffsets = new Dictionary<float, int>();
                int xOffsetDirection = -1;
                int index = 0;
                int totalColor = routedActions.Count;

                float maxXOffset = float.MinValue;
                int tempXOffsetCount = 0;
                int tempXOffsetDirection = -1;

                foreach (var (_, actionsList) in eventsWithActions)
                {
                    foreach (var _ in actionsList)
                    {
                        tempXOffsetCount++;
                        float tempXOffset = tempXOffsetCount * tempXOffsetDirection;
                        if (tempXOffset > maxXOffset)
                            maxXOffset = tempXOffset;
                    }
                }

                // Itération sur les événements
                foreach (var (evt, actionsList) in eventsWithActions)
                {
                    Console.WriteLine($"Event: {evt.Name} (ID: {evt.Id}) has {actionsList.Count} HDR routed action(s).");
                    foreach (var (action, busId) in actionsList)
                    {
                        index++;

                        Console.WriteLine($"  => Action: {action.Name} (ID: {action.Id}) => OutputBus: {busId}");

                        // Récupération du volume arrondi
                        float volume = 0;
                        if (WwiseCache.volumeCache.TryGetValue(action.TargetId!, out var volVal) && volVal.HasValue)
                            volume = (float)Math.Round(volVal.Value, 2);

                        var volumeRange = (WwiseCache.volumeRangeCache.TryGetValue(action.TargetId!, out var vr) ? vr : null);

                        // Occurrence / décalage selon le volume
                        int occurrence = volumeOffsets.TryGetValue(volume, out var existingCount) ? existingCount : 0;
                        volumeOffsets[volume] = occurrence + 1;

                        float volumeMin = 0, volumeMax = 0;

                        // Calcul du décalage horizontal (xOffset) selon occurrence, toujours cohérent
                        float xOffset = occurrence * xOffsetDirection;

                        if (volumeRange != null)
                        {
                            volumeMin = (float)Math.Round(volumeRange.Value.min, 2);
                            volumeMax = (float)Math.Round(volumeRange.Value.max, 2);
                        }

                        Console.WriteLine($"    Volume: {volume} | Range: [{volumeMin}, {volumeMax}] | XOffset: {xOffset}");

                        // Ajout du point au graphique
                        try
                        {
                            MainWindow.Instance.ChartViewModel.AddPointWithVerticalError(
                                action.TargetName,
                                index,
                                volume,
                                volumeMin,
                                volumeMax,
                                xOffset,
                                maxXOffset,
                                action.Color,
                                action.TargetId
                            );
                        }
                        catch (Exception ex)
                        {
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
    }
}
