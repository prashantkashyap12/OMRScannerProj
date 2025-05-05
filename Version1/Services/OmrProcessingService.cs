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
            // Grayshade, shapDetact, Green rectangle
            var highlightedImagePath = HighlightAndSaveBubbles(imagePath);
            
            // RGBA converstion
            using var image = Image.Load<Rgba32>(highlightedImagePath);

            // String JSON TEXT
            var templateJson = File.ReadAllText(templatePath);
            
            // return to parsal from jsonTemp.
            var template = JObject.Parse(templateJson);

            // omrResult model k base par nre
            var result = new OmrResult
            {
                FileName = Path.GetFileName(imagePath),
                FieldResults = new Dictionary<string, string>()
            };

            // Extract field Markup
            foreach (var field in template["fields"])
            {
                double bubbleIntensity = field["bubbleIntensity"]?.Value<double>() ?? 0.3;
                string fieldType = field["fieldType"]!.ToString();
                string fieldname = field["fieldName"]!.ToString();
                var bubblesArray = field["bubbles"]?.ToObject<List<BubbleInfo>>();
                bool allowMultiple = field["allowMultiple"]?.Value<bool>() ?? true;

                if (bubblesArray != null)
                {
                    var bubbleRects = bubblesArray.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();

                    // Mark Reactange 
                    var options = field["Options"]?.ToObject<List<string>>() ?? GenerateOptionsFromFieldType(fieldType, bubblesArray);

                    // Direction of it (horizntal/vertical)
                    string readdirection = field["ReadingDirection"]!.ToString();


                    if (fieldType == "Integer")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple);
                        var combined = string.Join("",
                            answers.OrderBy(kv => int.Parse(kv.Key.Replace("Q", "")))
                                   .Select(kv => kv.Value)
                                   .Where(val => val != "No bubble Mark" && val != "InvalidOption"));

                        result.FieldResults[fieldname] = combined;
                    }

                    else if (fieldType == "Alphabet")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple);
                        var combined = string.Join("",
                            answers.OrderBy(kv => int.Parse(kv.Key.Replace("Q", "")))
                                   .Select(kv => kv.Value)
                                   .Where(val => val != "No bubble Mark" && val != "InvalidOption"));

                        result.FieldResults[fieldname] = combined;

                    }
                    else if (fieldType == "Abc")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple);
                        result.FieldResults[fieldname] = JsonConvert.SerializeObject(answers);

                    }

                }
            }
            result.ProcessedAt = DateTime.UtcNow;
           
            return result;
        }
        private List<string> GenerateOptionsFromFieldType(string fieldType, List<BubbleInfo> bubbles)
        {
            if (fieldType == "Integer")
            {
                int colCount = bubbles.Select(b => b.Col).Distinct().Count();
                int RowCount = bubbles.Select(b => b.Row).Distinct().Count();

                return Enumerable.Range(0, RowCount).Select(i => i.ToString()).ToList(); 
            }

            if (fieldType.Equals("Alphabet", StringComparison.OrdinalIgnoreCase) || fieldType.Equals("Abc", StringComparison.OrdinalIgnoreCase))
            {
                int colCount = bubbles.Select(b => b.Col).Distinct().Count();
                int RowCount = bubbles.Select(b => b.Row).Distinct().Count();
                return Enumerable.Range(0, colCount).Select(i => ((char)('A' + i)).ToString()).ToList();
            }

            return new List<string>(); 
        }

        private Dictionary<string, string> ExtractAnswersFromBubbles(
          Image<Rgba32> image,
          List<Rectangle> bubbleRects,
          List<BubbleInfo> bubbleInfos,
          List<string> options,
          string? readdirection, double bubbleIntensity, bool allowMultiple)  
        {
            // Step 1: Validate reading direction
            if (string.IsNullOrWhiteSpace(readdirection))
                throw new ArgumentException("You must provide 'ReadingDirection' in the template. Allowed values: 'Horizontal' or 'Vertical'.");

            readdirection = readdirection.Trim();

            if (readdirection != "Horizontal" && readdirection != "Vertical")
                throw new ArgumentException($"Invalid 'ReadingDirection': '{readdirection}'. Allowed values: 'Horizontal' or 'Vertical'.");

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

                foreach (var bubble in sortedGroup)
                {
                    int index = bubbleInfos.IndexOf(bubble);
                    if (index < 0 || index >= bubbleRects.Count)
                        continue;

                    var cell = image.Clone(ctx => ctx.Crop(bubbleRects[index]));

                    if (IsBubbleFilled(cell, bubbleIntensity))
                    {
                       
                        int optionIndex = (readdirection == "Horizontal") ? bubble.Col : bubble.Row;

                        if (optionIndex >= 0 && optionIndex < options.Count)
                            filledOptions.Add(options[optionIndex]);
                    }
                }

               string output;

        if (filledOptions.Count == 0)
        {
            output = "#"; // No bubble filled
        }
        else if (filledOptions.Count == 1)
        {
            output = filledOptions[0]; // Only one marked
        }
        else
        {
            output = allowMultiple
                ? string.Join("", filledOptions)  // Multiple allowed, join
                : "*";                             // Multiple not allowed
        }

        result[$"Q{questionIndex + 1}"] = output;
            }

            return result;
        }

        private bool IsBubbleFilled(Image<Rgba32> bubble, double bubbleIntensity)
        {
            int blackPixels = 0;
            int totalPixels = bubble.Width * bubble.Height;

            // Calculate adaptive threshold using mean gray value
            int totalGray = 0;

            for (int y = 0; y < bubble.Height; y++)
            {
                for (int x = 0; x < bubble.Width; x++)
                {
                    var pixel = bubble[x, y];
                    int gray = (pixel.R + pixel.G + pixel.B) / 3;
                    totalGray += gray;
                }
            }

            int avgGray = totalGray / totalPixels;
            int adaptiveThreshold = avgGray - 30; // dynamic offset, can tune

            if (adaptiveThreshold < 50) adaptiveThreshold = 50; // prevent too low
            if (adaptiveThreshold > 180) adaptiveThreshold = 180; // prevent too high

            // Count dark pixels
            for (int y = 0; y < bubble.Height; y++)
            {
                for (int x = 0; x < bubble.Width; x++)
                {
                    var pixel = bubble[x, y];
                    int gray = (pixel.R + pixel.G + pixel.B) / 3;

                    if (gray < adaptiveThreshold)
                        blackPixels++;
                }
            }

            double fillRatio = (double)blackPixels / totalPixels;

            Console.WriteLine($"[DEBUG] FillRatio: {fillRatio:0.000}, Threshold: {adaptiveThreshold}, Sensitivity: {bubbleIntensity}");

            return fillRatio > bubbleIntensity;
            //int blackPixels = 0;

            //for (int y = 0; y < bubble.Height; y++)
            //{
            //    for (int x = 0; x < bubble.Width; x++)
            //    {
            //        var pixel = bubble[x, y];
            //        int gray = (pixel.R + pixel.G + pixel.B) / 3;

            //        if (gray < 100) // adjust if needed
            //            blackPixels++;
            //    }
            //}

            //int totalPixels = bubble.Width * bubble.Height;
            //double fillRatio = (double)blackPixels / totalPixels;

            //Console.WriteLine($"Bubble Fill Ratio: {fillRatio}");

            //return fillRatio > bubbleIntensity;
        }

        private string HighlightAndSaveBubbles(string imagePath)
        {
            var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
            var gray = new Mat();

            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            var binary = new Mat();

            Cv2.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.BinaryInv, 11, 2);

            var contours = Cv2.FindContoursAsArray(binary, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            var saveDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "HighlightedImages");
            Directory.CreateDirectory(saveDir); // Ensure the folder exists

            var fileName = $"highlighted_{Path.GetFileName(imagePath)}";
            var savePath = Path.Combine(saveDir, fileName);

            Cv2.ImWrite(savePath, mat);

            return savePath;
        }
        private void SetProperty(OmrResult result, string propName, string value)
        {
            var prop = typeof(OmrResult).GetProperty(propName);
            if (prop != null && prop.CanWrite)
                prop.SetValue(result, value);
        }
    }
}