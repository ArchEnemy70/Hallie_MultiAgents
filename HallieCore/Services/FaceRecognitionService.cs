using Emgu.CV;
using Emgu.CV.Dnn;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using ExternalServices;
using System.Drawing;
using System.Text;

namespace Hallie.Services
{
    #region Classe des paramètres FaceRecognitionService
    public class ParametresFaceRecognitionService
    {
        public string Path_TrainSet { get; set; } = "";
        public string Path_TestSet { get; set; } = "";
    }
    #endregion

    #region Service pour la reconnaissance faciale
    public class FaceRecognitionService
    {
        #region Variables privées statiques
        private static string NomFichierData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Parametres","paramsFaceRecognition.txt");
        #endregion

        #region Variables privées
        private readonly string _PathDataTrainSet = @"D:\_TrainingData\FaceRecognition\TrainSet";
        private readonly string _PathModel = @"Models";

        private readonly string _NomModelDetect = @"face_detection_yunet_2023mar.onnx";
        private readonly string _NomModel = @"model_reconnaissance.yml";
        private readonly string _FaceHaarXML = @"haarcascade_frontalface_alt.xml";

        private readonly int _ReSizeFaceImg = 300;
        private readonly int _ReSizeImage = 400;
        private readonly float _ScaleFactor = 0.9f;
        private readonly System.Drawing.Size _MinSizeDetect = new(30, 30);

        private readonly int _DistanceMini = 40;

        private readonly MCvScalar _MCvScalarColorGreen = new(0, 255, 0);
        private readonly MCvScalar _MCvScalarColorRed = new(0, 0, 255);
        private readonly int _SizeBorder = 2;

        private readonly LBPHFaceRecognizer _FaceRecognizer = new();
        private readonly FaceDetectorYN _FaceNet;

        private List<int> _LstLabels = new();
        private List<string> _LstLabelsString = new();

        private readonly System.Drawing.Size _InputSize = new(300, 300);
        #endregion

        #region Constructeur
        public FaceRecognitionService(bool isCuda = false)
        {
            var parametres = LoadParametres();
            _PathDataTrainSet = parametres.Path_TrainSet;

            // Créer un objet CascadeClassifier pour détecter les visages
            if (isCuda)
            {
                _FaceNet = new FaceDetectorYN(TrtFindFileFaceModelDetect(), "", _InputSize, backendId: Emgu.CV.Dnn.Backend.Cuda, targetId: Target.Cuda);
            }
            else
            {
                _FaceNet = new FaceDetectorYN(TrtFindFileFaceModelDetect(), "", _InputSize, backendId: Emgu.CV.Dnn.Backend.Default, targetId: Target.Cpu);
            }
        }
        #endregion

        #region Gestion des paramètres TrainSet et TestSet
        public static ParametresFaceRecognitionService LoadParametres()
        {
            LoggerService.LogInfo("ParametresFaceRecognitionService.LoadParametres");

            ParametresFaceRecognitionService SelectedItem = new();

            try
            {
                if (File.Exists(NomFichierData))
                {

                    string[] lignes = File.ReadAllLines(NomFichierData);

                    if (lignes.Length > 0)
                        SelectedItem.Path_TrainSet = lignes[0];

                    if (lignes.Length > 1)
                        SelectedItem.Path_TestSet = lignes[1];

                }
                return SelectedItem;

            }
            catch
            {
                return new();
            }
        }

        public static bool SaveParametres(ParametresFaceRecognitionService selectedItem)
        {
            LoggerService.LogInfo("ParametresFaceRecognitionService.SaveParametres");

            try
            {
                StringBuilder sb = new();
                sb.AppendLine(selectedItem.Path_TrainSet);
                sb.AppendLine(selectedItem.Path_TestSet);

                File.WriteAllText(NomFichierData, sb.ToString());

                return true;
            }
            catch
            {
                return false;
            }

        }
        #endregion

        #region Méthodes publiques
        public bool CapturePhoto(string filename)
        {
            try
            {
                var inputImage = new Mat(filename);
                var inputClone = inputImage.Clone();
                var lstRetour = new List<string>();

                if (!File.Exists(filename))
                {
                    throw new Exception($"Fichier manquant : {filename}.");
                }
                _FaceRecognizer.Read(TrtFindFileModel());

                // Détecter les visages dans l'image
                var w = (int)(_ReSizeImage * 3 * _ScaleFactor);
                var h = (int)(_ReSizeImage * 3 * inputImage.Height / inputImage.Width * _ScaleFactor);
                CvInvoke.Resize(inputImage, inputImage, new System.Drawing.Size(w, h));
                var faces = DetectMultiFaces(inputImage);

                if (faces.Length == 0)
                {
                    inputImage = inputClone.Clone();
                    w = (int)(_ReSizeImage * 2 * _ScaleFactor);
                    h = (int)(_ReSizeImage * 2 * inputImage.Height / inputImage.Width * _ScaleFactor);
                    CvInvoke.Resize(inputImage, inputImage, new System.Drawing.Size(w, h));
                    faces = DetectMultiFaces(inputImage);
                }

                if (faces.Length == 0)
                {
                    inputImage = inputClone.Clone();
                    w = (int)(_ReSizeImage * _ScaleFactor);
                    h = (int)(_ReSizeImage * inputImage.Height / inputImage.Width * _ScaleFactor);
                    CvInvoke.Resize(inputImage, inputImage, new System.Drawing.Size(w, h));
                    faces = DetectMultiFaces(inputImage);
                }

                // Parcourir chaque visage détecté
                foreach (var face in faces)
                {
                    // Extraire la région du visage de l'image 
                    var faceImage = new Mat(inputImage, face);

                    if (System.IO.File.Exists(filename))
                        System.IO.File.Delete(filename);

                    CvInvoke.Imwrite(filename, faceImage);

                }

                return true;
            }
            catch
            {
                return false;

            }
        }

        public (bool, string) Training(bool isPhotoVisible)
        {
            return TrtTraining(isPhotoVisible);
        }

        public List<string> Predict(string filename)
        {
            (var lst, _) = TrtPredict(new(filename));
            return lst;
        }

        public List<string> Predict(Mat inputImage)
        {
            (var lst, _) = TrtPredict(inputImage);
            return lst;
        }

        public List<string> PredictLiveNoShow()
        {
            List<string> lst = new();
            var videoCapture = new Emgu.CV.VideoCapture(0);
            try
            {
                int i = 0;
                while (true)
                {
                    try
                    {
                        i++;
                        Mat mat = new();
                        videoCapture.Read(mat);

                        // Préparation de l'affichage de l'image
                        (lst, _) = TrtPredict(mat, false, true);

                        if (i > 4)
                            break;
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError($"FaceRecognitionService.PredictLiveNoShow --> Erreur : {ex.Message}");
                    }
                }
            }
            catch
            {

            }
            return lst;
        }

        public List<string> PredictLive()
        {
            List<string> lst = new();
            var videoCapture = new Emgu.CV.VideoCapture(0);
            try
            {
                while (true)
                {
                    try
                    {
                        Mat mat = new();
                        Mat matOutput;

                        videoCapture.Read(mat);

                        // Préparation de l'affichage de l'image
                        (lst, matOutput) = TrtPredict(mat, false, true);
                        CvInvoke.Imshow("frame", matOutput);

                        // affichage et saisie d'un code clavier (Q ou ECHAP)
                        if (CvInvoke.WaitKey(1) == (int)ConsoleKey.Q || CvInvoke.WaitKey(1) == (int)ConsoleKey.Escape)
                            break;
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError($"FaceRecognitionService.PredictLive --> Erreur : {ex.Message}");

                    }
                }
            }
            catch
            {

            }
            finally
            {
                // Ne pas oublier de fermer le flux et la fenetre
                CvInvoke.WaitKey(0);
                CvInvoke.DestroyAllWindows();

            }
            return lst;
        }
        #endregion

        #region Méthodes privées
        private (bool, string) TrtTraining(bool isPhotoVisible)
        {
            bool isReturnOK = true;
            string msgReturn = "";

            try
            {
                // Créer des listes pour stocker les images de formation et les étiquettes correspondantes
                var trainingImages = TrtFindImagesAndLabels(true, isPhotoVisible);

                // Entraîner le reconnaiseur avec les images de formation et les étiquettes
                _FaceRecognizer.Train(trainingImages.ToArray(), _LstLabels.ToArray());
                for (int i = 0; i < _LstLabels.Count; i++)
                {
                    _FaceRecognizer.SetLabelInfo(_LstLabels[i], _LstLabelsString[i]);
                }


                // Enregistrer le modèle entraîné
                _FaceRecognizer.Write(Path.Combine(_PathModel, _NomModel));
            }

            catch (Exception ex)
            {
                isReturnOK = false;
                msgReturn = ex.Message;
            }

            return (isReturnOK, msgReturn);
        }

        private (List<string>, Mat) TrtPredict(Mat inputImage, bool isAffichage = true, bool isOnlyFirstName = true)
        {
            var inputClone = inputImage.Clone();
            var lstRetour = new List<string>();
            string filename = TrtFindFileModel();
            if (!File.Exists(filename))
            {
                throw new Exception($"Fichier manquant : {filename}.");
            }
            _FaceRecognizer.Read(TrtFindFileModel());

            // Convertir l'image en niveaux de gris pour faciliter la détection des visages
            var grayImage = new Mat();


            // Détecter les visages dans l'image
            var w = (int)(_ReSizeImage * 3 * _ScaleFactor);
            var h = (int)(_ReSizeImage * 3 * inputImage.Height / inputImage.Width * _ScaleFactor);
            CvInvoke.Resize(inputImage, inputImage, new System.Drawing.Size(w, h));
            var faces = DetectMultiFaces(inputImage);

            if (faces.Length == 0)
            {
                inputImage = inputClone.Clone();
                w = (int)(_ReSizeImage * 2 * _ScaleFactor);
                h = (int)(_ReSizeImage * 2 * inputImage.Height / inputImage.Width * _ScaleFactor);
                CvInvoke.Resize(inputImage, inputImage, new System.Drawing.Size(w, h));
                faces = DetectMultiFaces(inputImage);
            }

            if (faces.Length == 0)
            {
                inputImage = inputClone.Clone();
                w = (int)(_ReSizeImage * _ScaleFactor);
                h = (int)(_ReSizeImage * inputImage.Height / inputImage.Width * _ScaleFactor);
                CvInvoke.Resize(inputImage, inputImage, new System.Drawing.Size(w, h));
                faces = DetectMultiFaces(inputImage);
            }

            CvInvoke.CvtColor(inputImage, grayImage, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);

            // Parcourir chaque visage détecté
            foreach (var face in faces)
            {
                // Extraire la région du visage de l'image en niveaux de gris
                var faceImage = new Mat(grayImage, face);
                CvInvoke.Resize(faceImage, faceImage, new System.Drawing.Size(_ReSizeFaceImg, _ReSizeFaceImg));

                // Reconnaître le visage à partir de l'image redimensionnée
                var predict = _FaceRecognizer.Predict(faceImage);

                if (predict.Distance < _DistanceMini)
                {
                    var nameComplet = _FaceRecognizer.GetLabelInfo(predict.Label);
                    var name = "";
                    if (isOnlyFirstName)
                    {
                        name = nameComplet.Split('_')[0];
                    }
                    else
                    {
                        name = nameComplet.Replace("_", " ");
                    }
                    lstRetour.Add(name);

                    CvInvoke.Rectangle(inputImage, face, _MCvScalarColorGreen, _SizeBorder);
                    //CvInvoke.PutText(inputImage, name + " - " + predict.Distance.ToString("0.0"), new System.Drawing.Point(face.X, face.Y), Emgu.CV.CvEnum.FontFace.HersheySimplex, 1.0, _MCvScalarColor, _SizeBorder);
                    CvInvoke.PutText(inputImage, name, new System.Drawing.Point(face.X, face.Y), Emgu.CV.CvEnum.FontFace.HersheySimplex, 1.0, _MCvScalarColorGreen, _SizeBorder);
                    CvInvoke.PutText(inputImage, predict.Distance.ToString("0.0"), new System.Drawing.Point(face.X, face.Y + 30), Emgu.CV.CvEnum.FontFace.HersheySimplex, 0.75, _MCvScalarColorGreen, _SizeBorder);
                }
                else
                {
                    //var name = "???";
                    //lstRetour.Add(name);
                    CvInvoke.Rectangle(inputImage, face, _MCvScalarColorRed, _SizeBorder);
                    CvInvoke.PutText(inputImage, predict.Distance.ToString("0.0"), new System.Drawing.Point(face.X, face.Y), Emgu.CV.CvEnum.FontFace.HersheySimplex, 1.0, _MCvScalarColorRed, _SizeBorder);
                }
            }

            if (isAffichage)
            {
                CvInvoke.Imshow("reconnaissance", inputImage);
                CvInvoke.WaitKey(0);
                CvInvoke.DestroyAllWindows();
            }
            return (lstRetour, inputImage);
        }

        private List<Mat> TrtFindImagesAndLabels(bool isTraining, bool isPhotoVisible)
        {
            // Créer des listes pour stocker les images de formation et les étiquettes correspondantes
            var lstTrainingImages = new List<Mat>();

            if (System.IO.Directory.Exists(_PathDataTrainSet) == false)
            {
                throw new Exception($"Le dossier n'existe pas : {_PathDataTrainSet}");
            }

            try
            {
                _LstLabels = new();
                _LstLabelsString = new();

                var dirs = Directory.GetDirectories(_PathDataTrainSet);
                int numDir = 1;
                foreach (var dir in dirs)
                {
                    var files = Directory.GetFiles(dir);

                    foreach (var file in files)
                    {
                        var dernierDir = dir.Split(@"\");
                        var nomDernierDir = dernierDir[^1];

                        if (isTraining)
                        {
                            var image = new Mat(file);
                            var grayImage = new Mat();


                            //var w = (int)(image.Width * _ScaleFactor);
                            //var h = (int)(image.Height * _ScaleFactor);
                            var w = (int)(_ReSizeImage * _ScaleFactor);
                            var h = (int)(_ReSizeImage * image.Height / image.Width * _ScaleFactor);
                            CvInvoke.Resize(image, image, new System.Drawing.Size(w, h));
                            var faces = DetectMultiFaces(image);

                            CvInvoke.CvtColor(image, grayImage, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
                            if (faces.Length > 0)
                            {
                                var face = new Mat(grayImage, faces[0]);
                                CvInvoke.Resize(face, face, new System.Drawing.Size(_ReSizeFaceImg, _ReSizeFaceImg));
                                lstTrainingImages.Add(face);

                                _LstLabels.Add(numDir);
                                _LstLabelsString.Add(nomDernierDir);

                                if (isPhotoVisible)
                                {
                                    CvInvoke.PutText(image, nomDernierDir, new System.Drawing.Point(faces[0].X, faces[0].Y), Emgu.CV.CvEnum.FontFace.HersheySimplex, 1.0, _MCvScalarColorGreen, _SizeBorder);
                                    CvInvoke.Rectangle(image, faces[0], _MCvScalarColorGreen, _SizeBorder);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Visage non détecté");
                            }
                            if (isPhotoVisible)
                            {
                                CvInvoke.Imshow("reconnaissance", image);
                                CvInvoke.WaitKey(0);
                                CvInvoke.DestroyAllWindows();
                            }
                        }
                        else
                        {
                            _LstLabels.Add(numDir);
                            _LstLabelsString.Add(nomDernierDir);
                        }

                    }
                    numDir++;
                }

            }


            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            return lstTrainingImages;
        }

        public Rectangle[] DetectMultiFaces(Mat img)
        {
            var lstRect = new List<Rectangle>();

            _FaceNet.InputSize = new System.Drawing.Size(img.Width, img.Height);

            var outputFaces = new Mat();

            _FaceNet.Detect(img, outputFaces);

            var detectionArray = outputFaces.GetData();
            if (detectionArray is null)
            {
                return new Rectangle[0];
            }

            var max = detectionArray.GetLength(0);

            Parallel.For(0, max, i =>
            {
                var confidence = (float)((Single)detectionArray.GetValue(i, 14)!);
                if (confidence > 0.5)
                {
                    // Coordonnées 2 points qui tracent un rectangle englobe le visage
                    var x1 = (int)((Single)detectionArray.GetValue(i, 0)!);
                    var y1 = (int)((Single)detectionArray.GetValue(i, 1)!);
                    var x2 = (int)((Single)detectionArray.GetValue(i, 2)!);
                    var y2 = (int)((Single)detectionArray.GetValue(i, 3)!);

                    lstRect.Add(new System.Drawing.Rectangle(x1, y1, x2, y2));
                }
            });

            return lstRect.ToArray();
        }

        private string TrtFindFileFaceHaarXML()
        {
            return Path.Combine(_PathModel, _FaceHaarXML);
        }

        private string TrtFindFileFaceModelDetect()
        {
            return Path.Combine(_PathModel, _NomModelDetect);
        }

        private string TrtFindFileModel()
        {
            return Path.Combine(_PathModel, _NomModel);
        }
        #endregion
    }
    #endregion

}
