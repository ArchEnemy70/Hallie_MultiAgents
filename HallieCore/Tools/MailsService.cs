using ExternalServices;
using HallieDomain;
using MailKit;
using MailKit.Security;
using MimeKit;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hallie.Tools
{
    #region MailSendTool
    public class MailSendTool : ITool
    {
        public string Name => "send_mail";

        public string Description => "Permet d’envoyer des messages par email.";
        private readonly MailsService _service;
        public MailSendTool()
        {
            LoggerService.LogInfo("MailSendTool");
            _service = new MailsService();
        }
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("MailSendTool.ExecuteAsync");

            try
            {
                await Task.Delay(1);
                var subject = parameters["subject"].ToString() ?? "";
                var content = parameters["content"].ToString() ?? "";
                var dest = parameters["destinataires"].ToString() ?? "";
                var pj = parameters.ContainsKey("attachments")
                    ? parameters["attachments"].ToString()
                    : null;
                //var pj = parameters["attachments"].ToString() ?? "";

                var destinataires = dest.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(d => d.Trim())
                                       .ToList();

                var attachments = new List<string>();
                if(pj != null)
                {
                    attachments = pj.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(d => d.Trim())
                               .ToList();
                }

                var (bOk, reponse) = await _service.ExecuteSendMailAsync(content, subject, destinataires, attachments);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = reponse
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = reponse,
                    error = ""
                });
            }

            catch (Exception ex)
            {
                return JsonService.Serialize(new
                {
                    ok = false,
                    reponse = "",
                    error = ex.Message
                });
            }
        }
        public ToolParameter[] GetParameters()
        {
            return new[]
            {
                new ToolParameter
                {
                    Name = "subject",
                    Type = "string",
                    Description = "Le sujet du mail à envoyer",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "destinataires",
                    Type = "string",
                    Description = "Destinataires du mail à envoyer séparés par ; ou ,",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "content",
                    Type = "string",
                    Description = "Le contenu du mail à envoyer",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "attachments",
                    Type = "string",
                    Description = "Pièces-jointes à inclure dans le mail à envoyer séparés par ; ou ,",
                    Required = false
                }
            };
        }

    }
    #endregion

    #region MailSuiviTool
    public class MailSuiviTool : ITool
    {
        public string Name => "suivi_mails";

        public string Description => "Permet de connaitre le suivi des mails reçus et des mails envoyés.";
        private readonly MailsService _service;
        public MailSuiviTool()
        {
            LoggerService.LogInfo("MailSuiviTool");
            _service = new MailsService();
        }
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("MailSuiviTool.ExecuteAsync");

            try
            {
                await Task.Delay(1);
                var isWithDetail_str = parameters["isWithDetail"].ToString() ?? "0";
                var isWithDetail = (isWithDetail_str.ToLower() == "true");

                #region L'état des lieux
                var nbRelancesEnAttente = MailsService.NbRelancesEnAttenteEnRetard();
                var (nbMailsNonLus, nbMailsImportantsUrgents, nbMailsImportants, nbMailsUrgents) = MailsService.NbMessagesNonLus();
                StringBuilder sb = new();

                if (nbRelancesEnAttente > 0 || nbMailsNonLus > 0)
                {
                    sb.AppendLine("Précise que :");
                }
                if (nbRelancesEnAttente > 0)
                {
                    sb.AppendLine($"Il y a {nbRelancesEnAttente} relances en attente en retard dans vos échanges de mails.");
                }
                if (nbMailsNonLus > 0)
                {
                    sb.AppendLine($"Il y a {nbMailsNonLus} mails non lus dans la boite mail.");
                    if (nbMailsImportantsUrgents > 0)
                    {
                        sb.AppendLine($"dont {nbMailsImportantsUrgents} mails importants et urgents.");
                    }
                    if (nbMailsImportants > 0)
                    {
                        sb.AppendLine($"dont {nbMailsImportants} mails importants.");
                    }
                    if (nbMailsUrgents > 0)
                    {
                        sb.AppendLine($"dont {nbMailsUrgents} mails urgents.");
                    }
                }
                #endregion

                #region Avec les détails si demandé
                if(isWithDetail && nbMailsNonLus > 0)
                {
                    sb.AppendLine("Voici un résumé des mails non lus :");
                    var lst = MailsService.ChargerEmailsNonLus();
                    foreach (var mail in lst)
                    {
                        var txt = $"De {mail.From} du {mail.Date.Date} - Résumé : {mail.Resume}";
                        var important = mail.IsImportant ? "important" : "";
                        var urgent = mail.IsUrgent ? "urgent" : "";
                        sb.AppendLine($"{txt} {important} {urgent}");
                        sb.AppendLine("");
                    }

                }
                #endregion
                //return paragrapheMails;
                var bOk = true;

                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = sb.ToString()
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = sb.ToString(),
                    error = ""
                });
            }

            catch (Exception ex)
            {
                return JsonService.Serialize(new
                {
                    ok = false,
                    reponse = "",
                    error = ex.Message
                });
            }
        }
        public ToolParameter[] GetParameters()
        {
            return new[]
            {
                new ToolParameter
                {
                    Name = "isWithDetail",
                    Type = "boolean",
                    Description = "Indique si il faut fournir le détail des mails non lus",
                    Required = true
                }
            };
        }

    }
    #endregion

    #region Service destion des mails
    public class MailsService
    {
        public MailsService()
        {

        }

        #region Envoi de mails
        public async Task<(bool,string)> ExecuteSendMailAsync(string message, string sujet, List<string> destinataires, List<string>? attachments = null)
        {
            LoggerService.LogDebug($"MailsService.ExecuteSendMailAsync :\n destinataires : {string.Join(",", destinataires)}\nsujet : {sujet}\n message : {message}");
            var (b, s) = await SendMessageAsync(message, sujet, destinataires, attachments);
            if (b)
            {
                LoggerService.LogDebug($"MailsService.ExecuteSendMailAsync : envoyé avec succès");
                return (b,$"Message envoyé par mail:\n{message}.");
            }
            else
            {
                LoggerService.LogDebug($"MailsService.ExecuteSendMailAsync : Erreur lors de l'envoi du message par mail --> {s}");
                return (b,$"Erreur lors de l'envoi du message par mail : {s}");
            }
        }

        private async Task<(bool, string)> SendMessageAsync(string message, string sujet, List<string> destinataires, List<string>? attachments=null)
        {

            CompteMailUser? CompteMail = new();
            message = $"Bonjour,\n{message}\n\nPassez une agréable journée,\n{CompteMail.UserNomPrenom}\n{CompteMail.UserEntreprise}";

            var mail = new MimeMessage();
            mail.Subject = sujet;
            mail.From.Add(MailboxAddress.Parse(CompteMail.UserAdresse)); // expéditeur
            foreach (var dest in destinataires)
            {
                mail.To.Add(MailboxAddress.Parse(dest));
            }


            // Version texte brut (fallback)
            var plainText = new TextPart("plain")
            {
                Text = message
            };

            // Version HTML 
            // on remplace les sauts de ligne par des paragraphes <p>
            var htmlParagraphs = string.Join("",
                        message
                            .Split('\n')
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => $"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>")
                    );

            string signatureHtml = "";
            var htmlText = new TextPart("html")
            {
                Text = $"<html><body style='font-family: Calibri, sans-serif; font-size: 12pt;'>{htmlParagraphs + signatureHtml}</body></html>"
            };


            // Regroupe les deux versions (plain + html)
            var alternative = new MultipartAlternative();
            alternative.Add(plainText);
            alternative.Add(htmlText);

            var mixed = new Multipart("mixed");
            mixed.Add(alternative);

            // Ajout des pièces jointes
            if (attachments != null && attachments.Count > 0)
            {
                foreach (var filePath in attachments) // IEnumerable<string>
                {
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"Pièce jointe introuvable : {filePath}");

                    var attachment = new MimePart(MimeTypes.GetMimeType(filePath))
                    {
                        Content = new MimeContent(File.OpenRead(filePath)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = Path.GetFileName(filePath)
                    };

                    mixed.Add(attachment);
                }
            }


            mail.Body = mixed;

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(CompteMail.ServeurSmtp, CompteMail.ServeurSmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(CompteMail.UserAdresse, CompteMail.UserMotPasse);
            await smtp.SendAsync(mail);
            await smtp.DisconnectAsync(true);


            // --- Sauvegarde dans "Sent" via IMAP ---
            using var imap = new MailKit.Net.Imap.ImapClient();
            await imap.ConnectAsync(CompteMail.ServeurImap, CompteMail.ServeurImapPort, SecureSocketOptions.SslOnConnect);
            await imap.AuthenticateAsync(CompteMail.UserAdresse, CompteMail.UserMotPasse);

            // Accès à la boîte aux lettres personnelle (root)
            var personal = imap.GetFolder(imap.PersonalNamespaces[0]);

            // Récupérer le dossier "Sent"
            var sentFolder = await personal.GetSubfolderAsync("Sent");

            // Si ça ne marche pas (par exemple dossier en français "Envoyés") :
            if (sentFolder == null || !sentFolder.Exists)
            {
                var allFolders = await personal.GetSubfoldersAsync(false);
                sentFolder = allFolders.FirstOrDefault(f => f.Name.Equals("Sent", StringComparison.OrdinalIgnoreCase)
                                                          || f.Name.Equals("Envoyés", StringComparison.OrdinalIgnoreCase));
            }

            // Si on a bien trouvé un dossier "Sent" ou équivalent
            if (sentFolder != null)
            {
                await sentFolder.OpenAsync(FolderAccess.ReadWrite);
                await sentFolder.AppendAsync(mail, MessageFlags.Seen);
            }
            return (true, "");


        }
        #endregion

        #region Méthodes statiques

        #region Mails non lus
        public static (int,int,int, int) NbMessagesNonLus()
        {
            int total = 0;
            int importants_et_urgents = 0;
            int important = 0;
            int urgents= 0;
            var lst = ChargerEmailsNonLus();
            total = lst.Count;
            if (total > 0)
            {
                foreach (var email in lst)
                {
                    if (email != null)
                    {
                        if (email.IsImportant && email.IsUrgent)
                            importants_et_urgents++;
                        if (email.IsImportant && !email.IsUrgent)
                            important++;
                        if (!email.IsImportant && email.IsUrgent)
                            urgents++;
                    }
                }
            }
            return (total, importants_et_urgents, important, urgents);
        }
        public static ObservableCollection<EmailMessage> ChargerEmailsNonLus()
        {
            LoggerService.LogInfo("MailsService.ChargerEmailsNonLus");
            string NomFichierMails = Params.JsonMailsNonLus!;// @"D:\_Projets\Projets\IA_RAG_LLM\5. ReAct_Ollama\ReAct_PME_PUBLISH\ListesItems\listeMails.json";

            if (!File.Exists(NomFichierMails))
                return new ObservableCollection<EmailMessage>();

            using FileStream fs = File.OpenRead(NomFichierMails);
            var items = JsonSerializer.Deserialize<ObservableCollection<EmailMessage>>(fs);

            if (items == null)
            {
                return new ObservableCollection<EmailMessage>();
            }
            return items;
        }
        #endregion

        #region Suivi des mails 
        public static int NbRelancesEnAttenteEnRetard()
        {
            var lstReponsesEnAttente = LstRelancesEnAttenteEnRetard();
            return lstReponsesEnAttente.Count;
        }
        private static List<ThreadItem> LstRelancesEnAttenteEnRetard()
        {
            var store = LoadMailsSend();
            var lstReponsesEnAttente = store.Threads
                .Where(t => !t.SubjectClosed)      // Sujet non fermé
                .Where(t => t.IsOverdue)           // En retard
                .ToList();
            return lstReponsesEnAttente;
        }
        private static ThreadStore LoadMailsSend()
        {
            string FullFileName = Params.JsonMailsSuivi!;// @"D:\_Projets\Projets\IA_RAG_LLM\5. ReAct_Ollama\ReAct_PME_PUBLISH\ListesItems\listeMailsSend.json";
            if (!File.Exists(FullFileName)) return new ThreadStore();
            var json = File.ReadAllText(FullFileName);
            return JsonSerializer.Deserialize<ThreadStore>(json) ?? new ThreadStore();
        }
        #endregion

        #endregion
    }
    #endregion

    #region Classes annexes
    public class CompteMailUser
    {
        public string UserAdresse { get; set; } = $"{Params.SmtpUser}";
        public string UserMotPasse { get; set; } = $"{Params.SmtpPass}";
        public string ServeurImap { get; set; } = $"{Params.ImapHost}";
        public int ServeurImapPort { get; set; } = Params.ImapPort;
        public string ServeurSmtp { get; set; } = $"{Params.SmtpHost}";
        public int ServeurSmtpPort { get; set; } = Params.SmtpPort;

        public string UserNomPrenom { get; set; } = $"{Params.AvatarName}";
        public string UserEntreprise { get; set; } = $"De la part de {Params.NomPrenom}";
    }
    public class ThreadStore
    {
        public List<ThreadItem> Threads { get; set; } = new();
    }
    public class EmailMessage
    {
        [JsonIgnore]
        public MailKit.UniqueId? Uid { get; set; } // Pour charger le corps plus tard

        [JsonPropertyName("UidString")]
        public string? UidString
        {
            get => Uid != null ? Uid?.ToString() : null;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (MailKit.UniqueId.TryParse(value, out var parsed))
                        Uid = parsed;
                    else
                        Uid = null; // ou log une erreur
                }
                else
                {
                    Uid = null;
                }
            }
        }
        /*
        public string UidString
        {
            get => Uid.ToString();
            set => Uid = UniqueId.Parse(value);
        }
        */
        public string Id { get; set; } = "";
        public string To { get; set; } = "";
        public string Cc { get; set; } = "";
        public bool IsDestinatairePrincipal { get; set; } = false;
        public string From { get; set; } = "";
        public string FromName { get; set; } = "";
        public string Subject { get; set; } = "";
        public string InReplyTo { get; set; } = "";
        public DateTime Date { get; set; }
        public string DateSTR { get => Date.ToString("dd/MM/yyyy HH:mm"); }

        public string Analyse { get; set; } = "";

        public string Preview { get; set; } = "";
        public string TextBody { get; set; } = "";
        public string TextBodyHTML { get; set; } = "";

        [JsonIgnore]
        public string TextBody_PJ
        {
            get
            {
                if (ContentPJ == null || ContentPJ.Count == 0)
                    return TextBody;

                var joined = string.Join("\n---\n", ContentPJ);
                return $"{TextBody}\n\n--- Contenu des pièces jointes ---\n{joined}";
            }
        }

        [JsonIgnore]
        public string ContentPJ_STR
        {
            get
            {
                if (ContentPJ == null || ContentPJ.Count == 0)
                    return "";

                var joined = string.Join("\n---\n", ContentPJ);
                return $"--- Contenu des pièces jointes ---\n{joined}";
            }
        }

        public string Resume { get; set; } = "";
        public string Categorie { get; set; } = "";
        public string Strategie { get; set; } = "";
        public string Reponse { get; set; } = "";
        public bool IsImportant { get; set; } = false;
        public bool IsUrgent { get; set; } = false;
        public int ImportanceScore { get; set; } = 0; // de 0 à 5
        public int UrgenceScore { get; set; } = 0;    // de 0 à 5

        public List<string>? ContentPJ { get; set; } = new();
        public int PJ_NBR
        {
            get
            {
                if (ContentPJ == null || ContentPJ.Count == 0)
                    return 0;
                return ContentPJ.Count;
            }
        }

        [JsonIgnore]
        public string PJ_NBR_STR
        {
            get
            {
                if (PJ_NBR > 0)
                    return "📎";
                return "";
            }
        }

        public bool IsPJ { get => ContentPJ != null && ContentPJ.Count > 0; }

        public string Priorite { get => $"Important : {(IsImportant ? "✔️" : "❌")} - Urgent : {(IsUrgent ? "⚠️" : "❌")}"; }
        public string ImportanceSTR => $"{ImportanceScore}/5";
        public string UrgenceSTR => $"{UrgenceScore}/5";

        public string ModeleIA { get; set; } = "";

        [JsonIgnore]
        public bool ToRemove { get; set; } = false;

        public bool PresenceSuspecte { get; set; } = false;

        [JsonIgnore]
        public string PresenceSuspecte_STR
        {
            get
            {
                if (PresenceSuspecte)
                    return "⚠️";
                return "";
            }
        }

        [JsonIgnore]
        public string IsDestinatairePrincipal_STR
        {
            get
            {
                if (IsDestinatairePrincipal)
                    return "📩";
                else
                    return "👥";
            }
        }
        [JsonIgnore]
        public string IsDestinatairePrincipal_STR_TXT
        {
            get
            {
                if (IsDestinatairePrincipal)
                    return "Vous êtes destinataire principal de ce mail";
                else
                    return "Vous êtes en copie de ce mail";
            }
        }
    }
    public class ThreadItem
    {
        #region Constructeurs
        //Necessaire pour la déserialisation en json
        public ThreadItem()
        {
            OverdueDays = 7;
        }

        //Necessaire pour passer le paramètre lors de la création d'objets
        public ThreadItem(int overdueDays = 7)
        {
            OverdueDays = overdueDays;
        }
        #endregion

        #region Méthode privée
        private bool IsOverDueThread()
        {
            var t = this;
            if (t.LastFollowUpForMessageId == null || t.FollowUpDraft == null)
            {
                return false;
            }
            var m = t.Messages.Where(m => m.Id == LastFollowUpForMessageId).FirstOrDefault();
            if (m == null)
            {
                return false;
            }
            if (m.Date < DateTimeOffset.UtcNow.AddDays(-1 * OverdueDays))
                return true;
            else
                return false;
        }
        #endregion

        #region Variable
        [JsonIgnore]
        public int OverdueDays { get; set; } = 7;
        #endregion

        #region Propriétés
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Subject { get; set; } = "";
        public List<MailMessageItem> Messages { get; set; } = new();
        public string? FollowUpDraft { get; set; }
        public string? FollowUpDraft2 { get; set; }
        public string? LastFollowUpForMessageId { get; set; }
        public bool SubjectClosed { get; set; } = false;
        public DateTimeOffset DateMessage { get; set; }
        public DateTimeOffset DateLastMessage { get; set; }
        [JsonIgnore]
        public string DateMessageSTR { get => DateMessage.ToString("dd/MM/yyyy HH:mm"); }
        [JsonIgnore]
        public string DateLastMessageSTR { get => DateLastMessage.ToString("dd/MM/yyyy HH:mm"); }
        [JsonIgnore]
        public IEnumerable<MailMessageItem> MessagesSorted => Messages.OrderBy(m => m.Date.UtcDateTime);
        [JsonIgnore]
        public bool IsOverdue
        {
            get
            {
                return IsOverDueThread();
            }
        }
        #endregion
    }
    public class MailMessageItem
    {
        public string Id { get; set; } = "";
        public string InReplyTo { get; set; } = "";
        /*[JsonIgnore]
        public string UidString
        {
            get
            {
                return Uid.ToString();
            }
            set
            {
                if (value != "0")
                    Uid = UniqueId.Parse(value);
            }
        }*/
        public string Folder { get; set; } = "";
        public string Subject { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Direction { get; set; } = "out"; // "in" ou "out"
        public DateTimeOffset Date { get; set; }
        public string Body { get; set; } = "";
        public bool RequiresResponse { get; set; }
        public bool CoversAllPoints { get; set; }
        //[JsonIgnore]
        //public UniqueId Uid { get; set; }
        [JsonIgnore]
        public string DateSTR { get => $"Le : {Date.DateTime.ToString("dd/MM/yyyy HH:mm")}"; }
        [JsonIgnore]
        public string FromSTR { get => $"De : {From}"; }
        [JsonIgnore]
        public string ToSTR { get => $"Pour : {To}"; }
        [JsonIgnore]
        public string FolderSTR { get => $"Dossier : {Folder}"; }
        [JsonIgnore]
        public string SubjectSTR { get => $"Sujet : {Subject}"; }
    }
    #endregion
}
