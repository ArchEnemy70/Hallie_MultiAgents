using System.Windows;

namespace Hallie.Views
{
    /// <summary>
    /// Logique d'interaction pour InputDialog.xaml
    /// </summary>
    public partial class InputDialog : Window
    {
        public string Valeur { get; set; } = "";
        public InputDialog(string titre)
        {
            InitializeComponent();
            this.Titre.Text = $"Entrez la valeur de {titre}";
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Valeur = this.InputTextBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
