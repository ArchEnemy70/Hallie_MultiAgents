USE [master]
GO
/****** Object:  Database [Hallie]    Script Date: 21/03/2026 10:13:27 ******/
CREATE DATABASE [Hallie]
USE [Hallie]
GO
/****** Object:  Table [dbo].[AgentMemoryLog]    Script Date: 21/03/2026 10:13:27 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AgentMemoryLog](
	[Id] [uniqueidentifier] NOT NULL,
	[CreatedAtUtc] [datetime2](3) NOT NULL,
	[AgentName] [nvarchar](128) NOT NULL,
	[UserPrompt] [nvarchar](max) NULL,
	[ParametersJson] [nvarchar](max) NOT NULL,
	[Result] [nvarchar](max) NULL,
	[IsSuccess] [bit] NOT NULL,
	[Why] [nvarchar](512) NULL,
	[TechnicalSuccess] [bit] NULL,
	[TaskSuccess] [bit] NULL,
	[OutcomeKind] [nvarchar](64) NULL,
	[CorrelationId] [uniqueidentifier] NULL,
	[StepIndex] [int] NULL,
 CONSTRAINT [PK_AgentMemoryLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FeedbackLog]    Script Date: 21/03/2026 10:13:27 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FeedbackLog](
	[Id] [uniqueidentifier] NOT NULL,
	[CreatedAtUtc] [datetime2](3) NOT NULL,
	[ConversationId] [nvarchar](128) NOT NULL,
	[TurnId] [int] NOT NULL,
	[UserRating] [int] NOT NULL,
	[Outcome] [nvarchar](16) NOT NULL,
	[ErrorClass] [nvarchar](32) NULL,
	[ToolUsed] [nvarchar](64) NOT NULL,
	[ToolParamsHash] [char](64) NOT NULL,
	[ToolParamsJson] [nvarchar](max) NOT NULL,
	[ExpectedTool] [nvarchar](64) NULL,
	[Comment] [nvarchar](max) NULL,
	[PromptText] [nvarchar](max) NOT NULL,
	[StepIndex] [int] NULL,
 CONSTRAINT [PK_FeedbackLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ToolRoutingRules]    Script Date: 21/03/2026 10:13:27 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ToolRoutingRules](
	[RuleId] [uniqueidentifier] NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	[CreatedAtUtc] [datetime2](3) NOT NULL,
	[Name] [nvarchar](128) NOT NULL,
	[ConditionJson] [nvarchar](max) NOT NULL,
	[Tool] [nvarchar](64) NOT NULL,
	[ScoreDelta] [int] NOT NULL,
	[Source] [nvarchar](16) NOT NULL,
	[Confidence] [decimal](5, 4) NOT NULL,
 CONSTRAINT [PK_ToolRoutingRules] PRIMARY KEY CLUSTERED 
(
	[RuleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AgentMemoryLog_AgentName_CreatedAtUtc]    Script Date: 21/03/2026 10:13:27 ******/
CREATE NONCLUSTERED INDEX [IX_AgentMemoryLog_AgentName_CreatedAtUtc] ON [dbo].[AgentMemoryLog]
(
	[AgentName] ASC,
	[CreatedAtUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FeedbackLog_Conversation_Turn]    Script Date: 21/03/2026 10:13:27 ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackLog_Conversation_Turn] ON [dbo].[FeedbackLog]
(
	[ConversationId] ASC,
	[TurnId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FeedbackLog_Outcome_ErrorClass]    Script Date: 21/03/2026 10:13:27 ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackLog_Outcome_ErrorClass] ON [dbo].[FeedbackLog]
(
	[Outcome] ASC,
	[ErrorClass] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FeedbackLog_ToolUsed_CreatedAt]    Script Date: 21/03/2026 10:13:27 ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackLog_ToolUsed_CreatedAt] ON [dbo].[FeedbackLog]
(
	[ToolUsed] ASC,
	[CreatedAtUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ToolRoutingRules_Enabled_Tool]    Script Date: 21/03/2026 10:13:27 ******/
CREATE NONCLUSTERED INDEX [IX_ToolRoutingRules_Enabled_Tool] ON [dbo].[ToolRoutingRules]
(
	[IsEnabled] ASC,
	[Tool] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ToolRoutingRules_Source]    Script Date: 21/03/2026 10:13:27 ******/
CREATE NONCLUSTERED INDEX [IX_ToolRoutingRules_Source] ON [dbo].[ToolRoutingRules]
(
	[Source] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
USE [master]
GO
ALTER DATABASE [Hallie] SET  READ_WRITE 
GO

USE [Hallie]
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'd6ac3698-fed1-49e0-893e-146f9af66e07', CAST(N'2026-03-09T06:08:05.1170000' AS DateTime2), N'882cf50a-0d78-49d5-9191-31f537873e33', 1, 1, N'failed', N'bad_result', N'extract_file', N'25dab56e59a58e738b8043e684d5505843dd788ce2de9ad8d491e7ec22639701', N'{"fullfilename":"D:\\_Projets\\_Exemples\\Hallie\\Hallie\\bin\\x64\\Debug\\net10.0-windows7.0\\revue_presse.html"}', NULL, N'Mauvais choix', N'donne moi la revue de presse', 1)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'adf28d7a-eae1-42a7-9023-1cdf8b52e6d9', CAST(N'2026-03-21T07:59:29.7370000' AS DateTime2), N'd76d3f6b-e4c1-4c8a-8edd-1b8bcf247658', 1, 5, N'solved', NULL, N'coder_helper', N'd6a432d4858c982f1dc2a934ff3e48d2cd05660e05abee6ae4aa3faa64b6eeaf', N'[{"ParametersJson":"{\u0022query\u0022:\u0022D\\u00E9boguer et corriger la m\\u00E9thode C# suivante. Corriger le type de retour, \\u00E9viter la division par z\\u00E9ro et retourner le r\\u00E9sultat sous forme d\\u0027entier.\u0022,\u0022language\u0022:\u0022csharp\u0022,\u0022code_existant\u0022:\u0022public int CalculHelperTool()\\n{\\n    var nb = 10;\\n    var resultat = nb / (nb-10);\\n    return $\\u0022Le r\\u00E9sultat est {resultat}\\u0022;\\n}\u0022}","ToolName":"coder_helper"}]', NULL, N'Bon choix', N'peux tu m''aider à débogger cette méthode en c#:
        public int CalculHelperTool()
        {
            var nb = 10;
   var resultat = nb / (nb-10);
   return $"Le résultat est {resultat}";
        }', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'af8f3bb9-340c-42fa-b65b-1df147fe7cb1', CAST(N'2026-03-09T06:08:05.0970000' AS DateTime2), N'882cf50a-0d78-49d5-9191-31f537873e33', 1, 5, N'solved', NULL, N'press_review', N'44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a', N'{}', NULL, N'Bon choix d''outil', N'donne moi la revue de presse', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'd743ce20-b697-44be-805e-201ccd0782eb', CAST(N'2026-03-07T06:33:46.5630000' AS DateTime2), N'336bb1e4-8f26-4cb7-ac90-0d7b235d60fb', 1, 5, N'solved', NULL, N'create_bureatique_file', N'aa41382df534fccc04d3b991e8ead47d10373c8d9bc87fc75e0ba9dd192afc91', N'{"fileName":"Conges_Decembre_2025.xlsx","fileType":"excel","openFile":"1","specJson":"{\u0022title\u0022:\u0022Cong\u00E9s D\u00E9cembre 2025\u0022,\u0022sheets\u0022:[{\u0022name\u0022:\u0022Liste Cong\u00E9s\u0022,\u0022columns\u0022:[\u0022Nom\u0022,\u0022Prenom\u0022,\u0022Date Depart\u0022,\u0022Date Fin\u0022,\u0022Type Absence\u0022],\u0022rows\u0022:[[\u0022DOR\u0022,\u0022Benoit\u0022,\u002226/12/2025\u0022,\u002226/12/2025\u0022,\u0022Cong\u00E9s pay\u00E9s\u0022],[\u0022TOPENOT\u0022,\u0022Guillaume\u0022,\u002218/12/2025\u0022,\u002219/12/2025\u0022,\u0022RTT\u0022],[\u0022GARD\u0022,\u0022Caroline\u0022,\u002222/12/2025\u0022,\u002202/01/2026\u0022,\u0022RTT\u0022],[\u0022TOPENOT\u0022,\u0022Guillaume\u0022,\u002222/12/2025\u0022,\u002202/01/2026\u0022,\u0022Cong\u00E9s pay\u00E9s\u0022],[\u0022SERR\u0022,\u0022Jonathan\u0022,\u002215/12/2025\u0022,\u002202/01/2026\u0022,\u0022Cong\u00E9s pay\u00E9s\u0022],[\u0022GARD\u0022,\u0022Caroline\u0022,\u002203/12/2025\u0022,\u002203/12/2025\u0022,\u0022Cong\u00E9s pay\u00E9s\u0022],[\u0022TRAUCH\u0022,\u0022Maelan\u0022,\u002226/12/2025\u0022,\u002226/12/2025\u0022,\u0022Cong\u00E9s pay\u00E9s\u0022]],\u0022freezeHeader\u0022:true,\u0022autoFilter\u0022:true}]}"}', NULL, N'Bon choix', N'Quels sont les salariés en congés en décembre 2025 (donne NOM + PRENOM + DATE DEPART + DATE FIN + TYPE D''ABSENCE) ? Ensuite génére un fichier excel avec ces données', 1)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'25fc50ea-4299-4d1d-bb77-4a25b2a2c764', CAST(N'2026-03-10T09:03:09.6630000' AS DateTime2), N'b2323e26-6505-490f-b1d1-829beba32c91', 1, 5, N'solved', NULL, N'send_mail', N'e31dbb80ff19864e658223ee52334c638d674cfd5eba87227c446eef3ebcd2af', N'{"attachments":"D:\\_TrainingData\\Documents\\Generated\\Absent_Dec2025.xlsx","content":"Bonne r\u00E9ception","destinataires":"guillaume.topenot@gmail.com","subject":"Liste des absents"}', NULL, N'Bon choix d''outil', N'quels sont les salariés en congé en décembre 2025 (indique NOM + PRENOM). Ensuite, génère un fichier excel avec ces données. Ensuite, envoie ce fichier excel par mail à "guillaume.topenot@gmail.com" avec corps du mail : "Bonne réception" et objet du mail "Liste des absents"', 2)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'8f9742d2-a923-4311-ab08-602553b31e02', CAST(N'2026-03-21T05:59:54.3870000' AS DateTime2), N'd25c800f-643e-4041-aa13-e1e5fe54e78a', 1, 1, N'failed', N'bad_result', N'none', N'4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945', N'[]', NULL, N'hallucination. Tu n''as pas accès à ces outils', N'donne moi la liste des outils qui sont à ta disposition', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'b59c5763-516b-413f-be90-61ee7dfd2d3b', CAST(N'2026-03-20T04:50:14.6530000' AS DateTime2), N'1c7012ec-c308-4bf0-b1ff-4cde2bae17a2', 1, 5, N'solved', NULL, N'press_review', N'954f24552b157b03e738b1c13976dd18b86c480aba3be83143ec27b6fa75276d', N'[{"ParametersJson":"{}","ToolName":"press_review"}]', NULL, NULL, N'donne moi la revue de presse', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'c05fbc71-94d8-4deb-9e58-65c3162ba59d', CAST(N'2026-03-07T06:33:44.3730000' AS DateTime2), N'336bb1e4-8f26-4cb7-ac90-0d7b235d60fb', 1, 5, N'solved', NULL, N'sql_query>create_bureatique_file', N'71e751ddc98f4db1dae81598216e62cb1a0abe2a3e0de225009b4e483002a28d', N'[{"ParametersJson":"{\u0022bddname\u0022:\u0022Temporis\u0022,\u0022query\u0022:\u0022SELECT wf.LAST_NAME AS NOM, wf.FIRST_NAME AS PRENOM, d.DATE_START_DAY_OFF AS DATE_DEPART, d.DATE_END_DAY_OFF AS DATE_FIN, t.LIBELLE_DAY_OFF_TYPE AS TYPE_ABSENCE FROM dbo.DT_DAY_OFF d JOIN dbo.DT_WORK_FORCE wf ON d.ID_WORK_FORCE = wf.ID_WORK_FORCE JOIN dbo.DT_REF_DAY_OFF_TYPE t ON d.ID_DAY_OFF_TYPE = t.ID_DAY_OFF_TYPE WHERE d.DATE_START_DAY_OFF \\u003C= \\u00272025-12-31\\u0027 AND (d.DATE_END_DAY_OFF IS NULL OR d.DATE_END_DAY_OFF \\u003E= \\u00272025-12-01\\u0027) AND d.IsEnabled = 1 AND wf.IsEnabled = 1\u0022}","ToolName":"sql_query"},{"ParametersJson":"{\u0022fileType\u0022:\u0022excel\u0022,\u0022specJson\u0022:\u0022{\\u0022title\\u0022:\\u0022Cong\\u00E9s D\\u00E9cembre 2025\\u0022,\\u0022sheets\\u0022:[{\\u0022name\\u0022:\\u0022Liste Cong\\u00E9s\\u0022,\\u0022columns\\u0022:[\\u0022Nom\\u0022,\\u0022Prenom\\u0022,\\u0022Date Depart\\u0022,\\u0022Date Fin\\u0022,\\u0022Type Absence\\u0022],\\u0022rows\\u0022:[[\\u0022DOR\\u0022,\\u0022Benoit\\u0022,\\u002226/12/2025\\u0022,\\u002226/12/2025\\u0022,\\u0022Cong\\u00E9s pay\\u00E9s\\u0022],[\\u0022TOPENOT\\u0022,\\u0022Guillaume\\u0022,\\u002218/12/2025\\u0022,\\u002219/12/2025\\u0022,\\u0022RTT\\u0022],[\\u0022GARD\\u0022,\\u0022Caroline\\u0022,\\u002222/12/2025\\u0022,\\u002202/01/2026\\u0022,\\u0022RTT\\u0022],[\\u0022TOPENOT\\u0022,\\u0022Guillaume\\u0022,\\u002222/12/2025\\u0022,\\u002202/01/2026\\u0022,\\u0022Cong\\u00E9s pay\\u00E9s\\u0022],[\\u0022SERR\\u0022,\\u0022Jonathan\\u0022,\\u002215/12/2025\\u0022,\\u002202/01/2026\\u0022,\\u0022Cong\\u00E9s pay\\u00E9s\\u0022],[\\u0022GARD\\u0022,\\u0022Caroline\\u0022,\\u002203/12/2025\\u0022,\\u002203/12/2025\\u0022,\\u0022Cong\\u00E9s pay\\u00E9s\\u0022],[\\u0022TRAUCH\\u0022,\\u0022Maelan\\u0022,\\u002226/12/2025\\u0022,\\u002226/12/2025\\u0022,\\u0022Cong\\u00E9s pay\\u00E9s\\u0022]],\\u0022freezeHeader\\u0022:true,\\u0022autoFilter\\u0022:true}]}\u0022,\u0022fileName\u0022:\u0022Conges_Decembre_2025.xlsx\u0022,\u0022openFile\u0022:\u00221\u0022}","ToolName":"create_bureatique_file"}]', NULL, N'Bon enchainement d''outils', N'Quels sont les salariés en congés en décembre 2025 (donne NOM + PRENOM + DATE DEPART + DATE FIN + TYPE D''ABSENCE) ? Ensuite génére un fichier excel avec ces données', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'6befe7b4-bd31-4009-b07c-67ac315e32b8', CAST(N'2026-03-07T08:00:58.7900000' AS DateTime2), N'52711e4e-cb32-461f-9664-27e22fde3426', 2, 1, N'failed', N'bad_result', N'none', N'4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945', N'[]', NULL, N'Hallucination : Ce ne sont pas les outils que le systeme met à ta disposition', N'liste moi les outils à ta disposition', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'1c15d723-51c4-4c1a-bd4c-72993805d9b4', CAST(N'2026-03-21T07:36:06.0630000' AS DateTime2), N'ec842210-1cdf-4a64-b948-346fc484085d', 1, 1, N'failed', N'bad_result', N'create_bureatique_file', N'46c4da4d027af422f96b38aea2d99e35626c2049b594ea356442ec8823eea88f', N'{"fileName":"FilesService_traduction.txt","fileType":"txt","openFile":"1","specJson":"{\u0022title\u0022: \u0022Traduction C# vers Python\u0022, \u0022content\u0022: \u0022import os, shutil, zipfile, tempfile, subprocess, logging\\n\\nclass FilesService:\\n    @staticmethod\\n    def CreateZip(zipFileName, path=\\\u0022\\\u0022, isShow=False):\\n        try:\\n            summaries = []\\n            if not path:\\n                extractDirectory = os.path.join(tempfile.gettempdir(), f\u0027zip_{os.urandom(16).hex()}\u0027)\\n            else:\\n                extractDirectory = path\\n            os.makedirs(extractDirectory, exist_ok=True)\\n            if os.path.isfile(zipFileName):\\n                os.remove(zipFileName)\\n            shutil.make_archive(base_name=zipFileName.replace(\u0027.zip\u0027,\u0027\u0027), format=\u0027zip\u0027, root_dir=extractDirectory)\\n            summaries.append(f\u0027Dossier d\\\u0027extraction : {extractDirectory}\u0027)\\n            if isShow:\\n                subprocess.run([\u0027explorer.exe\u0027, zipFileName], check=False)\\n            return True, \u0027\\n\u0027.join(summaries)\\n        except Exception as ex:\\n            logging.error(f\u0027FilesService.CreateZip : {ex}\u0027)\\n            return False, str(ex)\\n\\n    @staticmethod\\n    def ExtractZip(zipFileName, summaryFiles=False, deleteDirectory=True, path=\\\u0022\\\u0022, isShow=False):\\n        try:\\n            summaries = []\\n            if not path:\\n                extractDirectory = os.path.join(tempfile.gettempdir(), f\u0027zip_{os.urandom(16).hex()}\u0027)\\n            else:\\n                extractDirectory = path\\n            os.makedirs(extractDirectory, exist_ok=True)\\n            with zipfile.ZipFile(zipFileName, \u0027r\u0027) as zip_ref:\\n                zip_ref.extractall(extractDirectory)\\n            if summaryFiles:\\n                for root, _, files in os.walk(extractDirectory):\\n                    for file in files:\\n                        innerFile = os.path.join(root, file)\\n                        logging.info(f\u0027Traitement de la pi\u00E8ce jointe dans {zipFileName} : {file}\u0027)\\n                        try:\\n                            ext = os.path.splitext(innerFile)[1].lower()\\n                            if FilesService.IsExtensionSupport(ext):\\n                                summaries.append(FilesService.ExtractText(innerFile))\\n                            else:\\n                                summaries.append(f\u0027\uD83D\uDCCE {file} :\\n Format de fichier non pris en charge... \u0027)\\n                        except Exception:\\n                            summaries.append(f\u0027\uD83D\uDCCE {file} :\\n Fichier illisible... \u0027)\\n            if deleteDirectory:\\n                shutil.rmtree(extractDirectory)\\n            else:\\n                summaries.append(f\u0027Dossier d\\\u0027extraction : {extractDirectory}\u0027)\\n                if isShow:\\n                    subprocess.run([\u0027explorer.exe\u0027, extractDirectory], check=False)\\n            return True, \u0027\\n\u0027.join(summaries)\\n        except Exception as ex:\\n            logging.error(f\u0027FilesService.ExtractZip : {ex}\u0027)\\n            return False, str(ex)\\n\\n    @staticmethod\\n    def IsExtensionSupport(extension):\\n        return extension in [\u0027.pdf\u0027, \u0027.docx\u0027, \u0027.xlsx\u0027, \u0027.txt\u0027, \u0027.csv\u0027, \u0027.rtf\u0027]\\n\\n    @staticmethod\\n    def ExtractTextFromBytes(bytes_data, fileName):\\n        ext = os.path.splitext(fileName)[1].lower()\\n        if ext == \u0027.pdf\u0027:\\n            return PdfService.ExtractPdfFromBytesNew(bytes_data)\\n        elif ext == \u0027.docx\u0027:\\n            return DocxService.ExtractDocxFromBytes(bytes_data)\\n        elif ext == \u0027.xlsx\u0027:\\n            return XlsxService.ExtractXlsxFromBytes(bytes_data)\\n        elif ext == \u0027.txt\u0027:\\n            return bytes_data.decode(\u0027utf-8\u0027)\\n        else:\\n            raise NotImplementedError(f\u0027Type non support\u00E9 : {ext}\u0027)\\n\\n    @staticmethod\\n    def ExtractText(fileFullname):\\n        logging.info(f\u0027FilesService.ExtractText : {fileFullname}\u0027)\\n        if not fileFullname:\\n            return \u0027\u0027\\n        ext = os.path.splitext(fileFullname)[1].lower()\\n        if ext == \u0027.pdf\u0027:\\n            return PdfService.ExtractTextFromPdf(fileFullname)\\n        elif ext == \u0027.docx\u0027:\\n            return DocxService.ExtractTextFromDocx(fileFullname)\\n        elif ext == \u0027.xlsx\u0027:\\n            return XlsxService.ExtractTextFromXlsx(fileFullname)\\n        elif ext == \u0027.txt\u0027:\\n            with open(fileFullname, \u0027r\u0027, encoding=\u0027utf-8\u0027) as f:\\n                return f.read()\\n        elif ext == \u0027.csv\u0027:\\n            return XlsxService.ExtractTextFromCsv(fileFullname)\\n        elif ext == \u0027.rtf\u0027:\\n            return DocxService.ExtractTextFromRtf(fileFullname)\\n        else:\\n            return TxtService.ExtractTextFromTxt(fileFullname)\\n\\n    @staticmethod\\n    def GetListesFichiers(fichiers):\\n        logging.info(\u0027FilesService.GetListesFichiers\u0027)\\n        extensionsImages = FilesService.ImagesExtensions()\\n        images = [f for f in fichiers if os.path.splitext(f)[1].lower() in extensionsImages]\\n        autres = [f for f in fichiers if os.path.splitext(f)[1].lower() not in extensionsImages]\\n        if images:\\n            logging.debug(f\u0027{len(images)} image(s) :\u0027)\\n            for d in images:\\n                logging.debug(d)\\n        if autres:\\n            logging.debug(f\u0027{len(autres)} document(s) :\u0027)\\n            for d in autres:\\n                logging.debug(f\u0027  - {d}\u0027)\\n        return images, autres\\n\\n    @staticmethod\\n    def ImagesExtensions():\\n        return [\u0027.jpg\u0027, \u0027.jpeg\u0027, \u0027.png\u0027, \u0027.gif\u0027, \u0027.bmp\u0027, \u0027.tiff\u0027, \u0027.webp\u0027]\\n\u0022}"}', NULL, NULL, N'voici un code c# que tu dois traduire en python. Genere un fichier texte avec cette traduction:
using System.Diagnostics;
using System.Text;

namespace ExternalServices
{
    public static class FilesService
    {
        public static (bool, string) CreateZip(string zipFileName, string path = "", bool isShow=false)
        {
            try
            {
                var summaries = new StringBuilder();

                var extractDirectory = "";
                if (path == "")
                    extractDirectory = Path.Combine(Path.GetTempPath(), "zip_" + Guid.NewGuid());
                else
                    extractDirectory = path;

                if (!Directory.Exists(extractDirectory))
                    Directory.CreateDirectory(extractDirectory);

                if (File.Exists(zipFileName))
                    File.Delete(zipFileName);

                System.IO.Compression.ZipFile.CreateFromDirectory(extractDirectory, zipFileName);


                summaries.AppendLine($"Dossier d''extraction : {extractDirectory}");
                if (isShow)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = zipFileName,
                        UseShellExecute = true
                    });
                }
                return (true, summaries.ToString());
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FilesService.ExtractZip : {ex.Message}");
                return (false, ex.Message);
            }
        }
        public static (bool, string) ExtractZip(string zipFileName, bool summaryFiles=false, bool deleteDirectory=true, string path="", bool isShow=false)
        {
            try
            {
                var summaries = new StringBuilder();

                var extractDirectory = "";
                if (path == "")
                    extractDirectory = Path.Combine(Path.GetTempPath(), "zip_" + Guid.NewGuid());
                else
                    extractDirectory = path;

                if(!Directory.Exists(extractDirectory))
                    Directory.CreateDirectory(extractDirectory);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipFileName, extractDirectory);
                if (summaryFiles)
                {
                    foreach (var innerFile in Directory.GetFiles(extractDirectory, "*.*", SearchOption.AllDirectories))
                    {
                        var filenameInZip = Path.GetFileName(innerFile);
                        LoggerService.LogInfo($"Traitement de la pièce jointe dans {zipFileName} : {filenameInZip}");

                        try
                        {
                            var ext = Path.GetExtension(innerFile).ToLowerInvariant();

                            if (IsExtensionSupport(ext))
                            {
                                summaries.AppendLine(ExtractText(innerFile));
                            }
                            else
                            {
                                summaries.AppendLine($"📎 {filenameInZip} :\n Format de fichier non pris en charge... ");
                            }
                        }
                        catch
                        {
                            summaries.AppendLine($"📎 {filenameInZip} :\n Fichier illisible... ");
                        }
                    }
                }
                if (deleteDirectory)
                {
                    Directory.Delete(extractDirectory, true);
                }
                else
                {
                    summaries.AppendLine($"Dossier d''extraction : {extractDirectory}");
                    if (isShow)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = extractDirectory,
                            UseShellExecute = true
                        });
                    }
                }
                return (true, summaries.ToString());
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FilesService.ExtractZip : {ex.Message}");
                return (false, ex.Message);
            }
        }
        public static bool IsExtensionSupport(string extension)
        {
            return extension is ".pdf" or ".docx" or ".xlsx" or ".txt" or ".csv" or ".rtf";
        }

        public static string ExtractTextFromBytes(byte[] bytes, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            return ext switch
            {
                ".pdf" => PdfService.ExtractPdfFromBytesNew(bytes),
                ".docx" => DocxService.ExtractDocxFromBytes(bytes),
                ".xlsx" => XlsxService.ExtractXlsxFromBytes(bytes),
                ".txt" => Encoding.UTF8.GetString(bytes),
                _ => throw new NotSupportedException($"Type non supporté : {ext}")
            };
        }

        public static string ExtractText(string fileFullname)
        {
            LoggerService.LogInfo($"FilesService.ExtractText : {fileFullname}");

            if (string.IsNullOrEmpty(fileFullname))
                return string.Empty;

            string ext = System.IO.Path.GetExtension(fileFullname).ToLower();
            return ext switch
            {
                ".pdf" => PdfService.ExtractTextFromPdf(fileFullname),
                ".docx" => DocxService.ExtractTextFromDocx(fileFullname),
                ".xlsx" => XlsxService.ExtractTextFromXlsx(fileFullname),
                ".txt" => File.ReadAllText(fileFullname),
                ".csv" => XlsxService.ExtractTextFromCsv(fileFullname),
                ".rtf" => DocxService.ExtractTextFromRtf(fileFullname),
                _ => TxtService.ExtractTextFromTxt(fileFullname)
            };
        }

        public static (List<string>, List<string>) GetListesFichiers(List<string> fichiers)
        {
            LoggerService.LogInfo($"FilesService.GetListesFichiers");

            // Extensions d’images acceptées
            string[] extensionsImages = ImagesExtensions();

            // Séparer les images
            var images = fichiers
                .Where(f => extensionsImages.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            var autres = fichiers
                .Where(f => !extensionsImages.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            if (images.Count > 0)
            {
                LoggerService.LogDebug($"{images.Count} image(s) : ");
                foreach (var d in images)
                {
                    LoggerService.LogDebug($"{d}");
                }
            }

            if (autres.Count > 0)
            {
                LoggerService.LogDebug($"{autres.Count} document(s) : ");
                foreach (var d in autres)
                {
                    LoggerService.LogDebug($"  - {d}");
                }
            }
            return (images, autres);
        }

        private static string[] ImagesExtensions()
        {
            string[] extensionsImages = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            return extensionsImages;
        }
    }
}
', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'48803604-ffe1-488f-8eb0-92015982746a', CAST(N'2026-03-20T08:49:51.2170000' AS DateTime2), N'c6d38567-3748-4122-a658-068739dd762b', 1, 5, N'solved', NULL, N'get_weather', N'963d5d9bad841dbfa5ba363a065aac90d54ad0fdca1b2e7656817e81837b9091', N'[{"ParametersJson":"{\u0022location\u0022:\u0022Sauve\u0022,\u0022include_forecast\u0022:false}","ToolName":"get_weather"}]', NULL, NULL, N'quelle est la météo à Sauve', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'c8dcd1cc-6d4d-44b7-a778-94e50dd2f839', CAST(N'2026-03-10T09:03:09.5370000' AS DateTime2), N'b2323e26-6505-490f-b1d1-829beba32c91', 1, 5, N'solved', NULL, N'sql_query', N'b0f10070a629c07833b1285f9e4df341542622dd45ec5d5da6cde1a2b929767a', N'{"bddname":"Temporis","query":"SELECT wf.LAST_NAME, wf.FIRST_NAME FROM dbo.DT_WORK_FORCE wf JOIN dbo.DT_DAY_OFF d ON wf.ID_WORK_FORCE = d.ID_WORK_FORCE WHERE d.DATE_START_DAY_OFF \u003C= \u00272025-12-31\u0027 AND d.DATE_END_DAY_OFF \u003E= \u00272025-12-01\u0027"}', NULL, N'Bon choix d''outil', N'quels sont les salariés en congé en décembre 2025 (indique NOM + PRENOM). Ensuite, génère un fichier excel avec ces données. Ensuite, envoie ce fichier excel par mail à "guillaume.topenot@gmail.com" avec corps du mail : "Bonne réception" et objet du mail "Liste des absents"', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'0af82e22-62de-4b73-929a-9a3fca0d5ff1', CAST(N'2026-03-10T09:03:09.6030000' AS DateTime2), N'b2323e26-6505-490f-b1d1-829beba32c91', 1, 5, N'solved', NULL, N'create_bureatique_file', N'b3490de20242485516cfb43a0e718d23d69b62effd0423775e3fefe789a44b22', N'{"fileName":"Absent_Dec2025.xlsx","fileType":"excel","openFile":"0","specJson":"{\u0022title\u0022:\u0022Liste des salari\u00E9s en cong\u00E9 - D\u00E9cembre 2025\u0022,\u0022sheets\u0022:[{\u0022name\u0022:\u0022Cong\u00E9s\u0022,\u0022columns\u0022:[\u0022Nom\u0022,\u0022Prenom\u0022],\u0022rows\u0022:[[\u0022DOR\u0022,\u0022Benoit\u0022],[\u0022TOPENOT\u0022,\u0022Guillaume\u0022],[\u0022GARD\u0022,\u0022Caroline\u0022],[\u0022TOPENOT\u0022,\u0022Guillaume\u0022],[\u0022SERR\u0022,\u0022Jonathan\u0022],[\u0022GARD\u0022,\u0022Caroline\u0022],[\u0022TRAUCH\u0022,\u0022Maelan\u0022]],\u0022freezeHeader\u0022:true,\u0022autoFilter\u0022:true}]}"}', NULL, N'Bon choix d''outil', N'quels sont les salariés en congé en décembre 2025 (indique NOM + PRENOM). Ensuite, génère un fichier excel avec ces données. Ensuite, envoie ce fichier excel par mail à "guillaume.topenot@gmail.com" avec corps du mail : "Bonne réception" et objet du mail "Liste des absents"', 1)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'914914b2-8df5-4251-820a-a39ddc645c4c', CAST(N'2026-03-20T08:49:53.3730000' AS DateTime2), N'c6d38567-3748-4122-a658-068739dd762b', 1, 5, N'solved', NULL, N'get_weather', N'f4dc7cc720689cf88101143025d983e9d07afea0e8cac45907d9b15e349532ad', N'{"include_forecast":false,"location":"Sauve"}', NULL, NULL, N'quelle est la météo à Sauve', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'797faca9-cae4-43a2-8092-b1dd58e4348d', CAST(N'2026-03-09T06:08:00.1470000' AS DateTime2), N'882cf50a-0d78-49d5-9191-31f537873e33', 1, 1, N'failed', N'bad_result', N'press_review>extract_file', N'38773b64d7456668277c78ffb0c37bd9c6426e09e55552a30d911453a388db41', N'[{"ParametersJson":"{}","ToolName":"press_review"},{"ParametersJson":"{\u0022fullfilename\u0022:\u0022D:\\\\_Projets\\\\_Exemples\\\\Hallie\\\\Hallie\\\\bin\\\\x64\\\\Debug\\\\net10.0-windows7.0\\\\revue_presse.html\u0022}","ToolName":"extract_file"}]', NULL, N'La revue de presse est donnée par press_review', N'donne moi la revue de presse', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'695b2492-6af7-4513-87aa-b83b79dacd2d', CAST(N'2026-03-20T04:50:16.7900000' AS DateTime2), N'1c7012ec-c308-4bf0-b1ff-4cde2bae17a2', 1, 5, N'solved', NULL, N'press_review', N'44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a', N'{}', NULL, NULL, N'donne moi la revue de presse', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'fb05c19d-065d-44ce-bf85-bc36a9ca8408', CAST(N'2026-03-07T06:33:46.5070000' AS DateTime2), N'336bb1e4-8f26-4cb7-ac90-0d7b235d60fb', 1, 5, N'solved', NULL, N'sql_query', N'071b2eed9c9e841ab096b009e2bbf768a7bd3ad4a31ca74281f31177fdced22d', N'{"bddname":"Temporis","query":"SELECT wf.LAST_NAME AS NOM, wf.FIRST_NAME AS PRENOM, d.DATE_START_DAY_OFF AS DATE_DEPART, d.DATE_END_DAY_OFF AS DATE_FIN, t.LIBELLE_DAY_OFF_TYPE AS TYPE_ABSENCE FROM dbo.DT_DAY_OFF d JOIN dbo.DT_WORK_FORCE wf ON d.ID_WORK_FORCE = wf.ID_WORK_FORCE JOIN dbo.DT_REF_DAY_OFF_TYPE t ON d.ID_DAY_OFF_TYPE = t.ID_DAY_OFF_TYPE WHERE d.DATE_START_DAY_OFF \u003C= \u00272025-12-31\u0027 AND (d.DATE_END_DAY_OFF IS NULL OR d.DATE_END_DAY_OFF \u003E= \u00272025-12-01\u0027) AND d.IsEnabled = 1 AND wf.IsEnabled = 1"}', NULL, N'Bon choix', N'Quels sont les salariés en congés en décembre 2025 (donne NOM + PRENOM + DATE DEPART + DATE FIN + TYPE D''ABSENCE) ? Ensuite génére un fichier excel avec ces données', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'35d3777e-f7a8-49dc-bfbf-c21513f46312', CAST(N'2026-03-09T06:10:32.0630000' AS DateTime2), N'46b97bf2-6703-46dd-bf45-80971330a065', 1, 5, N'solved', NULL, N'press_review', N'954f24552b157b03e738b1c13976dd18b86c480aba3be83143ec27b6fa75276d', N'[{"ParametersJson":"{}","ToolName":"press_review"}]', NULL, N'Bon choix', N'donne moi la revue de presse', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'8a1b517f-d26f-44f1-bfe4-cfe853225361', CAST(N'2026-03-09T06:10:34.1870000' AS DateTime2), N'46b97bf2-6703-46dd-bf45-80971330a065', 1, 5, N'solved', NULL, N'press_review', N'44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a', N'{}', NULL, N'Bon choix', N'donne moi la revue de presse', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'3aee247e-5098-4efd-9bad-d08a83eff06c', CAST(N'2026-03-21T07:59:31.8970000' AS DateTime2), N'd76d3f6b-e4c1-4c8a-8edd-1b8bcf247658', 1, 5, N'solved', NULL, N'coder_helper', N'ce231161d5c9e0e103907a9808b68472094edea7c2656c6cce840ebce735f02f', N'{"code_existant":"public int CalculHelperTool()\n{\n    var nb = 10;\n    var resultat = nb / (nb-10);\n    return $\u0022Le r\u00E9sultat est {resultat}\u0022;\n}","language":"csharp","query":"D\u00E9boguer et corriger la m\u00E9thode C# suivante. Corriger le type de retour, \u00E9viter la division par z\u00E9ro et retourner le r\u00E9sultat sous forme d\u0027entier."}', NULL, N'Bon choix', N'peux tu m''aider à débogger cette méthode en c#:
        public int CalculHelperTool()
        {
            var nb = 10;
   var resultat = nb / (nb-10);
   return $"Le résultat est {resultat}";
        }', 0)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'a7f8df96-52eb-42ae-bd96-da57517b9038', CAST(N'2026-03-21T07:36:03.6600000' AS DateTime2), N'ec842210-1cdf-4a64-b948-346fc484085d', 1, 1, N'failed', N'bad_result', N'create_bureatique_file', N'd0fcd3354206774b3b2601cfeb3ab989fcc897cea753fc8a49506aa0d9d19f13', N'[{"ParametersJson":"{\u0022fileType\u0022:\u0022txt\u0022,\u0022specJson\u0022:\u0022{\\u0022title\\u0022: \\u0022Traduction C# vers Python\\u0022, \\u0022content\\u0022: \\u0022import os, shutil, zipfile, tempfile, subprocess, logging\\\\n\\\\nclass FilesService:\\\\n    @staticmethod\\\\n    def CreateZip(zipFileName, path=\\\\\\u0022\\\\\\u0022, isShow=False):\\\\n        try:\\\\n            summaries = []\\\\n            if not path:\\\\n                extractDirectory = os.path.join(tempfile.gettempdir(), f\\u0027zip_{os.urandom(16).hex()}\\u0027)\\\\n            else:\\\\n                extractDirectory = path\\\\n            os.makedirs(extractDirectory, exist_ok=True)\\\\n            if os.path.isfile(zipFileName):\\\\n                os.remove(zipFileName)\\\\n            shutil.make_archive(base_name=zipFileName.replace(\\u0027.zip\\u0027,\\u0027\\u0027), format=\\u0027zip\\u0027, root_dir=extractDirectory)\\\\n            summaries.append(f\\u0027Dossier d\\\\\\u0027extraction : {extractDirectory}\\u0027)\\\\n            if isShow:\\\\n                subprocess.run([\\u0027explorer.exe\\u0027, zipFileName], check=False)\\\\n            return True, \\u0027\\\\n\\u0027.join(summaries)\\\\n        except Exception as ex:\\\\n            logging.error(f\\u0027FilesService.CreateZip : {ex}\\u0027)\\\\n            return False, str(ex)\\\\n\\\\n    @staticmethod\\\\n    def ExtractZip(zipFileName, summaryFiles=False, deleteDirectory=True, path=\\\\\\u0022\\\\\\u0022, isShow=False):\\\\n        try:\\\\n            summaries = []\\\\n            if not path:\\\\n                extractDirectory = os.path.join(tempfile.gettempdir(), f\\u0027zip_{os.urandom(16).hex()}\\u0027)\\\\n            else:\\\\n                extractDirectory = path\\\\n            os.makedirs(extractDirectory, exist_ok=True)\\\\n            with zipfile.ZipFile(zipFileName, \\u0027r\\u0027) as zip_ref:\\\\n                zip_ref.extractall(extractDirectory)\\\\n            if summaryFiles:\\\\n                for root, _, files in os.walk(extractDirectory):\\\\n                    for file in files:\\\\n                        innerFile = os.path.join(root, file)\\\\n                        logging.info(f\\u0027Traitement de la pi\\u00E8ce jointe dans {zipFileName} : {file}\\u0027)\\\\n                        try:\\\\n                            ext = os.path.splitext(innerFile)[1].lower()\\\\n                            if FilesService.IsExtensionSupport(ext):\\\\n                                summaries.append(FilesService.ExtractText(innerFile))\\\\n                            else:\\\\n                                summaries.append(f\\u0027\\uD83D\\uDCCE {file} :\\\\n Format de fichier non pris en charge... \\u0027)\\\\n                        except Exception:\\\\n                            summaries.append(f\\u0027\\uD83D\\uDCCE {file} :\\\\n Fichier illisible... \\u0027)\\\\n            if deleteDirectory:\\\\n                shutil.rmtree(extractDirectory)\\\\n            else:\\\\n                summaries.append(f\\u0027Dossier d\\\\\\u0027extraction : {extractDirectory}\\u0027)\\\\n                if isShow:\\\\n                    subprocess.run([\\u0027explorer.exe\\u0027, extractDirectory], check=False)\\\\n            return True, \\u0027\\\\n\\u0027.join(summaries)\\\\n        except Exception as ex:\\\\n            logging.error(f\\u0027FilesService.ExtractZip : {ex}\\u0027)\\\\n            return False, str(ex)\\\\n\\\\n    @staticmethod\\\\n    def IsExtensionSupport(extension):\\\\n        return extension in [\\u0027.pdf\\u0027, \\u0027.docx\\u0027, \\u0027.xlsx\\u0027, \\u0027.txt\\u0027, \\u0027.csv\\u0027, \\u0027.rtf\\u0027]\\\\n\\\\n    @staticmethod\\\\n    def ExtractTextFromBytes(bytes_data, fileName):\\\\n        ext = os.path.splitext(fileName)[1].lower()\\\\n        if ext == \\u0027.pdf\\u0027:\\\\n            return PdfService.ExtractPdfFromBytesNew(bytes_data)\\\\n        elif ext == \\u0027.docx\\u0027:\\\\n            return DocxService.ExtractDocxFromBytes(bytes_data)\\\\n        elif ext == \\u0027.xlsx\\u0027:\\\\n            return XlsxService.ExtractXlsxFromBytes(bytes_data)\\\\n        elif ext == \\u0027.txt\\u0027:\\\\n            return bytes_data.decode(\\u0027utf-8\\u0027)\\\\n        else:\\\\n            raise NotImplementedError(f\\u0027Type non support\\u00E9 : {ext}\\u0027)\\\\n\\\\n    @staticmethod\\\\n    def ExtractText(fileFullname):\\\\n        logging.info(f\\u0027FilesService.ExtractText : {fileFullname}\\u0027)\\\\n        if not fileFullname:\\\\n            return \\u0027\\u0027\\\\n        ext = os.path.splitext(fileFullname)[1].lower()\\\\n        if ext == \\u0027.pdf\\u0027:\\\\n            return PdfService.ExtractTextFromPdf(fileFullname)\\\\n        elif ext == \\u0027.docx\\u0027:\\\\n            return DocxService.ExtractTextFromDocx(fileFullname)\\\\n        elif ext == \\u0027.xlsx\\u0027:\\\\n            return XlsxService.ExtractTextFromXlsx(fileFullname)\\\\n        elif ext == \\u0027.txt\\u0027:\\\\n            with open(fileFullname, \\u0027r\\u0027, encoding=\\u0027utf-8\\u0027) as f:\\\\n                return f.read()\\\\n        elif ext == \\u0027.csv\\u0027:\\\\n            return XlsxService.ExtractTextFromCsv(fileFullname)\\\\n        elif ext == \\u0027.rtf\\u0027:\\\\n            return DocxService.ExtractTextFromRtf(fileFullname)\\\\n        else:\\\\n            return TxtService.ExtractTextFromTxt(fileFullname)\\\\n\\\\n    @staticmethod\\\\n    def GetListesFichiers(fichiers):\\\\n        logging.info(\\u0027FilesService.GetListesFichiers\\u0027)\\\\n        extensionsImages = FilesService.ImagesExtensions()\\\\n        images = [f for f in fichiers if os.path.splitext(f)[1].lower() in extensionsImages]\\\\n        autres = [f for f in fichiers if os.path.splitext(f)[1].lower() not in extensionsImages]\\\\n        if images:\\\\n            logging.debug(f\\u0027{len(images)} image(s) :\\u0027)\\\\n            for d in images:\\\\n                logging.debug(d)\\\\n        if autres:\\\\n            logging.debug(f\\u0027{len(autres)} document(s) :\\u0027)\\\\n            for d in autres:\\\\n                logging.debug(f\\u0027  - {d}\\u0027)\\\\n        return images, autres\\\\n\\\\n    @staticmethod\\\\n    def ImagesExtensions():\\\\n        return [\\u0027.jpg\\u0027, \\u0027.jpeg\\u0027, \\u0027.png\\u0027, \\u0027.gif\\u0027, \\u0027.bmp\\u0027, \\u0027.tiff\\u0027, \\u0027.webp\\u0027]\\\\n\\u0022}\u0022,\u0022fileName\u0022:\u0022FilesService_traduction.txt\u0022,\u0022openFile\u0022:\u00221\u0022}","ToolName":"create_bureatique_file"}]', NULL, N'Quand l''utilisateur demande de traduire du code informatique, il faut utiliser l''outil coder_helper', N'voici un code c# que tu dois traduire en python. Genere un fichier texte avec cette traduction:
using System.Diagnostics;
using System.Text;

namespace ExternalServices
{
    public static class FilesService
    {
        public static (bool, string) CreateZip(string zipFileName, string path = "", bool isShow=false)
        {
            try
            {
                var summaries = new StringBuilder();

                var extractDirectory = "";
                if (path == "")
                    extractDirectory = Path.Combine(Path.GetTempPath(), "zip_" + Guid.NewGuid());
                else
                    extractDirectory = path;

                if (!Directory.Exists(extractDirectory))
                    Directory.CreateDirectory(extractDirectory);

                if (File.Exists(zipFileName))
                    File.Delete(zipFileName);

                System.IO.Compression.ZipFile.CreateFromDirectory(extractDirectory, zipFileName);


                summaries.AppendLine($"Dossier d''extraction : {extractDirectory}");
                if (isShow)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = zipFileName,
                        UseShellExecute = true
                    });
                }
                return (true, summaries.ToString());
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FilesService.ExtractZip : {ex.Message}");
                return (false, ex.Message);
            }
        }
        public static (bool, string) ExtractZip(string zipFileName, bool summaryFiles=false, bool deleteDirectory=true, string path="", bool isShow=false)
        {
            try
            {
                var summaries = new StringBuilder();

                var extractDirectory = "";
                if (path == "")
                    extractDirectory = Path.Combine(Path.GetTempPath(), "zip_" + Guid.NewGuid());
                else
                    extractDirectory = path;

                if(!Directory.Exists(extractDirectory))
                    Directory.CreateDirectory(extractDirectory);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipFileName, extractDirectory);
                if (summaryFiles)
                {
                    foreach (var innerFile in Directory.GetFiles(extractDirectory, "*.*", SearchOption.AllDirectories))
                    {
                        var filenameInZip = Path.GetFileName(innerFile);
                        LoggerService.LogInfo($"Traitement de la pièce jointe dans {zipFileName} : {filenameInZip}");

                        try
                        {
                            var ext = Path.GetExtension(innerFile).ToLowerInvariant();

                            if (IsExtensionSupport(ext))
                            {
                                summaries.AppendLine(ExtractText(innerFile));
                            }
                            else
                            {
                                summaries.AppendLine($"📎 {filenameInZip} :\n Format de fichier non pris en charge... ");
                            }
                        }
                        catch
                        {
                            summaries.AppendLine($"📎 {filenameInZip} :\n Fichier illisible... ");
                        }
                    }
                }
                if (deleteDirectory)
                {
                    Directory.Delete(extractDirectory, true);
                }
                else
                {
                    summaries.AppendLine($"Dossier d''extraction : {extractDirectory}");
                    if (isShow)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = extractDirectory,
                            UseShellExecute = true
                        });
                    }
                }
                return (true, summaries.ToString());
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FilesService.ExtractZip : {ex.Message}");
                return (false, ex.Message);
            }
        }
        public static bool IsExtensionSupport(string extension)
        {
            return extension is ".pdf" or ".docx" or ".xlsx" or ".txt" or ".csv" or ".rtf";
        }

        public static string ExtractTextFromBytes(byte[] bytes, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            return ext switch
            {
                ".pdf" => PdfService.ExtractPdfFromBytesNew(bytes),
                ".docx" => DocxService.ExtractDocxFromBytes(bytes),
                ".xlsx" => XlsxService.ExtractXlsxFromBytes(bytes),
                ".txt" => Encoding.UTF8.GetString(bytes),
                _ => throw new NotSupportedException($"Type non supporté : {ext}")
            };
        }

        public static string ExtractText(string fileFullname)
        {
            LoggerService.LogInfo($"FilesService.ExtractText : {fileFullname}");

            if (string.IsNullOrEmpty(fileFullname))
                return string.Empty;

            string ext = System.IO.Path.GetExtension(fileFullname).ToLower();
            return ext switch
            {
                ".pdf" => PdfService.ExtractTextFromPdf(fileFullname),
                ".docx" => DocxService.ExtractTextFromDocx(fileFullname),
                ".xlsx" => XlsxService.ExtractTextFromXlsx(fileFullname),
                ".txt" => File.ReadAllText(fileFullname),
                ".csv" => XlsxService.ExtractTextFromCsv(fileFullname),
                ".rtf" => DocxService.ExtractTextFromRtf(fileFullname),
                _ => TxtService.ExtractTextFromTxt(fileFullname)
            };
        }

        public static (List<string>, List<string>) GetListesFichiers(List<string> fichiers)
        {
            LoggerService.LogInfo($"FilesService.GetListesFichiers");

            // Extensions d’images acceptées
            string[] extensionsImages = ImagesExtensions();

            // Séparer les images
            var images = fichiers
                .Where(f => extensionsImages.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            var autres = fichiers
                .Where(f => !extensionsImages.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            if (images.Count > 0)
            {
                LoggerService.LogDebug($"{images.Count} image(s) : ");
                foreach (var d in images)
                {
                    LoggerService.LogDebug($"{d}");
                }
            }

            if (autres.Count > 0)
            {
                LoggerService.LogDebug($"{autres.Count} document(s) : ");
                foreach (var d in autres)
                {
                    LoggerService.LogDebug($"  - {d}");
                }
            }
            return (images, autres);
        }

        private static string[] ImagesExtensions()
        {
            string[] extensionsImages = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            return extensionsImages;
        }
    }
}
', NULL)
GO
INSERT [dbo].[FeedbackLog] ([Id], [CreatedAtUtc], [ConversationId], [TurnId], [UserRating], [Outcome], [ErrorClass], [ToolUsed], [ToolParamsHash], [ToolParamsJson], [ExpectedTool], [Comment], [PromptText], [StepIndex]) VALUES (N'ac792bf4-34cb-4c69-83f1-f83a6330ffb0', CAST(N'2026-03-10T09:03:07.1830000' AS DateTime2), N'b2323e26-6505-490f-b1d1-829beba32c91', 1, 5, N'solved', NULL, N'sql_query>create_bureatique_file>send_mail', N'd4e4454fc92096ee4da75d25a339af1850825fcee471032e6d376c435a902c0a', N'[{"ParametersJson":"{\u0022bddname\u0022:\u0022Temporis\u0022,\u0022query\u0022:\u0022SELECT wf.LAST_NAME, wf.FIRST_NAME FROM dbo.DT_WORK_FORCE wf JOIN dbo.DT_DAY_OFF d ON wf.ID_WORK_FORCE = d.ID_WORK_FORCE WHERE d.DATE_START_DAY_OFF \\u003C= \\u00272025-12-31\\u0027 AND d.DATE_END_DAY_OFF \\u003E= \\u00272025-12-01\\u0027\u0022}","ToolName":"sql_query"},{"ParametersJson":"{\u0022fileType\u0022:\u0022excel\u0022,\u0022specJson\u0022:\u0022{\\u0022title\\u0022:\\u0022Liste des salari\\u00E9s en cong\\u00E9 - D\\u00E9cembre 2025\\u0022,\\u0022sheets\\u0022:[{\\u0022name\\u0022:\\u0022Cong\\u00E9s\\u0022,\\u0022columns\\u0022:[\\u0022Nom\\u0022,\\u0022Prenom\\u0022],\\u0022rows\\u0022:[[\\u0022DOR\\u0022,\\u0022Benoit\\u0022],[\\u0022TOPENOT\\u0022,\\u0022Guillaume\\u0022],[\\u0022GARD\\u0022,\\u0022Caroline\\u0022],[\\u0022TOPENOT\\u0022,\\u0022Guillaume\\u0022],[\\u0022SERR\\u0022,\\u0022Jonathan\\u0022],[\\u0022GARD\\u0022,\\u0022Caroline\\u0022],[\\u0022TRAUCH\\u0022,\\u0022Maelan\\u0022]],\\u0022freezeHeader\\u0022:true,\\u0022autoFilter\\u0022:true}]}\u0022,\u0022fileName\u0022:\u0022Absent_Dec2025.xlsx\u0022,\u0022openFile\u0022:\u00220\u0022}","ToolName":"create_bureatique_file"},{"ParametersJson":"{\u0022subject\u0022:\u0022Liste des absents\u0022,\u0022destinataires\u0022:\u0022guillaume.topenot@gmail.com\u0022,\u0022content\u0022:\u0022Bonne r\\u00E9ception\u0022,\u0022attachments\u0022:\u0022D:\\\\_TrainingData\\\\Documents\\\\Generated\\\\Absent_Dec2025.xlsx\u0022}","ToolName":"send_mail"}]', NULL, N'Respect de l''ordre des instructions', N'quels sont les salariés en congé en décembre 2025 (indique NOM + PRENOM). Ensuite, génère un fichier excel avec ces données. Ensuite, envoie ce fichier excel par mail à "guillaume.topenot@gmail.com" avec corps du mail : "Bonne réception" et objet du mail "Liste des absents"', NULL)
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'352d7f00-710f-4a73-b444-09dcb675d021', 1, CAST(N'2026-03-04T11:46:37.1940000' AS DateTime2), N'Fichier/PJ détecté -> éviter web_fetch', N'{ "has_file": true }', N'web_fetch', -3, N'manual', CAST(0.8500 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'a0a9ade8-6d8b-46f1-b32b-402b0d01acf1', 1, CAST(N'2026-03-04T11:46:37.1940000' AS DateTime2), N'Fichier/PJ détecté -> extract_file', N'{ "has_file": true }', N'extract_file', 6, N'manual', CAST(0.9200 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'c2999101-31e1-4720-bdb4-41572cbc8020', 1, CAST(N'2026-03-04T11:46:44.4420000' AS DateTime2), N'SQL/BDD détecté -> sql_query', N'{ "mentions_sql": true }', N'sql_query', 6, N'manual', CAST(0.9200 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'a0fab7c9-714e-4234-9c68-4fba309e3b2f', 1, CAST(N'2026-03-04T11:46:37.1940000' AS DateTime2), N'Fichier/PJ détecté -> éviter web_search', N'{ "has_file": true }', N'web_search', -4, N'manual', CAST(0.8500 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'0d010dfa-df2e-4591-99f6-6c092aa52f7d', 1, CAST(N'2026-03-04T11:46:21.1860000' AS DateTime2), N'Interne/procédure -> éviter web_search', N'{ "mentions_internal": true }', N'web_search', -4, N'manual', CAST(0.9000 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'9e4b36ea-e072-4e5a-b447-870e15def0de', 1, CAST(N'2026-03-04T11:46:21.1860000' AS DateTime2), N'Interne/procédure -> search_documents', N'{ "mentions_internal": true }', N'search_documents', 6, N'manual', CAST(0.9500 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'd0f4d9e8-b161-46ed-97db-e253dad217cf', 1, CAST(N'2026-03-04T11:46:44.4420000' AS DateTime2), N'SQL/BDD détecté -> éviter search_documents', N'{ "mentions_sql": true }', N'search_documents', -3, N'manual', CAST(0.8000 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'e54f28fa-e654-4c22-a225-e3abfc7cb003', 1, CAST(N'2026-03-04T11:46:29.3960000' AS DateTime2), N'URL présente -> éviter web_search', N'{ "has_url": true }', N'web_search', -3, N'manual', CAST(0.8500 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'3ad38d6a-f688-45db-9fa4-e8f1671f4dba', 1, CAST(N'2026-03-04T11:46:21.1860000' AS DateTime2), N'Interne/procédure -> éviter web_fetch', N'{ "mentions_internal": true }', N'web_fetch', -5, N'manual', CAST(0.9000 AS Decimal(5, 4)))
GO
INSERT [dbo].[ToolRoutingRules] ([RuleId], [IsEnabled], [CreatedAtUtc], [Name], [ConditionJson], [Tool], [ScoreDelta], [Source], [Confidence]) VALUES (N'6b6919f2-4a7a-4218-87db-fe42d5b1847b', 1, CAST(N'2026-03-04T11:46:29.3960000' AS DateTime2), N'URL présente -> web_fetch', N'{ "has_url": true }', N'web_fetch', 5, N'manual', CAST(0.9000 AS Decimal(5, 4)))
GO
