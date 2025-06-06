namespace SQCScanner.Services
{
    public class ImgSave
    {
        public async Task ScanedSave(string root, string imgPath, int templateId)
        {
            dynamic res = "";
            try
            {
                var getFile = Path.GetFileName(imgPath);
                var folderPathMain = Path.Combine(root, "ScannedImg/", $"Template_{templateId}");
                if (!Directory.Exists(folderPathMain))
                {
                    Directory.CreateDirectory(folderPathMain);
                }
                folderPathMain = Path.Combine(folderPathMain, getFile);

                // Save Img  
                using (var ImgTemp = new FileStream(imgPath, FileMode.Open, FileAccess.Read))
                using (var TempSet = new FileStream(folderPathMain, FileMode.Create, FileAccess.Write))
                {
                    await ImgTemp.CopyToAsync(TempSet);
                }

                res = new
                {
                    message = "File Save Into TemplateWise FolderName",
                    state = true,
                };
            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false,
                };
            }
        }
    }
}
