using CSharpMarkup.WinUI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WwiseHDRTool
{
    internal static class WaapiBridge
    {
        static AK.Wwise.Waapi.JsonClient client = new AK.Wwise.Waapi.JsonClient();
        public static bool ConnectedToWwise { get; private set; } = false;
        public static bool UpdateMeters;

        public static async Task ConnectToWwise()
        {
            try
            {
                await client.Connect();
                ConnectedToWwise = true;

                MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                MainWindow.Instance.MainViewModel.IsButtonEnabled = true);

                await GetProjectInfos();

                client.Disconnected += () =>
                {
                    Console.WriteLine("Connexion perdue !");
                    ConnectedToWwise = false;

                    MainWindow.Instance.DispatcherQueue.TryEnqueue(() =>
                    MainWindow.Instance.MainViewModel.IsButtonEnabled = false);
                };
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"Erreur lors de la connexion : {ex.Message}");
                Console.WriteLine($"Erreur lors de la connexion : {ex.Message}");
            }
        }

        public static async Task Disconnect()
        {
            if (ConnectedToWwise)
            {
                await client.Close();
                ConnectedToWwise = false;
            }
            else
            {
                Console.WriteLine("La connexion est déjà fermée.");
            }
        }

        public static async Task<JObject> GenericClienCall(string uri, JObject args, JObject options)
        {
            return await client.Call(uri, args, options);
        }

        public static async Task<JObject> GetWwiseInfos()
        {
            try
            {
                var response = await GenericClienCall(ak.wwise.core.getInfo, null, null);
                Console.WriteLine(response);
                return response;
            }
            catch (Exception e)
            {
                EnqueueErrorMessage("Error", "Erreur récupération infos Wwise : " + e.Message);
                Console.Error.WriteLine("Erreur récupération infos Wwise : " + e.Message);
                throw;
            }
        }

        public static async Task GetProjectInfos()
        {
            try
            {
                var projectInfo = await GenericClienCall(ak.wwise.core.getProjectInfo, null, null);
                // Extraction de la version Wwise depuis le champ displayTitle
                string displayTitle = projectInfo["displayTitle"]?.ToString() ?? "";
                string wwiseVersion = "unknown";

                var match = System.Text.RegularExpressions.Regex.Match(displayTitle, @"Wwise (\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    wwiseVersion = match.Groups[1].Value;
                }

                // Récupération du chemin complet du projet (fichier .wproj)
                string projectFullPath = projectInfo["path"]?.ToString() ?? "";

                // Récupération du dossier parent du projet (le dossier du projet audio)
                string projectFolder = "";
                if (!string.IsNullOrEmpty(projectFullPath))
                {
                    projectFolder = System.IO.Path.GetDirectoryName(projectFullPath) ?? "";
                }

                // Détermination du dossier racine à utiliser selon la version
                string audioObjFodlerName = wwiseVersion.StartsWith("2025") ? "Containers" : "Actor-Mixer Hierarchy";

                // Construction du chemin complet du dossier Audio object
                string audioObjWWUFolderPath = System.IO.Path.Combine(projectFolder, audioObjFodlerName);

                string eventsWWUFolderPath = System.IO.Path.Combine(projectFolder, "Events");

                Console.WriteLine($"Wwise version: {wwiseVersion}");
                Console.WriteLine($"Project folder path: {projectFolder}");
                Console.WriteLine($"Events folder path: {eventsWWUFolderPath}");
                Console.WriteLine($"Audio object folder path: {audioObjWWUFolderPath}");

                WWUParser.SetProjectFolderPathes(eventsWWUFolderPath, audioObjWWUFolderPath);
            }
            catch (Exception e)
            {
                EnqueueErrorMessage("Error", "Erreur récupération infos projet : " + e.Message);
                Console.Error.WriteLine("Erreur récupération infos projet : " + e.Message);
                throw;
            }
        }

        #region WAAPI Helpers (unchanged behaviour but batched + cached)

        public static async Task<List<AudioBus>> GetAudioBuses()
        {
            var buses = new List<AudioBus>();

            var query = new JObject(
                new JProperty("from", new JObject(
                    new JProperty("ofType", new JArray("Bus"))
                ))
            );

            var options = new JObject(
                new JProperty("return", new JArray("id", "name", "path", "HdrEnable"))
            );

            try
            {
                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                var hdrMap = result["return"]?
                    .ToDictionary(
                        bus => bus["path"]?.ToString() ?? "",
                        bus => bus["HdrEnable"]?.Value<bool>() ?? false
                    ) ?? new Dictionary<string, bool>();

                foreach (var bus in result["return"]!)
                {
                    var path = bus["path"]?.ToString() ?? "";
                    var isHDR = bus["HdrEnable"]?.Value<bool>() ?? false;

                    bool isHDRChild = false;
                    var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = parts.Length - 1; i > 0; i--)
                    {
                        var parentPath = "\\" + string.Join("\\", parts.Take(i));
                        if (hdrMap.TryGetValue(parentPath, out bool parentIsHDR) && parentIsHDR)
                        {
                            isHDRChild = true;
                            break;
                        }
                    }

                    buses.Add(new AudioBus
                    {
                        Name = bus["name"]?.ToString(),
                        Id = bus["id"]?.ToString(),
                        Path = path,
                        IsHDR = isHDR,
                        HDRChild = isHDRChild && !isHDR
                    });
                }

                return buses;
            }
            catch (Exception e)
            {
                EnqueueErrorMessage("Error", $"[Error] Failed to retrieve AudioBuses: {e.Message}");
                Console.Error.WriteLine($"[Error] Failed to retrieve AudioBuses: {e.Message}");
                return buses;
            }
        }

        public static async Task<List<WwiseEvent>> GetEvents()
        {
            var events = new List<WwiseEvent>();

            var query = new JObject(
                new JProperty("from", new JObject(
                    new JProperty("ofType", new JArray("Event"))
                ))
            );

            var options = new JObject(
                new JProperty("return", new JArray("id", "name", "path"))
            );

            try
            {
                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (var evt in result["return"]!)
                {
                    events.Add(new WwiseEvent
                    {
                        Name = evt["name"]?.ToString(),
                        Id = evt["id"]?.ToString(),
                        Path = evt["path"]?.ToString(),
                    });
                }

                return events;
            }
            catch (Exception e)
            {
                EnqueueErrorMessage("Error", $"[Error] Failed to retrieve Events: {e.Message}");
                Console.Error.WriteLine($"[Error] Failed to retrieve Events: {e.Message}");
                return events;
            }
        }

        #endregion

        #region Batched & Cached WAAPI Calls

        /// <summary>
        /// Batch fetch OutputBus for a list of target IDs and cache results in outputBusCache.
        /// </summary>
        public static async Task BatchGetTargetOutputBus(IEnumerable<string> targetIds)
        {
            var idsToFetch = targetIds.Where(id => !WwiseCache.outputBusCache.ContainsKey(id)).Distinct().ToList();
            if (idsToFetch.Count == 0) return;

            try
            {
                var query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(idsToFetch))
                    ))
                );

                var options = new JObject(
                    new JProperty("return", new JArray("id", "OutputBus"))
                );

                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (var obj in result["return"]!)
                {
                    var id = obj["id"]?.ToString();
                    var busId = obj["OutputBus"]?["id"]?.ToString();
                    if (id != null)
                        WwiseCache.outputBusCache[id] = busId;
                }

                // For any ids not returned by WAAPI, set null to avoid refetching later
                foreach (var id in idsToFetch)
                {
                    WwiseCache.outputBusCache.TryAdd(id, WwiseCache.outputBusCache.ContainsKey(id) ? WwiseCache.outputBusCache[id] : null);
                }
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"BatchGetTargetOutputBus failed: {ex.Message}");
                Console.WriteLine($"[Warning] BatchGetTargetOutputBus failed: {ex.Message}");
                // Ensure we don't re-request infinitely: mark them as null
                foreach (var id in idsToFetch) WwiseCache.outputBusCache.TryAdd(id, null);
            }
        }

        /// <summary>
        /// Batch fetch Volume for a list of target IDs and cache results in volumeCache.
        /// </summary>
        public static async Task BatchGetVolumes(IEnumerable<string> targetIds)
        {
            var idsToFetch = targetIds.Where(id => !WwiseCache.volumeCache.ContainsKey(id)).Distinct().ToList();
            if (idsToFetch.Count == 0) return;

            try
            {
                var query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(idsToFetch))
                    ))
                );

                var options = new JObject(
                    new JProperty("return", new JArray("id", "Volume"))
                );

                var result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (var obj in result["return"]!)
                {
                    var id = obj["id"]?.ToString();
                    var volStr = obj["Volume"]?.ToString();
                    if (id != null)
                    {
                        if (float.TryParse(volStr, out var v))
                            WwiseCache.volumeCache[id] = v;
                        else
                            WwiseCache.volumeCache[id] = null;
                    }
                }

                // Mark missing ids with null to avoid refetch
                foreach (var id in idsToFetch)
                {
                    WwiseCache.volumeCache.TryAdd(id, WwiseCache.volumeCache.ContainsKey(id) ? WwiseCache.volumeCache[id] : null);
                }
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"BatchGetVolumes failed: {ex.Message}");
                Console.WriteLine($"[Warning] BatchGetVolumes failed: {ex.Message}");
                foreach (var id in idsToFetch) WwiseCache.volumeCache.TryAdd(id, null);
            }
        }

        #endregion

        public static async Task FocusWwiseWindow()
        {
            try
            {
                await GenericClienCall("ak.wwise.ui.bringToForeground", null, null);
                Console.WriteLine("Wwise window brought to foreground.");
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"Failed to bring Wwise to foreground: {ex.Message}");
                Console.Error.WriteLine($"[Error] Failed to bring Wwise to foreground: {ex.Message}");
            }
        }

        public static async Task InspectWwiseObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                Console.Error.WriteLine("[Error] objectId is null or empty.");
                return;
            }

            try
            {
                var args = new JObject
                {
                    ["command"] = "Inspect",
                    ["objects"] = new JArray(objectId)
                };

                await GenericClienCall("ak.wwise.ui.commands.execute", args, null);
                Console.WriteLine($"Object {objectId} inspected in Wwise.");
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"Failed to inspect object {objectId}: {ex.Message}");
                Console.Error.WriteLine($"[Error] Failed to inspect object {objectId}: {ex.Message}");
            }
        }

        public static async Task FindObjectInProjectExplorer(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                EnqueueErrorMessage("Error", $"ObjectId is null or empty.");
                Console.Error.WriteLine("[Error] objectId is null or empty.");
                return;
            }
            try
            {
                var args = new JObject
                {
                    ["command"] = "FindInProjectExplorerSelectionChannel1",
                    ["objects"] = new JArray(objectId)
                };
                await GenericClienCall("ak.wwise.ui.commands.execute", args, null);
                Console.WriteLine($"Object {objectId} found in Project Explorer.");
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"Failed to find object {objectId} in Project Explorer: {ex.Message}");
                Console.Error.WriteLine($"[Error] Failed to find object {objectId} in Project Explorer: {ex.Message}");
            }
        }

        static async void EnqueueErrorMessage(string type, string message)
        {
            MainWindow.Instance.DispatcherQueue.TryEnqueue(async () => {
                MainWindow.Instance.ShowMessageAsync(type, $"Erreur lors de la connexion : {message}");
            });
        }
    }
}
