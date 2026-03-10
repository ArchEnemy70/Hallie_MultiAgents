using ExternalServices;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hallie.ViewModels
{
    public class ParametresPromptsViewModel : INotifyPropertyChanged
    {
        #region Propriétés
        public string IntitulePrompt { get; set; } = "Prompt : Aucun";
        public string Prompt { get; set; } = "";
        public string? SelectItem { get; set; } = "";
        public IEnumerable<string?> ListeItems { get; set; }
        #endregion
        
        private static string _Directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Raccourcis");

        #region Constructeur
        public ParametresPromptsViewModel()
        {
            SelectItem = null;
            if(!System.IO.Directory.Exists(_Directory))
                System.IO.Directory.CreateDirectory(_Directory);
            ListeItems = GetAllPrompts();

        }
        #endregion

        #region Méthodes publiques
        public void SelectPrompt()
        {
            if (SelectItem == null)
                return;

            string selectItem = (string)SelectItem;
            Prompt = GetPromptBrut(selectItem);
            IntitulePrompt = $"Prompt : {SelectItem}";

            OnPropertyChanged(nameof(Prompt));
            OnPropertyChanged(nameof(IntitulePrompt));
        }
        private static string GetPromptBrut(string nom)
        {
            string file = System.IO.Path.Combine(_Directory, $"{nom}.txt");
            var txt = "";
            if (!System.IO.File.Exists(file))
            {
                TxtService.CreateTextFile(file, txt);
            }

            txt = TxtService.ExtractTextFromTxt(file);
            if ((txt.Trim().Length == 0))
            {
                LoggerService.LogWarning($"Contenu vide. Fichier prompt : {file} ");
                TxtService.CreateTextFile(file, txt);
            }
            return txt;
        }
        public static List<string> GetAllPrompts()
        {
            List<string> lst = new();
            var prompts = System.IO.Directory.GetFiles(_Directory);
            foreach (var p in prompts)
            {
                var nom = System.IO.Path.GetFileNameWithoutExtension(p);
                lst.Add(nom.Trim());
            }
            
            return lst;
        }
        public (bool, string) CreatePrompt(string nom)
        {
            SelectItem = nom;
            Prompt = "";
            SavePrompt();
            ListeItems = GetAllPrompts();

            OnPropertyChanged(nameof(SelectItem));
            OnPropertyChanged(nameof(Prompt));
            OnPropertyChanged(nameof(ListeItems));
            return (true, "");
        }
        public (bool, string) SavePrompt()
        {
            if (SelectItem == null)
                return (false, "Aucun prompt sélectionné");

            string selectItem = (string)SelectItem;
            return SavePrompt(selectItem, Prompt);
        }
        private static (bool, string) SavePrompt(string nom, string prompt)
        {
            try
            {
                string file = System.IO.Path.Combine(_Directory, $"{nom}.txt");
                TxtService.CreateTextFile(file, prompt);
            }

            catch (Exception ex)
            {
                LoggerService.LogError($"PromptService.SavePrompt : {ex.Message}");
                return (false, ex.Message);
            }
            return (true, "");
        }
        #endregion

        #region INotifyPropertyChanged
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is null)
                return;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion
    }
}
