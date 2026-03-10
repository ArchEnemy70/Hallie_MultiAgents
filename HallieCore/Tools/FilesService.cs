using ExternalServices;
using System.Diagnostics;

namespace Hallie.Tools
{
    #region ExtractZipTool
    public class ExtractZipTool : ITool
    {
        public string Name => "extract_zip";
        public string Description => "Outil pour extraire le contenu d'un fichier ZIP.";

        private FilesService _Service;

        public ExtractZipTool()
        {
            LoggerService.LogInfo("ExtractZipTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("ExtractZipTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var fullfilename = parameters["zipfilename"].ToString();
                var path = parameters["path"].ToString();
                var isSummaryFiles = parameters["isSummaryFiles"].ToString() == "1" ? true : false;
                var isDeleteDirectory = parameters["isDeleteDirectory"].ToString() == "1" ? true : false;


                if (fullfilename == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "fullfilename introuvable."
                    });
                }


                var (bOk, txt) = _Service.ExtractZipCommand(fullfilename, isSummaryFiles, isDeleteDirectory, path);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de l'ouverture du document."
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{txt}",
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
                    Name = "zipfilename",
                    Type = "string",
                    Description = "Nom complet d'un fichier zip à décompresser (path + filenename + extension)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "path",
                    Type = "string",
                    Description = "Indique le chemin de décompression : dans quel répertoire sera décompressé le fichier ZIP",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "isSummaryFiles",
                    Type = "string",
                    Description = "Indique si le système doit fournir un résumé des documents inclus dans le fichier ZIP (1=oui | 0=non. Par défaut : 0)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "isDeleteDirectory",
                    Type = "string",
                    Description = "Indique si le système doit supprimer le répertoire où on été décompressé les fichiers contenus dans le fichier ZIP (1=oui | 0=non. Par défaut : 0)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region ExtractFileTool
    public class ExtractFileTool : ITool
    {
        public string Name => "extract_file";
        public string Description => "Outil pour extraire le contenu d'un document.";

        private FilesService _Service;

        public ExtractFileTool()
        {
            LoggerService.LogInfo("ExtractFileTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("ExtractFileTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var fullfilename = parameters["fullfilename"].ToString();

                if (fullfilename == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "fullfilename introuvable."
                    });
                }

                var (bOk, txt) = _Service.ExtractCommand(fullfilename);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de l'ouverture du document."
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{txt}",
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
                    Name = "fullfilename",
                    Type = "string",
                    Description = "Nom complet d'un document (path + filenename + extension)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region OpenFileTool
    public class OpenFileTool : ITool
    {
        public string Name => "open_file";
        public string Description => "Outil pour ouvrir un document.";

        private FilesService _Service;

        public OpenFileTool()
        {
            LoggerService.LogInfo("OpenFileTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("OpenFileTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var fullfilename = parameters["fullfilename"].ToString();

                if (fullfilename == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"fullfilename introuvable"
                    });
                }

                var (bOk, txt) = _Service.OpenCommand(fullfilename);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"{txt}"
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{txt}",
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
                    Name = "fullfilename",
                    Type = "string",
                    Description = "Nom complet d'un document (path + filenename + extension)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region DeleteFileTool
    public class DeleteFileTool : ITool
    {
        public string Name => "delete_file";
        public string Description => "Outil pour supprimer un fichier.";

        private FilesService _Service;

        public DeleteFileTool()
        {
            LoggerService.LogInfo("DeleteFileTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("DeleteFileTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var fullfilename = parameters["fullfilename"].ToString();

                if (fullfilename == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"fullfilename introuvable"
                    });
                }

                var (bOk, txt) = _Service.DeleteCommand(fullfilename);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"{txt}"
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{txt}",
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
                    Name = "fullfilename",
                    Type = "string",
                    Description = "Nom complet d'un fichier (path + filenename + extension)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region RenameFileTool 
    public class RenameFileTool : ITool
    {
        public string Name => "rename_file";
        public string Description => "Outil pour renommer un fichier.";

        private FilesService _Service;

        public RenameFileTool()
        {
            LoggerService.LogInfo("RenameFileTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("RenameFileTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var fullfilenameOld = parameters["fullfilenameOld"].ToString();
                var fullfilenameNew = parameters["fullfilenameNew"].ToString();

                if (fullfilenameOld == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"fullfilenameOld introuvable"
                    });
                }
                if (fullfilenameNew == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"fullfilenameNew introuvable"
                    });
                }

                var (bOk, txt) = _Service.RenameCommand(fullfilenameOld, fullfilenameNew, true);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"{txt}"
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{txt}",
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
                    Name = "fullfilenameOld",
                    Type = "string",
                    Description = "Nom complet du fichier qui va être renommer (path + filenename + extension)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "fullfilenameNew",
                    Type = "string",
                    Description = "Nouveau nom du fichier qui va être renommer (au minimum : filenename + extension)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region CopyFileTool 
    public class CopyFileTool : ITool
    {
        public string Name => "copy_file";
        public string Description => "Outil pour copier un fichier.";

        private FilesService _Service;

        public CopyFileTool()
        {
            LoggerService.LogInfo("CopyFileTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("CopyFileTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var fullfilenameOrigine = parameters["fullfilenameOrigine"].ToString();
                var fullfilenameDestination = parameters["fullfilenameDestination"].ToString();

                if (fullfilenameOrigine == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"fullfilenameOrigine introuvable"
                    });
                }
                if (fullfilenameDestination == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"fullfilenameDestination introuvable"
                    });
                }

                var (bOk, txt) = _Service.CopyCommand(fullfilenameOrigine, fullfilenameDestination, true);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"{txt}"
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{txt}",
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
                    Name = "fullfilenameOrigine",
                    Type = "string",
                    Description = "Nom complet du fichier qui va être copier (path + filenename + extension)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "fullfilenameDestination",
                    Type = "string",
                    Description = "Fichier de destination du fichier qui va être copier (filenename + extension)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region ListeFilesTool
    public class ListeFilesTool : ITool
    {
        public string Name => "liste_files";
        public string Description => "Outil pour lister les fichier d'un répertoire.";

        private FilesService _Service;

        public ListeFilesTool()
        {
            LoggerService.LogInfo("ListeFilesTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("ListeFilesTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var path = parameters["path"].ToString();

                if (path == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"path introuvable"
                    });
                }

                var (bOk, lst) = _Service.ListeCommand(path);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"{string.Join(",",lst)}"
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{string.Join(",", lst)}",
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
                    Name = "path",
                    Type = "string",
                    Description = "Nom du dossier qui contient les fichiers que l'on va lister",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region FindFileTool
    public class FindFileTool : ITool
    {
        public string Name => "find_file";
        public string Description => "Outil pour chercher un ou plusieurs fichiers.";

        private FilesService _Service;

        public FindFileTool()
        {
            LoggerService.LogInfo("FindFileTool");
            _Service = new FilesService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("FindFileTool.ExecuteAsync");

            try
            {
                var pattern = parameters.ContainsKey("pattern") ? parameters["pattern"]?.ToString() : null;
                var rootFolder = parameters.ContainsKey("rootFolder") ? parameters["rootFolder"]?.ToString() : null;

                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "pattern introuvable"
                    });
                }

                var results = new List<string>();

                var asyncEnum = _Service.FindCommandAsync(pattern, string.IsNullOrWhiteSpace(rootFolder) ? null : rootFolder);

                if (asyncEnum == null)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de la recherche (aucune donnée retournée)."
                    });
                }

                await foreach (var item in asyncEnum)
                {
                    if (!string.IsNullOrEmpty(item))
                        results.Add(item);
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{string.Join(",", results)}",
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
                    Name = "pattern",
                    Type = "string",
                    Description = "pattern du ou des fichiers recherchés (toto.txt ou toto.* ou *.txt ou to*.tx*)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "rootFolder",
                    Type = "string",
                    Description = "point de départ de la recherche (si pas explicite, laisser vide)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region Service
    public class FilesService
    {
        #region Méthodes publiques
        public (bool, string) ExtractZipCommand(string zipFilename, bool isSummaryFiles=false, bool isDeleteDirectory=true, string path="")
        {
            var txt = "";
            var b= false;
            try
            {
                (b,txt) = ExternalServices.FilesService.ExtractZip(zipFilename, isSummaryFiles, isDeleteDirectory, path);
                return (b, txt);
            }
            catch (Exception ex)
            {
                return (false, $"{ex.Message} - {txt}");
            }
        }
        public (bool, string) OpenCommand(string fullfilename)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullfilename,
                    UseShellExecute = true
                });
                var filename = System.IO.Path.GetFileName(fullfilename);
                return (true, $"Document {filename} ouvert avec succès");
            }
            catch(Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public (bool, string) ExtractCommand(string fullfilename)
        {
            try
            {
                var txt = ExternalServices.FilesService.ExtractText(fullfilename);
                return (true, txt);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public (bool, string) DeleteCommand(string fullfilename)
        {
            try
            {
                if (File.Exists(fullfilename))
                {
                    File.Delete(fullfilename);
                    return (true, $"Fichier supprimé avec succès : {fullfilename}");
                }
                return (false, $"Le fichier n'existe pas : {fullfilename}.");

            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public (bool, string) RenameCommand(string fullfilenameOld, string filenameNew, bool overwrite = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullfilenameOld))
                    return (false, "Chemin source vide");

                if (!File.Exists(fullfilenameOld))
                    return (false, "Fichier source introuvable");

                if (string.IsNullOrWhiteSpace(filenameNew))
                    return (false, "Nouveau nom vide");

                string finalPath;
                if (Path.IsPathRooted(filenameNew))
                {
                    // Cas : chemin complet (FullFileName)
                    finalPath = filenameNew;
                }
                else
                {
                    // Cas : juste nom de fichier (on garde le dossier d'origine)
                    var originalDir = Path.GetDirectoryName(fullfilenameOld)!;
                    finalPath = Path.Combine(originalDir, filenameNew);
                }
                finalPath = Path.GetFullPath(finalPath);

                if (File.Exists(finalPath))
                {
                    if (!overwrite)
                        return (false, "Le fichier cible existe déjà");

                    File.Delete(finalPath);
                }

                File.Move(fullfilenameOld, finalPath);
                return (true, "Renommage réussi");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public (bool, string) CopyCommand(string fullfilenameOrigin, string filenameDestination, bool overwrite = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullfilenameOrigin))
                    return (false, "Chemin origine vide");

                if (!File.Exists(fullfilenameOrigin))
                    return (false, "Fichier origine introuvable");

                if (string.IsNullOrWhiteSpace(filenameDestination))
                    return (false, "Nom destination vide");

                string finalPath;
                if (Path.IsPathRooted(filenameDestination))
                {
                    // Cas : chemin complet (FullFileName)
                    finalPath = filenameDestination;
                }
                else
                {
                    // Cas : juste nom de fichier (on garde le dossier d'origine)
                    var originalDir = Path.GetDirectoryName(fullfilenameOrigin)!;
                    finalPath = Path.Combine(originalDir, filenameDestination);
                }
                finalPath = Path.GetFullPath(finalPath);

                if (fullfilenameOrigin == finalPath)
                {
                    return (false, "Les fichiers origine et destination ont le même nom");
                }

                if (File.Exists(finalPath))
                {
                    if (!overwrite)
                        return (false, "Le fichier cible existe déjà");

                    File.Delete(finalPath);
                }

                File.Copy(fullfilenameOrigin, finalPath);
                return (true, "Copie réussie");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public (bool, List<string>) ListeCommand(string path)
        {
            List<string> list = new();
            try
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path);
                    list = files.ToList();
                    return (true, list);
                }
                list.Add("Le dossier n'existe pas : {path}.");
                return (false, list);

            }
            catch (Exception ex)
            {
                list.Add($"{ex.Message}");
                return (false, list);
            }
        }

        /// <summary>
        /// Recherche des fichiers par pattern (wildcards) dans un dossier racine.
        /// Pattern: "toto.txt", "toto.*", "*.pdf", etc.
        /// Inclut sous-dossiers. Ignore les dossiers inaccessibles.
        /// </summary>
        public async IAsyncEnumerable<string> FindCommandAsync(string pattern, string? rootFolder = null,
            IProgress<FileSearchProgress>? progress = null, bool includeHiddenAndSystemDirs = true)
        {
            CancellationToken ct = default;
            if (string.IsNullOrWhiteSpace(pattern))
                yield break;

            pattern = pattern.Trim();

            // Détermine les racines : soit rootFolder, soit tous les disques
            var roots = ResolveRoots(rootFolder);

            long dirsVisited = 0;
            long matched = 0;

            await Task.Yield();

            foreach (var root in roots)
            {
                ct.ThrowIfCancellationRequested();

                var stack = new Stack<string>();
                stack.Push(root);

                while (stack.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    var currentDir = stack.Pop();
                    dirsVisited++;

                    progress?.Report(new FileSearchProgress(
                        CurrentRoot: root,
                        CurrentPath: currentDir,
                        DirectoriesVisited: dirsVisited,
                        FilesMatched: matched));

                    // 1) match fichiers
                    IEnumerable<string> files = Array.Empty<string>();
                    try
                    {
                        files = Directory.EnumerateFiles(currentDir, pattern, SearchOption.TopDirectoryOnly);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (IOException) { }
                    catch (Exception) { }

                    foreach (var f in files)
                    {
                        ct.ThrowIfCancellationRequested();
                        matched++;
                        progress?.Report(new FileSearchProgress(root, currentDir, dirsVisited, matched));
                        yield return f;
                    }

                    // 2) sous-dossiers
                    IEnumerable<string> subDirs = Array.Empty<string>();
                    try
                    {
                        subDirs = Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (IOException) { }
                    catch (Exception) { }

                    foreach (var d in subDirs)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (!includeHiddenAndSystemDirs)
                        {
                            if (IsHiddenOrSystem(d))
                                continue;
                        }

                        stack.Push(d);
                    }

                    if (dirsVisited % 250 == 0)
                        await Task.Yield();
                }
            }
        }



        #endregion

        #region Méthodes privées
        private static List<string> ResolveRoots(string? rootFolder)
        {
            if (!string.IsNullOrWhiteSpace(rootFolder))
            {
                var root = rootFolder.Trim();

                // Autorise "c:\temp" ou "c:\temp\"
                try { root = Path.GetFullPath(root); } catch { /* si path invalide -> on laisse */ }

                if (Directory.Exists(root))
                    return new List<string> { root };

                // Si l’utilisateur a donné un truc invalide, on ne cherche pas partout par accident
                return new List<string>();
            }

            // rootFolder absent => tous les disques prêts
            return DriveInfo.GetDrives()
                .Where(d =>
                {
                    try { return d.IsReady; } catch { return false; }
                })
                .Select(d => d.RootDirectory.FullName)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsHiddenOrSystem(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                return (attr & FileAttributes.Hidden) != 0
                    || (attr & FileAttributes.System) != 0;
            }
            catch
            {
                // Si on ne peut pas lire les attributs, on considère "à éviter"
                return true;
            }
        }
        #endregion
    }
    #endregion

    #region Classes support
    public sealed record FileSearchProgress(
        string CurrentRoot,
        string CurrentPath,
        long DirectoriesVisited,
        long FilesMatched);
    #endregion
}
