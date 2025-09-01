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
using System.Linq;
using System.Threading.Tasks;

namespace HDRPriorityGraph
{
    internal class ChartBridge
    {
        public static async Task<List<AudioBus>> GetRelevantAudioBuses()
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

        public static async Task<List<WwiseEvent>> GetAllEvents()
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

        public static List<string> ExtractUniqueTargetIds(List<WwiseAction> actions)
        {
            List<string?> uniqueTargetIds = actions
                .Select(a => a.TargetId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            Log.Info($"{uniqueTargetIds.Count} unique TargetIds to query (batch).");
            return uniqueTargetIds;
        }

        public static async Task BatchRequestTargetData(List<string> targetIds)
        {
            Log.Info("Batch requesting OutputBus for targets...");
            await WaapiBridge.BatchGetTargetOutputBus(targetIds);
        }

        public static List<(WwiseAction action, string outputBusId)> FilterActionsRoutedToHDR(
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

                if (WwiseCache.outputBusCache.TryGetValue(action.TargetId!, out string? busId))
                {
                    if (!string.IsNullOrEmpty(busId) && allRelevantBusIds.Contains(busId))
                    {
                        routedActions.Add((action, busId!));
                    }
                }
                else
                {
                    Log.Warning($"OutputBusCache doesn't contain {action.TargetName} bus");
                }
            }

            Log.Info($"Found {routedActions.Count} actions routed to HDR buses via their targets.");

            return routedActions;
        }

        public static List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> GroupActionsByEvent(
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

        public static void PlotEvents(List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> eventsWithActions)
        {
            foreach ((WwiseEvent evt, List<(WwiseAction action, string busId)> actionsList) in eventsWithActions)
            {
                foreach ((WwiseAction action, string busId) in actionsList)
                {

                    // Récupère les volumes
                    (float value, float min, float max)? volumes =
                        WwiseCache.volumeRangeCache.TryGetValue(action.TargetId!, out (float value, float min, float max)? vr)
                        ? vr
                        : (0f, 0f, 0f);

                    try
                    {
                        MainWindow.Instance.MainViewModel.ChartViewModel.AddPointWithVerticalError(
                            action.TargetName,
                            volumes.Value.value,
                            Math.Max(volumes.Value.min, -96f),
                            volumes.Value.max,
                            action.ParentData,
                            action.TargetId
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to add point to graph: {ex}");
                    }
                }
            }
        }
    }
}
