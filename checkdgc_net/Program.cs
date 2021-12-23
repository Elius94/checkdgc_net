using System;
using System.Threading.Tasks;
using DgcReader;
using DgcReader.RuleValidators.Italy;
using DgcReader.TrustListProviders.Italy;
using System.Drawing;
using ZXing;
using System.Net.Http;
using System.Net;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using System.Threading;
using System.Speech.Synthesis;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.IO;
using DgcReader.Models;
using checkdgc_net.Properties;

namespace checkdgc_net
{
    internal class Program
    {
        public static Window window;
        public static async Task Main(string[] args)
        {
            if (args[0] != null)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var httpClient = new HttpClient();

                DgcItalianRulesValidatorOptions dgcItalianRulesValidatorOptions = new DgcItalianRulesValidatorOptions();
                IOptions<DgcItalianRulesValidatorOptions> options = Options.Create(dgcItalianRulesValidatorOptions);
                options.Value.BasePath = args[0];
                options.Value.UseAvailableListWhileRefreshing = true;
                // You can use the constructor
                var rulesValidator = new DgcItalianRulesValidator(httpClient, options);

                ItalianTrustListProviderOptions dgcItalianTrustListProviderOptions = new ItalianTrustListProviderOptions();
                IOptions<ItalianTrustListProviderOptions> optionsSignature = Options.Create(dgcItalianTrustListProviderOptions);
                optionsSignature.Value.BasePath = args[0];
                optionsSignature.Value.UseAvailableListWhileRefreshing = true;
                var trustListProvider = new ItalianTrustListProvider(httpClient, optionsSignature);

                // Create an instance of the DgcReaderService
                var dgcReader = new DgcReaderService(trustListProvider, rulesValidator, rulesValidator);

                Console.WriteLine("Dgc Reader Creato");

                if (args[1] == "--camera")
                {
                    VideoCapture capture = new VideoCapture(1);
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
                            window.ShowImage(img2show);
                            window.Resize(500, 500);
                            //Mat filtered = new Mat();
                            //Cv2.AdaptiveThreshold(image, filtered, 125, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 11, 12);
                            Cv2.WaitKey(50);
                            //Thread.Sleep(500);
                            var res = await ParseDgc(dgcReader, OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image));
                            if (res != null && res.Status == DgcResultStatus.Valid)
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
                                window.ShowImage(OpenCvSharp.Extensions.BitmapConverter.ToMat(ok));
                                //Console.Beep(880, 500);
                                SpeechSynthesizer synthesizer = new SpeechSynthesizer();
                                synthesizer.SelectVoice(synthesizer.GetInstalledVoices()[0].VoiceInfo.Name);
                                synthesizer.Volume = 100;  // 0...100
                                synthesizer.Rate = -2;     // -10...10

                                // Synchronous
                                synthesizer.Speak("Codice QR Valido");
                                Cv2.WaitKey(2000);
                            }
                        }
                    }
                }
                else
                {
                    await ParseDgc(dgcReader, (Bitmap)Image.FromFile(args[args.Length - 1]));
                }

            }
        }

        public static async Task<DgcValidationResult> ParseDgc(DgcReaderService dgc, Bitmap img)
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
                    // Decode and validate the signature.
                    // If anything fails, an exception is thrown containing the error details
                    var result = await dgc.VerifyForItaly(decodedQrcode.ToString(), ValidationMode.Strict2G);
                    //Console.WriteLine(decodedQrcode.ToString());
                    var status = result.Status;
                    var signatureIsValid = result.HasValidSignature;
                    Console.WriteLine(status + " for " + result.Dgc.Name.GivenName + " " + result.Dgc.Name.FamilyName + " _ " + result.Dgc.DateOfBirth);
                    return result;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error verifying DGC: {e.Message}");
                }
            }
            return new DgcValidationResult();
        }
    }
}