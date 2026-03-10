using Hallie.ViewModels;
using HallieDomain;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hallie.Views
{
    /// <summary>
    /// Logique d'interaction pour ConversationsHistorique.xaml
    /// </summary>
    public partial class ConversationsHistorique : Window
    {
        public ChatConversation? SelectedConversation { get; set; }
        private bool _shouldScroll = true;
        private ConversationsHistoriqueViewModel _VM;
        public ConversationsHistorique()
        {
            InitializeComponent();
            _VM = new ConversationsHistoriqueViewModel();
            DataContext = _VM;
        }


        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Si l'utilisateur scroll manuellement vers le haut, ne pas forcer le scroll
            _shouldScroll = e.ExtentHeight - e.ViewportHeight - e.VerticalOffset < 20;
        }

        private void ScrollToBottom()
        {
            if (_shouldScroll)
            {
                ChatScrollViewer.ScrollToEnd();
            }
        }

        private void OnMessageAdded()
        {
            ScrollToBottom();
        }

        private void OnConversationSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_VM.SelectedConversation != null)
                _VM.LoadConversation(_VM.SelectedConversation);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'envoi du message : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        private void BtnSelectConv_Click(object sender, RoutedEventArgs e)
        {
            SelectedConversation = _VM.SelectConversation();
            this.Close();
        }

        private void BtnDeleteConv_Click(object sender, RoutedEventArgs e)
        {
            _VM.DeleteConversation();
        }

        private void BtnSelectDocumentsClick(object sender, RoutedEventArgs e)
        {
            string filter = "Fichiers supportés (*.png;*.jpg;*.jpeg;*.bmp;*.pdf;*.docx;*.xlsx;*.txt)|*.png;*.jpg;*.jpeg;*.bmp;*.pdf;*.docx;*.xlsx;*.txt";



            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,

                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {

                }
            }
        }

        private void BtnDeleteDoc_Click(object sender, RoutedEventArgs e)
        {

        }

        // Pour envoyer un message avec le clavier : CTRL + ENTREE
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true; // empêche l'ajout de la nouvelle ligne
            }
        }

        private void CopierMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is System.Windows.Controls.TextBlock textBlock)
            {
                Clipboard.SetText(textBlock.Text);
            }
        }

        private void BtnEditConv_Click(object sender, RoutedEventArgs e)
        {
            var conv = _VM.SelectConversation();
            if (conv == null)
                return;

            var id = conv.Id;
            string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Conversations");
            string file = System.IO.Path.Combine(folder, $"{id}.json");
            Process.Start(new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = true
            });


        }
    }
}
