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
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using OpenCvSharp.Extensions;

namespace Version1.Services
{
    public class OmrProcessingService
    {
        private readonly ApplicationDbContext _context;

        public OmrProcessingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Point2f> demoImg = new List<Point2f>();
        public List<Point2f> ScanningImg = new List<Point2f>();

        public async Task<OmrResult> ProcessOmrSheet(string imagePath, string templatePath, string imageUrl)
        {
            string alignedImages = "wwwroot/alignedImages/";
            Directory.CreateDirectory(alignedImages);

            var result = new OmrResult         // make model get Img name and make Dictionary <key, value>
            {
                FileName = Path.GetFileName(imagePath),
                FieldResults = new Dictionary<string, string>()
            };


            var templateJson = File.ReadAllText(templatePath); // Json k har Stringify karta hai.
            var template = JObject.Parse(templateJson);        // string ko Parse karta object banata hai.


            using var demoImage = Image.Load<Rgba32>(imageUrl);
            using var originalImage = Image.Load<Rgba32>(imagePath);
            // 1. Rotated Detacions -- OPEN
            var exif = originalImage.Metadata.ExifProfile;
            if (exif != null)
            {
                // Orientation tag nikalne ki koshish
                var orientation = exif.GetValue(ExifTag.Orientation);
                if (orientation != null)
                {
                    // Value ko int me cast karo
                    int orientationValue = (int)orientation.Value;

                    Console.WriteLine($"Raw EXIF orientation value = {orientationValue}");
                    int rotationDegrees = orientationValue switch
                    {
                        1 => 0,   
                        3 => 180, 
                        6 => 90,   
                        8 => 270,  
                        _ => 0
                    };
                    result.FieldResults["Image"] = $"{rotationDegrees}";
                }
                else
                {
                    Console.WriteLine("Orientation tag not found in EXIF metadata.");
                }
            }
            else
            {
                Console.WriteLine("No EXIF metadata found.");
            }

            // Demo
            using var ms2 = new MemoryStream();                 // RAM ke andar ek virtual file
            demoImage.SaveAsBmp(ms2);                       // Convert into Bit Image Sharp and Open Cv k liye 
            ms2.Seek(0, SeekOrigin.Begin);                      // 
            using var bmp2 = new System.Drawing.Bitmap(ms2);     // 
            using var demoImg = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp2);   // convert into matrix


            // Scaning
            using var ms = new MemoryStream();                 // RAM ke andar ek virtual file
            originalImage.SaveAsBmp(ms);                       // Convert into Bit Image Sharp and Open Cv k liye 
            ms.Seek(0, SeekOrigin.Begin);                      // 
            using var bmp = new System.Drawing.Bitmap(ms);     // 
            using var matInput = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp);   // convert into matrix



            //string fileName2 = $"aligned_{Guid.NewGuid()}.png";
            //string outputPath2 = Path.Combine(alignedImages, fileName2);
            //using var mat = Cv2.ImRead(imagePath);                          //  Read with OpenCV
            //Cv2.ImWrite(outputPath2, mat);                                  //  Save processed image
            //originalImage.Save(outputPath2);
            // save_image _CLOSE


            // 2. Input image ko demo image k base par prespactivly wrap convert kar return karna.
            var template2 = JObject.Parse(templateJson);

            // Demo Image cordications
            var MatImgOut = AlignWithTemplate(demoImg, template2, "demo");
            
            // Scanning Image cordications
            var MatImgOut2 = AlignWithTemplate(matInput, template2, "scan");

            // Convert into Prespative mode
            //Console.WriteLine(MatImgOut);
            //Console.WriteLine(MatImgOut2);

            // Source points (detected from image)
            if (MatImgOut.Count < 4)
                throw new Exception("Demo corners kam hai! 4 points required.");
            Point2f[] demoDetaction = new Point2f[]
            {
                new Point2f(MatImgOut[0].X, MatImgOut[0].Y),  // top-left
                new Point2f(MatImgOut[1].X, MatImgOut[1].Y),  // top-right
                new Point2f(MatImgOut[2].X, MatImgOut[2].Y),  // bottom-left
                new Point2f(MatImgOut[3].X, MatImgOut[3].Y)   // bottom-right
            };

            // Source points (detected from image)
            if (MatImgOut2.Count < 4)
                throw new Exception("Scainng corners kam hai! 4 points required.");
            Point2f[] scaningDetaction = new Point2f[]
           {
                new Point2f(MatImgOut2[0].X, MatImgOut2[0].Y),  // top-left
                new Point2f(MatImgOut2[1].X, MatImgOut2[1].Y),  // top-right
                new Point2f(MatImgOut2[2].X, MatImgOut2[2].Y),  // bottom-left
                new Point2f(MatImgOut2[3].X, MatImgOut2[3].Y)   // bottom-right
           };


            // Detected corners mark (Green) and SAVE
            foreach (var pt in demoDetaction)
            {
                Cv2.Circle(demoImg, (int)pt.X, (int)pt.Y, 1, new Scalar(0, 255, 0), -1);
            }
            //string markedPath = Path.Combine(alignedImages, $"marked_{Guid.NewGuid()}.png");
            //demoImg.SaveImage(markedPath);


            // Detact corners mark scaning and SAVE
            foreach (var pt in scaningDetaction)
            {
                Cv2.Circle(matInput, (int)pt.X, (int)pt.Y, 1, new Scalar(0, 0, 255), -1);
            }
            //string markedPath2 = Path.Combine(alignedImages, $"marked_{Guid.NewGuid()}.png");
            //matInput.SaveImage(markedPath2);


            // Convert into Wrap image <old image cordination > New Image Cordination) and SAVE
            Mat homography = Cv2.GetPerspectiveTransform(scaningDetaction, demoDetaction);
            Mat aligned = new Mat();
            Cv2.WarpPerspective(matInput, aligned, homography, matInput.Size());
            //string debugPath = Path.Combine(alignedImages, $"roi_{Guid.NewGuid()}.png");
            //aligned.SaveImage(debugPath);


            // Convert Mat to byte[] (e.g., PNG in memory)
            byte[] imageBytes = aligned.ToBytes(".png");
            Image<Rgba32> scanningImage = Image.Load<Rgba32>(imageBytes);

            // Bitmap bitmap = BitmapConverter.ToBitmap(aligned);

            // Continue All Process and SAVE (before bubble Scanning)
            var image = scanningImage.Clone();  // Move forword for scanning
            string fileName11 = $"aligned_{Guid.NewGuid()}.png";
            string outputPath11 = Path.Combine(alignedImages, fileName11);
            image.Save(outputPath11);

            var debugImage = image.Clone();     // Make clone init.

            // 3. Reffrence points check -- DONE
            var referenceFields = template["referncefield"]?.ToArray();  // Reffrecne points ko array me return karta hai
            if (referenceFields != null && referenceFields.Length > 0)   // agr mila to check image par apply hai ya ni hai bubble detaction.
            {
                bool allFilled = AreReferenceMarkersFilled(scanningImage, template); // Any One False to IMAGE Returning ERROR MSG
                if (!allFilled)
                {
                    result.Success = false;
                    result.FieldResults["Report"] = "Skew markers are not filled or missing. Cannot proceed with OMR processing.";
                    return result;
                }
            }

            // 4. Add Error DPI Range   -- 
            if (!IsDpiValid(originalImage, out string dpiError))
            {
                return new OmrResult
                {
                    FileName = Path.GetFileName(imagePath),
                    Success = false,
                    FieldResults = new Dictionary<string, string>
                    {
                        { "Report", dpiError}
                    }
                };
            }

            // 5. Detact Angle of demo image BUT should be new Image.
            double angle = CalculateSkewAngleFromMarkers(template);
            using var angledFix = DeskewImage(scanningImage, -angle);


            // ** Add fields scaning filed **
            var imgServ = Path.GetFileName(imagePath);           // Image File Name
            result.FieldResults["FileName"] = imgServ;           // Add New FileName into Dictronary

            // Bubble Detaction from fields
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

        // 2. Image Prespactive view _ from demo image refrence points.
        public List<Point2f> AlignWithTemplate(Mat inputImage, JObject template, string SelctImg)
        {
            // 1. Template ke reference points JSON se lo
            var refField = template["referncefield"]?.FirstOrDefault();
            if (refField == null)
                throw new Exception("Template JSON me 'referncefield' missing hai!");

            var topLeft = refField["topLeft"]?.ToObject<BubbleInfo2Class>();
            var topRight = refField["topRight"]?.ToObject<BubbleInfo2Class>();
            var bottomLeft = refField["bottomLeft"]?.ToObject<BubbleInfo2Class>();
            var bottomRight = refField["bottomRight"]?.ToObject<BubbleInfo2Class>();
            var templateCorners = new[]
            {
                new Point2f(topLeft.X, topLeft.Y),
                new Point2f(topRight.X, topRight.Y),
                new Point2f(bottomLeft.X, bottomLeft.Y),
                new Point2f(bottomRight.X, bottomRight.Y),
            };

            // Template corners mark (Red)
            float cxGlobal = -1;
            float cyGlobal = -1;
            foreach (var pt in templateCorners)
            {
                int x = (int)pt.X;
                int y = (int)pt.Y;
                int width = topLeft.Width;
                int height = topLeft.Height;
                
                // make cordination variable
                OpenCvSharp.Point topLeft1 = new OpenCvSharp.Point(x, y);
                
                // Increase width and height into cordination
                OpenCvSharp.Point bottomRight1 = new OpenCvSharp.Point(x + width, y + height);

                // Drow Reactangle
                //Cv2.Rectangle(inputImage, topLeft1, bottomRight1, new Scalar(0, 0, 255), 2);

                // ROI rectangle (inflated karna ho to yahi pe +extraPixels kar sakte ho)
                int extra = 10;
                int newX = Math.Max(0, x - extra);
                int newY = Math.Max(0, y - extra);
                int newWidth = Math.Min(inputImage.Width - newX, width + extra * 2);
                int newHeight = Math.Min(inputImage.Height - newY, height + extra * 2);
                OpenCvSharp.Rect Rect2 = new OpenCvSharp.Rect(newX, newY, newWidth, newHeight);
                using var roiImage = new Mat(inputImage, Rect2);

                // ROI ko gray to binary
                Mat gray = new Mat();
                Cv2.CvtColor(roiImage, gray, ColorConversionCodes.BGR2GRAY);

                // ROI ko Threshold image me convert karna
                Mat binary = new Mat();
                Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

                // Blank Value
                OpenCvSharp.Point[][] contoursVal;
                HierarchyIndex[] _;
                Cv2.FindContours(binary, out contoursVal, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

                Cv2.DrawContours(roiImage, contoursVal, -1, new Scalar(225, 255, 0), 1);

                if (contoursVal.Length > 0)
                {
                    int maxIndexFound = contoursVal
                        .Select((c, i) => new { Area = Cv2.ContourArea(c), Index = i })
                        .OrderByDescending(c => c.Area)
                        .First().Index;

                    var M = Cv2.Moments(contoursVal[maxIndexFound]);
                    if (M.M00 != 0)
                    {
                        float cx = (float)(M.M10 / M.M00);
                        float cy = (float)(M.M01 / M.M00);
                        
                        OpenCvSharp.Point localCenter = new OpenCvSharp.Point((int)cx, (int)cy);
                        OpenCvSharp.Point globalCenter = new OpenCvSharp.Point(
                            (int)(cx + Rect2.X),
                            (int)(cy + Rect2.Y)
                        );

                        Cv2.DrawContours(inputImage, contoursVal, maxIndexFound, new Scalar(0, 255, 0), 1, lineType: LineTypes.Link8, hierarchy: null, maxLevel: int.MaxValue, offset: new OpenCvSharp.Point(Rect2.X, Rect2.Y));
                        Cv2.Circle(inputImage, globalCenter, 4, new Scalar(255, 255, 255), -1);

                        // ROI → Global shift
                        cxGlobal = cx + Rect2.X;
                        cyGlobal = cy + Rect2.Y;
                        if (SelctImg == "demo")
                        {
                            pointsList.Add(new Point2f(cxGlobal, cyGlobal));
                        }
                        else if(SelctImg == "scan")
                        {
                            pointsList2.Add(new Point2f(cxGlobal, cyGlobal));
                        }
                    }
                }
            }

            //string outputFolder2 = "wwwroot/alignedImages/";
            //Directory.CreateDirectory(outputFolder2);
            //string fileName2 = $"aligned_{Guid.NewGuid()}.png";
            //string outputPath2 = Path.Combine(outputFolder2, fileName2);
            //inputImage.SaveImage(outputPath2);
            //Mat aligned = new Mat();
            return (SelctImg == "demo") ? pointsList : pointsList2;
        }


        // Add in array list of cordinations.
        private List<Point2f> pointsList = new List<Point2f>();
        private List<Point2f> pointsList2 = new List<Point2f>();

        public void AddPoint(float x, float y)
        {
            // Create a new Point2f object and add it to the list
            Point2f newPoint = new Point2f(x, y);
            pointsList.Add(newPoint);
        }


        // 3. Image k Refrence field TEST   --  
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


                // Check croped Image Filled hai with intensity. >> False  / << True
                if (!IsBubbleFilled(region, bubbleIntensity))
                {
                    return false;
                }

                // Save cropped bubble -
                //string outputFolder = "wwwroot/newFolder/";
                //Directory.CreateDirectory(outputFolder);
                //string fileName = $"bubble_{pos}_{Guid.NewGuid()}.png";
                //string outputPath = Path.Combine(outputFolder, fileName);
                //region.Save(outputPath, new PngEncoder());
            }
            return true;
        }


        // 4. Add Error DPI Range  _ Calling Funcation
        private bool IsDpiValid(Image<Rgba32> imagePath, out string errorMassage)  // << here we will use fresh scanning image 
        {
            using var ms = new MemoryStream();
            imagePath.SaveAsPng(ms); // or use image.SaveAsJpeg(ms) depending on your image type
            ms.Seek(0, SeekOrigin.Begin);

            using var bitmap = new System.Drawing.Bitmap(ms);
            float dpiX = bitmap.HorizontalResolution;
            float dpiY = bitmap.VerticalResolution;

            if (dpiX < 95 || dpiY < 95)
            {
                errorMassage = $"Image DPI is too law DPI shpuld be at least 100";
                return false;
            }
            errorMassage = string.Empty;
            return true;
        }


        // 5. Check Skew Angle Refrance Marks   -- Use less 
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

        private Image<Rgba32> DeskewImage(Image<Rgba32> image, double angle)
        {
            using var ms = new MemoryStream();
            image.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            using var bitmap = new System.Drawing.Bitmap(ms);
            using var mat = BitmapConverter.ToMat(bitmap);

            var center = new OpenCvSharp.Point2f(mat.Width / 2, mat.Height / 2);
            var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);    // Rotation matrix create karna.
            Cv2.WarpAffine(mat, mat, rotationMatrix, mat.Size());                // Rotate karna.
            using var rotatedBmp = BitmapConverter.ToBitmap(mat);
            using var mem = new MemoryStream();
            rotatedBmp.Save(mem, System.Drawing.Imaging.ImageFormat.Bmp);
            mem.Seek(0, SeekOrigin.Begin);
            return Image.Load<Rgba32>(mem.ToArray());
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


        // Checking Horizontal Or Vertical direction me Bubble Check kar k result return karta hai.  // Direction ko validate karna 
        private Dictionary<string, string> ExtractAnswersFromBubbles(Image<Rgba32> image, List<Rectangle> bubbleRects, List<BubbleInfo> bubbleInfos, List<string> options,
         string? readdirection, double bubbleIntensity, bool allowMultiple, string blankOuputSymbol, string multipleBubbleOutput)
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


        // Checking bubble is filled or not 
        private bool IsBubbleFilled(Image<Rgba32> bubble, double bubbleIntensity)   
        {
            //string alignedImages = "wwwroot/alignedImages/";
            //string fileName11 = $"aligned_{Guid.NewGuid()}.png";
            //string outputPath11 = Path.Combine(alignedImages, fileName11);
            //bubble.Save(outputPath11);

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