using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;


namespace WwiseHDRTool;

public static class Log
{
    // Active/désactive tous les logs
    public static bool Enabled { get; set; } = true;

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