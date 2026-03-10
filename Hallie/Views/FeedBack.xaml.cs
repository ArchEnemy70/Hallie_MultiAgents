using Hallie.ViewModels;
using System.Windows;

namespace Hallie.Views
{
    public partial class FeedBack : Window
    {
        private readonly AvatarViewModel _VM;

        public FeedBack(bool isPositif, AvatarViewModel vm)
        {
            InitializeComponent();
            _VM = vm;
            DataContext = _VM;
            _VM.IsFeedbackPositif = isPositif;
            _VM.PrepareFeedbackEntries();
            var msg = isPositif ? "POSITIF" : "NEGATIF";
            this.Title = $"Feed Back {msg}";
        }

        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            await _VM.SubmitFeedbackAsync(_VM.IsFeedbackPositif);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
