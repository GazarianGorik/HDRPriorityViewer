using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSharpMarkup.WinUI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace HDRPriorityViewer;
internal class UpdateManager
{
    public static async Task CheckForUpdateAsync()
    {
        var release = await GetLatestReleaseAsync(true);

        var latestVersion = new Version(release.tag_name.TrimStart('v'));

        if (latestVersion == null)
        {
            Log.Error("Latest version: null");
            return;
        }

        var zipAsset = release.assets.FirstOrDefault(a => a.name.EndsWith(".zip"));

        if (zipAsset == null)
        {
            Log.Error("zipAsset: null");
            return;
        }

        string prereleaseText = release.prerelease ? " [Pre-release]" : "";
        var changelog = GetChangelogSections(release.body);

        var localVersion = AppUtility.GetAppVersion();


        Log.Info($"Current version {localVersion} / latest version {latestVersion}");

        if (latestVersion > localVersion)
        {
            Log.Info("Maj found!");

            var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock { TextWrapping = TextWrapping.Wrap};

            textBlock.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = $"Latest release {latestVersion}{prereleaseText} is available, do you want to update now?\n" });
            /*
            textBlock.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                FontSize = 17,
                Text = $"Changelog 💫\n"
            });*/


            foreach (var kv in changelog)
            {
                textBlock.Inlines.Add("\n");
                textBlock.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                {
                    FontWeight = FontWeights.Bold,
                    Text = $"{kv.Key}"
                });

                foreach (var item in kv.Value)
                {
                    textBlock.Inlines.Add("\n");
                    textBlock.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                    {
                        Text = $" • {item}"
                    });
                }
            }

            //string dialogText = $"Latest release {latestVersion} is available, do you want to update now?\n\n{changelog}";

            ContentDialogResult? result = null;

            await MainWindow.Instance.DispatcherQueue.EnqueueAsync(async () =>
            {
                result = await
                MainWindow.Instance.EnqueueDialogAsync(
                    "New update available! 💫",
                    null,
                    false,
                    null,
                    "Cancel",
                    "Update",
                    null,
                    (Microsoft.UI.Xaml.Style)Application.Current.Resources["RiskyButton"],
                    (Microsoft.UI.Xaml.Style)Application.Current.Resources["ValidateButton"],
                    textBlock
                );
            });

            if (result == ContentDialogResult.Secondary)
            {
                await UpdateFromZipAsync(zipAsset.browser_download_url);
            }
        }
    }

    static async Task<GitHubRelease?> GetLatestReleaseAsync(bool includePrerelease)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HDRPriorityViewer");

        var json = await client.GetStringAsync("https://api.github.com/repos/gazariangorik/HDRPriorityViewer/releases");
        var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json);

        var latest = releases
            .Where(r => !r.draft)
            .Where(r => includePrerelease || !r.prerelease)
            .FirstOrDefault();

        if (latest != null && latest.assets != null && latest.assets.Count > 0)
        {
            // par exemple, prendre le premier asset
            var downloadUrl = latest.assets[0].browser_download_url;
            Console.WriteLine("Download URL: " + downloadUrl);
        }

        return latest;
    }

    static async Task UpdateFromZipAsync(string downloadUrl)
    {
        var tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HDRPriorityViewer.zip");
        var tempExtract = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HDRPriorityViewer_update");

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            MainWindow.Instance.OpenLoadingDialog("Donwloading latest update from", $"{downloadUrl}");
        });

        // Create a temporary folder for extraction
        Directory.CreateDirectory(tempExtract);

        // Download the zip
        using var client = new HttpClient();
        var data = await client.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(tempZip, data); // <- write complete file

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            await MainWindow.Instance.CloseLoadingDialog();
        });

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            MainWindow.Instance.OpenLoadingDialog("Extracting file...", "HDRPriorityViewer.exe");
        });

        // Extract the zip
        ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            await MainWindow.Instance.CloseLoadingDialog();
        });

        // Path to the new exe inside the zip
        var newExePath = System.IO.Path.Combine(tempExtract, "HDRPriorityViewer.exe");

        // Create batch file with visible CMD and step messages
        var batchFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HDRPriorityViewerUpdate.bat");
        File.WriteAllText(batchFile, $@"
                                    @echo off
                                    echo =======================================
                                    echo       Updating HDRPriorityViewer...
                                    echo =======================================

                                    echo Step 1: Replacing old executable...
                                    timeout /t 1 /nobreak > nul
                                    move /Y ""{newExePath}"" ""{Process.GetCurrentProcess().MainModule.FileName}""

                                    echo Step 2: Deleting temporary zip file...
                                    del /f /q ""{tempZip}""

                                    echo Step 3: Deleting extracted folder...
                                    rmdir /s /q ""{tempExtract}""

                                    echo Step 5: Restarting application...
                                    start """" ""{Process.GetCurrentProcess().MainModule.FileName}""

                                    echo Update complete.

                                    :: Countdown before auto-destruction
                                    echo Step 4: Auto-destruction...
                                    for /L %%i in (2,-1,1) do (
                                        echo %%i...
                                        timeout /t 1 /nobreak > nul
                                    )
                                    del /f /q ""%~f0""
                                    pause
                                    ");


        // Inform user the update is ready
        await MainWindow.Instance.EnqueueDialogAsync("Update ready!",
            $"To finish the update, the app will now restart automatically.",
            false,
            null,
            "Ok");

        // Launch batch (visible) and exit the app
        Process.Start(new ProcessStartInfo(batchFile)
        {
            WindowStyle = ProcessWindowStyle.Normal, // show CMD window
            UseShellExecute = true
        });

        Environment.Exit(0);
    }



    static Dictionary<string, List<string>> GetChangelogSections(string releaseBody)
    {
        var result = new Dictionary<string, List<string>>();

        if (string.IsNullOrEmpty(releaseBody))
        {
            return result;
        }

        // Cut footer
        var footerMarker = "\r\n\r\n##\r\n\r\n🚀";
        var index = releaseBody.IndexOf(footerMarker, StringComparison.Ordinal);
        if (index >= 0)
        {
            releaseBody = releaseBody.Substring(0, index).Trim();
        }

        // Regex to capture sections like "#### Changed" and their list items
        var sectionRegex = new Regex(@"####\s+(\w+)\s*(.*?)((?=####)|\z)", RegexOptions.Singleline);

        foreach (Match match in sectionRegex.Matches(releaseBody))
        {
            var sectionName = match.Groups[1].Value; // Changed / Added / Removed
            var sectionContent = match.Groups[2].Value.Trim();

            // Extract list items (- something)
            var items = Regex.Matches(sectionContent, @"- (.+)")
                             .Cast<Match>()
                             .Select(m => StripMarkdown(m.Groups[1].Value.Trim()))
                             .ToList();

            if (items.Count > 0)
            {
                result[sectionName] = items;
            }
        }

        return result;
    }

    static string StripMarkdown(string md)
    {
        // Remove headers and markdown symbols
        //md = md.Replace("Fixed", "  Fixed:").Replace("Added", "  Added:").Replace("-", "\t•").Replace("Changelog 💫", "");

        return Regex.Replace(md, @"[#*\\[\]`]", "").Trim();
    }
}

public class GitHubRelease
{
    public string tag_name
    {
        get; set;
    }
    public bool prerelease
    {
        get; set;
    }
    public bool draft
    {
        get; set;
    }
    public string body
    {
        get; set;
    }
    public List<GitHubAsset> assets
    {
        get; set;
    }
}
public class GitHubAsset
{
    public string name
    {
        get; set;
    }
    public string browser_download_url
    {
        get; set;
    }
}