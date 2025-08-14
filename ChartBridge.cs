using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    internal class ChartBridge
    {
        public static async Task ListSoundObjectsRoutedToHDR()
        {
            try
            {
                List<AudioBus> allBusses = await GetRelevantAudioBuses();
                List<WwiseEvent> allEvents = await GetAllEvents();
                List<WwiseAction> allActionsWithTargets = ParseActionsFromWWU();

                List<string> uniqueTargetIds = ExtractUniqueTargetIds(allActionsWithTargets);

                PreloadVolumeRanges();

                await BatchRequestTargetData(uniqueTargetIds);

                List<(WwiseAction action, string outputBusId)> routedActions = FilterActionsRoutedToHDR(allActionsWithTargets, allBusses);

                List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> eventsWithActions = GroupActionsByEvent(routedActions, allEvents);

                PlotEvents(eventsWithActions);

                MainWindow.Instance.MainViewModel.ChartViewModel.UpdateBorders();

            }
            catch (Exception ex)
            {
                Console.WriteLine("[Error] Final error: " + ex.Message);
            }
        }

        private static async Task<List<AudioBus>> GetRelevantAudioBuses()
        {
            Console.WriteLine("[Info] Requesting Audio Buses...");
            List<AudioBus> allBusses = await WaapiBridge.GetAudioBuses();

            if (allBusses == null || allBusses.Count == 0)
            {
                Console.WriteLine("[Warning] No AudioBus found.");
                return new List<AudioBus>();
            }

            return allBusses.Where(bus => bus.IsHDR || bus.HDRChild).ToList();
        }

        private static async Task<List<WwiseEvent>> GetAllEvents()
        {
            Console.WriteLine("[Info] Requesting Events...");
            List<WwiseEvent> allEvents = await WaapiBridge.GetEvents();

            if (allEvents == null || allEvents.Count == 0)
            {
                Console.WriteLine("[Warning] No Event found.");
                return new List<WwiseEvent>();
            }

            Console.WriteLine($"[Info] {allEvents.Count} Events retrieved.");
            return allEvents;
        }

        private static List<WwiseAction> ParseActionsFromWWU()
        {
            Console.WriteLine("[Info] Parsing Actions and Targets from WWU files...");
            List<WwiseAction> actions = WWUParser.ParseEventActionsFromWorkUnits();

            if (actions.Count == 0)
            {
                Console.WriteLine("[Info] No actions with targets found in events WWU.");
            }

            return actions;
        }

        private static List<string> ExtractUniqueTargetIds(List<WwiseAction> actions)
        {
            List<string?> uniqueTargetIds = actions
                .Select(a => a.TargetId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            Console.WriteLine($"[Info] {uniqueTargetIds.Count} unique TargetIds to query (batch).");
            return uniqueTargetIds;
        }

        private static void PreloadVolumeRanges()
        {
            Console.WriteLine("[Info] Preloading volume RTPC ranges from audio object WWU files...");
            WWUParser.PreloadVolumeRanges();
        }

        private static async Task BatchRequestTargetData(List<string> targetIds)
        {
            Console.WriteLine("[Info] Batch requesting OutputBus for targets...");
            await WaapiBridge.BatchGetTargetOutputBus(targetIds);

            Console.WriteLine("[Info] Batch requesting Volume for targets...");
            await WaapiBridge.BatchGetVolumes(targetIds);
        }

        private static List<(WwiseAction action, string outputBusId)> FilterActionsRoutedToHDR(
            List<WwiseAction> allActionsWithTargets,
            List<AudioBus> relevantBusses)
        {
            HashSet<string> allRelevantBusIds = new HashSet<string>(relevantBusses.Select(b => b.Id));

            List<(WwiseAction, string)> routedActions = new List<(WwiseAction, string)>();

            foreach (WwiseAction action in allActionsWithTargets)
            {
                if (string.IsNullOrEmpty(action.TargetId))
                {
                    continue;
                }

                if (WwiseCache.outputBusCache.TryGetValue(action.TargetId!, out string? busId)
                    && !string.IsNullOrEmpty(busId)
                    && allRelevantBusIds.Contains(busId))
                {
                    routedActions.Add((action, busId!));
                }
            }

            Console.WriteLine($"[Info] Found {routedActions.Count} actions routed to HDR buses via their targets.");

            return routedActions;
        }

        private static List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> GroupActionsByEvent(
            List<(WwiseAction action, string busId)> routedActions,
            List<WwiseEvent> allEvents)
        {
            Dictionary<string, WwiseEvent> eventsByName = allEvents.ToDictionary(e => e.Name ?? "", e => e);

            List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> grouped = routedActions
                .GroupBy(t => t.action.Path ?? "")
                .Select(g => (
                    evt: eventsByName.TryGetValue(g.Key, out WwiseEvent? e) ? e : new WwiseEvent { Name = g.Key },
                    actions: g.ToList()
                ))
                .Where(x => x.actions.Count > 0)
                .ToList();

            Console.WriteLine($"[Info] {grouped.Count} Events have Actions routed to HDR.");
            return grouped;
        }

        private static void PlotEvents(List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> eventsWithActions)
        {
            List<(float, float)> yMinMaxList = new List<(float, float)>();
            int xOffsetDirection = 1;
            int index = 0;

            foreach ((WwiseEvent evt, List<(WwiseAction action, string busId)> actionsList) in eventsWithActions)
            {
                Console.WriteLine($"Event: {evt.Name} (ID: {evt.Id}) has {actionsList.Count} HDR routed action(s).");
                foreach ((WwiseAction action, string busId) in actionsList)
                {
                    index++;

                    Console.WriteLine($"  => Action: {action.Name} (ID: {action.Id}) => OutputBus: {busId}");

                    // Get rounded volume
                    float volume = 0;
                    if (WwiseCache.volumeCache.TryGetValue(action.TargetId!, out float? volVal) && volVal.HasValue)
                    {
                        volume = (float)Math.Round(volVal.Value, 2);
                    }

                    (float min, float max)? volumeRange = WwiseCache.volumeRangeCache.TryGetValue(action.TargetId!, out (float min, float max)? vr) ? vr : (0, 0);

                    (float, float) yMinMax = (volumeRange.Value.min + volume, volumeRange.Value.max + volume);

                    int occurrence = 0;
                    foreach ((float, float) range in yMinMaxList)
                    {
                        if (yMinMax.Item1 <= range.Item2 || yMinMax.Item2 >= range.Item1)
                        {
                            occurrence++;
                        }
                    }

                    yMinMaxList.Add(yMinMax);

                    float xOffset = occurrence * xOffsetDirection;

                    Console.WriteLine($"    Volume: {volume} | Range: [{volumeRange.Value.min}, {volumeRange.Value.max}] | XOffset: {xOffset}");

                    try
                    {
                        MainWindow.Instance.MainViewModel.ChartViewModel.AddPointWithVerticalError(
                            action.TargetName,
                            index,
                            volume,
                            volumeRange.Value.min,
                            volumeRange.Value.max,
                            xOffset,
                            action.ParentData,
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
    }
}
