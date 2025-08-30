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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CSharpMarkup.WinUI;
using Newtonsoft.Json.Linq;

namespace WwiseHDRTool
{
    internal static class WaapiBridge
    {
        static AK.Wwise.Waapi.JsonClient client = new AK.Wwise.Waapi.JsonClient();
        public static bool ConnectedToWwise { get; private set; } = false;

        public static async Task<bool> ConnectToWwise()
        {
            try
            {
                await client.Connect($"ws://{WwiseCache.wampIP}:{WwiseCache.wampPort}/waapi", 10000);
                ConnectedToWwise = true;

                Log.Info("Connected to Wwise!");

                client.Disconnected += () =>
                {
                    Log.Error("Lost connection to Wwise!");
                    ConnectedToWwise = false;
                };

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"{ex.ToString()}");
                // Force reset the client if it was partially connected
                client = new AK.Wwise.Waapi.JsonClient();
                return false;
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
                Log.Info("The connection is already closed.");
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
                JObject response = await GenericClienCall(ak.wwise.core.getInfo, null, null);
                Debug.WriteLine(response);
                return response;
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        public static async Task GetProjectInfos()
        {
            try
            {
                JObject projectInfo = await GenericClienCall(ak.wwise.core.getProjectInfo, null, null);

                // Get the project name
                string displayTitle = projectInfo["displayTitle"]?.ToString() ?? "";

                string wwiseVersion = "unknown";

                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(displayTitle, @"Wwise (\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    wwiseVersion = match.Groups[1].Value;
                }

                // Get the full path of the project (.wproj file)
                string projectFullPath = projectInfo["path"]?.ToString() ?? "";

                // Get the parent folder of the project (the audio project folder)
                string projectFolder = "";
                if (!string.IsNullOrEmpty(projectFullPath))
                {
                    projectFolder = System.IO.Path.GetDirectoryName(projectFullPath) ?? "";
                }

                // Determine the root folder to use depending on the version
                string audioObjFolderName = wwiseVersion.StartsWith("2025") ? "Containers" : "Actor-Mixer Hierarchy";
                string busFolderName = wwiseVersion.StartsWith("2025") ? "Busses" : "Master-Mixer Hierarchy";

                // Build the full path of the Audio object folder
                string audioObjWWUFolderPath = System.IO.Path.Combine(projectFolder, audioObjFolderName);
                // Build the full path of the bus folder
                string busWWUFolderPath = System.IO.Path.Combine(projectFolder, busFolderName);

                string eventsWWUFolderPath = System.IO.Path.Combine(projectFolder, "Events");

                Log.Info($"Wwise version: {wwiseVersion}");
                Log.Info($"Project folder path: {projectFolder}");
                Log.Info($"Events folder path: {eventsWWUFolderPath}");
                Log.Info($"Audio object folder path: {audioObjWWUFolderPath}");
                Log.Info($"Bus folder path: {busWWUFolderPath}");

                WWUParser.SetProjectFolderPaths(eventsWWUFolderPath, audioObjWWUFolderPath, busWWUFolderPath);

                WriteDataToUI(wwiseVersion, displayTitle);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        static void WriteDataToUI(string wwiseVersion, string projectName)
        {
            MainWindow.MainDispatcherQueue.TryEnqueue(() =>
            {
                MainWindow.Instance.MainViewModel.WwiseVersion = wwiseVersion;
                Log.Info($"Wwise version set to {wwiseVersion}");
                MainWindow.Instance.MainViewModel.WwiseProjectName = projectName;
                Log.Info($"Wwise project name set to {projectName}");
            });

        }

        #region WAAPI Helpers (unchanged behaviour but batched + cached)

        public static async Task<List<AudioBus>> GetAudioBuses()
        {
            List<AudioBus> buses = new List<AudioBus>();

            JObject query = new JObject(
                new JProperty("from", new JObject(
                    new JProperty("ofType", new JArray("Bus"))
                ))
            );

            JObject options = new JObject(
                new JProperty("return", new JArray("id", "name", "path", "HdrEnable"))
            );

            try
            {
                JObject result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                Dictionary<string, bool> hdrMap = result["return"]?
                    .ToDictionary(
                        bus => bus["path"]?.ToString() ?? "",
                        bus => bus["HdrEnable"]?.Value<bool>() ?? false
                    ) ?? new Dictionary<string, bool>();

                foreach (JToken bus in result["return"]!)
                {
                    string path = bus["path"]?.ToString() ?? "";
                    bool isHDR = bus["HdrEnable"]?.Value<bool>() ?? false;

                    bool isHDRChild = false;
                    string[] parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = parts.Length - 1; i > 0; i--)
                    {
                        string parentPath = "\\" + string.Join("\\", parts.Take(i));
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
                Log.Error(e);
                return buses;
            }
        }

        public static async Task<List<WwiseEvent>> GetEvents()
        {
            List<WwiseEvent> events = new List<WwiseEvent>();

            JObject query = new JObject(
                new JProperty("from", new JObject(
                    new JProperty("ofType", new JArray("Event"))
                ))
            );

            JObject options = new JObject(
                new JProperty("return", new JArray("id", "name", "path"))
            );

            try
            {
                JObject result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (JToken evt in result["return"]!)
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
                Log.Error(e);
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
            List<string> idsToFetch = targetIds.Where(id => !WwiseCache.outputBusCache.ContainsKey(id)).Distinct().ToList();
            if (idsToFetch.Count == 0)
            {
                return;
            }

            try
            {
                JObject query = new JObject(
                    new JProperty("from", new JObject(
                        new JProperty("id", new JArray(idsToFetch))
                    ))
                );

                JObject options = new JObject(
                    new JProperty("return", new JArray("id", "OutputBus"))
                );

                JObject result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (JToken obj in result["return"]!)
                {
                    string? id = obj["id"]?.ToString();
                    string? busId = obj["OutputBus"]?["id"]?.ToString();
                    if (id != null)
                    {
                        WwiseCache.outputBusCache[id] = busId;
                    }
                }

                // For any ids not returned by WAAPI, set null to avoid refetching later
                foreach (string? id in idsToFetch)
                {
                    WwiseCache.outputBusCache.TryAdd(id, WwiseCache.outputBusCache.ContainsKey(id) ? WwiseCache.outputBusCache[id] : null);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"BatchGetTargetOutputBus failed: {ex.ToString()}");
                // Ensure we don't re-request infinitely: mark them as null
                foreach (string? id in idsToFetch)
                {
                    WwiseCache.outputBusCache.TryAdd(id, null);
                }
            }
        }
        #endregion

        public static async Task FocusWwiseWindow()
        {
            try
            {
                await GenericClienCall("ak.wwise.ui.bringToForeground", null, null);
                Log.Info("Wwise window brought to foreground.");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public static async Task InspectWwiseObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                Log.Error("ObjectId is null or empty.");
                return;
            }

            try
            {
                JObject args = new JObject
                {
                    ["command"] = "Inspect",
                    ["objects"] = new JArray(objectId)
                };

                await GenericClienCall("ak.wwise.ui.commands.execute", args, null);
                Log.Info($"Object {objectId} inspected in Wwise.");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public static async Task FindObjectInProjectExplorer(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                Log.Error("ObjectId is null or empty.");
                return;
            }
            try
            {
                JObject args = new JObject
                {
                    ["command"] = "FindInProjectExplorerSelectionChannel1",
                    ["objects"] = new JArray(objectId)
                };
                await GenericClienCall("ak.wwise.ui.commands.execute", args, null);
                Log.Info($"Object {objectId} found in Project Explorer.");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
