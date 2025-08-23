using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
