using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using SkiaSharp;


namespace WwiseHDRTool
{
    public static class WWUParser
    {
        private static string eventsWWUFolderPath = "";
        private static string audioObjWWUFolderPath = "";
        private static readonly SKColor[] WwisePalette = {
            /*  0 */ new SKColor(100,110,120),  // a
            /*  1 */ new SKColor(104,107,230),  // b
            /*  2 */ new SKColor(55,129,243),  // c
            /*  3 */ new SKColor(2,169,185),  // d
            /*  4 */ new SKColor(0,185,18),  // e
            /*  5 */ new SKColor(131,185,18),  // f
            /*  6 */ new SKColor(190, 174, 17),  // g
            /*  7 */ new SKColor(226, 159, 25),  // h
            /*  8 */ new SKColor(234, 123, 20),  // i
            /*  9 */ new SKColor(217, 77, 47),  // j
            /* 10 */ new SKColor(215, 59, 58),  // k
            /* 11 */ new SKColor(231, 22, 229),  // l (ajouté en bleu clair)
            /* 12 */ new SKColor(174, 27, 248),  // m
            /* 13 */ new SKColor(145, 72, 253),  // n
            /* 14 */ new SKColor(150, 152, 229),  // p (cyan très clair)
            /* 15 */ new SKColor(121, 154, 217),  // q
            /* 16 */ new SKColor(82, 181, 181),  // r
            /* 17 */ new SKColor(102, 181, 105),  // s
            /* 18 */ new SKColor(162, 190, 81),  // t
            /* 19 */ new SKColor(210, 199, 53),  // u
            /* 20 */ new SKColor(202, 164, 89),  // v
            /* 21 */ new SKColor(201, 149, 95),  // w (garde le même si tu veux)
            /* 22 */ new SKColor(197, 133, 120),  // x
            /* 23 */ new SKColor(203, 125, 125),  // y
            /* 24 */ new SKColor(207, 113, 181),  // z
            /* 25 */ new SKColor(190, 108, 215),  // é (rose vif)
            /* 26 */ new SKColor(172, 147, 232),  // è (magenta doux)
            /* 27 */ new SKColor(129, 140, 150)   // o
        };

        public static void SetProjectFolderPathes(string _eventsWWUFolderPath, string _audioObjWWUFolderPath)
        {
            eventsWWUFolderPath = _eventsWWUFolderPath;
            audioObjWWUFolderPath = _audioObjWWUFolderPath;

            Console.WriteLine($"[Info] Working with the following folders : \n{eventsWWUFolderPath}\n{audioObjWWUFolderPath}\n");
        }

        /// <summary>
        /// Parse events' actions from WWU files (events workunits).
        /// This remains single-pass over events WWU files.
        /// </summary>
        public static List<WwiseAction> ParseEventActionsFromWorkUnits()
        {
            var actionsWithTargets = new List<WwiseAction>();
            if (string.IsNullOrEmpty(eventsWWUFolderPath) || !Directory.Exists(eventsWWUFolderPath))
            {
                Console.WriteLine($"[Warning] Events WWU folder path is not set or doesn't exist: {eventsWWUFolderPath}");
                return actionsWithTargets;
            }

            var wwuFiles = Directory.GetFiles(eventsWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
            Console.WriteLine($"[Info] Found {wwuFiles.Length} .wwu event files.");

            var IDsAddedToChart = new List<String>();

            foreach (var wwuFile in wwuFiles)
            {
                try
                {
                    var doc = XDocument.Load(wwuFile);
                    var events = doc.Descendants("Event");

                    foreach (var evt in events)
                    {
                        var eventPath = evt.Attribute("Name")?.Value ?? "";

                        foreach (var action in evt.Descendants("Action"))
                        {
                            var actionId = action.Attribute("ID")?.Value;
                            var actionName = action.Attribute("Name")?.Value ?? "";

                            var targetRef = action.Descendants("Reference")
                                .FirstOrDefault(r => r.Attribute("Name")?.Value == "Target");

                            var objectRef = targetRef?.Element("ObjectRef");

                            if (objectRef != null)
                            {
                                var targetId = objectRef.Attribute("ID")?.Value;

                                if (!IDsAddedToChart.Contains(targetId))
                                {
                                    IDsAddedToChart.Add(targetId);

                                    var targetName = objectRef.Attribute("Name")?.Value;
                                    ParentData parentData = GetInheritedParentData(objectRef);

                                    actionsWithTargets.Add(new WwiseAction
                                    {
                                        Id = actionId,
                                        Name = actionName,
                                        Path = eventPath,
                                        TargetId = targetId,
                                        TargetName = targetName,
                                        ParentData = parentData
                                    });

                                    Console.WriteLine($"[Info] Found target '{objectRef.Attribute("Name")?.Value}' (Parent: {parentData.Name} with color {parentData.Color}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed parsing WWU '{wwuFile}': {ex.Message}");
                }
            }

            Console.WriteLine($"[Info] Extracted {actionsWithTargets.Count} actions with targets.");
            return actionsWithTargets;
        }

        #region RTPC Preloading (parse audio object WWU once)

        private static SKColor GetSkColorFromWwiseCode(int index)
        {
            if (index >= 0 && index < WwisePalette.Length)
                return WwisePalette[index];
            return new SKColor(200, 200, 200); // défaut
        }

        private static ParentData GetInheritedParentData(XElement element)
        {
            ParentData parentData = new ParentData
            {
                Name = "[NONE]",
                Color = new SKColor(200, 200, 200)
            };

            XElement current = element;

            while (current != null)
            {
                var propertyList = current.Element("PropertyList");
                if (propertyList != null)
                {
                    var overrideColor = propertyList.Elements("Property")
                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "OverrideColor")
                        ?.Attribute("Value")?.Value;

                    var colorProp = propertyList.Elements("Property")
                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "Color")
                        ?.Attribute("Value")?.Value;

                    if (colorProp != null && int.TryParse(colorProp, out int colorCode))
                    {
                        if (string.Equals(overrideColor, "True", StringComparison.OrdinalIgnoreCase))
                        {
                            // Retourne la couleur + nom du parent qui a override
                            var nameAttr = current.Attribute("Name")?.Value;

                            parentData.Color = GetSkColorFromWwiseCode(colorCode);
                            parentData.Name = nameAttr ?? "[NONE]";
                            
                            return parentData;
                        }
                        // Si pas override, on garde la couleur mais pas le nom
                        parentData.Color = GetSkColorFromWwiseCode(colorCode);
                        parentData.Name = "[NONE]";
                        return parentData;
                    }
                }
                current = current.Parent;
            }

            return parentData;
        }

        /// <summary>
        /// Scans audio object WWU files once and fills volumeRangeCache with RTPC min/max for each object ID found.
        /// Uses ConcurrentDictionary for safety.
        /// </summary>
        public static void PreloadVolumeRanges()
        {
            try
            {
                if (string.IsNullOrEmpty(audioObjWWUFolderPath) || !Directory.Exists(audioObjWWUFolderPath))
                {
                    Console.WriteLine($"[Warning] audioObjWWUFolderPath is not set or doesn't exist: {audioObjWWUFolderPath}");
                    return;
                }

                var wwuFiles = Directory.GetFiles(audioObjWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
                Console.WriteLine($"[Info] {wwuFiles.Length} .wwu files found in {audioObjWWUFolderPath}");

                foreach (var file in wwuFiles)
                {
                    try
                    {
                        var doc = XDocument.Load(file);
                        var objects = doc.Descendants()
                            .Where(e => e.Name == "ActorMixer" || e.Name == "Sound" || e.Name == "RandomSequenceContainer" || e.Name == "BlendContainer");

                        foreach (var obj in objects)
                        {
                            var id = obj.Attribute("ID")?.Value;
                            if (string.IsNullOrEmpty(id)) continue;

                            var rtpcs = obj.Descendants("RTPC")
                                .Where(rtpc =>
                                    rtpc.Element("PropertyList")?.Elements("Property")
                                        .Any(p => p.Attribute("Name")?.Value == "PropertyName" &&
                                                  p.Attribute("Value")?.Value == "Volume") == true);

                            if (!rtpcs.Any()) continue;

                            var yValues = rtpcs
                                .SelectMany(rtpc => rtpc.Descendants("Point")
                                    .Select(p => p.Element("YPos")?.Value)
                                    .Where(y => !string.IsNullOrWhiteSpace(y))
                                    .Select(y =>
                                    {
                                        if (float.TryParse(y.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                                            return v;
                                        return float.NaN;
                                    })
                                    .Where(v => !float.IsNaN(v)))
                                .ToList();

                            if (yValues.Count > 0)
                            {
                                var min = yValues.Min();
                                var max = yValues.Max();
                                WwiseCache.volumeRangeCache[id] = (min, max);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Warning] Failed parsing audioObj WWU '{file}': {ex.Message}");
                    }
                }

                Console.WriteLine($"[Info] Preloaded RTPC ranges for {WwiseCache.volumeRangeCache.Count} audio objects.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] PreloadVolumeRanges failed: {ex.Message}");
            }
        }
        #endregion
    }
}