using HallieCore.Services;
using System.Windows;

namespace Hallie.Views
{
    /// <summary>
    /// Logique d'interaction pour Approvals.xaml
    /// </summary>
    public partial class Approvals : Window
    {
        private readonly ApprovalRequest _req;
        private readonly IApprovalService _approvalService;

        public Approvals(ApprovalRequest req, IApprovalService approvalService)
        {
            InitializeComponent();
            _req = req;
            _approvalService = approvalService;

            TitleText.Text = $"{req.ToolName} ({req.RiskLevel})";
            SummaryText.Text = req.ActionSummary;
            PayloadText.Text = req.PayloadJson;
        }

        private void Approve_Click(object sender, RoutedEventArgs e)
        {
            _approvalService.Resolve(_req.Id, approved: true);
            Close();
        }

        private void Reject_Click(object sender, RoutedEventArgs e)
        {
            _approvalService.Resolve(_req.Id, approved: false);
            Close();
        }
    }
}
