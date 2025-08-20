using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Version1.Data;
using Version1.Modal;
using TesseractOCR;
using Tesseract;
using System;
using OpenCvSharp.Extensions;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Linq;
using SixLabors.ImageSharp.Processing;
using Newtonsoft.Json.Linq;
using System.Drawing.Design;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using TesseractOCR.Renderers;
using SQCScanner.Modal;
using System.Text.RegularExpressions;
//using SixLabors.ImageSharp.Drawing;
using System.Drawing.Text;
using Microsoft.AspNetCore.Http;
using Syncfusion.EJ2.Navigations;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SixLabors.ImageSharp.Formats.Png;
using Syncfusion.EJ2.Spreadsheet;
using Image = SixLabors.ImageSharp.Image;

namespace Version1.Services
{
    public class OmrProcessingService
    {
        private readonly ApplicationDbContext _context;

        public OmrProcessingService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<OmrResult> ProcessOmrSheet(string imagePath, string templatePath)
        {
            var result = new OmrResult         // make model get Img name and make Dictionary <key, value>
            {
                FileName = Path.GetFileName(imagePath),
                FieldResults = new Dictionary<string, string>()
            };

            var templateJson = File.ReadAllText(templatePath); // Json k har Stringify karta hai.
            var template = JObject.Parse(templateJson);        // string ko Parse karta object banata hai.

            using var originalImage = Image.Load<Rgba32>(imagePath);
            //using var correctedImage = CorrectRotation(originalImage, template); // ✅ Rotation check


            var image = originalImage.Clone(); // आगे processing के लिए
            var debugImage = image.Clone();     // Make clone init.


            // 1. Reffrence points check
            var referenceFields = template["referncefield"]?.ToArray();  // Reffrecne points ko array me return karta hai
            if (referenceFields != null && referenceFields.Length > 0)   // agr mila to check image par apply hai ya ni hai bubble detaction.
            {
                bool allFilled = AreReferenceMarkersFilled(image, template); // Any One False to IMAGE Returning ERROR MSG
                if (!allFilled)
                {
                    result.Success = false;
                    result.FieldResults["Error"] = "Skew markers are not filled or missing. Cannot proceed with OMR processing.";
                    return result;
                }
            }

            //2.Add Error DPI Range
            if (!IsDpiValid(imagePath, out string dpiError))
            {
                return new OmrResult
                {
                    FileName = Path.GetFileName(imagePath),
                    Success = false,
                    FieldResults = new Dictionary<string, string>
                    {
                        { "Error", dpiError}
                    }
                };
            }


            double angle = CalculateSkewAngleFromMarkers(template);
            //using var image = DeskewImage(images, -angle);



            var imgServ = Path.GetFileName(imagePath);           // Image File Name
            result.FieldResults["FileName"] = imgServ;           // Add New FileName into Dictronary

            foreach (var field in template["fields"])
            {
                double bubbleIntensity = field["bubbleIntensity"]?.Value<double>() ?? 0.3;
                
                string fieldType = field["fieldType"]!.ToString();    // convert into string if blank return error
                if (string.IsNullOrWhiteSpace(fieldType))
                {
                    throw new ArgumentException($"Missing 'fieldType' in field: must be \"formfield\" or \"questionfield\"");
                }
                string fieldValue = field["fieldValue"]!.ToString();  // convert into string if blank return error
                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    throw new ArgumentException($"Missing 'fieldValue' in field: must be \"Integer\", \"Alphabet\", or \"Custom\"");
                }

                string fieldname = field["fieldName"]!.ToString();                              // extract value
                var bubblesArray = field["bubbles"]?.ToObject<List<BubbleInfo>>();              // extract value
                bool allowMultiple = field["allowMultiple"]?.Value<bool>() ?? true;             // extract value
                string blankOuputSymbol = field["blankOuputSymbol"]?.ToString() ?? "#";         // extract value
                string multipleBubbleOutput = field["multipleBubbleOutput"]?.ToString() ?? "*"; // extract value

                if (bubblesArray != null)
                {
                    var bubbleRects = bubblesArray.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();

                    var options = field["Custom"]?.ToObject<List<string>>()?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

                    if (field["Custom0"] != null && (options == null || options.Count == 0))
                    {
                        throw new ArgumentException($"The field '{fieldname}' includes an 'Custom' array but it is empty or invalid. Please provide valid Custom.");
                    }

                    if (options == null || options.Count == 0)
                    {
                        options = GenerateOptionsFromFieldType(fieldValue, bubblesArray);
                    }

                    string readdirection = field["ReadingDirection"]!.ToString();


                    if (fieldType == "formfield")     // scan formField (Alphabat / Intizer)
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple, blankOuputSymbol, multipleBubbleOutput);   
                        var combined = string.Join("", answers.OrderBy(kv => int.Parse(kv.Key.Replace("Q", "")))
                        .Select(kv => kv.Value).Where(val => val != "No bubble Mark" && val != "InvalidOption"));

                        result.FieldResults[fieldname] = combined;
                    }

                    else if (fieldType == "questionfield")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple, blankOuputSymbol, multipleBubbleOutput);

                        if (Regex.IsMatch(fieldname, @"q\d+-q\d+", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(fieldname, @"q(\d+)-q(\d+)", RegexOptions.IgnoreCase);
                            int start = int.Parse(match.Groups[1].Value);
                            int end = int.Parse(match.Groups[2].Value);

                            int index = 0;
                            for (int q = start; q <= end; q++)
                            {
                                string questionKey = $"Q{q}";
                                if (answers.TryGetValue($"Q{index + 1}", out string? val))
                                {
                                    result.FieldResults[questionKey] = val;
                                }
                                index++;
                            }
                        }
                        else
                        {
                            // Default behavior
                            foreach (var kv in answers)
                            {
                                if (!string.IsNullOrWhiteSpace(kv.Value) && kv.Value != "No bubble Mark" && kv.Value != "InvalidOption")
                                {
                                    result.FieldResults[kv.Key] = kv.Value;
                                }
                            }
                        }
                    }
                }
            }
            result.Success = true;
            result.ProcessedAt = DateTime.UtcNow;

            return result;
        }


        // Image k Refrence field TEST 
        private bool AreReferenceMarkersFilled(Image<Rgba32> image, JObject template, double bubbleIntensity = 0.3)
        {
            // Find Refrence mark. form array
            var referenceFields = template["referncefield"]?.ToArray();

            // agr Reffrence mark nahi diye from JSON to True otherwise NOTHING
            if (referenceFields == null || referenceFields.Length == 0)
                return true;

            // Pahli Refrence field li jati hai.
            var refMarker = referenceFields[0];
            var positions = new[] { "topLeft", "topRight", "bottomLeft", "bottomRight" };
            var points = new List<PointF>();

            // For courner k liye ek loop 
            foreach (var pos in positions)
            {
                var marker = refMarker[pos]?.ToObject<BubbleInfo>();

                //extract kar k list me add karte hai. 
                points.Add(new PointF(marker.X + marker.Width / 2f, marker.Y + marker.Height / 2f));

                // Mark Null Return False.
                if (marker == null) return false;

                // Rectangle me Crop karta hai.
                var rect = new SixLabors.ImageSharp.Rectangle(marker.X, marker.Y, marker.Width, marker.Height);
                var region = image.Clone(ctx => ctx.Crop(rect));

                // Save cropped bubble
                string outputFolder = "wwwroot/newFolder/";
                Directory.CreateDirectory(outputFolder);

                // Check croped Image Filled hai with intensity. >> False  / << True
                if (!IsBubbleFilled(region, bubbleIntensity))
                {
                    return false;
                }
                string fileName = $"bubble_{pos}_{Guid.NewGuid()}.png";
                string outputPath = Path.Combine(outputFolder, fileName);
                region.Save(outputPath, new PngEncoder());
            }
            return true;
        }

        // Make grid setting as per Range.
        private List<string> GenerateOptionsFromFieldType(string fieldValue, List<BubbleInfo> bubbles)
        {
            if (fieldValue == "Integer")
            {
                int colCount = bubbles.Select(b => b.Col).Distinct().Count();
                int RowCount = bubbles.Select(b => b.Row).Distinct().Count();
                return Enumerable.Range(0, RowCount).Select(i => i.ToString()).ToList();
            }

            if (fieldValue.Equals("Alphabet", StringComparison.OrdinalIgnoreCase))
            {
                int colCount = bubbles.Select(b => b.Col).Distinct().Count();
                int RowCount = bubbles.Select(b => b.Row).Distinct().Count();
                return Enumerable.Range(0, colCount).Select(i => ((char)('A' + i)).ToString()).ToList();
            }
            return new List<string>();
        }

        // Checking Horizontal Or Vertical direction me Bubble Check kar k result return karta hai. 
        // Direction ko validate karna 
        private Dictionary<string, string> ExtractAnswersFromBubbles(Image<Rgba32> image, List<Rectangle> bubbleRects, List<BubbleInfo> bubbleInfos, List<string> options,
         string? readdirection, double bubbleIntensity, bool allowMultiple,string blankOuputSymbol, string  multipleBubbleOutput)
        {
            if (string.IsNullOrWhiteSpace(readdirection))
                throw new ArgumentException("You must provide 'ReadingDirection' in the template. Allowed values: 'Horizontal' or 'Vertical'.");

            readdirection = readdirection.Trim();

            if (readdirection != "Horizontal" && readdirection != "Vertical")
                throw new ArgumentException($"Invalid 'ReadingDirection': '{readdirection}'. Allowed values: 'Horizontal' or 'Vertical'.");

            // Bubble Grouping 
            var result = new Dictionary<string, string>();
            var grouped = (readdirection == "Horizontal")
                ? bubbleInfos.GroupBy(b => b.Row).OrderBy(g => g.Key)
                : bubbleInfos.GroupBy(b => b.Col).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                int questionIndex = group.Key;
                var filledOptions = new List<string>();
                var sortedGroup = (readdirection == "Horizontal")
                    ? group.OrderBy(b => b.Col)
                    : group.OrderBy(b => b.Row);

                int bubbleInGroupIndex = 0;

                foreach (var bubble in sortedGroup)
                {
                    int index = bubbleInfos.IndexOf(bubble);
                    if (index < 0 || index >= bubbleRects.Count)
                        continue;

                    // Har group ke liye bubble crop karta hai:
                    var cell = image.Clone(ctx => ctx.Crop(bubbleRects[index]));
                    if (IsBubbleFilled(cell, bubbleIntensity))
                    {
                        int optionIndex = bubbleInGroupIndex;
                        // Result Return hua to Add into Fields.
                        if (optionIndex >= 0 && optionIndex < options.Count)
                        {
                            filledOptions.Add(options[optionIndex]);
                        }
                    }
                    bubbleInGroupIndex++;
                }

                string output = "";
                if (filledOptions.Count == 0)
                {

                    output = blankOuputSymbol;
                }
                else if (filledOptions.Count == 1)
                {
                    output = filledOptions[0];
                }
                else if (filledOptions.Count > 1)
                {
                    output = allowMultiple
                        ? string.Join("", filledOptions)
                        : multipleBubbleOutput;
                }

                result[$"Q{questionIndex + 1}"] = output;
            }

            return result;
        }


        // Add Error DPI Range  _ Calling Funcation
        private bool IsDpiValid(string imagePath, out string errorMassage)
        {
            using var img = new System.Drawing.Bitmap(imagePath);
            float dpiX = img.HorizontalResolution;
            float dpiY = img.VerticalResolution;
            if (dpiX < 100 || dpiY < 100)
            {
                errorMassage = $"Image DPI is too law DPI shpuld be at least 100";
                return false;
            }
            errorMassage = string.Empty;

            return true;
        }


        // Check Skew Angle Refrance Marks
        private double CalculateSkewAngleFromMarkers(JObject template)
        {
            var refField = template["referncefield"]?.FirstOrDefault();
            if (refField == null) return 0;
            var topLeft = refField["topLeft"]?.ToObject<BubbleInfo>();
            var topRight = refField["topRight"]?.ToObject<BubbleInfo>();
            if (topLeft == null || topRight == null) return 0;
            double dx = topRight.X - topLeft.X;
            double dy = topRight.Y - topLeft.Y;
            double angleRadians = Math.Atan2(dy, dx);
            double angleDegrees = angleRadians * (180.0 / Math.PI);
            return angleDegrees;
        }


        // Set According to angle
        //private Image<Rgba32> DeskewImage(Image<Rgba32> image, double angle)
        //{
        //    using var ms = new MemoryStream();
        //    image.SaveAsBmp(ms);
        //    ms.Seek(0, SeekOrigin.Begin);
        //    using var bitmap = new System.Drawing.Bitmap(ms);
        //    using var mat = BitmapConverter.ToMat(bitmap);

        //    var center = new OpenCvSharp.Point2f(mat.Width / 2, mat.Height / 2);
        //    var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);

        //    Cv2.WarpAffine(mat, mat, rotationMatrix, mat.Size());
        //    using var rotatedBmp = BitmapConverter.ToBitmap(mat);
        //    using var mem = new MemoryStream();
        //    rotatedBmp.Save(mem, System.Drawing.Imaging.ImageFormat.Bmp);
        //    mem.Seek(0, SeekOrigin.Begin);
        //    return Image.Load<Rgba32>(mem.ToArray());
        //}



        // Checking bubble is filled or not 
        private bool IsBubbleFilled(Image<Rgba32> bubble, double bubbleIntensity)   
        {
            // bubble cordination ko memoryStream me store karna
            using var ms = new MemoryStream();
            bubble.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);

            //Save image into BMP
            using var bitmap = new System.Drawing.Bitmap(ms);
            
            // BitMap ko OpenCV matrix me convert
            using var mat = BitmapConverter.ToMat(bitmap);

            // Use OpenCvSharp.Rect explicitly to resolve ambiguity
            var roi = new OpenCvSharp.Rect(0, 0, mat.Width, mat.Height);
            using var cropped = new Mat(mat, roi);

            //Convert into Gray Scale 
            using var gray = new Mat();
            Cv2.CvtColor(cropped, gray, ColorConversionCodes.BGR2GRAY);

            // Throsholding (Gray into B/W img)
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 50, 255, ThresholdTypes.Binary);

            // convert into negative black <:> white
            using var inverted = new Mat();
            Cv2.BitwiseNot(binary, inverted);

            // count blackPixcel 
            int blackPixels = Cv2.CountNonZero(inverted);
            // Apply Condi = like blackPixels se jyada hua to filled mana jayega. otherwise ni.
            // by Default 0.3 diya hai just lite to hard dark 30.3 <from Front End>
            return blackPixels > bubbleIntensity;

        }


        private void SetProperty(OmrResult result, string propName, string value)
        {
            var prop = typeof(OmrResult).GetProperty(propName);
            if (prop != null && prop.CanWrite)
                prop.SetValue(result, value);
        }
    }
}