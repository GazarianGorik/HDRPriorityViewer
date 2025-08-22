using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml.Linq;
using LiveChartsCore.Defaults;
using SkiaSharp;

namespace WwiseHDRTool
{
    public class AudioBus
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Path { get; set; }
        public bool IsHDR { get; set; }
        public bool HDRChild { get; set; }
    }

    public class WwiseEvent
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Path { get; set; }
    }

    public class WwiseAction
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? TargetId { get; set; }
        public string? TargetName { get; set; }
        public ParentData? ParentData { get; set; }
    }

    public static class WwiseCache
    {
        // Caches & stores

        public static ConcurrentDictionary<string, string?> outputBusCache = new(); // A link between AudioOjbId and OutputBusId
        public static ConcurrentDictionary<string, (float value, float min, float max)?> volumeRangeCache = new();
        public static readonly ConcurrentDictionary<string, XElement> audioObjectsByIdCache= new ConcurrentDictionary<string, XElement>();
        public static readonly ConcurrentDictionary<string, XElement> busesByIdCache = new ConcurrentDictionary<string, XElement>();
        public static List<ErrorPoint> chartDefaultPoints = new List<ErrorPoint>();
        public static string wampPort = "8080";
        public static string wampIP = "localhost";
    }

    public class ParentData()
    {
        public string Name { get; set; }
        public SKColor Color { get; set; }
    }
}
