using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DgcReader;
using DgcReader.RuleValidators.Italy;
using DgcReader.TrustListProviders.Italy;
using System.Drawing;
using ZXing;
using System.Net.Http;
using System.Net;

namespace checkdgc_net
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            if (args[0] != null)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var httpClient = new HttpClient();
                // You can use the constructor
                var rulesValidator = new DgcItalianRulesValidator(httpClient);

                var trustListProvider = new ItalianTrustListProvider(httpClient);

                // Create an instance of the DgcReaderService
                var dgcReader = new DgcReaderService(trustListProvider, rulesValidator, rulesValidator);

                Console.WriteLine("Dgc Reader Creato");

                // create a barcode reader instance
                IBarcodeReader reader = new BarcodeReader();
                // load a bitmap
                var barcodeBitmap = (Bitmap)Image.FromFile(args[0]);
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
                        var result = await dgcReader.VerifyForItaly(decodedQrcode.ToString(), ValidationMode.Strict2G);
                        //Console.WriteLine(decodedQrcode.ToString());
                        var status = result.Status;
                        var signatureIsValid = result.HasValidSignature;
                        Console.WriteLine(status);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error verifying DGC: {e.Message}");
                    }
                }
            }
        }
    }
}