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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;

namespace WwiseHDRTool
{
    public static class WWUParser
    {
        private static string eventsWWUFolderPath = "";
        private static string audioObjWWUFolderPath = "";
        private static string busWWUFolderPath = "";

        private static readonly SKColor[] WwisePalette = {
            /*  0 */ new SKColor(100,110,120),
            /*  1 */ new SKColor(104,107,230),
            /*  2 */ new SKColor(55,129,243),
            /*  3 */ new SKColor(2,169,185),
            /*  4 */ new SKColor(0,185,18),
            /*  5 */ new SKColor(131,185,18),
            /*  6 */ new SKColor(190,174,17),
            /*  7 */ new SKColor(226,159,25),
            /*  8 */ new SKColor(234,123,20),
            /*  9 */ new SKColor(217,77,47),
            /* 10 */ new SKColor(215,59,58),
            /* 11 */ new SKColor(231,22,229),
            /* 12 */ new SKColor(174,27,248),
            /* 13 */ new SKColor(145,72,253),
            /* 14 */ new SKColor(150,152,229),
            /* 15 */ new SKColor(121,154,217),
            /* 16 */ new SKColor(82,181,181),
            /* 17 */ new SKColor(102,181,105),
            /* 18 */ new SKColor(162,190,81),
            /* 19 */ new SKColor(210,199,53),
            /* 20 */ new SKColor(202,164,89),
            /* 21 */ new SKColor(201,149,95),
            /* 22 */ new SKColor(197,133,120),
            /* 23 */ new SKColor(203,125,125),
            /* 24 */ new SKColor(207,113,181),
            /* 25 */ new SKColor(190,108,215),
            /* 26 */ new SKColor(172,147,232),
            /* 27 */ new SKColor(129,140,150)
        };

        public static void ResetProjectFolderPaths()
        {
            eventsWWUFolderPath = string.Empty;
            audioObjWWUFolderPath = string.Empty;
            busWWUFolderPath = string.Empty;
        }


        public static void SetProjectFolderPaths(string _eventsWWUFolderPath, string _audioObjWWUFolderPath, string _busWWUFolderPath)
        {
            eventsWWUFolderPath = _eventsWWUFolderPath;
            audioObjWWUFolderPath = _audioObjWWUFolderPath;
            busWWUFolderPath = _busWWUFolderPath;

            Log.Info($"Working with the following folders : \n{eventsWWUFolderPath}\n{audioObjWWUFolderPath}\n{busWWUFolderPath}\n");
        }

        /// <summary>
        /// Charge tous les Bus des WWU et les met en cache (par ID).
        /// </summary>
        public static void PreloadBusData()
        {
            if (string.IsNullOrEmpty(busWWUFolderPath) || !Directory.Exists(busWWUFolderPath))
            {
                Log.Warning($"busWWUFolderPath is not set or doesn't exist: {busWWUFolderPath}");
                return;
            }

            var wwuFiles = Directory.GetFiles(busWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
            Log.Info($"{wwuFiles.Length} bus .wwu files found in {busWWUFolderPath}");

            foreach (var file in wwuFiles)
            {
                try
                {
                    var doc = XDocument.Load(file);
                    var buses = doc.Descendants("Bus");
                    foreach (var bus in buses)
                    {
                        var id = bus.Attribute("ID")?.Value;
                        if (string.IsNullOrEmpty(id))
                            continue;

                        // Cache complet des bus XML
                        WwiseCache.busesByIdCache[id] = bus;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed parsing bus WWU '{file}': {ex}");
                }
            }

            Log.Info($"Cached {WwiseCache.busesByIdCache.Count} buses.");
        }


        /// <summary>
        /// Remonte la hiérarchie des Bus (OutputBus → parent → root HDR)
        /// et retourne le cumul de leurs contributions Volume.
        /// </summary>
        private static (float value, float min, float max) GetCumulativeBusVolume(string? busId)
        {
            float totalValue = 0f, totalMin = 0f, totalMax = 0f;

            while (!string.IsNullOrEmpty(busId) && WwiseCache.busesByIdCache.TryGetValue(busId, out var bus))
            {
                Log.Info($"Extracting volume contribution of: {bus.Attribute("Name")?.Value} ({busId})");
                var contrib = ExtractVolumeContributions(bus, false);
                totalValue += contrib.value;
                totalMin += contrib.min;
                totalMax += contrib.max;

                Log.Info($"\t found: {contrib.value}, {contrib.min}, {contrib.max}");

                // remonter via busParentCache
                if (bus.Parent?.Parent == null)
                    break;

                busId = bus.Parent?.Parent.Attribute("ID")?.Value;
            }

            return (totalValue, totalMin, totalMax);
        }

        /// <summary>
        /// Parse events' actions from WWU files (events workunits).
        /// Single pass over events WWU files.
        /// </summary>
        public static List<WwiseAction> ParseEventActionsFromWorkUnits()
        {
            Log.Info("Parsing Actions and Targets from WWU files...");

            var actionsWithTargets = new List<WwiseAction>();
            if (string.IsNullOrEmpty(eventsWWUFolderPath) || !Directory.Exists(eventsWWUFolderPath))
            {
                Log.Warning($"Events WWU folder path is not set or doesn't exist: {eventsWWUFolderPath}");
                return actionsWithTargets;
            }

            var wwuFiles = Directory.GetFiles(eventsWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
            Log.Info($"Found {wwuFiles.Length} .wwu event files.");

            var IDsAddedToChart = new HashSet<string>();

            foreach (var wwuFile in wwuFiles)
            {
                try
                {
                    var doc = XDocument.Load(wwuFile);
                    var events = doc.Descendants("Event");

                    foreach (var evt in events)
                    {
                        var eventPath = evt.Attribute("Name")?.Value ?? "";

                        foreach (var action in evt.Element("ChildrenList")?.Elements("Action") ?? Enumerable.Empty<XElement>())
                        {
                            var actionId = action.Attribute("ID")?.Value;
                            var actionName = action.Attribute("Name")?.Value ?? "";

                            var targetRef = action
                                                .Element("ReferenceList")?
                                                .Elements("Reference")
                                                .FirstOrDefault(r => r.Attribute("Name")?.Value == "Target");

                            var objectRef = targetRef?.Element("ObjectRef");

                            if (objectRef != null)
                            {
                                var targetId = objectRef.Attribute("ID")?.Value;

                                var audioObj = ResolveAudioObjectById(targetId);

                                if (audioObj != null)
                                {
                                    // Nouvelle logique : applique l'algorigramme optimisé
                                    var finalTargets = GetHDRTargetsWithFlowchartLogic(audioObj);

                                    foreach (var target in finalTargets)
                                    {
                                        var childID = target.Attribute("ID")?.Value;
                                        if (string.IsNullOrEmpty(childID) || IDsAddedToChart.Contains(childID))
                                        {
                                            continue;
                                        }

                                        IDsAddedToChart.Add(childID);

                                        var targetName = target.Attribute("Name")?.Value;
                                        var parentData = GetInheritedParentData(target);

                                        actionsWithTargets.Add(new WwiseAction
                                        {
                                            Id = actionId,
                                            Name = actionName,
                                            Path = eventPath,
                                            TargetId = childID,
                                            TargetName = targetName,
                                            ParentData = parentData
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed parsing WWU '{wwuFile}': {ex.ToString()}");
                }
            }

            Log.Info($"Extracted {actionsWithTargets.Count} actions with targets.");
            return actionsWithTargets;
        }

        #region Algorithme optimisé (flowchart)

        private static List<XElement> GetHDRTargetsWithFlowchartLogic(XElement root)
        {
            var result = new List<XElement>();
            var visibleTags = new HashSet<string>(); // IDs marqués comme "visible"
            var addedIds = new HashSet<string>();    // IDs déjà ajoutés
            var cache = new Dictionary<string, bool>();

            var audioObjName = root.Attribute("Name")?.Value ?? "Unknown";

            MainWindow.Instance.loadingDialog?.SetDetailsText($"Processing {audioObjName}...");

            // 🔹 Récupérer TOUS les leafs valides
            var validLeafs = FindAllValidLeafs(root, cache);

            if (!validLeafs.Any())
            {
                var rid = root.Attribute("ID")?.Value;
                if (!string.IsNullOrEmpty(rid))
                {
                    result.Add(root);
                }

                Log.Info("No valid leafs found, returning root only.");
                return result;
            }

            Log.Info($"Found {validLeafs.Count} valid leafs.");

            void ProcessParent(XElement parent)
            {
                Log.Separator();
                Log.Info($"Processing parent: {parent.Attribute("Name")?.Value} ({parent.Attribute("ID")?.Value})");

                var children = GetImmediateChildren(parent).ToList();
                var anyChildValid = children.Any(c => HasSpecificVoiceVolumeSetupCached(c, cache));
                var anyChildVisible = children.Any(c => visibleTags.Contains(c.Attribute("ID")?.Value));

                if (anyChildValid || anyChildVisible)
                {
                    foreach (var child in children)
                    {
                        var cid = child.Attribute("ID")?.Value;
                        if (!string.IsNullOrEmpty(cid) && addedIds.Add(cid) && !visibleTags.Contains(cid))
                        {
                            result.Add(child);
                            Log.Info($"Added child: {child.Attribute("Name")?.Value} ({cid})");
                        }
                    }

                    var pid = parent.Attribute("ID")?.Value;
                    if (!string.IsNullOrEmpty(pid))
                    {
                        visibleTags.Add(pid);
                        var toRemove = result.FirstOrDefault(e => e.Attribute("ID")?.Value == pid);
                        if (toRemove != null)
                        {
                            result.Remove(toRemove);
                            addedIds.Remove(pid); // pour que l’ID puisse resservir si besoin
                            Log.Info($"Removed parent from result: {parent.Attribute("Name")?.Value} ({pid})");
                        }
                        Log.Info($"Parent tagged visible: {parent.Attribute("Name")?.Value} ({pid})");
                    }
                }

                if (parent.Parent?.Parent != null && parent != root)
                {
                    ProcessParent(parent.Parent.Parent);
                }
            }


            foreach (var leaf in validLeafs)
            {
                Log.Info($"Processing branch from leaf: {leaf.Attribute("Name")?.Value} ({leaf.Attribute("ID")?.Value})");
                if (leaf.Parent?.Parent != null)
                {
                    ProcessParent(leaf.Parent.Parent);
                }
                else
                {
                    Log.Error("Leaf parent structure is invalid, skipping.");
                }
            }

            Log.AddSpace();
            return result;
        }

        /// <summary>
        /// Trouve tous les leafs qui ont un setup volume spécifique.
        /// </summary>
        private static List<XElement> FindAllValidLeafs(XElement root, Dictionary<string, bool> cache)
        {
            var validLeafs = new List<XElement>();
            var stack = new Stack<(XElement node, int depth)>();
            stack.Push((root, 0));

            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();

                if (node != root && HasSpecificVoiceVolumeSetupCached(node, cache))
                {
                    validLeafs.Add(node);
                }

                foreach (var child in GetImmediateChildren(node))
                {
                    stack.Push((child, depth + 1)); // ici on utilise "depth"
                }
            }

            return validLeafs;
        }

        private static bool HasSpecificVoiceVolumeSetupCached(XElement e, Dictionary<string, bool> cache)
        {
            var id = e.Attribute("ID")?.Value ?? e.GetHashCode().ToString();
            if (cache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var r = HasSpecificVoiceVolumeSetup(e);
            cache[id] = r;
            return r;
        }

        /// <summary>
        /// Renvoie les enfants immédiats (dans &lt;ChildrenList&gt;) ayant un ID.
        /// </summary>
        private static IEnumerable<XElement> GetImmediateChildren(XElement node)
        {
            return node.Element("ChildrenList")?.Elements()
                       .Where(e => e.Attribute("ID") != null)
                   ?? Enumerable.Empty<XElement>();
        }

        /// <summary>
        /// true si l'objet a un Volume != 0 (Value ou ValueList), un RTPC/automation Volume, un state volume ou des override de bus/aux.
        /// </summary>
        private static bool HasSpecificVoiceVolumeSetup(XElement obj)
        {
            // On ne quitte pas trop tôt : propertyList peut être absent mais RTPC/State peuvent exister ailleurs
            var propertyList = obj.Element("PropertyList");

            // --- Cas 1 et 2 : Volume direct ou ValueList (si PropertyList présent) ---
            var volumeProperty = propertyList?.Elements("Property")
                .FirstOrDefault(p => (string)p.Attribute("Name") == "Volume");

            if (volumeProperty != null)
            {
                // Value direct (attribut Value)
                var valAttr = volumeProperty.Attribute("Value")?.Value;
                if (!string.IsNullOrWhiteSpace(valAttr)
                    && double.TryParse(valAttr.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var val)
                    && Math.Abs(val) > double.Epsilon)
                {
                    return true;
                }

                // ValueList
                var values = volumeProperty.Element("ValueList")?.Elements("Value");
                if (values != null)
                {
                    foreach (var v in values)
                    {
                        var s = v.Value;
                        if (!string.IsNullOrWhiteSpace(s)
                            && double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var vv)
                            && Math.Abs(vv) > double.Epsilon)
                        {
                            return true;
                        }
                    }
                }

                // RTPC/automation (ModifierList attaché à la propriété Volume)
                if (volumeProperty.Element("ModifierList") != null)
                {
                    return true;
                }
            }

            // --- Cas 3 : CustomState qui contient une propriété Volume (n'importe où sous l'élément) ---
            var customStates = obj.Descendants("CustomState");
            if (customStates.Any(cs => cs.Element("PropertyList")?.Elements("Property")
                    .Any(p => (string)p.Attribute("Name") == "Volume") == true))
            {
                return true;
            }

            // --- Cas 4 : RTPC ---
            var rtpcs = obj.Element("ObjectLists")?
                           .Elements("ObjectList")
                           .Where(ol => (string)ol.Attribute("Name") == "RTPC")
                           .Elements("RTPC");

            if (rtpcs != null)
            {
                if (rtpcs.Any(r => (string?)r.Element("PropertyList")?.Elements("Property")
                        .FirstOrDefault(p => (string)p.Attribute("Name") == "PropertyName")
                        ?.Attribute("Value") == "Volume"))
                {
                    Log.Info($"RTPC Volume detected on {obj.Attribute("Name")?.Value} ({obj.Attribute("ID")?.Value})");
                    return true;
                }
            }

            // --- Cas 5 : Overrides Output / Aux bus (si PropertyList présent) ---
            string[] overrideProps = { "OverrideOutput"/*, "OverrideGameAuxSends", "OverrideUserAuxSends" */};
            if (propertyList?.Elements("Property")
                .Any(p => overrideProps.Contains((string)p.Attribute("Name")) &&
                          string.Equals((string?)p.Attribute("Value"), "True", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }

            Log.Info($"No specific volume setup found for {obj.Attribute("Name")?.Value ?? "Unknown"}");
            return false;
        }
        #endregion

        #region RTPC Preloading (parse audio object WWU once)

        private static SKColor GetSkColorFromWwiseCode(int index)
        {
            if (index >= 0 && index < WwisePalette.Length)
            {
                return WwisePalette[index];
            }

            return new SKColor(200, 200, 200); // default color
        }

        private static XElement? ResolveAudioObjectById(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            // Regarde si déjà en cache
            if (WwiseCache.audioObjectsByIdCache.TryGetValue(targetId, out var cachedObj))
            {
                return cachedObj;
            }

            return null; // pas trouvé
        }

        private static ParentData GetInheritedParentData(XElement element)
        {
            var parentData = new ParentData
            {
                Name = "[NONE]",
                Color = new SKColor(200, 200, 200)
            };

            var current = element;

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

                    if (colorProp != null && int.TryParse(colorProp, out var colorCode))
                    {
                        if (string.Equals(overrideColor, "True", StringComparison.OrdinalIgnoreCase))
                        {
                            // Return parent color + name if override is enabled
                            var nameAttr = current.Attribute("Name")?.Value;

                            parentData.Color = GetSkColorFromWwiseCode(colorCode);
                            parentData.Name = nameAttr ?? "[NONE]";

                            return parentData;
                        }
                        // If no override, keep the color but reset the name
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
        /// Scans audio object WWU files once and fills volumeRangeCache with cumulative VoiceVolume value/min/max
        /// (including contributions from parents). Uses ConcurrentDictionary for thread safety.
        /// </summary>
        public static void PreloadVolumeRanges()
        {
            try
            {
                if (string.IsNullOrEmpty(audioObjWWUFolderPath) || !Directory.Exists(audioObjWWUFolderPath))
                {
                    Log.Warning($"audioObjWWUFolderPath is not set or doesn't exist: {audioObjWWUFolderPath}");
                    return;
                }

                var wwuFiles = Directory.GetFiles(audioObjWWUFolderPath, "*.wwu", SearchOption.AllDirectories);
                Log.Info($"{wwuFiles.Length} .wwu files found in {audioObjWWUFolderPath}");

                foreach (var file in wwuFiles)
                {
                    try
                    {
                        var doc = XDocument.Load(file);
                        var objects = doc.Descendants()
                            .Where(e => e.Name == "ActorMixer" || e.Name == "Sound" ||
                                        e.Name == "RandomSequenceContainer" || e.Name == "BlendContainer" ||
                                        e.Name == "SwitchContainer");

                        foreach (var obj in objects)
                        {
                            var id = obj.Attribute("ID")?.Value;
                            if (string.IsNullOrEmpty(id))
                            {
                                continue;
                            }

                            // Ajouter au cache brut des objets
                            WwiseCache.audioObjectsByIdCache[id] = obj;

                            // Ajouter lien objet audio => bus
                            WwiseCache.outputBusCache.TryAdd(id, ResolveEffectiveOutputBus(obj));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed parsing audioObj WWU '{file}': {ex}");
                    }
                }

                // Une fois tous les objets chargés, calculer les ranges cumulés
                foreach (var kvp in WwiseCache.audioObjectsByIdCache)
                {
                    var id = kvp.Key;
                    if (!WwiseCache.volumeRangeCache.ContainsKey(id))
                    {
                        var cumulative = GetCumulativeVolumeRange(id);
                        if (cumulative != null)
                        {
                            WwiseCache.volumeRangeCache[id] = cumulative.Value;
                            Log.AddSpace();
                            Log.Info($"Volume data of {WwiseCache.audioObjectsByIdCache[id].Attribute("Name")?.Value}: {cumulative.Value.value} ({cumulative.Value.min}|{cumulative.Value.max})");
                        }
                    }
                }

                Log.Info($"Preloaded cumulative volume ranges for {WwiseCache.volumeRangeCache.Count} audio objects.");
                Log.Info($"Cached {WwiseCache.audioObjectsByIdCache.Count} audio objects by ID.");
            }
            catch (Exception ex)
            {
                Log.Info($"[Error] PreloadVolumeRanges failed: {ex}");
            }
        }

        /// <summary>
        /// Résout le OutputBus effectif d’un objet audio en tenant compte
        /// des OverrideOutput et de l’héritage parent.
        /// </summary>
        private static string? ResolveEffectiveOutputBus(XElement audioObj)
        {
            XElement? current = audioObj;

            while (current != null)
            {
                // Vérifie OverrideOutput
                var overrideProp = current.Element("PropertyList")
                    ?.Elements("Property")
                    .FirstOrDefault(p => p.Attribute("Name")?.Value == "OverrideOutput");

                bool overrideOutput = overrideProp?.Attribute("Value")?.Value == "True";

                if (overrideOutput || current.Name == "ActorMixer" || current.Name == "Bus")
                {
                    // Si override, ou si on est à la racine (ActorMixer/Bus),
                    // on prend le OutputBus de ce niveau
                    var outputBusRef = current.Element("ReferenceList")
                        ?.Elements("Reference")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "OutputBus")
                        ?.Element("ObjectRef");

                    if (outputBusRef != null)
                    {
                        return outputBusRef.Attribute("ID")?.Value;
                    }
                }

                // remonter à l’ancêtre (ChildrenList → Parent)
                current = current.Parent?.Parent;
            }

            return null; // pas trouvé (rare)
        }


        /// <summary>
        /// Remonte la hiérarchie et cumule :
        /// - VoiceVolume de l'objet et ses parents jusqu'au HDR bus
        /// - Randoms de VoiceVolume
        /// - RTPC min/max de VoiceVolume
        /// - States affectant le VoiceVolume
        /// </summary>
        private static (float value, float min, float max)? GetCumulativeVolumeRange(string id)
        {
            var totalValue = 0f;
            var totalMin = 0f;
            var totalMax = 0f;

            var currentId = id;
            while (!string.IsNullOrEmpty(currentId) && WwiseCache.audioObjectsByIdCache.TryGetValue(currentId, out var obj))
            {
                var contrib = ExtractVolumeContributions(obj, true);
                totalValue += contrib.value;
                totalMin += contrib.min;
                totalMax += contrib.max;

                // remonter au parent
                currentId = obj.Parent?.Parent?.Attribute("ID")?.Value;
            }

            if (WwiseCache.outputBusCache.TryGetValue(id, out string? outputBusId))
            {
                var busContrib = GetCumulativeBusVolume(outputBusId);
                totalValue += busContrib.value;
                totalMin += busContrib.min;
                totalMax += busContrib.max;
            }

            return (totalValue, totalMin, totalMax);
        }

        /// <summary>
        /// Extrait toutes les contributions volume (VoiceVolume, Random, RTPC min/max, States)
        /// pour un seul objet.
        /// </summary>
        public static (float value, float min, float max) ExtractVolumeContributions(XElement obj, bool xtractFromRTPC)
        {
            var value = 0f;
            var min = 0f;
            var max = 0f;

            // Volume direct (Property "Volume")
            var volProp = obj.Element("PropertyList")?
                .Elements("Property")
                .FirstOrDefault(p => (string)p.Attribute("Name") == "Volume");

            if (volProp != null)
            {
                var valNode = volProp.Element("ValueList")?.Element("Value")?.Value
                           ?? volProp.Attribute("Value")?.Value;

                if (!string.IsNullOrWhiteSpace(valNode) &&
                    float.TryParse(valNode.Replace(',', '.'),
                                   NumberStyles.Float,
                                   CultureInfo.InvariantCulture,
                                   out var baseVol))
                {
                    value += baseVol;
                }

                // Min/Max (random ranges, automation)
                foreach (var prop in volProp.Descendants("Property"))
                {
                    if (prop.Attribute("Name")?.Value == "Min" &&
                        float.TryParse(prop.Attribute("Value")?.Value?.Replace(',', '.'),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var minVal))
                    {
                        min += minVal;
                    }

                    if (prop.Attribute("Name")?.Value == "Max" &&
                        float.TryParse(prop.Attribute("Value")?.Value?.Replace(',', '.'),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var maxVal))
                    {
                        max += maxVal;
                    }
                }
            }

            // RTPC → min/max YPos (uniquement ceux définis directement sur l'objet)
            if (xtractFromRTPC)
            {
                var rtpcs = obj.Element("ObjectLists")?
                               .Elements("ObjectList")
                               .Where(ol => (string)ol.Attribute("Name") == "RTPC")
                               .Elements("RTPC");

                if (rtpcs != null)
                {
                    foreach (var rtpc in rtpcs)
                    {
                        var propertyName = rtpc.Element("PropertyList")?
                            .Elements("Property")
                            .FirstOrDefault(p => (string)p.Attribute("Name") == "PropertyName")
                            ?.Attribute("Value")?.Value;

                        if (propertyName == "Volume")
                        {
                            var yValues = rtpc.Descendants("Point")
                                              .Select(p => p.Element("YPos")?.Value)
                                              .Where(s => !string.IsNullOrWhiteSpace(s))
                                              .Select(s => float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ? y : 0f)
                                              .ToList();

                            if (yValues.Count > 0)
                            {
                                min += yValues.Min();
                                max += yValues.Max();
                            }
                        }
                    }
                }
            }

            // States Volume
            var stateProps = obj.Element("StateInfo")?
                                 .Element("CustomStateList")?
                                 .Descendants("Property")
                                 .Where(p => p.Attribute("Name")?.Value == "Volume" && p.Attribute("Value") != null)
                             ?? Enumerable.Empty<XElement>();

            foreach (var sp in stateProps)
            {
                if (float.TryParse(sp.Attribute("Value")?.Value?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var sVal))
                {
                    min += sVal < 0 ? sVal : 0;
                    max += sVal > 0 ? sVal : 0;
                }
            }

            return (value, min, max);
        }

        #endregion
    }
}
