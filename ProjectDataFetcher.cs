using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;

namespace WwiseHDRTool
{
    internal static class ProjectDataFetcher
    {
        public static (float value, float min, float max)? ParseVolumeRTPCMinMaxFromWwu(string targetId, string audioObjWWUFolderPathParam)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            if (WwiseCache.volumeRangeCache.TryGetValue(targetId, out (float value, float min, float max)? range))
            {
                return range;
            }

            return null;
        }
    }
}
