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
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpMarkup.WinUI;
using Newtonsoft.Json.Linq;

namespace HDRPriorityViewer;

internal static class WaapiBridge
{
    static AK.Wwise.Waapi.JsonClient client = new ();
    public static bool ConnectedToWwise { get; private set; } = false;

    public static async Task<bool> ConnectToWwise()
    {
        try
        {
            if (client.IsConnected())
            {
                Log.Info("Client already connected.");
                return true;
            }

            await client.Connect($"ws://{WwiseCache.wampIP}:{WwiseCache.wampPort}/waapi", 10000);
            ConnectedToWwise = true;

            MainWindow.MainDispatcherQueue.TryEnqueue(() =>
            {
                MainWindow.Instance.SetWwiseProjectSavedState();
                MainWindow.Instance.SetChartUpdatedState();
            });

            Log.Info("Connected to Wwise!");

            // Détacher tout ancien handler pour éviter les doublons
            client.Disconnected += null;

            client.Disconnected += async () =>
            {
                Log.Error("Lost connection to Wwise!");
                ConnectedToWwise = false;

                // Déconnecter seulement si le client pense être connecté
                if (client.IsConnected())
                    await client.Close();

                MainWindow.MainDispatcherQueue.TryEnqueue(() =>
                {
                    MainWindow.Instance.SetWwiseProjectSavedState();
                    MainWindow.Instance.SetChartUpdatedState();
                });

                // Optionnel : recréer le client pour une prochaine reconnexion
                client = new AK.Wwise.Waapi.JsonClient();
            };

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
            client = new AK.Wwise.Waapi.JsonClient(); // reset pour reconnecter plus tard
            ConnectedToWwise = false;
            return false;
        }
    }

    public static async Task Disconnect()
    {
        if (client.IsConnected())
        {
            await client.Close();
            ConnectedToWwise = false;
        }
        else
        {
            Log.Info("The connection is already closed.");
        }
    }

    private static readonly SemaphoreSlim _waapiLock = new(1, 1);

    public static async Task<JObject> GenericClientCall(string uri, JObject? args = null, JObject? options = null)
    {
        await _waapiLock.WaitAsync();
        try
        {
            return await client.Call(uri, args ?? new JObject(), options ?? new JObject());
        }
        finally
        {
            _waapiLock.Release();
        }
    }

    public static async Task WatchForWwiseProjectDirtyStateChanges()
    {
        bool lastDirtyState = false;

        while (ConnectedToWwise)
        {
            try
            {
                Log.Info("Checking wwise project dirty state...");
                var result = await GenericClientCall("ak.wwise.core.getProjectInfo");
                var isDirty = result["isDirty"]?.Value<bool>();

                if (isDirty != lastDirtyState)
                {
                    lastDirtyState = isDirty ?? true;
                    Log.Info("Dirty state changed: " + isDirty);

                    WwiseCache.isProjectDirty = isDirty ?? true;
                    WwiseCache.hasProjectChangeSinceLastAnalyze = true;
                }

                await Task.Delay(500);
            }
            catch (WebSocketException wse)
            {
                Log.Error(wse);
                break; // ou tenter une reconnexion ici
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                break;
                throw;
            }
        }
    }

    public static async Task GetProjectInfos()
    {
        try
        {
            var projectInfo = await GenericClientCall(ak.wwise.core.getProjectInfo, null, null);

            // Get the project name
            var displayTitle = projectInfo["displayTitle"]?.ToString() ?? "";

            var match = System.Text.RegularExpressions.Regex.Match(displayTitle, @"Wwise (\d+\.\d+\.\d+)");
            if (match.Success)
            {
                WwiseCache.wwiseVersion = match.Groups[1].Value;
            }

            // Get the full path of the project (.wproj file)
            var projectFullPath = projectInfo["path"]?.ToString() ?? "";

            // Get the parent folder of the project (the audio project folder)
            var projectFolder = "";
            if (!string.IsNullOrEmpty(projectFullPath))
            {
                projectFolder = System.IO.Path.GetDirectoryName(projectFullPath) ?? "";
            }

            // Determine the root folder to use depending on the version
            var audioObjFolderName = WwiseCache.wwiseVersion.StartsWith("2025") ? "Containers" : "Actor-Mixer Hierarchy";
            var busFolderName = WwiseCache.wwiseVersion.StartsWith("2025") ? "Busses" : "Master-Mixer Hierarchy";

            // Build the full path of the Audio object folder
            var audioObjWWUFolderPath = System.IO.Path.Combine(projectFolder, audioObjFolderName);
            // Build the full path of the bus folder
            var busWWUFolderPath = System.IO.Path.Combine(projectFolder, busFolderName);

            var eventsWWUFolderPath = System.IO.Path.Combine(projectFolder, "Events");

            Log.Info($"Wwise version: {WwiseCache.wwiseVersion}");
            Log.Info($"Project folder path: {projectFolder}");
            Log.Info($"Events folder path: {eventsWWUFolderPath}");
            Log.Info($"Audio object folder path: {audioObjWWUFolderPath}");
            Log.Info($"Bus folder path: {busWWUFolderPath}");

            WWUParser.SetProjectFolderPaths(eventsWWUFolderPath, audioObjWWUFolderPath, busWWUFolderPath);

            WriteDataToUI(WwiseCache.wwiseVersion, displayTitle);
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
            Log.AddSpace();
        });

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
            var result = await WaapiBridge.GenericClientCall("ak.wwise.core.object.get", query, options);

            var hdrMap = result["return"]?
                .ToDictionary(
                    bus => bus["path"]?.ToString() ?? "",
                    bus => bus["HdrEnable"]?.Value<bool>() ?? false
                ) ?? new Dictionary<string, bool>();

            foreach (var bus in result["return"]!)
            {
                var path = bus["path"]?.ToString() ?? "";
                var isHDR = bus["HdrEnable"]?.Value<bool>() ?? false;

                var isHDRChild = false;
                var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                for (var i = parts.Length - 1; i > 0; i--)
                {
                    var parentPath = "\\" + string.Join("\\", parts.Take(i));
                    if (hdrMap.TryGetValue(parentPath, out var parentIsHDR) && parentIsHDR)
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
            var result = await WaapiBridge.GenericClientCall("ak.wwise.core.object.get", query, options);

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
        var idsToFetch = targetIds.Where(id => !WwiseCache.outputBusCache.ContainsKey(id)).Distinct().ToList();
        if (idsToFetch.Count == 0)
        {
            Log.Warning("No output bus foud in outputBusCache!");
            return;
        }

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

            var result = await WaapiBridge.GenericClientCall("ak.wwise.core.object.get", query, options);

            foreach (var obj in result["return"]!)
            {
                var id = obj["id"]?.ToString();
                var busId = obj["OutputBus"]?["id"]?.ToString();
                if (id != null)
                {
                    WwiseCache.outputBusCache[id] = busId;
                }
            }

            // For any ids not returned by WAAPI, set null to avoid refetching later
            foreach (var id in idsToFetch)
            {
                WwiseCache.outputBusCache.TryAdd(id, WwiseCache.outputBusCache.ContainsKey(id) ? WwiseCache.outputBusCache[id] : null);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"BatchGetTargetOutputBus failed: {ex.ToString()}");
            // Ensure we don't re-request infinitely: mark them as null
            foreach (var id in idsToFetch)
            {
                WwiseCache.outputBusCache.TryAdd(id, null);
            }
        }
    }
    #endregion

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int dwProcessId);

    public static async Task FocusWwiseWindow()
    {
        try
        {
            var info = await GenericClientCall("ak.wwise.core.getInfo", null, null);
            var wwisePid = info["processId"].Value<int>();

            AllowSetForegroundWindow(wwisePid);

            await GenericClientCall("ak.wwise.ui.bringToForeground", null, null);
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
            var args = new JObject
            {
                ["command"] = "Inspect",
                ["objects"] = new JArray(objectId)
            };

            await GenericClientCall("ak.wwise.ui.commands.execute", args, null);
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
            var args = new JObject
            {
                ["command"] = "FindInProjectExplorerSelectionChannel1",
                ["objects"] = new JArray(objectId)
            };
            await GenericClientCall("ak.wwise.ui.commands.execute", args, null);
            Log.Info($"Object {objectId} found in Project Explorer.");
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}
