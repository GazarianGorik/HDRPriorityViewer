/****************************************************************************** 
 Copyright (c) 2025 Gorik Gazarian
 
 This file is part of WwiseHDRTool.
 
 Licensed under the PolyForm Noncommercial License 1.0.0.

 You may not use this file except in compliance with the License.
 You may obtain a copy of the License at
 https://polyformproject.org/licenses/noncommercial/1.0.0
 and in the LICENSE file in this repository.
 
 Unless required by applicable law or agreed to in writing,
 software distributed under the License is distributed on
 an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 either express or implied. See the License for the specific
 language governing permissions and limitations under the License.
******************************************************************************/

using System;
using System.Collections.Generic;
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

                    await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                    {
                        MainWindow.Instance.MainViewModel.ChartViewModel.RepositionPointsWithoutOverlap();
                    });

                    await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() =>
                    {
                        MainWindow.Instance.MainViewModel.ChartViewModel.UpdateHighlightedPointPoistion();
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