using ExternalServices;
using Hallie.ViewModels;
using Hallie.Views.Helper;
using HallieCore.Services;
using HallieDomain;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Hallie.Views
{
    #region Converters
    /// <summary>
    /// Convertisseur pour afficher l'icône du micro (🎙 ou 🔇)
    /// </summary>
    public class MicIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? "🎙" : "🔇";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
    #endregion

    /// <summary>
    /// Code-behind minimaliste - Toute la logique est dans le ViewModel
    /// </summary>
    public partial class AvatarFace : Window
    {
        private readonly AvatarViewModel _viewModel;

        public AvatarFace(IApprovalService approval, IApprovalSummaryBuilder approvalSummary)
        {
            InitializeComponent();
            try
            {
                // Créer et assigner le ViewModel
                _viewModel = new AvatarViewModel(approval, approvalSummary);
                DataContext = _viewModel;

                // Configurer le titre de la fenêtre
                var title = Params.AvatarName ?? "Hallie";
                title += $" - {_viewModel.NumVersion}";
    #if DEBUG
                title += " - DEV";
    #endif


                this.Title = title;

                // TEST 
                //Test();

                // Attacher les événements de cycle de vie
                this.Loaded += Window_Loaded;
                this.Closing += Window_Closing;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "AvatarViewModel crash");
                throw;
            }
        }

        /// <summary>
        /// Initialisation au chargement de la fenêtre
        /// Délègue tout au ViewModel
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Erreur lors de l'initialisation : {ex.Message}");
            }
        }

        /// <summary>
        /// Nettoyage à la fermeture de la fenêtre
        /// Délègue tout au ViewModel
        /// </summary>
        private async void Window_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                await _viewModel.CleanupAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors du nettoyage : {ex.Message}");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var f = new SaisiePrompt(_viewModel);
            f.ShowDialog();
        }

        private void Mnu_HistoriqueConv_Click(object sender, RoutedEventArgs e)
        {
            var f = new ConversationsHistorique();
            f.ShowDialog();
            if(f.SelectedConversation != null)
                AvatarViewModel.SelectedConversation = f.SelectedConversation;
        }

        private void Mnu_NewConv_Click(object sender, RoutedEventArgs e)
        {
            AvatarViewModel.SelectedConversation = new();
        }

        private void Mnu_Raccourcis_Click(object sender, RoutedEventArgs e)
        {
            var f = new ParametresPrompts();
            f.ShowDialog();
        }

        private void Mnu_Outils_Click(object sender, RoutedEventArgs e)
        {
            var f = new NotificationLongMessageWindows(_viewModel.ToolsDescription, "Outils");
            f.ShowDialog();

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var f = new FeedBack(false, _viewModel);
            f.ShowDialog();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var f = new FeedBack(true, _viewModel);
            f.ShowDialog();
        }
    }
}