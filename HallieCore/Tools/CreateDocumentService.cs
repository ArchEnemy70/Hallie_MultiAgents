using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using ExternalServices;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;


namespace Hallie.Tools
{
    #region Tool
    public sealed class CreateDocumentTool : ITool
    {
        public string Name => "create_bureatique_file";

        public string Description =>
            "Crée un fichier (PowerPoint/Word/Excel) à partir d’une spécification JSON structurée. " +
            "Paramètres: fileType (powerpoint|word|excel), specJson (JSON), fileName, openFile, optionsJson (optionnel).";

        private readonly string _exportDir;
        private readonly FileCreatorRegistry _registry;

        public CreateDocumentTool(string exportDir)
        {
            LoggerService.LogInfo("CreateFileTool");

            var fileCreators = new IFileCreator[]
            {
                new PowerPointFileService(),
                new ExcelFileService(),
                new WordFileService()
            };

            _exportDir = exportDir;
            if(!Directory.Exists(exportDir))
                Directory.CreateDirectory(_exportDir);
            _registry = new FileCreatorRegistry(fileCreators); 
        }
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("CreateFileTool.ExecuteAsync");

            try
            {
                var openFile = GetString(parameters, "openFile", required: false)!;
                var fileTypeStr = GetString(parameters, "fileType", required: true)!;
                var specJson = GetString(parameters, "specJson", required: true)!;
                var fileName = GetString(parameters, "fileName", required: false);
                var optionsJson = GetString(parameters, "optionsJson", required: false);

                if (!TryParseFileType(fileTypeStr, out var fileType))
                    return JsonService.Serialize(new { ok = false, fileType = fileTypeStr, filePath = (string?)null, count = 0, error = $"Unsupported fileType '{fileTypeStr}'." });

                // Sanity checks minimalistes (évite les énormes payloads / junk)
                if (!IsGoodJson(specJson))
                    return JsonService.Serialize(new { ok = false, fileType = fileTypeStr, filePath = (string?)null, count = 0, error = "specJson is not valid JSON." });

                if (optionsJson is not null && !IsGoodJson(optionsJson))
                    return JsonService.Serialize(new { ok = false, fileType = fileTypeStr, filePath = (string?)null, count = 0, error = "optionsJson is not valid JSON." });

                if (!_registry.TryGet(fileType, out var creator))
                    return JsonService.Serialize(new { ok = false, fileType = fileTypeStr, filePath = (string?)null, count = 0, error = $"No creator registered for '{fileType}'." });

                LoggerService.LogDebug($"CreateFileTool.ExecuteAsync :\n{fileType}, {fileName}");

                var request = new CreateFileRequest(fileType, specJson, fileName, optionsJson);
                var result = await creator.CreateAsync(request, _exportDir);

                if (File.Exists(result.FilePath))
                {
                    LoggerService.LogDebug($"CreateFileTool.ExecuteAsync : fichier généré");
                    if (openFile == "1")
                    {
                        LoggerService.LogDebug($"CreateFileTool.ExecuteAsync : ouverture fichier généré");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = result.FilePath,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    LoggerService.LogDebug($"CreateFileTool.ExecuteAsync : échec de la génération du fichier");
                }

                return JsonService.Serialize(new
                {
                    ok = result.Ok,
                    fileType = fileTypeStr,
                    filePath = result.FilePath,
                    count = result.Count,
                    error = result.Error != null ? result.Error : "0"
                });
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CreateFileTool.ExecuteAsync : {ex.Message}");
                return JsonService.Serialize(new { ok = false, fileType = (string?)null, filePath = (string?)null, count = 0, error = ex.Message });
            }
        }

        public ToolParameter[] GetParameters() => new[]
        {
            new ToolParameter
            {
                Name = "fileType",
                Type = "string",
                Description = "Type de fichier à générer: powerpoint|word|excel",
                Required = true
            },
            new ToolParameter
            {
                Name = "specJson",
                Type = "string",
                Description = "Spécification JSON du document (structure dépend du type).",
                Required = true
            },
            new ToolParameter
            {
                Name = "fileName",
                Type = "string",
                Description = "Nom du fichier (optionnel). Extension ajoutée si manquante.",
                Required = true
            },
            new ToolParameter
            {
                Name = "optionsJson",
                Type = "string",
                Description = "Options JSON optionnelles (ex: thème, langue, etc.).",
                Required = false
            }
            ,
            new ToolParameter
            {
                Name = "openFile",
                Type = "string",
                Description = """indique s'il faut ouvrir le fichier après l'avoir généré ou pas ("0": non, "1":oui)""",
                Required = true
            }
        };

        #region Méthodes privées
        private static string? GetString(Dictionary<string, object> parameters, string key, bool required)
        {
            if (!parameters.TryGetValue(key, out var val) || val is null)
            {
                if (required) throw new ArgumentException($"Missing parameter '{key}'.");
                return null;
            }

            return val switch
            {
                string s => string.IsNullOrWhiteSpace(s) ? null : s,
                JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
                _ => val.ToString()
            };
        }

        private static bool TryParseFileType(string s, out FileType type)
        {
            type = default;
            s = s.Trim().ToLowerInvariant();

            return s switch
            {
                "powerpoint" or "ppt" or "pptx" => (type = FileType.PowerPoint) == FileType.PowerPoint,
                "word" or "doc" or "docx" => (type = FileType.Word) == FileType.Word,
                "excel" or "xls" or "xlsx" => (type = FileType.Excel) == FileType.Excel,
                _ => false
            };
        }

        private static bool IsGoodJson(string json)
        {
            try
            {
                using var _ = JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }

    #endregion

    #region WordService

    #region Structures json
    public sealed class WordSpec
    {
        public string? Title { get; set; }
        public string? Subtitle { get; set; }

        public WordHeaderFooterSpec? Header { get; set; }
        public WordHeaderFooterSpec? Footer { get; set; }

        public List<WordSectionSpec> Sections { get; set; } = new();
    }
    public sealed class WordHeaderFooterSpec
    {
        public string? Left { get; set; }
        public string? Center { get; set; }
        public string? Right { get; set; }
    }
    public sealed class WordSectionSpec
    {
        public string Title { get; set; } = "";
        public List<string> Paragraphs { get; set; } = new();
        public List<string> Bullets { get; set; } = new();
        public List<WordTableSpec> Tables { get; set; } = new();
        public List<WordImageSpec> Images { get; set; } = new();
    }
    public sealed class WordTableSpec
    {
        public string? Title { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<List<object?>> Rows { get; set; } = new();
    }
    public sealed class WordImageSpec
    {
        public string Path { get; set; } = "";
        public string? Caption { get; set; }
        public double? MaxWidthCm { get; set; } // ex: 15
    }
    #endregion

    #region Service
    public class WordFileService : IFileCreator
    {
        #region Propriétés
        public FileType Type => FileType.Word;
        public string DefaultExtension => ".docx";
        #endregion

        #region Méthode publique
        public Task<CreateFileResult> CreateAsync(CreateFileRequest request, string exportDir)
        {
            LoggerService.LogInfo("WordFileService.CreateAsync");

            var spec = JsonSerializer.Deserialize<WordSpec>(
                request.SpecJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (spec is null || spec.Sections is null || spec.Sections.Count == 0)
                return Task.FromResult(new CreateFileResult(false, Type, null, 0, "Word spec is empty."));

            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var baseName = string.IsNullOrWhiteSpace(request.FileName) ? "document" : request.FileName!;
            var safe = SanitizeFileName(baseName);

            if (!safe.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase))
                safe += DefaultExtension;

            var path = Path.Combine(exportDir, safe);

            CreateDocx(path, spec);

            if (!File.Exists(path))
                return Task.FromResult(new CreateFileResult(false, Type, null, 0, $"File not created: {path}"));

            var len = new FileInfo(path).Length;
            if (len == 0)
                return Task.FromResult(new CreateFileResult(false, Type, path, 0, $"File created but empty: {path}"));


            return Task.FromResult(new CreateFileResult(true, Type, path, spec.Sections.Count, null));
        }
        #endregion

        #region Méthode principale
        private static void CreateDocx(string path, WordSpec spec)
        {
            if (File.Exists(path)) File.Delete(path);

            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            EnsureNumberingPart(mainPart);

            // Header / Footer
            AddHeaderFooter(mainPart, spec);

            // Titre
            if (!string.IsNullOrWhiteSpace(spec.Title))
                body.Append(CreateParagraph(spec.Title!, "Title"));

            if (!string.IsNullOrWhiteSpace(spec.Subtitle))
                body.Append(CreateParagraph(spec.Subtitle!, "Subtitle"));

            if (!string.IsNullOrWhiteSpace(spec.Title) || !string.IsNullOrWhiteSpace(spec.Subtitle))
                body.Append(BlankLine());

            // Sections
            foreach (var section in spec.Sections ?? new List<WordSectionSpec>())
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                    body.Append(CreateParagraph(section.Title, "Heading1"));

                foreach (var p in section.Paragraphs ?? new List<string>())
                    if (!string.IsNullOrWhiteSpace(p))
                        body.Append(CreateParagraph(p.Trim(), "Normal"));

                foreach (var b in section.Bullets ?? new List<string>())
                    if (!string.IsNullOrWhiteSpace(b))
                        body.Append(CreateBulletParagraph(b.Trim()));

                // Tables
                foreach (var t in section.Tables ?? new List<WordTableSpec>())
                {
                    if (!string.IsNullOrWhiteSpace(t.Title))
                        body.Append(CreateParagraph(t.Title!, "Heading2"));

                    body.Append(AppendTable(t));
                    body.Append(BlankLine());
                }

                // Images
                foreach (var img in section.Images ?? new List<WordImageSpec>())
                {
                    if (string.IsNullOrWhiteSpace(img.Path) || !File.Exists(img.Path))
                    {
                        // on met une ligne informative plutôt que planter
                        body.Append(CreateParagraph($"[Image introuvable] {img.Path}", "Normal"));
                        continue;
                    }

                    body.Append(AppendImage(mainPart, img.Path, img.MaxWidthCm ?? 15.0));
                    if (!string.IsNullOrWhiteSpace(img.Caption))
                        body.Append(CreateParagraph(img.Caption!, "Caption")); // style Caption natif Word
                    body.Append(BlankLine());
                }

                body.Append(BlankLine());
            }

            mainPart.Document.Save();
        }
        #endregion

        #region Header / Footer
        private static void AddHeaderFooter(MainDocumentPart mainPart, WordSpec spec)
        {
            // Crée une section properties si absente
            var body = mainPart!.Document!.Body!;
            var sectPr = body.Elements<SectionProperties>().LastOrDefault();
            if (sectPr == null)
            {
                sectPr = new SectionProperties();
                body.Append(sectPr);
            }

            // Header
            if (spec.Header != null && (spec.Header.Left != null || spec.Header.Center != null || spec.Header.Right != null))
            {
                var headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = BuildHeader(spec.Header);
                headerPart!.Header!.Save();

                var headerRelId = mainPart.GetIdOfPart(headerPart);
                sectPr.RemoveAllChildren<HeaderReference>();
                sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerRelId });
            }

            // Footer
            if (spec.Footer != null && (spec.Footer.Left != null || spec.Footer.Center != null || spec.Footer.Right != null))
            {
                var footerPart = mainPart.AddNewPart<FooterPart>();
                footerPart.Footer = BuildFooter(spec.Footer);
                footerPart!.Footer!.Save();

                var footerRelId = mainPart.GetIdOfPart(footerPart);
                sectPr.RemoveAllChildren<FooterReference>();
                sectPr.AppendChild(new FooterReference { Type = HeaderFooterValues.Default, Id = footerRelId });
            }
        }
        private static Header BuildHeader(WordHeaderFooterSpec hf)
        {
            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None }
                    )
                ),
                new TableRow(
                    MakeHeaderFooterCell(hf.Left, JustificationValues.Left),
                    MakeHeaderFooterCell(hf.Center, JustificationValues.Center),
                    MakeHeaderFooterCell(hf.Right, JustificationValues.Right)
                )
            );

            var pAfter = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));

            return new Header(table, pAfter);
        }
        private static Footer BuildFooter(WordHeaderFooterSpec hf)
        {
            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None }
                    )
                ),
                new TableRow(
                    MakeHeaderFooterCell(hf.Left, JustificationValues.Left),
                    MakeHeaderFooterCell(hf.Center, JustificationValues.Center),
                    MakeHeaderFooterCell(hf.Right, JustificationValues.Right)
                )
            );

            var pAfter = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));

            return new Footer(table, pAfter);
        }
        private static TableCell MakeHeaderFooterCell(string? text, JustificationValues align)
        {
            var p = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = align },
                    new SpacingBetweenLines { After = "0" }
                )
            );

            foreach (var el in ExpandFooterTokens(text ?? ""))
                p.Append(el);

            return new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1666" }
                ),
                p
            );
        }
        private static IEnumerable<OpenXmlElement> ExpandFooterTokens(string text)
        {
            // Tokens supportés :
            // {page} / {pages} -> champs PAGE / NUMPAGES
            // {date:yyyy-MM-dd} -> date formatée (au moment de génération)
            // Sinon texte brut.

            if (string.IsNullOrEmpty(text))
                return new OpenXmlElement[] { new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("")) };

            // Date token
            // ex "{date:yyyy-MM-dd}"
            if (text.Contains("{date:", StringComparison.OrdinalIgnoreCase))
            {
                text = ReplaceDateToken(text);
            }

            // Page tokens : on construit un run list simple en split
            var parts = SplitKeepTokens(text, new[] { "{page}", "{pages}" });

            var runs = new List<OpenXmlElement>();
            foreach (var part in parts)
            {
                if (part.Equals("{page}", StringComparison.OrdinalIgnoreCase))
                    runs.AddRange(Field("PAGE"));
                else if (part.Equals("{pages}", StringComparison.OrdinalIgnoreCase))
                    runs.AddRange(Field("NUMPAGES"));
                else
                    runs.Add(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(part) { Space = SpaceProcessingModeValues.Preserve }));
            }

            return runs;
        }
        private static string ReplaceDateToken(string input)
        {
            // cherche {date:FORMAT}
            var start = input.IndexOf("{date:", StringComparison.OrdinalIgnoreCase);
            while (start >= 0)
            {
                var end = input.IndexOf('}', start);
                if (end < 0) break;

                var inside = input.Substring(start + 6, end - (start + 6)); // FORMAT
                var fmt = string.IsNullOrWhiteSpace(inside) ? "yyyy-MM-dd" : inside.Trim();
                var value = DateTime.Now.ToString(fmt, CultureInfo.InvariantCulture);

                input = input.Substring(0, start) + value + input.Substring(end + 1);
                start = input.IndexOf("{date:", StringComparison.OrdinalIgnoreCase);
            }
            return input;
        }
        private static List<string> SplitKeepTokens(string input, string[] tokens)
        {
            var result = new List<string>();
            int i = 0;
            while (i < input.Length)
            {
                int nextPos = -1;
                string? nextTok = null;

                foreach (var t in tokens)
                {
                    var p = input.IndexOf(t, i, StringComparison.OrdinalIgnoreCase);
                    if (p >= 0 && (nextPos < 0 || p < nextPos))
                    {
                        nextPos = p;
                        nextTok = t;
                    }
                }

                if (nextPos < 0)
                {
                    result.Add(input.Substring(i));
                    break;
                }

                if (nextPos > i)
                    result.Add(input.Substring(i, nextPos - i));

                result.Add(nextTok!);
                i = nextPos + nextTok!.Length;
            }
            return result;
        }
        private static IEnumerable<OpenXmlElement> Field(string fieldName)
        {
            // Construction standard d'un champ Word
            return new OpenXmlElement[]
            {
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode($" {fieldName} ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("1")), // placeholder, Word calcule à l’ouverture
            new Run(new FieldChar { FieldCharType = FieldCharValues.End })
            };
        }
        #endregion

        #region Tables
        private static Table AppendTable(WordTableSpec t)
        {
            if (t.Columns == null || t.Columns.Count == 0)
                throw new InvalidOperationException("Table sans colonnes.");

            var table = new Table();

            // Propriétés "TableGrid" : bordures visibles + largeur 100%
            table.AppendChild(new TableProperties(
                new TableStyle { Val = "TableGrid" },
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                new TableLook { Val = "04A0" } // look standard
            ));

            // TableGrid
            var grid = new TableGrid();
            for (int i = 0; i < t.Columns.Count; i++)
                grid.Append(new GridColumn());
            table.Append(grid);

            // Header row
            var headerRow = new TableRow(
                new TableRowProperties(new TableHeader()) // répète l’entête si saut de page
            );

            foreach (var col in t.Columns)
                headerRow.Append(MakeCell(col ?? "", isHeader: true));

            table.Append(headerRow);

            // Data rows
            foreach (var row in (t.Rows ?? new List<List<object?>>()))
            {
                var tr = new TableRow();

                for (int i = 0; i < t.Columns.Count; i++)
                {
                    var v = i < row.Count ? row[i] : null;
                    tr.Append(MakeCell(ValueToString(v), isHeader: false));
                }

                table.Append(tr);
            }

            return table;
        }
        private static TableCell MakeCell(string text, bool isHeader)
        {
            // ParagraphProperties indispensables dans certains Word
            var pPr = new ParagraphProperties(
                new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
            );

            var rPr = new RunProperties();
            if (isHeader)
            {
                rPr.Append(new Bold());
            }

            var p = new Paragraph(
                pPr,
                new Run(
                    rPr,
                    new DocumentFormat.OpenXml.Wordprocessing.Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }
                )
            );

            // Word aime bien voir au moins un paragraphe dans chaque cellule
            // TableCellProperties : largeur auto + align vertical
            var tcPr = new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Auto },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );

            return new TableCell(tcPr, p);
        }
        private static string ValueToString(object? v)
        {
            if (v is null) return "";

            if (v is System.Text.Json.JsonElement je)
            {
                return je.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => je.GetString() ?? "",
                    System.Text.Json.JsonValueKind.Number => je.ToString(),
                    System.Text.Json.JsonValueKind.True => "true",
                    System.Text.Json.JsonValueKind.False => "false",
                    System.Text.Json.JsonValueKind.Null => "",
                    System.Text.Json.JsonValueKind.Undefined => "",
                    _ => je.ToString()
                };
            }

            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }
        #endregion

        #region Images
        private static Paragraph AppendImage(MainDocumentPart mainPart, string imagePath, double maxWidthCm)
        {
            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            var imgType = ext switch
            {
                ".png" => ImagePartType.Png,
                ".gif" => ImagePartType.Gif,
                ".bmp" => ImagePartType.Bmp,
                ".tif" or ".tiff" => ImagePartType.Tiff,
                _ => ImagePartType.Jpeg
            };

            var imagePart = mainPart.AddImagePart(imgType);
            using (var stream = File.OpenRead(imagePath))
                imagePart.FeedData(stream);

            var relId = mainPart.GetIdOfPart(imagePart);

            // Taille : on limite à maxWidthCm
            // Conversion cm -> EMU : 1 inch = 914400 EMU, 1 inch = 2.54 cm
            long maxWidthEmu = (long)(maxWidthCm / 2.54 * 914400);

            // Pour ne pas dépendre de lecture dimensions image (GDI+), on met une taille “safe”.
            // Word ajustera; si tu veux sizing exact: on pourra lire width/height pixels et DPI en v2.1
            long cx = maxWidthEmu;
            long cy = (long)(maxWidthEmu * 0.60); // ratio approx

            var element =
                new Drawing(
                    new DW.Inline(
                        new DW.Extent { Cx = cx, Cy = cy },
                        new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                        new DW.DocProperties { Id = 1U, Name = Path.GetFileName(imagePath) },
                        new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                        new A.Graphic(
                            new A.GraphicData(
                                new PIC.Picture(
                                    new PIC.NonVisualPictureProperties(
                                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = Path.GetFileName(imagePath) },
                                        new PIC.NonVisualPictureDrawingProperties()
                                    ),
                                    new PIC.BlipFill(
                                        new A.Blip { Embed = relId },
                                        new A.Stretch(new A.FillRectangle())
                                    ),
                                    new PIC.ShapeProperties(
                                        new A.Transform2D(
                                            new A.Offset { X = 0L, Y = 0L },
                                            new A.Extents { Cx = cx, Cy = cy }
                                        ),
                                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                                    )
                                )
                            )
                            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                        )
                    )
                    {
                        DistanceFromTop = 0U,
                        DistanceFromBottom = 0U,
                        DistanceFromLeft = 0U,
                        DistanceFromRight = 0U
                    }
                );

            return new Paragraph(new Run(element));
        }
        #endregion

        #region Basics + bullets
        private static Paragraph CreateParagraph(string text, string styleId)
        {
            return new Paragraph(
                new ParagraphProperties(
                    new ParagraphStyleId { Val = styleId }
                ),
                new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text) { Space = SpaceProcessingModeValues.Preserve })
            );
        }
        private static Paragraph CreateBulletParagraph(string text)
        {
            return new Paragraph(
                new ParagraphProperties(
                    new NumberingProperties(
                        new NumberingLevelReference { Val = 0 },
                        new NumberingId { Val = 1 }
                    )
                ),
                new Run(new DocumentFormat.OpenXml.Wordprocessing.Text(text) { Space = SpaceProcessingModeValues.Preserve })
            );
        }
        private static Paragraph BlankLine() => new Paragraph(new Run(new DocumentFormat.OpenXml.Wordprocessing.Text("")));

        // Crée un NumberingPart simple pour les puces
        private static void EnsureNumberingPart(MainDocumentPart mainPart)
        {
            var numberingPart = mainPart.NumberingDefinitionsPart;
            if (numberingPart != null) return;

            numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering(
                new AbstractNum(
                    new Level(
                        new NumberingFormat { Val = NumberFormatValues.Bullet },
                        new LevelText { Val = "•" },
                        new LevelJustification { Val = LevelJustificationValues.Left },
                        new ParagraphProperties(
                            new Indentation { Left = "720", Hanging = "360" } // ~0.5" left, 0.25" hanging
                        ),
                        new RunProperties(
                            new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }
                        )
                    )
                    { LevelIndex = 0 }
                )
                { AbstractNumberId = 1 },

                new NumberingInstance(
                    new AbstractNumId { Val = 1 }
                )
                { NumberID = 1 }
            );

            numberingPart.Numbering.Save();
        }
        #endregion

        #region Propreté du nom de fichier
        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            return string.IsNullOrEmpty(name) ? "file" : name;
        }
        #endregion
    }
    #endregion

    #endregion

    #region ExcelService

    #region Structures json
    public sealed class ExcelSpec
    {
        public string? Title { get; set; }
        public List<ExcelSheetSpec> Sheets { get; set; } = new();
    }

    public sealed class ExcelSheetSpec
    {
        public string Name { get; set; } = "Sheet1";
        public List<string> Columns { get; set; } = new();
        public List<List<object?>> Rows { get; set; } = new();

        public bool FreezeHeader { get; set; } = true;
        public bool AutoFilter { get; set; } = true;
    }
    #endregion

    #region Service
    public sealed class ExcelFileService : IFileCreator
    {
        #region Propriétés
        public FileType Type => FileType.Excel;
        public string DefaultExtension => ".xlsx";
        #endregion

        #region Méthode publique
        public Task<CreateFileResult> CreateAsync(CreateFileRequest request, string exportDir)
        {
            LoggerService.LogInfo("ExcelFileService.CreateAsync");

            var spec = JsonSerializer.Deserialize<ExcelSpec>(
                request.SpecJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (spec is null || spec.Sheets is null || spec.Sheets.Count == 0)
                return Task.FromResult(new CreateFileResult(false, Type, null, 0, "Excel spec is empty."));

            if(!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var baseName = string.IsNullOrWhiteSpace(request.FileName) ? "tableau" : request.FileName!;
            var safe = SanitizeFileName(baseName);

            if (!safe.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase))
                safe += DefaultExtension;

            var path = Path.Combine(exportDir, safe);

            CreateXlsx(path, spec);

            if (!File.Exists(path))
                return Task.FromResult(new CreateFileResult(false, Type, null, 0, $"File not created: {path}"));

            var len = new FileInfo(path).Length;
            if (len == 0)
                return Task.FromResult(new CreateFileResult(false, Type, path, 0, $"File created but empty: {path}"));


            return Task.FromResult(new CreateFileResult(true, Type, path, spec.Sheets.Count, null));
        }
        #endregion

        #region Méthode principale
        private static void CreateXlsx(string path, ExcelSpec spec)
        {
            LoggerService.LogInfo("ExcelFileService.CreateXlsx");

            if (File.Exists(path)) File.Delete(path);

            using var wb = new XLWorkbook();

            if (spec.Sheets == null || spec.Sheets.Count == 0)
                throw new InvalidOperationException("ExcelSpec.Sheets est vide.");

            foreach (var sheet in spec.Sheets)
            {
                var wsName = string.IsNullOrWhiteSpace(sheet.Name) ? "Sheet1" : sheet.Name.Trim();
                var ws = wb.Worksheets.Add(SanitizeWorksheetName(wsName, wb));

                // 1) Header (ligne 1)
                if (sheet.Columns == null || sheet.Columns.Count == 0)
                    throw new InvalidOperationException($"La feuille '{ws.Name}' n'a pas de colonnes.");

                for (int c = 0; c < sheet.Columns.Count; c++)
                {
                    ws.Cell(1, c + 1).Value = sheet.Columns[c] ?? "";
                }

                var headerRange = ws.Range(1, 1, 1, sheet.Columns.Count);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                // 2) Rows (à partir de ligne 2)
                int r = 2;
                foreach (var row in sheet.Rows ?? Enumerable.Empty<List<object?>>())
                {
                    for (int c = 0; c < sheet.Columns.Count; c++)
                    {
                        object? v = c < row.Count ? row[c] : null;
                        SetCellValueSmart(ws.Cell(r, c + 1), v);
                    }
                    r++;
                }

                // 3) Table + Autofilter
                var lastRow = Math.Max(1, r - 1);
                var usedRange = ws.Range(1, 1, lastRow, sheet.Columns.Count);

                if (sheet.AutoFilter)
                {
                    // Crée une table (plus propre qu'un autofilter brut)
                    var table = usedRange.CreateTable();
                    table.Theme = XLTableTheme.TableStyleMedium2;
                    table.ShowAutoFilter = true;
                }

                // 4) Freeze pane (header)
                if (sheet.FreezeHeader)
                    ws.SheetView.FreezeRows(1);

                // 5) Auto-fit
                ws.Columns(1, sheet.Columns.Count).AdjustToContents();
                ws.Rows().AdjustToContents();
            }

            // Propriétés du document
            wb.Properties.Title = spec.Title ?? "Generated Excel";
            wb.Properties.Author = "YourApp";
            wb.Properties.Company = "YourCompany";

            wb.SaveAs(path);
        }
        #endregion

        #region Autres méthodes
        private static void SetCellValueSmart(IXLCell cell, object? v)
        {
            if (v is null)
            {
                cell.Value = "";
                return;
            }

            // Si le JSON te donne un number => souvent System.Text.Json => JsonElement
            // Si tu désérialises en object, tu peux recevoir JsonElement.
            if (v is System.Text.Json.JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        cell.Value = je.GetString() ?? "";
                        return;

                    case System.Text.Json.JsonValueKind.Number:
                        if (je.TryGetInt64(out var l)) { cell.Value = l; return; }
                        if (je.TryGetDouble(out var d)) { cell.Value = d; return; }
                        cell.Value = je.ToString();
                        return;

                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        cell.Value = je.GetBoolean();
                        return;

                    case System.Text.Json.JsonValueKind.Null:
                    case System.Text.Json.JsonValueKind.Undefined:
                        cell.Value = "";
                        return;

                    default:
                        cell.Value = je.ToString();
                        return;
                }
            }

            // Date ISO "YYYY-MM-DD" => on la met en Date si possible
            if (v is string s)
            {
                s = s.Trim();
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                {
                    // heuristique : si format date simple, appliquer format date
                    if (s.Length <= 10)
                    {
                        cell.Value = dt.Date;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                        return;
                    }
                }

                cell.Value = s;
                return;
            }

            if (v is bool b) { cell.Value = b; return; }
            if (v is int i) { cell.Value = i; return; }
            if (v is long lo) { cell.Value = lo; return; }
            if (v is float f) { cell.Value = f; return; }
            if (v is double db) { cell.Value = db; return; }
            if (v is decimal dec) { cell.Value = dec; return; }

            cell.Value = v.ToString() ?? "";
        }
        private static string SanitizeWorksheetName(string name, XLWorkbook wb)
        {
            // Excel: max 31 chars, pas de : \ / ? * [ ]
            var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            foreach (var ch in invalid) name = name.Replace(ch, '_');

            name = name.Trim();
            if (name.Length == 0) name = "Sheet1";
            if (name.Length > 31) name = name.Substring(0, 31);

            // éviter doublons
            var baseName = name;
            int k = 2;
            while (wb.Worksheets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                var suffix = $" ({k++})";
                var maxBase = 31 - suffix.Length;
                name = (baseName.Length > maxBase ? baseName.Substring(0, maxBase) : baseName) + suffix;
            }

            return name;
        }
        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            return string.IsNullOrEmpty(name) ? "file" : name;
        }
        #endregion
    }
    #endregion

    #endregion

    #region PowerpointService

    #region Structures json
    public record PptSpec(string? Title, List<PptSlide> Slides);
    public record PptSlide(string Layout, string Title, List<string> Bullets, string? Notes);
    #endregion

    #region Service
    public sealed class PowerPointFileService: IFileCreator
    {
        #region Propriétés
        public FileType Type => FileType.PowerPoint;
        public string DefaultExtension => ".pptx";
        #endregion

        #region Méthode publique
        public Task<CreateFileResult> CreateAsync(CreateFileRequest request, string exportDir)
        {
            LoggerService.LogInfo("PowerPointFileService.CreateAsync");

            try
            {
                var spec = JsonSerializer.Deserialize<PptSpec>(
                    request.SpecJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (spec is null || spec.Slides is null || spec.Slides.Count == 0)
                    return Task.FromResult(new CreateFileResult(false, Type, null, 0, "PowerPoint spec is empty."));

                Directory.CreateDirectory(exportDir);

                var baseName = string.IsNullOrWhiteSpace(request.FileName) ? "presentation" : request.FileName!;
                var safe = SanitizeFileName(baseName);

                if (!safe.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase))
                    safe += DefaultExtension;

                var path = Path.Combine(exportDir, safe);

                // IMPORTANT : appel réel
                PptxBuilder.CreatePptx(path, spec);

                // Vérif béton
                if (!File.Exists(path))
                    return Task.FromResult(new CreateFileResult(false, Type, null, 0, $"File not created: {path}"));

                var len = new FileInfo(path).Length;
                if (len == 0)
                    return Task.FromResult(new CreateFileResult(false, Type, path, 0, $"File created but empty: {path}"));

                ValidatePptx(path);



                return Task.FromResult(new CreateFileResult(true, Type, path, spec.Slides.Count, null));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new CreateFileResult(false, Type, null, 0, ex.ToString()));
            }
        }
        #endregion

        #region Méthodes privées
        static void ValidatePptx(string path)
        {
            using var doc = PresentationDocument.Open(path, false);
            var validator = new OpenXmlValidator();
            var errors = validator.Validate(doc).ToList();

            LoggerService.LogDebug($"ValidatePptx : OpenXML validation errors: {errors.Count}");
            Console.WriteLine($"ValidatePptx : OpenXML validation errors: {errors.Count}");
            foreach (var e in errors.Take(30))
            {
                LoggerService.LogDebug($"- {e.Description}");
                LoggerService.LogDebug($"  Path: {e.Path?.XPath}");
                LoggerService.LogDebug($"  Part: {e.Part?.Uri}");

                Console.WriteLine($"- {e.Description}");
                Console.WriteLine($"  Path: {e.Path?.XPath}");
                Console.WriteLine($"  Part: {e.Part?.Uri}");
            }
        }
        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            return string.IsNullOrEmpty(name) ? "file" : name;
        }
        #endregion
    }
    #endregion

    #region Builder
    public static class PptxBuilder
    {
        #region Constantes
        // 16:9 en EMU (1 inch = 914400)
        private const long SlideW = 12192000;
        private const long SlideH = 6858000;
        #endregion

        #region Record
        private readonly record struct LayoutSpec(
            Rect TitleRect,
            Rect ContentRect,
            int TitleFontSize,
            int ContentFontSize,
            A.TextAlignmentTypeValues TitleHAlign,
            A.TextAnchoringTypeValues TitleVAnchor
        );

        private readonly record struct Rect(long X, long Y, long Cx, long Cy);
        #endregion

        #region Méthode publique
        public static void CreatePptx(string path, PptSpec spec)
        {
            if (File.Exists(path)) File.Delete(path);

            using var presentationDoc = PresentationDocument.Create(path, PresentationDocumentType.Presentation);

            // --------------------------------------------------------------------
            // 0) Core properties (docProps/core.xml) — via PackageProperties (simple et sûr)
            // --------------------------------------------------------------------
            presentationDoc.PackageProperties.Creator = "YourApp";
            presentationDoc.PackageProperties.LastModifiedBy = "YourApp";
            presentationDoc.PackageProperties.Created = DateTime.UtcNow;
            presentationDoc.PackageProperties.Modified = DateTime.UtcNow;
            presentationDoc.PackageProperties.Title = spec.Title ?? "Generated presentation";

            // --------------------------------------------------------------------
            // 1) PresentationPart + Presentation (ppt/presentation.xml)
            // --------------------------------------------------------------------
            var presentationPart = presentationDoc.AddPresentationPart();

            presentationPart.Presentation = new Presentation
            {
                SlideIdList = new SlideIdList(),

                // Format 16:9
                SlideSize = new SlideSize
                {
                    Cx = 12192000,
                    Cy = 6858000,
                    Type = SlideSizeValues.Screen16x9
                },

                NotesSize = new NotesSize
                {
                    Cx = 6858000,
                    Cy = 9144000
                }
            };

            // Default text style minimal
            presentationPart.Presentation.AppendChild(new DefaultTextStyle(new A.DefaultParagraphProperties()));

            // --------------------------------------------------------------------
            // 2) Extended properties (docProps/app.xml) — PowerPoint aime ça
            // --------------------------------------------------------------------
            var appPart = presentationDoc.AddExtendedFilePropertiesPart();
            appPart.Properties = new DocumentFormat.OpenXml.ExtendedProperties.Properties(
                new DocumentFormat.OpenXml.ExtendedProperties.Application("Microsoft PowerPoint"),
                new DocumentFormat.OpenXml.ExtendedProperties.ApplicationVersion("16.0000")
                //new DocumentFormat.OpenXml.ExtendedProperties.DocSecurity(0),
                //new DocumentFormat.OpenXml.ExtendedProperties.ScaleCrop(),
                //new DocumentFormat.OpenXml.ExtendedProperties.HeadingPairs(),
                //new DocumentFormat.OpenXml.ExtendedProperties.TitlesOfParts()
            );
            appPart.Properties.Save();


            // --------------------------------------------------------------------
            // 3) presProps.xml / viewProps.xml / tableStyles.xml (PowerPoint “canonique”)
            // --------------------------------------------------------------------
            var presPropsPart = presentationPart.AddNewPart<PresentationPropertiesPart>();
            presPropsPart.PresentationProperties = new PresentationProperties();
            presPropsPart.PresentationProperties.Save();

            var viewPropsPart = presentationPart.AddNewPart<ViewPropertiesPart>();
            viewPropsPart.ViewProperties = new ViewProperties();
            viewPropsPart.ViewProperties.Save();

            var tableStylesPart = presentationPart.AddNewPart<TableStylesPart>();
            tableStylesPart.TableStyleList = new A.TableStyleList
            {
                Default = "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}"
            };
            tableStylesPart.TableStyleList.Save();

            // --------------------------------------------------------------------
            // 4) ThemePart au BON endroit : /ppt/theme/theme1.xml (pas sous slideMasters)
            // --------------------------------------------------------------------
            var themePart = presentationPart.AddNewPart<ThemePart>();
            themePart.Theme = BuildMinimalTheme();
            themePart.Theme.Save();

            // --------------------------------------------------------------------
            // 5) SlideMaster + 2 SlideLayouts (ppt/slideMasters + ppt/slideLayouts)
            //    - IDs >= 2147483648 (contrainte OOXML)
            //    - clrMap dans le bon ordre
            // --------------------------------------------------------------------
            var clrMap = new ColorMap
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
            };

            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
            slideMasterPart.SlideMaster = new SlideMaster(
                new CommonSlideData(
                    new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = "" },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()
                        ),
                        new GroupShapeProperties(new A.TransformGroup())
                    )
                ),
                (ColorMap)clrMap.CloneNode(true),
                new SlideLayoutIdList(),
                new TextStyles()
            );
            slideMasterPart.SlideMaster.Save();

            // Lier le thème au master (crée la relation master -> theme)
            slideMasterPart.AddPart(themePart);

            // Layout 1
            var slideLayoutPart1 = slideMasterPart.AddNewPart<SlideLayoutPart>();
            slideLayoutPart1.SlideLayout = CreateMinimalSlideLayout();
            slideLayoutPart1.SlideLayout.Save();

            slideMasterPart.SlideMaster.SlideLayoutIdList!.Append(
                new SlideLayoutId
                {
                    Id = 2147483648U,
                    RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart1)
                });

            // Layout 2 (PowerPoint en met souvent au moins 2)
            var slideLayoutPart2 = slideMasterPart.AddNewPart<SlideLayoutPart>();
            slideLayoutPart2.SlideLayout = (SlideLayout)slideLayoutPart1.SlideLayout.CloneNode(true);
            slideLayoutPart2.SlideLayout.Save();

            slideMasterPart.SlideMaster.SlideLayoutIdList.Append(
                new SlideLayoutId
                {
                    Id = 2147483649U,
                    RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart2)
                });

            slideMasterPart.SlideMaster.Save();

            // Référencer le master dans la présentation (ID >= 2147483648)
            presentationPart.Presentation.SlideMasterIdList = new SlideMasterIdList(
                new SlideMasterId
                {
                    Id = 2147483648U,
                    RelationshipId = presentationPart.GetIdOfPart(slideMasterPart)
                });

            // --------------------------------------------------------------------
            // 6) NotesMaster (ppt/notesMasters) + relation au thème (pour rels)
            // --------------------------------------------------------------------
            var notesMasterPart = presentationPart.AddNewPart<NotesMasterPart>();
            notesMasterPart.NotesMaster = new NotesMaster(
                new CommonSlideData(
                    new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = "" },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()
                        ),
                        new GroupShapeProperties(new A.TransformGroup())
                    )
                ),
                (ColorMap)clrMap.CloneNode(true)
            );
            notesMasterPart.NotesMaster.Save();

            //notesMasterPart.AddPart(themePart); // crée notesMaster1.xml.rels

            // NotesMasterIdList : ici Id = rId string
            var notesMasterRelId = presentationPart.GetIdOfPart(notesMasterPart);
            presentationPart.Presentation.NotesMasterIdList = new NotesMasterIdList(
                new NotesMasterId { Id = notesMasterRelId }
            );

            // --------------------------------------------------------------------
            // 7) Slides
            // --------------------------------------------------------------------
            uint slideId = 256U;

            foreach (var s in spec.Slides)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();

                // Lier la slide à un layout existant
                slidePart.AddPart(slideLayoutPart1);

                // Ta logique de slide (LayoutEngine + shapes)
                slidePart.Slide = BuildSlide(s);
                slidePart.Slide.Save();

                // Notes (si présentes) -> relier à NotesMaster
                if (!string.IsNullOrWhiteSpace(s.Notes))
                {
                    var notesPart = slidePart.AddNewPart<NotesSlidePart>();
                    notesPart.AddPart(notesMasterPart);
                    notesPart.NotesSlide = BuildNotes(s.Notes!);
                    notesPart.NotesSlide.Save();

                    var notesRelId = slidePart.GetIdOfPart(notesPart);
                    LoggerService.LogDebug($"Notes rel id for slide '{s.Title}': {notesRelId}");
                }

                presentationPart.Presentation.SlideIdList!.AppendChild(new SlideId
                {
                    Id = slideId++,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });
            }

            presentationPart.Presentation.Save();
        }
        #endregion

        #region Méthodes privées
        private static A.Theme BuildMinimalTheme()
        {
            var colorScheme = new A.ColorScheme { Name = "Office" };
            colorScheme.Append(
                new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }),
                new A.Light2Color(new A.RgbColorModelHex { Val = "EEECE1" }),
                new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }),
                new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }),
                new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }),
                new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }),
                new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" })
            );

            var fontScheme = new A.FontScheme { Name = "Office" };
            fontScheme.Append(
                new A.MajorFont(
                    new A.LatinFont { Typeface = "Calibri" },
                    new A.EastAsianFont { Typeface = "" },
                    new A.ComplexScriptFont { Typeface = "" }
                ),
                new A.MinorFont(
                    new A.LatinFont { Typeface = "Calibri" },
                    new A.EastAsianFont { Typeface = "" },
                    new A.ComplexScriptFont { Typeface = "" }
                )
            );

            static A.Outline MakeLine(int width)
            {
                var ln = new A.Outline
                {
                    Width = width,
                    CapType = A.LineCapValues.Flat,
                    CompoundLineType = A.CompoundLineValues.Single,
                    Alignment = A.PenAlignmentValues.Center
                };
                ln.Append(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }));
                return ln;
            }

            var fillStyleList = new A.FillStyleList(
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Light1 }),
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Light2 })
            );

            var lineStyleList = new A.LineStyleList(
                MakeLine(9525),
                MakeLine(12700),
                MakeLine(19050)
            );

            var effectStyleList = new A.EffectStyleList(
                new A.EffectStyle(new A.EffectList()),
                new A.EffectStyle(new A.EffectList()),
                new A.EffectStyle(new A.EffectList())
            );

            var bgFillStyleList = new A.BackgroundFillStyleList(
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Light1 }),
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Light2 }),
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })
            );

            var formatScheme = new A.FormatScheme { Name = "Office" };
            formatScheme.Append(fillStyleList, lineStyleList, effectStyleList, bgFillStyleList);

            var themeElements = new A.ThemeElements(colorScheme, fontScheme, formatScheme);

            return new A.Theme
            {
                Name = "Office Theme",
                ThemeElements = themeElements
            };
        }
        private static SlideLayout CreateMinimalSlideLayout()
        {
            var shapeTree = new ShapeTree(
                new NonVisualGroupShapeProperties(
                    new NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()
                ),
                new GroupShapeProperties(new A.TransformGroup())
            );

            return new SlideLayout(
                new CommonSlideData(shapeTree),
                new ColorMapOverride(new A.MasterColorMapping())
            )
            {
                Type = SlideLayoutValues.TextAndObject
            };
        }
        private static Slide BuildSlide(PptSlide s)
        {
            var shapeTree = new ShapeTree(
                new NonVisualGroupShapeProperties(
                    new NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()
                ),
                new GroupShapeProperties(new A.TransformGroup())
            );

            var layoutKey = NormalizeLayout(s.Layout);
            var layout = LayoutEngine.Get(layoutKey);

            // SECTION
            if (layoutKey == "section")
            {
                shapeTree.Append(CreateTextBoxSingle(
                    id: 2U,
                    name: "SectionTitle",
                    text: s.Title ?? "",
                    rect: layout.TitleRect,
                    fontSize: layout.TitleFontSize,
                    bold: true,
                    hAlign: A.TextAlignmentTypeValues.Center,
                    vAnchor: A.TextAnchoringTypeValues.Center
                ));

                return new Slide(new CommonSlideData(shapeTree), new ColorMapOverride(new A.MasterColorMapping()));
            }

            // TITLE / TITLE_CONTENT : titre
            shapeTree.Append(CreateTextBoxSingle(
                id: 2U,
                name: "Title",
                text: s.Title ?? "",
                rect: layout.TitleRect,
                fontSize: layout.TitleFontSize,
                bold: true,
                hAlign: layout.TitleHAlign,
                vAnchor: layout.TitleVAnchor
            ));

            // Zone contenu / subtitle
            if (s.Bullets is { Count: > 0 })
            {
                if (layoutKey == "title")
                {
                    // Sur title slide : on traite les bullets comme sous-titre (plus petit, centré)
                    shapeTree.Append(CreateTextBoxBullets(
                        id: 3U,
                        name: "Subtitle",
                        lines: s.Bullets,
                        rect: layout.ContentRect,
                        fontSize: layout.ContentFontSize,
                        baseLevel: 0,
                        hAlign: A.TextAlignmentTypeValues.Center
                    ));
                }
                else
                {
                    // title_content
                    shapeTree.Append(CreateTextBoxBullets(
                        id: 3U,
                        name: "Content",
                        lines: s.Bullets,
                        rect: layout.ContentRect,
                        fontSize: layout.ContentFontSize,
                        baseLevel: 0,
                        hAlign: A.TextAlignmentTypeValues.Left
                    ));
                }
            }

            return new Slide(new CommonSlideData(shapeTree), new ColorMapOverride(new A.MasterColorMapping()));
        }
        private static NotesSlide BuildNotes(string notes)
        {
            var shapeTree = new ShapeTree(
                new NonVisualGroupShapeProperties(
                    new NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()
                ),
                new GroupShapeProperties(new A.TransformGroup(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = 0, Cy = 0 },
                    new A.ChildOffset { X = 0, Y = 0 },
                    new A.ChildExtents { Cx = 0, Cy = 0 }
                ))
            );

            // Placeholder image de la slide (standard)
            shapeTree.Append(CreateNotesPlaceholder(
                id: 2U,
                name: "Slide Image Placeholder",
                placeholderType: PlaceholderValues.SlideImage,
                text: "",
                rect: new Rect(457200, 457200, 5943600, 3000000)
            ));

            // Placeholder notes (LE TRUC IMPORTANT)
            shapeTree.Append(CreateNotesPlaceholder(
                id: 3U,
                name: "Notes Body Placeholder",
                placeholderType: PlaceholderValues.Body,
                text: notes ?? "",
                rect: new Rect(457200, 3600000, 5943600, 4629600)
            ));

            return new NotesSlide(
                new CommonSlideData(shapeTree),
                new ColorMapOverride(new A.MasterColorMapping())
            );
        }
        private static Shape CreateNotesPlaceholder(uint id, string name, PlaceholderValues placeholderType, string text, Rect rect)
        {
            var nvSpPr = new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = id, Name = name },
                new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(
                    // ✅ ceci produit <p:nvPr><p:ph type="body"/></p:nvPr>
                    new PlaceholderShape { Type = placeholderType }
                )
            );

            var spPr = new ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = rect.X, Y = rect.Y },
                    new A.Extents { Cx = rect.Cx, Cy = rect.Cy }
                ),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
            );

            var tb = new TextBody(
                new A.BodyProperties { Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Top },
                new A.ListStyle(),
                new A.Paragraph(
                    new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
                    new A.Run(
                        new A.RunProperties { Language = "fr-FR", FontSize = 2000 },
                        new A.Text(text ?? "")
                    ),
                    new A.EndParagraphRunProperties { Language = "fr-FR" }
                )
            );

            return new Shape(nvSpPr, spPr, tb);
        }
        private static class LayoutEngine
        {
            // marges confort 16:9
            private const long MarginX = 914400;  // 1"
            private const long MarginTop = 457200; // 0.5"
            private const long GapY = 228600;     // 0.25"

            public static LayoutSpec Get(string layoutKey)
            {
                return layoutKey switch
                {
                    "title" => Title(),
                    "section" => Section(),
                    _ => TitleContent()
                };
            }

            private static LayoutSpec Title()
            {
                // Titre centré au milieu haut, et sous-titre en dessous, centré.
                var titleH = 1500000;
                var subtitleH = 1800000;

                var titleRect = new Rect(
                    X: MarginX,
                    Y: 1500000,
                    Cx: SlideW - 2 * MarginX,
                    Cy: titleH
                );

                var contentRect = new Rect(
                    X: MarginX + 457200, // un peu plus étroit
                    Y: titleRect.Y + titleRect.Cy + GapY,
                    Cx: SlideW - 2 * (MarginX + 457200),
                    Cy: subtitleH
                );

                return new LayoutSpec(
                    TitleRect: titleRect,
                    ContentRect: contentRect,
                    TitleFontSize: 5200,
                    ContentFontSize: 2800,
                    TitleHAlign: A.TextAlignmentTypeValues.Center,
                    TitleVAnchor: A.TextAnchoringTypeValues.Center
                );
            }

            private static LayoutSpec Section()
            {
                // Gros titre centré verticalement (zone large au milieu)
                var titleRect = new Rect(
                    X: MarginX,
                    Y: 2000000,
                    Cx: SlideW - 2 * MarginX,
                    Cy: 2500000
                );

                // content rect pas utilisé
                return new LayoutSpec(
                    TitleRect: titleRect,
                    ContentRect: default,
                    TitleFontSize: 6000,
                    ContentFontSize: 0,
                    TitleHAlign: A.TextAlignmentTypeValues.Center,
                    TitleVAnchor: A.TextAnchoringTypeValues.Center
                );
            }

            private static LayoutSpec TitleContent()
            {
                // Titre en haut, contenu en dessous sur large zone 16:9
                var titleH = 900000;

                var titleRect = new Rect(
                    X: MarginX,
                    Y: MarginTop,
                    Cx: SlideW - 2 * MarginX,
                    Cy: titleH
                );

                var contentRect = new Rect(
                    X: MarginX,
                    Y: titleRect.Y + titleRect.Cy + GapY,
                    Cx: SlideW - 2 * MarginX,
                    Cy: SlideH - (titleRect.Y + titleRect.Cy + GapY) - MarginTop
                );

                return new LayoutSpec(
                    TitleRect: titleRect,
                    ContentRect: contentRect,
                    TitleFontSize: 3600,
                    ContentFontSize: 2400,
                    TitleHAlign: A.TextAlignmentTypeValues.Left,
                    TitleVAnchor: A.TextAnchoringTypeValues.Top
                );
            }
        }
        private static string NormalizeLayout(string? layout)
        {
            var l = (layout ?? "").Trim().ToLowerInvariant();
            return l switch
            {
                "title" => "title",
                "section" => "section",
                "title_content" => "title_content",
                "" => "title_content",
                _ => "title_content"
            };
        }
        private static Shape CreateTextBoxSingle(uint id, string name, string text, Rect rect, int fontSize, bool bold, A.TextAlignmentTypeValues hAlign, A.TextAnchoringTypeValues vAnchor)
        {
            var pPr = new A.ParagraphProperties { Alignment = hAlign };

            var rPr = new A.RunProperties
            {
                Language = "fr-FR",
                FontSize = fontSize,
                Bold = bold
            };

            var textBody = new TextBody(
                new A.BodyProperties
                {
                    Wrap = A.TextWrappingValues.Square,
                    Anchor = vAnchor
                },
                new A.ListStyle(),
                new A.Paragraph(
                    pPr,
                    new A.Run(rPr, new A.Text(text ?? "")),
                    new A.EndParagraphRunProperties { Language = "fr-FR" }
                )
            );

            return CreateShape(id, name, rect, textBody);
        }
        private static Shape CreateTextBoxBullets(uint id, string name, List<string> lines, Rect rect, int fontSize, int baseLevel, A.TextAlignmentTypeValues hAlign)
        {
            var textBody = new TextBody(
                new A.BodyProperties
                {
                    Wrap = A.TextWrappingValues.Square,
                    Anchor = A.TextAnchoringTypeValues.Top
                },
                new A.ListStyle()
            );

            foreach (var (level, text) in ParseIndentedLines(lines, baseLevel))
            {
                var pPr = new A.ParagraphProperties { Alignment = hAlign };

                // Niveau (0..8)
                pPr.Level = Math.Clamp(level, 0, 8);

                // Retraits
                // 0.35" ~ 320040 EMU (on prend 342900 pour rester cohérent)
                var step = 342900;
                var lvl = pPr.Level.Value;

                pPr.LeftMargin = step * (lvl + 1);
                pPr.Indent = -step;

                // Puce
                pPr.Append(
                    new A.BulletFont { Typeface = "Calibri" },
                    new A.CharacterBullet { Char = "•" }
                );

                var rPr = new A.RunProperties
                {
                    Language = "fr-FR",
                    FontSize = fontSize
                };

                textBody.Append(new A.Paragraph(
                    pPr,
                    new A.Run(rPr, new A.Text(text)),
                    new A.EndParagraphRunProperties { Language = "fr-FR" }
                ));
            }

            if (!textBody.Elements<A.Paragraph>().Any())
                textBody.Append(new A.Paragraph(new A.Run(new A.Text(""))));

            return CreateShape(id, name, rect, textBody);
        }
        private static Shape CreateShape(uint id, string name, Rect rect, TextBody textBody)
        {
            return new Shape(
                new NonVisualShapeProperties(
                    new NonVisualDrawingProperties { Id = id, Name = name },
                    new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties()
                ),
                new ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = rect.X, Y = rect.Y },
                        new A.Extents { Cx = rect.Cx, Cy = rect.Cy }
                    ),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                ),
                textBody
            );
        }
        private static IEnumerable<(int level, string text)> ParseIndentedLines(List<string> lines, int baseLevel)
        {
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                int i = 0;
                while (i + 1 < raw.Length && raw[i] == ' ' && raw[i + 1] == ' ')
                    i += 2;

                var level = baseLevel + (i / 2);
                var text = raw.TrimStart();

                yield return (level, text);
            }
        }
        #endregion
    }
    #endregion

    #endregion

    #region Classes et outils de support

    #region Interface
    public interface IFileCreator
    {
        FileType Type { get; }
        string DefaultExtension { get; } // pptx/docx/xlsx
        Task<CreateFileResult> CreateAsync(CreateFileRequest request, string exportDir);
    }
    #endregion

    #region Enum
    public enum FileType { PowerPoint, Word, Excel }
    #endregion

    #region Records
    public record CreateFileRequest(FileType FileType, string SpecJson, string? FileName, string? OptionsJson);
    public record CreateFileResult(bool Ok, FileType FileType, string? FilePath, int Count, string? Error);
    #endregion

    #region Registry
    public sealed class FileCreatorRegistry
    {
        private readonly Dictionary<FileType, IFileCreator> _creators;

        public FileCreatorRegistry(IEnumerable<IFileCreator> creators)
            => _creators = creators.ToDictionary(c => c.Type);

        public bool TryGet(FileType type, out IFileCreator creator) => _creators.TryGetValue(type, out creator!);
    }
    #endregion

    #endregion

}
