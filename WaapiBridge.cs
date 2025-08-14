using CSharpMarkup.WinUI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WwiseHDRTool
{
    internal static class WaapiBridge
    {
        static AK.Wwise.Waapi.JsonClient client = new AK.Wwise.Waapi.JsonClient();
        public static bool ConnectedToWwise { get; private set; } = false;

        public static async Task ConnectToWwise()
        {
            try
            {
                await client.Connect();
                ConnectedToWwise = true;

                Console.WriteLine("[Info] Connected to Wwise!");

                await GetProjectInfos();

                client.Disconnected += () =>
                {
                    Console.Error.WriteLine("Lost connection to Wwise!");
                    ConnectedToWwise = false;
                };
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"{ex.Message}");
                Console.WriteLine($"{ex.Message}");
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
                Console.WriteLine("The connection is already closed.");
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
                Console.WriteLine(response);
                return response;
            }
            catch (Exception e)
            {
                EnqueueErrorMessage("Error", "Error retrieving Wwise info: " + e.Message);
                Console.Error.WriteLine("Error retrieving Wwise info: " + e.Message);
                throw;
            }
        }

        public static async Task GetProjectInfos()
        {
            try
            {
                JObject projectInfo = await GenericClienCall(ak.wwise.core.getProjectInfo, null, null);

                // Extract Wwise version from the displayTitle field
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

                // Build the full path of the Audio object folder
                string audioObjWWUFolderPath = System.IO.Path.Combine(projectFolder, audioObjFolderName);

                string eventsWWUFolderPath = System.IO.Path.Combine(projectFolder, "Events");

                Console.WriteLine($"Wwise version: {wwiseVersion}");
                Console.WriteLine($"Project folder path: {projectFolder}");
                Console.WriteLine($"Events folder path: {eventsWWUFolderPath}");
                Console.WriteLine($"Audio object folder path: {audioObjWWUFolderPath}");

                WWUParser.SetProjectFolderPathes(eventsWWUFolderPath, audioObjWWUFolderPath);
            }
            catch (Exception e)
            {
                EnqueueErrorMessage("Error", "Error retrieving project info: " + e.Message);
                Console.Error.WriteLine("Error retrieving project info: " + e.Message);
                throw;
            }
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
                EnqueueErrorMessage("Error", $"[Error] Failed to retrieve AudioBuses: {e.Message}");
                Console.Error.WriteLine($"[Error] Failed to retrieve AudioBuses: {e.Message}");
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
                EnqueueErrorMessage("Error", $"BatchGetTargetOutputBus failed: {ex.Message}");
                Console.WriteLine($"[Warning] BatchGetTargetOutputBus failed: {ex.Message}");
                // Ensure we don't re-request infinitely: mark them as null
                foreach (string? id in idsToFetch)
                {
                    WwiseCache.outputBusCache.TryAdd(id, null);
                }
            }
        }

        /// <summary>
        /// Batch fetch Volume for a list of target IDs and cache results in volumeCache.
        /// </summary>
        public static async Task BatchGetVolumes(IEnumerable<string> targetIds)
        {
            List<string> idsToFetch = targetIds.Where(id => !WwiseCache.volumeCache.ContainsKey(id)).Distinct().ToList();
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
                    new JProperty("return", new JArray("id", "Volume"))
                );

                JObject result = await WaapiBridge.GenericClienCall("ak.wwise.core.object.get", query, options);

                foreach (JToken obj in result["return"]!)
                {
                    string? id = obj["id"]?.ToString();
                    string? volStr = obj["Volume"]?.ToString();
                    if (id != null)
                    {
                        if (float.TryParse(volStr, out float v))
                        {
                            WwiseCache.volumeCache[id] = v;
                        }
                        else
                        {
                            WwiseCache.volumeCache[id] = null;
                        }
                    }
                }

                // Mark missing ids with null to avoid refetch
                foreach (string? id in idsToFetch)
                {
                    WwiseCache.volumeCache.TryAdd(id, WwiseCache.volumeCache.ContainsKey(id) ? WwiseCache.volumeCache[id] : null);
                }
            }
            catch (Exception ex)
            {
                EnqueueErrorMessage("Error", $"BatchGetVolumes failed: {ex.Message}");
                Console.WriteLine($"[Warning] BatchGetVolumes failed: {ex.Message}");
                foreach (string? id in idsToFetch)
                {
                    WwiseCache.volumeCache.TryAdd(id, null);
                }
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
                JObject args = new JObject
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
                JObject args = new JObject
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
            MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
            {
                await MainWindow.Instance.ShowMessageAsync(type, $"Error during connection: {message}");
            });
        }
    }
}
