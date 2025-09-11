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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HDRPriorityViewer.Views
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

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Attendre que le layout soit finalisé
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                var grid = (Grid)this.Content;
                var size = Math.Min(grid.ActualWidth, grid.ActualHeight);
                SquareContainer.Width = size;
                SquareContainer.Height = size * 2 / 3;

                // S'abonner au changement de taille si le dialog est redimensionné
                grid.SizeChanged += (s, args) =>
                {
                    var newSize = Math.Min(grid.ActualWidth, grid.ActualHeight);
                    SquareContainer.Width = newSize;
                    SquareContainer.Height = newSize * 2 / 3;
                };
            });
        }
    }
}
