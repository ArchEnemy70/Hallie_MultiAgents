using HallieCore.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Hallie.ViewModels
{

    public sealed class ApprovalsViewModel
    {
        public ObservableCollection<ApprovalRequest> Pending { get; } = new();

        private readonly IApprovalService _approvalService;
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        // Propriété exposée pour que la vue puisse lier l'élément sélectionné
        public ApprovalRequest? Selected { get; set; }

        public ApprovalsViewModel(IApprovalService approvalService)
        {
            _approvalService = approvalService;
            /*
            _approvalService.OnNewRequest += req =>
            {
                // ⚠️ si tu es hors thread UI, marshal via Dispatcher (voir plus bas)
                Pending.Add(req);
            };
            */
            
            _approvalService.OnNewRequest += req =>
            {
                Application.Current.Dispatcher.Invoke(() => Pending.Add(req));
            };
            
            ApproveCommand = new RelayCommand(
                execute: () => Approve()
            );
            RejectCommand = new RelayCommand(
                execute: () => Reject()
            );
        }

        public void Approve(ApprovalRequest req, string? comment = null)
        {
            Pending.Remove(req);
            _approvalService.Resolve(req.Id, approved: true, comment);
        }

        public void Reject(ApprovalRequest req, string? comment = null)
        {
            Pending.Remove(req);
            _approvalService.Resolve(req.Id, approved: false, comment);
        }

        // Surcharges sans paramètre utilisées par les commandes (utilisent `Selected`)
        public void Approve()
        {
            if (Selected is null) return;
            var req = Selected;
            Selected = null;
            Approve(req);
        }

        public void Reject()
        {
            if (Selected is null) return;
            var req = Selected;
            Selected = null;
            Reject(req);
        }
    }
}
