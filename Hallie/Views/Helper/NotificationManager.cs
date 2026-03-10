using Hallie.Helpers;
using Hallie.Views.Helper;
using System.Windows;

namespace Hallie
{
    public static class NotificationManager
    {
        private static readonly List<NotificationWindow> _openNotifications = new();
        private static readonly List<NotificationLongMessageWindows> _openNotificationsLongMessage = new();
        private const int Margin = 10;

        public static void ShowNotification(string message, string titre="")
        {
            // Version long message
            if(message.Length > 2000)//2000
            {
                var f = new NotificationLongMessageWindows(message, titre);
                f.Closed += (s, e) =>
                {
                    _openNotificationsLongMessage.Remove(f);
                    PositionNotifications();
                };
                _openNotificationsLongMessage.Add(f);
                f.Show();

                return;
            }

            // Version classique
            var toast = new NotificationWindow(message, titre);
            toast.Loaded += (s, e) => PositionNotifications();

            toast.Closed += (s, e) =>
            {
                _openNotifications.Remove(toast);
                PositionNotifications();
            };

            _openNotifications.Add(toast);
            toast.Show();
        }

        public static void HideNotification()
        {
            var i = _openNotifications.Count;
            while (i > 0) 
            {
                _openNotifications[0].Close();
                i = i - 1;
            }

            i = _openNotificationsLongMessage.Count;
            while (i > 0)
            {
                _openNotificationsLongMessage[0].Close();
                i = i - 1;
            }
        }

        private static void PositionNotifications()
        {
            var workArea = SystemParameters.WorkArea;

            double offsetY = 0;

            foreach (var win in _openNotifications)
            {
                // Assurez-vous que le layout est terminé
                win.Dispatcher.Invoke(() =>
                {
                    win.Left = workArea.Right - win.ActualWidth - Margin;
                    win.Top = workArea.Bottom - win.ActualHeight - offsetY - Margin;
                });

                offsetY += win.ActualHeight + Margin;
            }
        }
    }

}
