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
using System.Windows.Forms;

namespace checkdgc_net
{
    internal class Program
    {
        public static Window window;
        private static bool showCamera = true;
        private static SpeechSynthesizer synthesizer = new SpeechSynthesizer();

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
                        Cv2.WaitKey(20);
                        if (!decodeOnly)
                        {
                            DgcValidationResult res = await VerifyDgc(dgcReader, BitmapConverter.ToBitmap(image));
                            if (res != null && res.Dgc != null)
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
            if (res.Status == DgcResultStatus.Valid)
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
            } else
            {
                Console.Beep(440, 200);
                Bitmap ko = new Bitmap(checkdgc_net.Properties.Resources.ko);
                RectangleF rectf = new RectangleF(50, 390, 350, 100);

                Graphics g = Graphics.FromImage(ko);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawString("Codice QR Non Valido:\n" + res.Dgc.Name.GivenName + " " + res.Dgc.Name.FamilyName + "  " + res.Dgc.DateOfBirth, new Font("Calibri", 18), Brushes.Black, rectf);
                g.Flush();
                window.ShowImage(BitmapConverter.ToMat(ko));

                // Synchronous
                synthesizer.SpeakAsync("Codice QR Non Valido");
            }
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
    }
}