namespace ExternalServices
{
    public static class FichiersInternesService
    {
        public static string DossierLogs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        public static string ListeMails { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ListesItems", "listeMails.json");
        public static string EmailsSensibiliteTon { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Parametres", "EmailsSensibiliteTon.txt");
        public static string EmailsSensibilitePosture { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Parametres", "EmailsSensibilitePosture.txt");
        public static string EmailsSensibiliteCouleur { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Parametres", "EmailsSensibiliteCouleur.txt");
        public static string EmailsSensibiliteNiveauDetail { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Parametres", "EmailsSensibiliteNiveauDetail.txt");

    }
}
