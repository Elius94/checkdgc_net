using System;
using System.Threading.Tasks;
using DgcReader;
using DgcReader.RuleValidators.Italy;
using DgcReader.TrustListProviders.Italy;
using System.Drawing;
using ZXing;
using System.Net.Http;
using System.Net;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Speech.Synthesis;
using System.Drawing.Drawing2D;
using DgcReader.Models;
using DgcReader.Interfaces.BlacklistProviders;
using DgcReader.BlacklistProviders.Italy;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace checkdgc_net
{
    internal class Program
    {
        public static Window window;
        private static bool showCamera = true;
        private static SpeechSynthesizer synthesizer = new SpeechSynthesizer();

        private static Dictionary<string, string> vaccineDictionary = new Dictionary<string, string>();
        private static Dictionary<string, string> holderDictionary = new Dictionary<string, string>();
        private static Dictionary<string, string> vacProphDictionary = new Dictionary<string, string>();
        private static Dictionary<string, string> diseaseDictionary = new Dictionary<string, string>();
        private static Dictionary<string, string> testsDictionary = new Dictionary<string, string>();

        public static async Task Main(string[] args)
        {
            Thread.CurrentThread.Name = "Main";
            synthesizer.SelectVoice(synthesizer.GetInstalledVoices()[0].VoiceInfo.Name);
            synthesizer.Volume = 100;  // 0...100
            synthesizer.Rate = -2;     // -10...10

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var httpClient = new HttpClient();

            // You can use the constructor
            var rulesValidator = DgcItalianRulesValidator.Create(httpClient,
                new DgcItalianRulesValidatorOptions
                {
                    RefreshInterval = TimeSpan.FromHours(24),
                    MinRefreshInterval = TimeSpan.FromHours(1),
                    UseAvailableValuesWhileRefreshing = true,
                    ValidationMode = ValidationMode.Basic3G
                });

            var trustListProvider = ItalianTrustListProvider.Create(httpClient,
                new ItalianTrustListProviderOptions
                {
                    RefreshInterval = TimeSpan.FromHours(24),
                    MinRefreshInterval = TimeSpan.FromHours(1),
                    SaveCertificate = true,
                    UseAvailableListWhileRefreshing = true
                });

            var drlBlacklistProvider = ItalianDrlBlacklistProvider.Create(httpClient,
                new ItalianDrlBlacklistProviderOptions
                {
                    RefreshInterval = TimeSpan.FromHours(24),
                    MinRefreshInterval = TimeSpan.FromHours(1),
                    UseAvailableValuesWhileRefreshing = true
                });

            // Create an instance of the DgcReaderService
            var dgcReader = DgcReaderService.Create(
                trustListProviders: new[] { trustListProvider },
                blackListProviders: new IBlacklistProvider[] { rulesValidator, drlBlacklistProvider },
                rulesValidators: new[] { rulesValidator }
            );

            bool decodeOnly = args.Contains("--decode-only");
            bool msgBox = args.Contains("--messagebox");
            FillDictionaries();

            Console.WriteLine("Dgc Reader Creato");

            VideoCapture capture = new VideoCapture(0);
            using (window = new Window("Camera", WindowFlags.FullScreen))
            using (QRCodeDetector qrCodeDetector = new QRCodeDetector())
            using (Mat image = new Mat()) // Frame image buffer
            {
                // When the movie playback reaches end, Mat.data becomes NULL.
                while (true)
                {
                    capture.Read(image); // same as cvQueryFrame
                    if (image.Empty()) break;
                    Mat img2show = image.Clone();
                    qrCodeDetector.Detect(image, out Point2f[] points);
                    if (points.Length == 4)
                    {
                        for (int i = 1; i < points.Length; i++)
                        {
                            OpenCvSharp.Point pt1 = new OpenCvSharp.Point(points[i - 1].X, points[i - 1].Y);
                            OpenCvSharp.Point pt2 = new OpenCvSharp.Point(points[i].X, points[i].Y);
                            Cv2.Line(img2show, pt1, pt2, Scalar.Red, 3);
                        }
                        OpenCvSharp.Point pt1_ = new OpenCvSharp.Point(points[points.Length - 1].X, points[points.Length - 1].Y);
                        OpenCvSharp.Point pt2_ = new OpenCvSharp.Point(points[0].X, points[0].Y);
                        Cv2.Line(img2show, pt1_, pt2_, Scalar.Red, 3);
                    }

                    Cv2.Line(img2show, new OpenCvSharp.Point(0, 225), new OpenCvSharp.Point(500, 225), Scalar.Red, 2);
                    if (showCamera)
                    {
                        window.ShowImage(img2show);
                        window.Resize(500, 500);
                        Cv2.WaitKey(50);
                        //Mat filtered = new Mat();
                        //Cv2.AdaptiveThreshold(image, filtered, 125, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 11, 12);
                        //Thread.Sleep(500);
                        if (!decodeOnly)
                        {
                            DgcValidationResult res = await VerifyDgc(dgcReader, BitmapConverter.ToBitmap(image));
                            if (res != null && res.Status == DgcResultStatus.Valid)
                            {
                                showCamera = false;
                                synthesizer.SpeakAsyncCancelAll();
                                Parallel.Invoke(() => OutputShow(res));
                                PromptDgcText(res.Dgc, msgBox);
                            }
                        }
                        else
                        {
                            SignedDgc res = await ParseDgc(dgcReader, BitmapConverter.ToBitmap(image));
                            if (res != null && res.Dgc != null)
                            {
                                PromptDgcText(res.Dgc, msgBox);
                            }
                        }
                    }
                }
            }
        }
        private static bool OutputShow(DgcValidationResult res)
        {
            Console.Beep(880, 200);
            Bitmap ok = new Bitmap(checkdgc_net.Properties.Resources.ok);
            RectangleF rectf = new RectangleF(50, 390, 350, 100);

            Graphics g = Graphics.FromImage(ok);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawString("Codice QR Valido:\n" + res.Dgc.Name.GivenName + " " + res.Dgc.Name.FamilyName + "  " + res.Dgc.DateOfBirth, new Font("Calibri", 18), Brushes.Black, rectf);
            g.Flush();
            window.ShowImage(BitmapConverter.ToMat(ok));

            // Synchronous
            synthesizer.SpeakAsync("Codice QR Valido");
            Cv2.WaitKey(2000);
            showCamera = true;
            return true;
        }

        private static void PromptDgcText(GreenpassReader.Models.EuDGC dgc, bool msgBox = false)
        {
            string text = "";
            text += "\n\n### DGC READER RESULT ###\n\n";
            text += "Name: " + dgc.Name.FamilyName + " " + dgc.Name.GivenName + "\n";
            text += "Date of birth: " + dgc.DateOfBirth + "\n";
            if (dgc.Vaccinations != null)
            {
                text += "Vaccinations: \n";
                foreach (var vaccination in dgc.Vaccinations)
                {
                    text += $"Unique certificate identifier: {vaccination.CertificateIdentifier}\n" +
                        $"{vaccination.Date:dd/MM/yyyy}: Country: {vaccination.Country}, Dose: {vaccination.DoseNumber}/{vaccination.TotalDoseSeries}\n" +
                        $"Issuer: {vaccination.Issuer}\n" +
                        $"Vaccine marketing authorisation holder or manufacturer: {(holderDictionary[vaccination.MarketingAuthorizationHolder] ?? vaccination.MarketingAuthorizationHolder)} ({vaccination.MarketingAuthorizationHolder})\n" +
                        $"Vaccine medicinal product: {(vaccineDictionary[vaccination.MedicinalProduct] ?? vaccination.MedicinalProduct)} ({vaccination.MedicinalProduct})\n" +
                        $"Disease or agent targeted: {(diseaseDictionary[vaccination.TargetedDiseaseAgent] ?? vaccination.TargetedDiseaseAgent)} ({vaccination.TargetedDiseaseAgent})\n" +
                        $"Vaccine/prophylaxis: {(vacProphDictionary[vaccination.VaccineOrProphylaxis] ?? vaccination.VaccineOrProphylaxis)} ({vaccination.VaccineOrProphylaxis})";
                }
            }
            if (dgc.Recoveries != null)
            {
                text += "Recoveries: ";
                foreach (var recovery in dgc.Recoveries)
                {
                    text += $"Unique certificate identifier: {recovery.CertificateIdentifier}\n" +
                        $"Valid from {recovery.ValidFrom:dd/MM/yyyy} to {recovery.ValidUntil:dd/MM/yyyy}: Country: {recovery.Country}, " +
                        $"First positive result: {recovery.FirstPositiveTestResult:dd/MM/yyyy}\n" +
                        $"Issuer: {recovery.Issuer}\n" +
                        $"Disease or agent targeted: {(diseaseDictionary[recovery.TargetedDiseaseAgent] ?? recovery.TargetedDiseaseAgent)} ({recovery.TargetedDiseaseAgent})";
                }
            }
            if (dgc.Tests != null)
            {
                text += "Tests: ";
                foreach (var test in dgc.Tests)
                {
                    text += $"Unique certificate identifier: {test.CertificateIdentifier}\n" +
                        $"Date: {test.SampleCollectionDate:dd/MM/yyyy}, Country: {test.Country}\n" +
                        $"Type: {(testsDictionary[test.TestType] ?? test.TestType)} ({test.TestType})\n" +
                        $"Result: {(testsDictionary[test.TestResult] ?? test.TestResult)} ({test.TestResult})\n" +
                        $"Made by: {test.TestingCentre}, Test type: {test.Ma}, Used Naat: {test.NaatName}";
                }
            }

            if (msgBox)
            {
                MessageBox.Show(text, "DGC READER RESULT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Console.WriteLine(text);
            }
        }

        public static async Task<DgcValidationResult> VerifyDgc(DgcReaderService dgc, Bitmap img)
        {
            // create a barcode reader instance
            IBarcodeReader reader = new BarcodeReader();
            // load a bitmap
            var barcodeBitmap = img;
            // detect and decode the barcode inside the bitmap
            var decodedQrcode = reader.Decode(barcodeBitmap);
            //Console.WriteLine(decodedQrcode);
            // do something with the result
            if (decodedQrcode != null)
            {
                try
                {
                    string acceptanceCountry = "IT";    // Specify the 2-letter ISO code of the acceptance country

                    // Decode and validate the qr code data.
                    // The result will contain all the details of the validated object
                    var result = await dgc.Verify(decodedQrcode.ToString(), acceptanceCountry);
                    return result;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error verifying DGC: {e.Message}");
                }
            }
            return new DgcValidationResult();
        }

        public static async Task<SignedDgc> ParseDgc(DgcReaderService dgc, Bitmap img)
        {
            // create a barcode reader instance
            IBarcodeReader reader = new BarcodeReader();
            // load a bitmap
            var barcodeBitmap = img;
            // detect and decode the barcode inside the bitmap
            var decodedQrcode = reader.Decode(barcodeBitmap);
            //Console.WriteLine(decodedQrcode);
            // do something with the result
            if (decodedQrcode != null)
            {
                try
                {
                    var result = await dgc.Decode(decodedQrcode.ToString());
                    return result;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error verifying DGC: {e.Message}");
                }
            }
            return new SignedDgc();
        }

        private static void FillDictionaries()
        {
            // Vaccines
            vaccineDictionary["EU/1/20/1528"] = "Comirnaty";
            vaccineDictionary["EU/1/20/1507"] = "COVID-19 Vaccine Moderna";
            vaccineDictionary["EU/1/21/1529"] = "Vaxzevria";
            vaccineDictionary["EU/1/20/1525"] = "COVID-19 Vaccine Janssen";
            vaccineDictionary["CVnCoV"] = "CVnCoV";
            vaccineDictionary["NVX-CoV2373"] = "NVX-CoV2373";
            vaccineDictionary["Sputnik-V "] = "Sputnik V";
            vaccineDictionary["Convidecia"] = "Convidecia";
            vaccineDictionary["EpiVacCorona"] = "EpiVacCorona";
            vaccineDictionary["BBIBP-CorV"] = "BBIBP-CorV";
            vaccineDictionary["Inactivated-SARS-CoV-2-Vero - Cell"] = "Inactivated SARS-CoV - 2 (Vero Cell)";
            vaccineDictionary["CoronaVac"] = "CoronaVac";
            vaccineDictionary["Covaxin"] = "Covaxin (also known as BBV152 A, B, C)";

            // Holders
            holderDictionary["ORG-100001699"] = "AstraZeneca AB";
            holderDictionary["ORG-100030215"] = "Biontech Manufacturing GmbH";
            holderDictionary["ORG-100001417"] = "Janssen-Cilag Internationa";
            holderDictionary["ORG-100031184"] = "Moderna Biotech Spain S.L.";
            holderDictionary["ORG-100006270"] = "Curevac AG";
            holderDictionary["ORG-100013793"] = "CanSino Biologics";
            holderDictionary["ORG-100020693"] = "China Sinopharm International Corp. - Beijing location";
            holderDictionary["ORG-100010771"] = "Sinopharm Weiqida Europe Pharmaceutical s.r.o. - Prague location";
            holderDictionary["ORG-100024420"] = "Sinopharm Zhijun (Shenzhen) Pharmaceutical Co.Ltd. - Shenzhen location";
            holderDictionary["ORG-100032020"] = "Novavax CZ AS";
            holderDictionary["Gamaleya-Research-Institute"] = "Gamaleya Research Institute";
            holderDictionary["Vector-Institute"] = "Vector Institute";
            holderDictionary["Sinovac-Biotech"] = "Sinovac Biotech";
            holderDictionary["Bharat-Biotech"] = "Bharat Biotech";

            // Vaccine/prophylaxis
            vacProphDictionary["1119305005"] = "SARS-CoV-2 antigen vaccine";
            vacProphDictionary["1119349007"] = "SARS-CoV-2 mRNA vaccine";
            vacProphDictionary["J07BX03"] = "covid-19 vaccines";

            // disease
            diseaseDictionary["840539006"] = "COVID-19";

            // tests
            testsDictionary["LP6464-4"] = "Nucleic acid amplification with probe detection";
            testsDictionary["LP217198-3"] = "Rapid immunoassay";
            testsDictionary["260415000"] = "Not detected";
            testsDictionary["260373001"] = "Detected";
        }
    }
}