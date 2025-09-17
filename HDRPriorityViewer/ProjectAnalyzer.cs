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
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HDRPriorityViewer;

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
                var allBusses = await ChartBridge.GetRelevantAudioBusses();
                var allEvents = await ChartBridge.GetAllEvents();

                WWUParser.PreloadBusData();
                WWUParser.PreloadVolumeRanges();

                var allActionsWithTargets = WWUParser.ParseEventActionsFromWorkUnits();

                var uniqueTargetIds = ChartBridge.ExtractUniqueTargetIds(allActionsWithTargets);


                await ChartBridge.BatchRequestTargetData(uniqueTargetIds);

                var routedActions =
                    ChartBridge.FilterActionsRoutedToHDR(allActionsWithTargets, allBusses);

                var eventsWithActions =
                    ChartBridge.GroupActionsByEvent(routedActions, allEvents);

                var chartTotalPoints = ChartBridge.PrecalculateChartTotalPoints(eventsWithActions);
                var needToApplyDefaultFilter = false;

                if (chartTotalPoints > AppSettings.totalChartPointsWarningTreshold)
                {
                    var message =
                        $"A large number of audio elements were detected ({chartTotalPoints}). Rendering all of them at once may impact performance.\n\n" +
                        "You can choose to proceed anyway or apply a default filter to hide some categories. " +
                        "Filtered categories can be re-enabled using the filter panel.";



                    ContentDialogResult? result = null;

                    await MainWindow.Instance.DispatcherQueue.EnqueueAsync(async () =>
                    {
                        result = await
                        MainWindow.Instance.EnqueueDialogAsync(
                        "Potential Performance Issue",
                        message,
                        false,
                        null,
                        "Proceed anyway",
                        "Apply default filter",
                        null,
                        (Microsoft.UI.Xaml.Style)Application.Current.Resources["RiskyButton"],
                        (Microsoft.UI.Xaml.Style)Application.Current.Resources["ValidateButton"]);
                    });

                    if (result == ContentDialogResult.Secondary)
                    {
                        needToApplyDefaultFilter = true;
                    }
                }


                // === 2. Adding chart points on UI thread ===
                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    Log.Info("Plotting events on UI thread...");
                    ChartBridge.PlotEvents(eventsWithActions);
                });

                if (needToApplyDefaultFilter)
                {
                    await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                    {
                        Log.Info("Applying default filter...");
                        MainWindow.Instance.MainViewModel.ApplyDefaultFilter();
                    });

                    needToApplyDefaultFilter = false;
                }

                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    MainWindow.Instance.MainViewModel.ChartViewModel.RepositionPointsWithoutOverlap();
                });

                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    MainWindow.Instance.MainViewModel.ChartViewModel.UpdateHighlightedPointPoistion();
                });

                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                {
                    MainWindow.Instance.MainViewModel.ChartViewModel.UpdateBorders();
                });
            }
            catch (Exception ex)
            {
                Log.Info("[Error] Final error: " + ex);
            }
        });

        Log.Info("Project analysis complete.");
    }
}