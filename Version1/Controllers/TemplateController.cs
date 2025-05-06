using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SQCScanner.Modal;
using SQCScanner.Services;
using Version1.Data;
using Version1.Modal;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TemplateController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _dbContext;

        public TemplateController(IWebHostEnvironment env, ApplicationDbContext dbContext)
        {
            _env = env;
            _dbContext = dbContext;
        }
        // uploadData
        [HttpPost]
        [Route("UploadTemp")]
        public async Task<IActionResult> processTemp(IFormFile TemplateSet)
        {
            //var imageFiles = Directory.GetFiles(folderPath, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg")).ToList();

            if (TemplateSet != null && Path.GetExtension(TemplateSet.FileName).ToLower() == ".json")
            {
                // FolderPath, FilePath, RenameFile save file
                string uploadsFolder = Path.Combine(_env.WebRootPath, "TempMaster");
                var fileName = Path.GetFileName(TemplateSet.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);
                //string fileNames = Path.GetFileName(filePath);

                DateTime currentTimes = DateTime.Now;
                string DateTimeFormat = currentTimes.ToString("yyyy-MM-dd HH:mm:ss");

                // does fileName is Exist in DB
                var exists = await _dbContext.TemplateRec.
                    AnyAsync(t => t.FileName == fileName);

                // NOT EXIST
                if (!exists)
                {
                    // Save into Folder
                    using (var TempSet = new FileStream(filePath, FileMode.Create))
                    {
                        await TemplateSet.CopyToAsync(TempSet);
                    }
                    // Find From FolderLoc, get JsonHead for save into table name.
                    string templateJson = await System.IO.File.ReadAllTextAsync(filePath);
                    var template = JObject.Parse(templateJson);
                    string tempNames = template["name"]?.ToString();

                    _dbContext.Add(new TempRecord
                    {
                        TemplateName = tempNames,
                        FileName = fileName,
                        FileUrl = filePath,

                        Time = DateTimeFormat
                    });
                    await _dbContext.SaveChangesAsync();
                    return Ok("File Save into Table and Folder");
                }
                else
                {
                    return BadRequest("File Already Exist");
                }
            }
            else
            {
                return BadRequest("Please use JSON File");
            }
        }

        // getData
        [HttpGet]
        [Route("ViewTemplate")]
        public async Task<IActionResult> viewTemp()
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "TempMaster");
            if (!Directory.Exists(uploadsFolder))
            {
                return BadRequest("Folder path is invalid.");
            }
            var JSonFileFiles = Directory.GetFiles(uploadsFolder, "*.*")
            .Where(f => f.EndsWith(".json"))
             .ToList();
            var results = new List<TempModelRev>();
            foreach (var TempPath in JSonFileFiles)
            {
                string fileNames = Path.GetFileName(TempPath);  // Get the file name
                string filePath = Path.Combine(uploadsFolder, fileNames); // File ka full path
                string templateJson = await System.IO.File.ReadAllTextAsync(TempPath);  // TempPath is your file path
                var template = JObject.Parse(templateJson);   // json parse
                string tempNames = template["name"]?.ToString();  // Using indexing to access 'name'
                // save TemplateFile into Table
                _dbContext.Add(new TempRecord
                {
                    TemplateName = tempNames,
                    FileName = fileNames,
                    FileUrl = filePath
                });
                await _dbContext.SaveChangesAsync();
                // save TemplateFile into Table
            }
            return Ok(results);
        }


        // GetRecord
        [HttpPost]
        [Route("ImgTemplate")]
        public async Task<IActionResult> imgRec(IFormFile ImgTemp, string TempName)
        {
            dynamic res;
            if (ImgTemp == null || ImgTemp.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }
            string extension = Path.GetExtension(ImgTemp.FileName).ToLower();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                return BadRequest("Invalid file type. Only .jpg, .jpeg, or .png are allowed.");
            }
            try
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "TempMaster");
                var fileName = Path.GetFileName(ImgTemp.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                var exists = await _dbContext.ImgTemplate.
                    AnyAsync(t => t.FileName == fileName);
                var results = new List<ImgTemp>();
                if (!exists)
                {
                    using (var TempSet = new FileStream(filePath, FileMode.Create))
                    {
                        await ImgTemp.CopyToAsync(TempSet);
                    }
                    var resp = _dbContext.Add(new ImgTemp
                    {
                        FileName = TempName,
                        imgPath = filePath,
                        JsonPath = ""
                    });
                    await _dbContext.SaveChangesAsync();
                    results.Add(resp.Entity);
                }
                res = new
                {
                    message = "File Save into Table and Folder",
                    status = true,
                    data = results
                };
            }
            catch(Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    status = false,
                };
            }
            return Ok(res);
        }

        [HttpGet]
        [Route("GetImgTemp")]
        public async Task<IActionResult> getImgTemp()
        {
            var results = await _dbContext.ImgTemplate.ToListAsync();
            if (results == null)
            {
                return NotFound("No data found.");
            }
            return Ok(results);
        }

        [HttpDelete]
        [Route("DeleteTemp")]
        public async Task<IActionResult> deleteTemp(int id)
        {
            var tempRecord = await _dbContext.TemplateRec.FindAsync(id);
            if (tempRecord == null)
            {
                return NotFound("Template not found.");
            }
            _dbContext.TemplateRec.Remove(tempRecord);
            await _dbContext.SaveChangesAsync();
            return Ok("Template deleted successfully.");
        }
    }
}
