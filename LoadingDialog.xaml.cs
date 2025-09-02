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

using Microsoft.UI.Xaml.Controls;

namespace HDRPriorityGraph.Views
{
    public sealed partial class LoadingDialog : ContentDialog
    {
        public LoadingDialog()
        {
            this.InitializeComponent();

            DetailsMessage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        public void SetLoadingText(string text)
        {
            MainWindow.MainDispatcherQueue.TryEnqueue(() =>
            {
                LoadingMessage.Text = text;
            });
        }

        public void SetDetailsText(string text)
        {
            MainWindow.MainDispatcherQueue.TryEnqueue(() =>
            {
                DetailsMessage.Text = text;

                if (string.IsNullOrEmpty(text))
                {
                    DetailsMessage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                }
                else
                {
                    DetailsMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
            });
        }
    }
}
