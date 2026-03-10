using HallieCore.Services;
using System;
using System.Windows;

namespace Hallie.Views
{
    public sealed class ApprovalWindowManager
    {
        private readonly IApprovalService _approvalService;

        public ApprovalWindowManager(IApprovalService approvalService)
        {
            try {
                _approvalService = approvalService;

                _approvalService.OnNewRequest += req =>
                {
                    // Toujours sur le thread UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var win = new Approvals(req, _approvalService)
                        {
                            Owner = Application.Current.MainWindow
                        };
                        win.ShowDialog(); // ou Show()
                        win.Activate();
                    });
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ApprovalWindowManager crash");
                throw;
            }
        }
    }
}
