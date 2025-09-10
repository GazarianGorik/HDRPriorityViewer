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
using System.Threading.Tasks;
using CSharpMarkup.WinUI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace HDRPriorityViewer
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

                    int chartTotalPoints = ChartBridge.PrecalculateChartTotalPoints(eventsWithActions);
                    bool needToApplyDefaultFilter = false;

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
                            "Apply default filter",
                            "Proceed anyway",
                            (Microsoft.UI.Xaml.Style)Application.Current.Resources["ValidateButton"],
                            (Microsoft.UI.Xaml.Style)Application.Current.Resources["RiskyButton"]);
                        });

                        // This looks weird but it's correct, None is the close button which is Apply default filter here cause I want it to the right side
                        // I cant use primary / secondary cause primary is only used for copy
                        // TODO, clean up this mess 
                        if (result == ContentDialogResult.None)
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
}