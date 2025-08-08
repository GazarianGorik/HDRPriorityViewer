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

                ProjectDataFetcher.SetProjectFolderPathes(eventsWWUFolderPath, audioObjWWUFolderPath);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Erreur récupération infos projet : " + e.Message);
                throw;
            }
        }
    }
}
