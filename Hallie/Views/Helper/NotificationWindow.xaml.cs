using System.IO;
using System.Text;
using System.Windows;

namespace Hallie.Helpers
{
    /// <summary>
    /// Logique d'interaction pour NotificationWindow.xaml
    /// </summary>
    public partial class NotificationWindow : Window
    {
        public double OffsetY { get; set; } = 0;

        public NotificationWindow(string message, string titre = "")
        {
            InitializeComponent();
            Message.Text = message;
            if (titre.Trim().Length > 0)
            {
                Titre.Text = "🔔 " + titre;
            }

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Positionner en bas à droite
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.ActualWidth - 10;
            this.Top = desktopWorkingArea.Bottom - this.ActualHeight - 10 - OffsetY;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var filePath = "temp.txt";

            if (File.Exists(filePath))
                File.Delete(filePath);
            File.WriteAllText(filePath, Message.Text, new UTF8Encoding(true));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

    }
}
