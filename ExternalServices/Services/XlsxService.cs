using ClosedXML.Excel;
using System.Text;

namespace ExternalServices
{
    public static class XlsxService
    {
        #region Méthodes publiques
        public static string ExtractXlsxFromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(ms);

            var builder = new StringBuilder();

            foreach (var ws in workbook.Worksheets)
            {
                builder.AppendLine($"--- Feuille : {ws.Name} ---");

                var range = ws.RangeUsed();
                if (range == null)
                    continue;

                foreach (var row in range.Rows())
                {
                    foreach (var cell in row.Cells())
                    {
                        var value = cell.GetFormattedString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            builder.Append(value).Append(" ");
                        }
                    }
                    builder.AppendLine();
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }



        public static string ExtractTextFromXlsx(string fileFullname)
        {
            LoggerService.LogInfo($"XlsxService.ExtractTextFromXlsx : {fileFullname}");

            using var workbook = new XLWorkbook(fileFullname);
            return string.Join("\n", workbook.Worksheets.SelectMany(ws => ws.CellsUsed().Select(c => c.GetValue<string>())));
        }

        public static string ExtractTextFromCsv(string fileFullname)
        {
            LoggerService.LogInfo($"XlsxService.ExtractTextFromCsv : {fileFullname}");

            // Lire la première ligne du fichier
            var lignes = File.ReadAllLines(fileFullname);
            if (lignes.Length == 0) return string.Empty;

            var premiereLigne = lignes[0];

            // Compter les séparateurs
            int countPointVirgule = premiereLigne.Count(c => c == ';');
            int countVirgule = premiereLigne.Count(c => c == ',');

            // Choisir le séparateur le plus utilisé
            char separateur = countPointVirgule >= countVirgule ? ';' : ',';

            // Parser le fichier
            var lignesFormatees = lignes
                .Select(ligne => ligne.Split(separateur))
                .Select(colonnes => string.Join(" | ", colonnes));

            return string.Join(Environment.NewLine, lignesFormatees);
        }

        #endregion
    }
}
