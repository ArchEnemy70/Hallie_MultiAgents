using Hallie.ViewModels;
using System.Windows;
using System.Windows.Media;

namespace Hallie.Views
{
    /// <summary>
    /// Logique d'interaction pour ParametresPrompts.xaml
    /// </summary>
    public partial class ParametresPrompts : Window
    {
        #region Variables
        private ParametresPromptsViewModel _VM = new();
        #endregion
        #region Constructeur
        public ParametresPrompts()
        {
            InitializeComponent();
            this.DataContext = _VM;
        }
        #endregion

        #region Méthodes privées
        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            ShowMessage("", true);
            _VM.SelectPrompt();
        }

        private void OnFermerClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var (b, m) = _VM.SavePrompt();
            if (!b)
            {
                ShowMessage($"Erreur lors de la sauvegarde du prompt : {m}", false);              
            }
            else
            {
                ShowMessage("Prompt sauvegardé avec succès", true);
            }
        }

        private void ShowMessage(string msg, bool isSuccess)
        {
            StatusText.Text = msg;
            StatusText.Foreground = isSuccess ? Brushes.Green : Brushes.Red;
        }
        #endregion

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var p = _VM.Prompt;
            Clipboard.SetText(p);
            StatusText.Text = "Le prompt est copié dans le presse-papier. Vous pouvez le coller dans la zone de texte (CTRL+V)";
        }

        private void OnCreateClick(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Nom du prompt");
            if (dialog.ShowDialog() == true)
            {
                string value = dialog.Valeur;
                _VM.CreatePrompt(value);
            }
        }
    }
}
