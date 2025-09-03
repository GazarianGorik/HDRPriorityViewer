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

namespace HDRPriorityViewer
{
    public static class AppUtility
    {
        public static void ResetCacheAndUI()
        {
            WWUParser.ResetProjectFolderPaths();

            WwiseCache.audioObjectsByIdCache.Clear();
            WwiseCache.busesByIdCache.Clear();
            WwiseCache.volumeRangeCache.Clear();
            WwiseCache.outputBusCache.Clear();
            WwiseCache.chartDefaultPoints.Clear();

            var mainWindow = MainWindow.Instance;
            if (mainWindow != null)
            {
                var vm = mainWindow.MainViewModel;
                vm.ChartViewModel.ClearChart();
                vm.SearchItems.Clear();
                vm.CategorieFilterButtons.Clear();
                vm.Searches.Clear();
            }

            Log.Info("All caches and state have been reset for a fresh rescan.");
        }
    }
}
