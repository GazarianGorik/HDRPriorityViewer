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

namespace WwiseHDRTool
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
