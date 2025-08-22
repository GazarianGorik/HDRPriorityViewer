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
