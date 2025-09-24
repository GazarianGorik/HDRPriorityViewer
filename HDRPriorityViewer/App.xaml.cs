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
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;

namespace HDRPriorityViewer
{
    public partial class App : Application
    {
        public Window MainWindow => m_window!;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private Window? m_window;

        public App()
        {
            EnsureSingleInstance();

            RegisterGlobalExceptionHandlers();

            this.InitializeComponent();
        }

        /// <summary>
        /// Prevents multiple instances of the app from running.
        /// Redirects activation to the first instance if another is launched.
        /// </summary>
        private void EnsureSingleInstance()
        {
            string key = "HDRPriorityViewer_SingleInstance";
            var mainInstance = AppInstance.FindOrRegisterForKey(key);

            if (!mainInstance.IsCurrent)
            {
                // Notify user
                MessageBox(IntPtr.Zero,
                    "HDRPriorityViewer is already running.\nOnly one instance can run at a time.",
                    "HDRPriorityViewer",
                    0x30); // MB_ICONWARNING

                // Kill this process
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Hooks global exception handlers (UI, task, and domain).
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            this.UnhandledException += App_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                if (m_window == null)
                {
                    m_window = new MainWindow();

                    // Handle window closed
                    m_window.Closed += OnMainWindowClosed;
                }

                m_window.Activate(); // bring to foreground if already running
            }
            catch (Exception ex)
            {
                MessageBox(IntPtr.Zero, ex.ToString(), "OnLaunch() crash!", 0);
            }
        }

        private void OnMainWindowClosed(object sender, WindowEventArgs e)
        {
            Environment.Exit(0); // Force the process to terminate
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception);
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}