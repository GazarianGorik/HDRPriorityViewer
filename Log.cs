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
using System.Diagnostics;

namespace WwiseHDRTool;

public static class Log
{
    // Active/désactive tous les logs
    public static bool Enabled { get; set; } = false;

    public static void AddSpace()
    {
        if (Enabled) Debug.WriteLine(string.Empty);
    }

    public static void Separator()
    {
        if (Enabled) Debug.WriteLine(new string('-', 50));
    }

    public static void TempOverrided(string message)
    {
        Debug.WriteLine("[TEMP] " + message);
    }

    public static void Info(string message)
    {
        if (Enabled) Debug.WriteLine("[Info] " + message);
    }

    public static void Warning(string message)
    {
        if (Enabled) Debug.WriteLine("[Warning] " + message);
    }

    public static void Error(string message)
    {
        if (Enabled) Debug.WriteLine("[Error] " + message);

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            MainWindow.Instance.EnqueueMessage("Error", $"{message}");
        });
    }

    public static void Error(Exception ex)
    {
        if (Enabled) Debug.WriteLine($"[Error] {ex.Message}\n{ex.ToString()}");

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            MainWindow.Instance.EnqueueMessage("Error", $"{ex.Message}\n{ex.ToString()}");
        });
    }
}