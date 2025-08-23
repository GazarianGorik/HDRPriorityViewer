using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    public static class ProjectAnalyzer
    {
        public static async Task AnalyzeProjectAsync()
        {
            await Task.Run(async () =>
            {
                Log.Info("Fetching Wwise project data...");
                await WaapiBridge.GetProjectInfos();

                Log.Info("Project infos gathered");
            });

            await Task.Run(async () =>
            {
                Log.Info("Fetching Wwise project data...");

                try
                {
                    // === 1. Fetching data on default thread ===
                    List<AudioBus> allBusses = await ChartBridge.GetRelevantAudioBuses();
                    List<WwiseEvent> allEvents = await ChartBridge.GetAllEvents();

                    WWUParser.PreloadBusData();
                    WWUParser.PreloadVolumeRanges();

                    List<WwiseAction> allActionsWithTargets = WWUParser.ParseEventActionsFromWorkUnits();

                    List<string> uniqueTargetIds = ChartBridge.ExtractUniqueTargetIds(allActionsWithTargets);


                    await ChartBridge.BatchRequestTargetData(uniqueTargetIds);

                    List<(WwiseAction action, string outputBusId)> routedActions =
                        ChartBridge.FilterActionsRoutedToHDR(allActionsWithTargets, allBusses);

                    List<(WwiseEvent evt, List<(WwiseAction action, string busId)> actions)> eventsWithActions =
                        ChartBridge.GroupActionsByEvent(routedActions, allEvents);

                    // === 2. Adding chart points on UI thread ===
                    await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                    {
                        Log.Info("Plotting events on UI thread...");
                        ChartBridge.PlotEvents(eventsWithActions);
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
            });

            Log.Info("Project analysis complete.");
        }
    }
}