using System.Collections.Generic;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    public static class ProjectAnalyzer
    {
        public static async Task AnalyzeProjectAsync()
        {
            // 1. Reset state
            AppUtility.ResetCacheAndUI();

            // 2. Get project info and set folder paths
            await WaapiBridge.GetProjectInfos();

            // 3. Preload all caches
            WWUParser.PreloadBusData();
            WWUParser.PreloadVolumeRanges();

            // 4. Parse actions
            var actions = WWUParser.ParseEventActionsFromWorkUnits();

            // 5. Extract unique target IDs
            var targetIds = ChartBridge.ExtractUniqueTargetIds(actions);

            // 6. Batch WAAPI calls
            await WaapiBridge.BatchGetTargetOutputBus(targetIds);

            // 7. Continue with chart logic...
            await ChartBridge.ListSoundObjectsRoutedToHDR();

            Log.Info("Project analysis complete.");
        }
    }
}