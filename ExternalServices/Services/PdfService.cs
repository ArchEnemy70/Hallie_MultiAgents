using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace ExternalServices
{
    public static class PdfService
    {
        #region Méthodes publiques

        public static string ExtractPdfFromBytesiText(byte[] bytes)
        {
            // iText travaille directement sur un Stream
            using var ms = new MemoryStream(bytes, writable: false);
            using var pdfReader = new PdfReader(ms);
            using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader);

            var sb = new StringBuilder();

            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var page = pdfDoc.GetPage(i);
                var strategy = new SimpleTextExtractionStrategy(); // ou LocationTextExtractionStrategy pour garder les espaces
                var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                sb.AppendLine(pageText);
            }

            return sb.ToString();
        }

        public static string ExtractPdfFromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var document = UglyToad.PdfPig.PdfDocument.Open(ms);

            var builder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
            }

            return builder.ToString();
        }

        public static string ExtractPdfFromBytesNew(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var document = UglyToad.PdfPig.PdfDocument.Open(ms);

            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                // 1. Récupérer tous les mots de la page
                var words = page.GetWords();

                // 2. Regrouper les mots par lignes (basé sur la coordonnée Y)
                // On utilise une tolérance de 3-4 points pour gérer les légers décalages
                var lines = GroupWordsIntoLines(words);

                // 3. Construire le texte ligne par ligne
                foreach (var line in lines)
                {
                    sb.AppendLine(line);
                }

                // Séparateur de page pour aider le LLM
                sb.AppendLine("\n--- PAGE BREAK ---\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Regroupe les mots d'une page en lignes et insère un séparateur fort (|||) entre les colonnes.
        /// </summary>
        private static List<string> GroupWordsIntoLines(IEnumerable<Word> words)
        {
            // 1. Tri initial : du haut vers le bas (Bottom décroissant), puis de gauche à droite (Left croissant)
            // Cela garantit que nous traitons les lignes dans le bon ordre.
            var sortedWords = words
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var outputLines = new List<string>();
            if (!sortedWords.Any()) return outputLines;

            List<Word> currentLine = new List<Word> { sortedWords[0] };

            // Tolérance verticale pour considérer deux mots comme appartenant à la même ligne
            double yTolerance = 5.0;

            // Seuil de détection de colonne (en points PDF). 
            // Si l'espace entre deux mots est > 40 points, c'est probablement un saut de colonne.
            double columnGapThreshold = 40.0;

            // Itérer sur les mots pour former les lignes
            for (int i = 1; i < sortedWords.Count; i++)
            {
                var word = sortedWords[i];
                var lastWordInLine = currentLine.Last();

                // Vérifier la différence verticale (Y)
                double verticalDiff = Math.Abs(word.BoundingBox.Bottom - lastWordInLine.BoundingBox.Bottom);

                if (verticalDiff < yTolerance)
                {
                    // Mots sur la même ligne
                    currentLine.Add(word);
                }
                else
                {
                    // Nouvelle ligne détectée - on traite la ligne précédente
                    outputLines.Add(ProcessLineForColumns(currentLine, columnGapThreshold));

                    // Démarrer la nouvelle ligne
                    currentLine = new List<Word> { word };
                }
            }

            // la dernière ligne
            if (currentLine.Any())
            {
                outputLines.Add(ProcessLineForColumns(currentLine, columnGapThreshold));
            }

            return outputLines;
        }

        /// <summary>
        /// Reconstruit une ligne de mots en insérant des séparateurs basés sur les écarts horizontaux (X).
        /// </summary>
        private static string ProcessLineForColumns(List<Word> lineWords, double columnGapThreshold)
        {
            // Tri horizontal strict des mots de la ligne (gauche à droite)
            lineWords.Sort((w1, w2) => w1.BoundingBox.Left.CompareTo(w2.BoundingBox.Left));

            var lineOutput = new StringBuilder();
            double lastWordRight = 0;

            foreach (var word in lineWords)
            {
                if (lineOutput.Length > 0)
                {
                    double currentWordLeft = word.BoundingBox.Left;

                    // Calcul de l'écart horizontal entre la fin du mot précédent et le début du mot actuel
                    double gap = currentWordLeft - lastWordRight;

                    if (gap > columnGapThreshold)
                    {
                        // Écart important : Insérer le séparateur de colonne fort pour Ollama
                        lineOutput.Append(" ||| ");
                    }
                    else if (gap > 5.0)
                    {
                        // Petit écart (plus que de l'espacement normal, mais pas une colonne) : Ajouter un espace
                        lineOutput.Append(" ");
                    }
                    // Sinon (gap <= 5.0), les mots sont très proches, on les colle ou on ajoute juste un espace simple si nécessaire.
                }

                lineOutput.Append(word.Text);
                lastWordRight = word.BoundingBox.Right;
            }

            return lineOutput.ToString();
        }

        /// <summary>
        /// Algorithme pour regrouper les mots qui sont visuellement sur la même ligne.
        /// </summary>
        private static List<List<Word>> GroupWordsIntoLinesOld(IEnumerable<Word> words)
        {
            // On trie tous les mots du haut vers le bas (Y descendant en PDF), puis gauche à droite
            var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();

            var lines = new List<List<Word>>();
            if (!sortedWords.Any()) return lines;

            var currentLine = new List<Word> { sortedWords[0] };
            lines.Add(currentLine);

            // Tolérance verticale (en points PDF). Si deux mots ont moins de 3 pts d'écart vertical, c'est la même ligne.
            double yTolerance = 5.0;

            for (int i = 1; i < sortedWords.Count; i++)
            {
                var word = sortedWords[i];
                var lastWordInLine = currentLine.Last();

                // Comparer la position verticale (Bottom) avec le dernier mot ajouté
                double verticalDiff = Math.Abs(word.BoundingBox.Bottom - lastWordInLine.BoundingBox.Bottom);

                if (verticalDiff < yTolerance)
                {
                    // C'est la même ligne
                    currentLine.Add(word);
                }
                else
                {
                    // Nouvelle ligne détectée
                    // On retrie la ligne précédente de gauche à droite pour être sûr (l'axe X)
                    currentLine.Sort((w1, w2) => w1.BoundingBox.Left.CompareTo(w2.BoundingBox.Left));

                    currentLine = new List<Word> { word };
                    lines.Add(currentLine);
                }
            }

            return lines;
        }

        public static string ExtractTextFromPdf(string fileFullname)
        {
            LoggerService.LogInfo($"PdfService.ExtractTextFromPdf : {fileFullname}");
            var text = new StringBuilder();
            using (var document = UglyToad.PdfPig.PdfDocument.Open(fileFullname))
            {
                foreach (var page in document.GetPages())
                {
                    string texte = NettoyerTexte(page.Text);
                    text.AppendLine(texte);

                    foreach (var letter in page.GetWords())
                    {
                        text.Append(letter.Text);
                    }
                }
            }
            return text.ToString();
        }

        public static string NettoyerTexte(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // 1. Supprimer les caractères de contrôle ASCII < 32 (sauf retour à la ligne)
            string cleaned = new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray());

            // 2. Normaliser les espaces
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            // 3. Supprimer les coupures de mots (tiret suivi d’espace ou retour ligne)
            cleaned = Regex.Replace(cleaned, @"-\s+", "");

            // 4. Normaliser Unicode pour remettre les accents
            cleaned = cleaned.Normalize(NormalizationForm.FormC);

            // 5. Supprimer les caractères non imprimables restants
            cleaned = new string(cleaned.Where(c => c >= 32).ToArray());

            return cleaned.Trim();
        }

        #endregion
    }
}
