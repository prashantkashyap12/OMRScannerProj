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

namespace Version1.Services
{
    public class OmrProcessingService
    {
        private readonly ApplicationDbContext _context;

        public OmrProcessingService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<OmrResult> ProcessOmrSheet(string imagePath, string templatePath, string sharePath)
        {
            // 

            using var image = Image.Load<Rgba32>(imagePath);
            var debugImage = image.Clone();

            var templateJson = File.ReadAllText(templatePath);

            var template = JObject.Parse(templateJson);

            var result = new OmrResult
            {
                FileName = Path.GetFileName(imagePath),
                FieldResults = new Dictionary<string, string>()
            };

            // Add File Name
            var imgServ = Path.GetFileName(imagePath);
            templatePath = Path.Combine(sharePath, imgServ);
            var fileNames = templatePath.Replace("\\", "/");
            result.FieldResults["FileName"] = fileNames;

            foreach (var field in template["fields"])
            {
                double bubbleIntensity = field["bubbleIntensity"]?.Value<double>() ?? 0.3;
                string fieldType = field["fieldType"]!.ToString();
                if (string.IsNullOrWhiteSpace(fieldType))
                {
                    throw new ArgumentException($"Missing 'fieldType' in field: must be \"formfield\" or \"questionfield\"");
                }
                string fieldValue = field["fieldValue"]!.ToString();
                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    throw new ArgumentException($"Missing 'fieldValue' in field: must be \"Integer\", \"Alphabet\", or \"Custom\"");
                }

                string fieldname = field["fieldName"]!.ToString();
                var bubblesArray = field["bubbles"]?.ToObject<List<BubbleInfo>>();
                bool allowMultiple = field["allowMultiple"]?.Value<bool>() ?? true;

                if (bubblesArray != null)
                {
                    var bubbleRects = bubblesArray.Select(b => new Rectangle(b.X, b.Y, b.Width, b.Height)).ToList();

                    var options = field["Custom"]?.ToObject<List<string>>()?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

                    if (field["Custom"] != null && (options == null || options.Count == 0))
                    {
                        throw new ArgumentException($"The field '{fieldname}' includes an 'Custom' array but it is empty or invalid. Please provide valid Custom.");
                    }

                    if (options == null || options.Count == 0)
                    {
                        options = GenerateOptionsFromFieldType(fieldValue, bubblesArray);
                    }

                    string readdirection = field["ReadingDirection"]!.ToString();


                    if (fieldType == "formfield")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple);
                        var combined = string.Join("",
                            answers.OrderBy(kv => int.Parse(kv.Key.Replace("Q", "")))
                                   .Select(kv => kv.Value)
                                   .Where(val => val != "No bubble Mark" && val != "InvalidOption"));

                        result.FieldResults[fieldname] = combined;
                    }

                    else if (fieldType == "questionfield")
                    {
                        var answers = ExtractAnswersFromBubbles(image, bubbleRects, bubblesArray, options, readdirection, bubbleIntensity, allowMultiple);

                        // Check if fieldName is like "q6-q10"
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
            result.ProcessedAt = DateTime.UtcNow;

            return result;
        }
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
        private Dictionary<string, string> ExtractAnswersFromBubbles(
     Image<Rgba32> image,
     List<Rectangle> bubbleRects,
     List<BubbleInfo> bubbleInfos,
     List<string> options,
     string? readdirection, double bubbleIntensity, bool allowMultiple)
        {
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

                int bubbleInGroupIndex = 0;

                foreach (var bubble in sortedGroup)
                {
                    int index = bubbleInfos.IndexOf(bubble);
                    if (index < 0 || index >= bubbleRects.Count)
                        continue;

                    var cell = image.Clone(ctx => ctx.Crop(bubbleRects[index]));

                    if (IsBubbleFilled(cell, bubbleIntensity))
                    {
                        int optionIndex = bubbleInGroupIndex;

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

                    output = "#";
                }
                else if (filledOptions.Count == 1)
                {
                    output = filledOptions[0];
                }
                else if (filledOptions.Count > 1)
                {
                    output = allowMultiple
                        ? string.Join("", filledOptions)
                        : "*";
                }

                result[$"Q{questionIndex + 1}"] = output;
            }

            return result;
        }
        private bool IsBubbleFilled(Image<Rgba32> bubble, double bubbleIntensity)
        {
            using var ms = new MemoryStream();
            bubble.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            using var bitmap = new System.Drawing.Bitmap(ms);
            using var mat = BitmapConverter.ToMat(bitmap);

            // Use OpenCvSharp.Rect explicitly to resolve ambiguity
            var roi = new OpenCvSharp.Rect(0, 0, mat.Width, mat.Height);
            using var cropped = new Mat(mat, roi);

            using var gray = new Mat();
            Cv2.CvtColor(cropped, gray, ColorConversionCodes.BGR2GRAY);

            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 50, 255, ThresholdTypes.Binary);

            using var inverted = new Mat();
            Cv2.BitwiseNot(binary, inverted);

            int blackPixels = Cv2.CountNonZero(inverted);
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