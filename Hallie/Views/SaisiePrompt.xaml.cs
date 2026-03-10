using Hallie.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Hallie.Views
{
    /// <summary>
    /// Logique d'interaction pour SaisiePrompt.xaml
    /// </summary>
    public partial class SaisiePrompt : Window
    {
        private readonly AvatarViewModel _viewModel;
        public SaisiePrompt(AvatarViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true; // empêche la TextBox d'ajouter un retour à la ligne

                // Option A: laisse le KeyBinding exécuter la commande ? -> NON, car e.Handled bloque souvent la suite.
                // Donc on exécute la commande nous-mêmes ici :

                if (_viewModel.LireTextSaisiCommand?.CanExecute(null) == true)
                {
                    _viewModel.LireTextSaisiCommand.Execute(null);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
