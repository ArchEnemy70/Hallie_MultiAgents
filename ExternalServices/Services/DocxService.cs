using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using ExternalServices;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ExternalServices
{
    public static class DocxService
    {
        #region Méthodes publiques
        public static string ExtractDocxFromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);
            if (doc == null || doc.MainDocumentPart == null || doc.MainDocumentPart.Document == null)
                return string.Empty;

            var body = doc.MainDocumentPart.Document.Body;
            if(body == null)
                return string.Empty;

            return body.InnerText;
        }


        public static string ExtractTextFromDocx(string fileFullname)
        {
            LoggerService.LogInfo($"DocxService.ExtractTextFromDocx : {fileFullname}");

            using var doc = WordprocessingDocument.Open(fileFullname, false);
            if (doc == null || doc.MainDocumentPart == null || doc.MainDocumentPart.Document == null)
                return string.Empty;

            return string.Join(" ", doc.MainDocumentPart.Document.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
        }

        public static string ExtractTextFromRtf(string fileFullname)
        {
            LoggerService.LogInfo($"DocxService.ExtractTextFromRtf : {fileFullname}");

            string rtfContent = File.ReadAllText(fileFullname);

            // Supprimer les balises RTF simples
            rtfContent = Regex.Replace(rtfContent, @"\\[a-z]+\d*", string.Empty);
            rtfContent = Regex.Replace(rtfContent, @"{\\[^}]+}", string.Empty);
            rtfContent = Regex.Replace(rtfContent, @"[{}]", string.Empty);
            rtfContent = Regex.Replace(rtfContent, @"\r\n|\n", " ");



            // Convertir les caractères encodés (\'xx) en Unicode
            rtfContent = Regex.Replace(rtfContent, @"\\'([0-9a-fA-F]{2})", match =>
            {
                int value = Convert.ToInt32(match.Groups[1].Value, 16);
                return Encoding.Default.GetString(new byte[] { (byte)value });
            });

            // Remplacer les sauts de ligne RTF
            rtfContent = rtfContent.Replace(@"\par", "\n").Replace(@"\tab", "\t");

            // Supprimer les balises RTF
            rtfContent = Regex.Replace(rtfContent, @"\\[a-zA-Z]+\d*", string.Empty);

            // Supprimer les groupes de styles inutiles
            rtfContent = Regex.Replace(rtfContent, @"{\\[^}]+}", string.Empty);

            // Supprimer les accolades et le reste des balises
            rtfContent = Regex.Replace(rtfContent, @"[{}]", string.Empty);

            // Nettoyage final
            return rtfContent.Trim();


        }

        public static bool Genere_Docx(string fullFilenameTemplate, string fullFilenameXML, string fullFilenameOutputDocx, string titreCV, int nbrMaxCompetences, bool isCVAnonyme)
        {
            LoggerService.LogInfo($"DocxService.Genere_Docx : {fullFilenameOutputDocx}");

            try
            {
                if (System.IO.File.Exists(fullFilenameOutputDocx))
                {
                    System.IO.File.Delete(fullFilenameOutputDocx);
                }

                return CreateDocx(fullFilenameTemplate, fullFilenameXML, fullFilenameOutputDocx, titreCV, nbrMaxCompetences, isCVAnonyme);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Erreur dans la génération d'un CV au format Word (DocxService.Genere_Docx) : {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Propriétés privées
        private static string Font_Name = "Calibri";
        private static int Font_Size_int = 11;// "22"; // 11pt = 22 demi-points
        private static string Font_Size
        {
            get
            {
                return (Font_Size_int * 2).ToString(); ;
            }
        }
        private static string Font_Size_Minus
        {
            get
            {
                return ((Font_Size_int - 2) * 2).ToString(); ;
            }
        }
        #endregion

        #region Méthodes privées
        private static bool CreateDocx(string templatePath, string xmlPath, string outputPath, string _TitreCV, int _NbrMaxCompetences, bool isCVAnonyme)
        {
            LoggerService.LogInfo($"DocxService.CreateDocx : {templatePath}");

            try
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                File.Copy(templatePath, outputPath, true); // Copie le modèle

                #region Lire le XML
                var cv = XDocument.Load(xmlPath).Root;

                var placeholders = new Dictionary<string, string>();

                #region Identité
                var identite = cv!.Element("Identite");
                if (isCVAnonyme)
                {
                    var trigramme = "AAA";
                    if (identite!.Element("Prenom")?.Value.Length > 1)
                    {
                        trigramme = identite!.Element("Prenom")?.Value.Substring(0, 1).ToUpper();
                    }
                    if (identite!.Element("Nom")?.Value.Length > 2)
                    {
                        trigramme += identite!.Element("Nom")?.Value.Substring(0, 2).ToUpper();
                    }

                    placeholders["{{Nom}}"] = trigramme!;
                    placeholders["{{Prenom}}"] = "";
                }
                else
                {
                    placeholders["{{Nom}}"] = identite!.Element("Nom")?.Value ?? "";
                    placeholders["{{Prenom}}"] = identite!.Element("Prenom")?.Value ?? "";
                }
                placeholders["{{Email}}"] = identite!.Element("Email")?.Value ?? "";
                placeholders["{{Telephone}}"] = identite!.Element("Telephone")?.Value ?? "";
                #endregion

                #region profil
                // Profil : première phrase en gras, le reste plus petit sur lignes séparées
                string profil = cv.Element("Profil")?.Value ?? "";
                string profilAccroche = "";
                string profilDetails = "";

                var phrases = profil.Split('.').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                if (phrases.Count > 0)
                {
                    if (_TitreCV != "")
                    {
                        profilAccroche = _TitreCV;
                        profilDetails = string.Join("\n", phrases.Select(p => p));
                    }
                    else
                    {
                        profilAccroche = phrases[0];
                        profilDetails = string.Join("\n", phrases.Skip(1).Select(p => p));
                    }

                }
                #endregion

                #region Langues
                var langues = cv.Element("Langues")?.Elements("Langue") ?? Enumerable.Empty<XElement>();
                var languesTextList = langues.Select(l => $"{l.Attribute("nom")?.Value} ({l.Attribute("niveau")?.Value})").ToList();
                string languesText = string.Join(", ", languesTextList);
                #endregion

                #region Formations
                var formations = cv.Element("Formations")?.Elements("Formation") ?? Enumerable.Empty<XElement>();
                var formationsTextList = formations.Select(f => $"{f.Element("Diplome")?.Value} à {f.Element("Etablissement")?.Value} ({f.Element("DateFin")?.Value})").ToList();
                string formationsText = string.Join("\n", formationsTextList);
                #endregion

                #region Certifications
                var certifications = cv.Element("Certifications")?.Elements("Certification") ?? Enumerable.Empty<XElement>();
                var certificationsTextList = formations.Select(f => $"{f.Element("Nom")?.Value} à {f.Element("Organisme")?.Value} ({f.Element("Date")?.Value})").ToList();
                //var certificationsTextList = formations.Select(f => $"{f.Element("Nom")?.Value}").ToList();
                string certificationsText = string.Join("\n", certificationsTextList);
                #endregion

                #region Compétences : x premières
                var competences = cv.Element("Competences")?
                    .Elements("Competence")
                    .Select(c => c.Attribute("nom")?.Value)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Take(_NbrMaxCompetences)
                    .ToList() ?? new List<string>()!;
                string competencesText = string.Join(", ", competences);
                #endregion

                #region Expériences : chaque bloc avec première ligne en gras
                var experiences = cv.Element("Experiences")?.Elements("Experience") ?? Enumerable.Empty<XElement>();
                var xpParagraphs = new List<Paragraph>();
                foreach (var xp in experiences)
                {
                    string datefin = xp.Element("DateFin")?.Value! != "" ? xp.Element("DateFin")?.Value! : "Aujourd’hui";
                    string titre = $"{xp.Element("Poste")?.Value} chez {xp.Element("Entreprise")?.Value} à {xp.Element("Lieu")?.Value}";
                    string dates = $"{xp.Element("DateDebut")?.Value} à {datefin}";
                    string desc = xp.Element("Description")?.Value ?? "";

                    var paraTitre = new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(titre)));
                    paraTitre.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Run>()!.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                        new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = Font_Size },
                        new DocumentFormat.OpenXml.Wordprocessing.Bold()
                        );

                    var runDates = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(dates));
                    runDates.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                        new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = Font_Size },
                        new DocumentFormat.OpenXml.Wordprocessing.Italic()
                    );
                    var paraDates = new Paragraph(runDates);

                    var runDesc = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(desc));
                    runDesc.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                        new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = Font_Size_Minus }
                    );
                    var paraDesc = new Paragraph(runDesc);

                    xpParagraphs.Add(paraTitre);
                    xpParagraphs.Add(paraDates);
                    xpParagraphs.Add(paraDesc);
                    xpParagraphs.Add(new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("")))); // Ligne vide entre les expériences
                }
                #endregion

                #endregion

                #region Ecrire dans le document
                using (var doc = WordprocessingDocument.Open(outputPath, true))
                {
                    var body = doc!.MainDocumentPart!.Document.Body;

                    #region Ajouter un style de puces s’il n'existe pas
                    var numberingPart = doc!.MainDocumentPart!.NumberingDefinitionsPart;
                    if (numberingPart == null)
                        numberingPart = doc.MainDocumentPart.AddNewPart<NumberingDefinitionsPart>();

                    numberingPart.Numbering = new Numbering(
                        new AbstractNum(
                            new Level(
                                new DocumentFormat.OpenXml.Wordprocessing.NumberingFormat() { Val = NumberFormatValues.Bullet },
                                new LevelText() { Val = "•" },
                                new LevelJustification() { Val = LevelJustificationValues.Left }
                            )
                            { LevelIndex = 0 }
                        )
                        { AbstractNumberId = 1 },

                        new NumberingInstance(
                            new AbstractNumId() { Val = 1 }
                        )
                        { NumberID = 1 }
                    );

                    #endregion

                    #region Profil (mise en forme particulière)
                    foreach (var para in body!.Descendants<Paragraph>())
                    {
                        if (para.InnerText.Contains("{{Profil}}"))
                        {
                            para.RemoveAllChildren<DocumentFormat.OpenXml.Wordprocessing.Run>();

                            var runBold = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(profilAccroche));
                            runBold.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                                new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "24" },
                                new DocumentFormat.OpenXml.Wordprocessing.Bold()
                                );

                            para.Append(runBold);

                            foreach (var line in profilDetails.Split('\n'))
                            {
                                var run = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(line));
                                var smallPara = new Paragraph(run);
                                var props = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name }, new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "18" }); // 9pt
                                smallPara!.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Run>()!.RunProperties = props;
                                smallPara!.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                                para.InsertAfterSelf(smallPara);
                            }

                            break;
                        }
                    }
                    #endregion

                    #region Experiences
                    foreach (var para in body.Descendants<Paragraph>().ToList())
                    {
                        if (para.InnerText.Contains("{{Experiences}}"))
                        {
                            var parent = para.Parent;
                            foreach (var xpPara in xpParagraphs)
                            {
                                parent!.InsertBefore(xpPara.CloneNode(true), para);
                            }
                            para.Remove();
                            break;
                        }
                    }
                    #endregion 

                    #region Langues
                    if (body.InnerText.Contains("{{Langues}}"))
                    {
                        var paraToReplace = body.Descendants<Paragraph>()
                                                .FirstOrDefault(p => p.InnerText.Contains("{{Langues}}"));
                        if (paraToReplace != null)
                        {
                            var parent = paraToReplace.Parent;
                            var lstLangues = cv.Element("Langues")?.Elements("Langue")
                                            .Select(l =>
                                            {
                                                string sReturn = "";
                                                string nom = l.Attribute("nom")?.Value ?? "";
                                                string niveau = l.Attribute("niveau")?.Value ?? "";

                                                sReturn = $"{nom}";
                                                if (niveau != null && niveau != "") sReturn += $" ({niveau})";
                                                return sReturn;
                                            })
                                            .ToList() ?? new List<string>();


                            foreach (var langue in lstLangues)
                            {
                                var run = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(langue));
                                run.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                    new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                                    new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = Font_Size } // 11pt = 22 demi-points
                                );

                                var para = new Paragraph(
                                    new ParagraphProperties(
                                        new NumberingProperties(
                                            new NumberingLevelReference() { Val = 0 },
                                            new NumberingId() { Val = 1 })
                                    ),
                                    run
                                );
                                parent!.InsertAfter(para, paraToReplace);
                            }

                            paraToReplace.Remove();
                        }
                    }
                    #endregion

                    #region Formations
                    if (body.InnerText.Contains("{{Formations}}"))
                    {
                        var paraToReplace = body.Descendants<Paragraph>()
                                                .FirstOrDefault(p => p.InnerText.Contains("{{Formations}}"));
                        if (paraToReplace != null)
                        {
                            var parent = paraToReplace.Parent;
                            var lstFormations = cv.Element("Formations")?.Elements("Formation")
                                               .Select(f =>
                                               {
                                                   string sReturn = "";

                                                   string intitule = f.Element("Diplome")?.Value ?? "";
                                                   string lieu = f.Element("Etablissement")?.Value ?? "";
                                                   string annee = f.Element("DateFin")?.Value ?? "";
                                                   sReturn = $"{intitule}";
                                                   if (lieu != null && lieu != "") sReturn += $" - {lieu}";
                                                   if (annee != null && annee != "") sReturn += $" ({annee})";
                                                   return sReturn;// $"{intitule} - {lieu} ({annee})";
                                               })
                                               .ToList() ?? new List<string>();

                            foreach (var formation in lstFormations)
                            {
                                var run = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(formation));
                                run.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                    new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                                    new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = Font_Size } // 11pt = 22 demi-points
                                );

                                var para = new Paragraph(
                                    new ParagraphProperties(
                                        new NumberingProperties(
                                            new NumberingLevelReference() { Val = 0 },
                                            new NumberingId() { Val = 1 })
                                    ),
                                    run
                                );
                                parent!.InsertAfter(para, paraToReplace);
                            }

                            paraToReplace.Remove();
                        }
                    }
                    #endregion

                    #region Certifications
                    if (body.InnerText.Contains("{{Certifications}}"))
                    {
                        var paraToReplace = body.Descendants<Paragraph>()
                                                .FirstOrDefault(p => p.InnerText.Contains("{{Certifications}}"));
                        if (paraToReplace != null)
                        {
                            var parent = paraToReplace.Parent;
                            var lstCertifications = cv.Element("Certifications")?.Elements("Certification")
                                               .Select(f =>
                                               {
                                                   string sReturn = "";
                                                   string intitule = f.Element("Nom")?.Value ?? "";
                                                   string lieu = f.Element("Organisme")?.Value ?? "";
                                                   string annee = f.Element("Date")?.Value ?? "";

                                                   sReturn = $"{intitule}";
                                                   if (lieu != null && lieu != "") sReturn += $" - {lieu}";
                                                   if (annee != null && annee != "") sReturn += $" ({annee})";
                                                   return sReturn;
                                               })
                                               .ToList() ?? new List<string>();

                            foreach (var certification in lstCertifications)
                            {
                                var run = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(certification));
                                run.RunProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                    new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                                    new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = Font_Size } // 11pt = 22 demi-points
                                );

                                var para = new Paragraph(
                                    new ParagraphProperties(
                                        new NumberingProperties(
                                            new NumberingLevelReference() { Val = 0 },
                                            new NumberingId() { Val = 1 })
                                    ),
                                    run
                                );
                                parent!.InsertAfter(para, paraToReplace);
                            }

                            paraToReplace.Remove();
                        }
                    }
                    #endregion

                    #region Le reste
                    foreach (var para in body!.Descendants<Paragraph>())
                    {
                        foreach (var run in para.Descendants<DocumentFormat.OpenXml.Wordprocessing.Run>())
                        {
                            /*
                            run.RunProperties = new RunProperties(
                                new RunFonts() { Ascii = Font_Name, HighAnsi = Font_Name },
                                new FontSize() { Val = Font_Size });
                            */
                            var text = run.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>();
                            if (text == null) continue;

                            if (text.Text.Contains("{{Nom}}")) text.Text = text.Text.Replace("{{Nom}}", placeholders["{{Nom}}"]);
                            if (text.Text.Contains("{{Prenom}}")) text.Text = text.Text.Replace("{{Prenom}}", placeholders["{{Prenom}}"]);
                            if (text.Text.Contains("{{Telephone}}")) text.Text = text.Text.Replace("{{Telephone}}", placeholders["{{Telephone}}"]);
                            if (text.Text.Contains("{{Email}}")) text.Text = text.Text.Replace("{{Email}}", placeholders["{{Email}}"]);
                            if (text.Text.Contains("{{Competences}}")) text.Text = text.Text.Replace("{{Competences}}", competencesText);
                            //if (text.Text.Contains("{{Formations}}")) text.Text = text.Text.Replace("{{Formations}}", formationsText);
                            //if (text.Text.Contains("{{Langues}}")) text.Text = text.Text.Replace("{{Langues}}", languesText);
                        }
                    }
                    #endregion

                    doc.MainDocumentPart.Document.Save();
                }
                #endregion

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Erreur dans la génération d'un CV au format Word (DocxService.CreateDocx) : {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
