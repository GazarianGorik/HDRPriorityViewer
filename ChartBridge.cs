using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WwiseHDRTool
{
    internal class ChartBridge
    {
        public static async Task ListSoundObjectsRoutedToHDR()
        {
            try
            {
                // === 1. Fetching data on default thread ===
                List<AudioBus> allBusses = await GetRelevantAudioBuses();
                List<WwiseEvent> allEvents = await GetAllEvents();

                WWUParser.PreloadBusData();
                WWUParser.PreloadVolumeRanges();

                List<WwiseAction> allActionsWithTargets = WWUParser.ParseEventActionsFromWorkUnits();

                List<string> uniqueTargetIds = ExtractUniqueTargetIds(allActionsWithTargets);


                await BatchRequestTargetData(uniqueTargetIds);

                List<(WwiseAction action, string outputBusId)> routedActions =
                    FilterActionsRoutedToHDR(allActionsWithTargets, allBusses);

                List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> eventsWithActions =
                    GroupActionsByEvent(routedActions, allEvents);

                // === 2. Adding chart points on UI thread ===
                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    Log.Info("Plotting events on UI thread...");
                    PlotEvents(eventsWithActions);
                });

                // === 3. Updating borders on UI thread ===
                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    Log.Info("Updating chart borders on UI thread...");
                    MainWindow.Instance.MainViewModel.ChartViewModel.UpdateBorders();
                });

                Log.Info("Chart update complete!");
            }
            catch (Exception ex)
            {
                Log.Info("[Error] Final error: " + ex);
            }
        }

        private static async Task<List<AudioBus>> GetRelevantAudioBuses()
        {
            Log.Info("Requesting Audio Buses...");
            List<AudioBus> allBusses = await WaapiBridge.GetAudioBuses();

            if (allBusses == null || allBusses.Count == 0)
            {
                Log.Info("[Warning] No AudioBus found.");
                return new List<AudioBus>();
            }

            return allBusses.Where(bus => bus.IsHDR || bus.HDRChild).ToList();
        }

        private static async Task<List<WwiseEvent>> GetAllEvents()
        {
            Log.Info("Requesting Events...");
            List<WwiseEvent> allEvents = await WaapiBridge.GetEvents();

            if (allEvents == null || allEvents.Count == 0)
            {
                Log.Info("[Warning] No Event found.");
                return new List<WwiseEvent>();
            }

            Log.Info($"{allEvents.Count} Events retrieved.");
            return allEvents;
        }

        private static List<string> ExtractUniqueTargetIds(List<WwiseAction> actions)
        {
            List<string?> uniqueTargetIds = actions
                .Select(a => a.TargetId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            Log.Info($"{uniqueTargetIds.Count} unique TargetIds to query (batch).");
            return uniqueTargetIds;
        }

        private static async Task BatchRequestTargetData(List<string> targetIds)
        {
            Log.Info("Batch requesting OutputBus for targets...");
            await WaapiBridge.BatchGetTargetOutputBus(targetIds);
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

            Log.Info($"Found {routedActions.Count} actions routed to HDR buses via their targets.");

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

            Log.Info($"{grouped.Count} Events have Actions routed to HDR.");
            return grouped;
        }

        private static void PlotEvents(List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> eventsWithActions)
        {
            List<(float, float)> yMinMaxList = new List<(float, float)>();
            int xOffsetDirection = 1;
            int index = 0;

            foreach ((WwiseEvent evt, List<(WwiseAction action, string busId)> actionsList) in eventsWithActions)
            {
                //Log.Info($"Event: {evt.Name} (ID: {evt.Id}) has {actionsList.Count} HDR routed action(s).");
                foreach ((WwiseAction action, string busId) in actionsList)
                {
                    index++;

                    //Log.Info($"  => Action: {action.Name} (ID: {action.Id}) => OutputBus: {busId}");

                    // Get rounded volume

                    (float value, float min, float max)? volumes = WwiseCache.volumeRangeCache.TryGetValue(action.TargetId!, out (float value, float min, float max)? vr) ? vr : (0, 0, 0);

                    float volume = volumes.Value.value;

                    (float, float) yMinMax = (Math.Max(volumes.Value.min, -96), volumes.Value.max);

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

                    //Log.Info($"    Volume: {volume} | Range: [{volumeRange.Value.min}, {volumeRange.Value.max}] | XOffset: {xOffset}");

                    try
                    {
                        MainWindow.Instance.MainViewModel.ChartViewModel.AddPointWithVerticalError(
                            action.TargetName,
                            index,
                            volume,
                            yMinMax.Item1,
                            yMinMax.Item2,
                            xOffset,
                            action.ParentData,
                            action.TargetId
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to add point to graph: {ex.ToString()}");
                    }
                }
                //Log.Info("");
            }
        }
    }
}
