using Hallie.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HallieDomain;

namespace Hallie.ViewModels
{
    public class ConversationsHistoriqueViewModel : INotifyPropertyChanged
    {
        private string TypeConv = "LLM";
        public ObservableCollection<ChatConversation> Conversations { get; set; } = new();
        public ObservableCollection<ConversationMessage> CurrentMessages { get; set; } = new();

        private ChatConversation? _SelectedConversation;
        public ChatConversation? SelectedConversation
        {
            get
            {
                return _SelectedConversation;
            }
            set
            {
                _SelectedConversation = value;
            }
        }

        public ConversationsHistoriqueViewModel()
        {
            LoadConversations();
        }

        public void LoadConversation(ChatConversation conv)
        {
            if (conv == null)
                return;
            SelectedConversation = conv;
            CurrentMessages.Clear();
            foreach (var m in conv.Messages)
                CurrentMessages.Add(m);
            OnPropertyChanged(nameof(CanChangeModel));
        }

        public void LoadConversations()
        {
            var all = ConversationsService.LoadAll(TypeConv).Where(e=>e.Id != null && e.Id != "").ToList();
            all = all.OrderByDescending(c => c.LastUse).ToList();
            foreach (var c in all)
            {
                var msgs = c.Messages.Where(m => m.Id != null && m.Id != "").ToList();
                c.Messages = msgs;
                Conversations.Add(c);
            }
            OnPropertyChanged(nameof(Conversations));
        }

        public bool CanChangeModel => (SelectedConversation?.Messages == null || SelectedConversation?.Messages.Count == 0);

        public void NewConversation()
        {
            CurrentMessages.Clear();
            var conv = new ChatConversation();
            //Conversations.Add(conv);
            SelectedConversation = conv;
            OnPropertyChanged(nameof(CanChangeModel));

        }

        public ChatConversation? SelectConversation()
        {
            if(SelectedConversation == null)
                return null;

            return SelectedConversation;
        }

        public void DeleteConversation()
        {
            if (SelectedConversation != null)
            {
                CurrentMessages.Clear();
                ConversationsService.Delete(SelectedConversation.Id);
                Conversations.Remove(SelectedConversation);
                SelectedConversation = Conversations.FirstOrDefault();
                OnPropertyChanged(nameof(CanChangeModel));
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is null)
                return;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion
    }
}
