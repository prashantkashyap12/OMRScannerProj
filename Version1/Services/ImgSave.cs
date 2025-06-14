using Microsoft.AspNetCore.Mvc;

namespace SQCScanner.Services
{
    public class ImgSave 
    {
        public async Task<IActionResult> ScanedSave(string root, string imgPath, int templateId, bool status)
        {
            dynamic res = "";
            try
            {
                var getFile = Path.GetFileName(imgPath);
                var folderPathMain = "";
                if (status)
                {
                   folderPathMain = Path.Combine(root, "ScannedImg/", $"Template_{templateId}");
                }
                else
                {
                    folderPathMain = Path.Combine(root, "RejectImg/", $"Template_{templateId}");
                }

                if (!Directory.Exists(folderPathMain))
                {
                    Directory.CreateDirectory(folderPathMain);
                }
                folderPathMain = Path.Combine(folderPathMain, getFile);
                
                // Check file Already Saved.
                if (!File.Exists(folderPathMain))
                {
                    // Save Img  
                    using (var ImgTemp = new FileStream(imgPath, FileMode.Open, FileAccess.Read))
                    using (var TempSet = new FileStream(folderPathMain, FileMode.Create, FileAccess.Write))
                    {
                        await ImgTemp.CopyToAsync(TempSet);
                        // delete root folder 
                    }
                    res = new
                    {
                        message = "File Save Into TemplateWise FolderName",
                        state = true,
                    };
                 
                }
                else
                {
                    res = new
                    {
                        message = "Already File Save Into TemplateWise FolderName.",
                        state = false,
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false,
                };
            }

            Console.WriteLine(res);
            return new JsonResult(res);
        }
    }
}
