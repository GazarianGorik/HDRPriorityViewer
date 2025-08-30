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

using Microsoft.UI.Xaml.Controls;

namespace WwiseHDRTool.Views
{
    public sealed partial class LoadingDialog : ContentDialog
    {
        public LoadingDialog()
        {
            this.InitializeComponent();
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
            });
        }

        /*
        // Si tu veux un bouton Cancel plus tard
        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Ici tu peux annuler l'opération
        }*/
    }
}
