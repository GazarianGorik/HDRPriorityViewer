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
using System.Diagnostics;

namespace HDRPriorityViewer;

public static class Log
{
#if DEBUG
    public static bool Enabled { get; set; } = true;
#else
    public static bool Enabled { get; set; } = false;
#endif

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
    public static void TempOverridedPopup(string message)
    {
        Debug.WriteLine("[TEMP] " + message);

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            await MainWindow.Instance.EnqueueDialogAsync("[Debug Popup] (you should not see this message!)", $"{message}", true, "Ok");
        });
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
            await MainWindow.Instance.EnqueueDialogAsync("Error", $"{message}", true, "Ok");
        });
    }

    public static void Error(Exception ex)
    {
        if (Enabled) Debug.WriteLine($"[Error] {ex.Message}\n{ex.ToString()}");

        MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
        {
            await MainWindow.Instance.EnqueueDialogAsync("Error", $"{ex.Message}\n{ex.ToString()}", true, "Ok");
        });
    }
}