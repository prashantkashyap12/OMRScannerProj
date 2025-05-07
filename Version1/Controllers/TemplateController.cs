using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using SQCScanner.Modal;
using SQCScanner.Services;
using Version1.Data;
using Version1.Modal;
using static OpenCvSharp.XImgProc.CvXImgProc;

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
        // DONE - -
        [HttpPost]
        [Route("Create_ImeTemp")]
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

                //// DONE Unique ImgType
                string fileName = Path.GetFileName(ImgTemp.FileName);
                var imgPaths = _dbContext.ImgTemplate.Select(x => x.imgPath).ToList();
                foreach (var imgPath in imgPaths)
                {
                    Uri uri = new Uri(imgPath);
                    string ImgFileName = Path.GetFileName(uri.AbsolutePath);
                    if (ImgFileName == fileName)
                    {
                        return BadRequest("ImageFile is Already Exist");
                    }
                }

                // Done Unique FileName
                var TempNameUnq = _dbContext.ImgTemplate.Select(x => x.FileName).ToList();
                foreach (var tempUnq in TempNameUnq)
                {
                    if (tempUnq == TempName)
                    {
                        return BadRequest("FileName is Already Exist");
                    }
                }

                string uploadsFolder = Path.Combine(_env.WebRootPath, "ImageManager");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }
                var ImgfileName = Path.GetFileName(ImgTemp.FileName);   
                string filePath = Path.Combine(uploadsFolder, ImgfileName);
                var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == fileName);
                var results = new List<ImgTemp>();
                if (!exists)
                {
                    // Save into Root
                    using (var TempSet = new FileStream(filePath, FileMode.Create))
                    {
                        await ImgTemp.CopyToAsync(TempSet);
                    }
                    // Save into DB
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

        // DONE - -
        [HttpGet]
        [Route("List_ImeTemp")]
        public async Task<IActionResult> getImgTemp()
        {
            dynamic res;
            try
            {
                var results = await _dbContext.ImgTemplate.ToListAsync();
                if (results.Count == null || results.Count == 0)
                {
                    res = new
                    {
                        state = false,
                        Massage = "Record Not Found"
                    };
                }
                else
                {
                    res = new
                    {
                        state = true,
                        Massage = "Record Found",
                        body = results
                    };
                }
            }catch(Exception ex)
            {
                res = new
                {
                    state = false,
                    Massage = ex.Message,
                };
            }
            return Ok(res);
        }

        // file will be deleted from DB and File Manager too
        [HttpDelete]
        [Route("Del_ImeTemp")]
        public async Task<IActionResult> deleteTemp(int id)
        {
            dynamic res;
            try
            {
                // Remove perticular id from DB
                var tempRecord = await _dbContext.ImgTemplate.FindAsync(id);
                if (tempRecord == null)
                {
                    return NotFound("Template not found.");
                }
                _dbContext.ImgTemplate.Remove(tempRecord);
                await _dbContext.SaveChangesAsync();

                // Img Del  -- Root
                string ImgFold = Path.Combine(_env.WebRootPath, "ImageManager");
                var imgTemp = tempRecord.imgPath;
                Uri uri = new Uri(imgTemp);
                string ImgFileName = Path.GetFileName(uri.AbsolutePath);
                string ImgFilePath = Path.Combine(ImgFold, ImgFileName);
                if (System.IO.File.Exists(ImgFilePath))
                {
                    System.IO.File.Delete(ImgFilePath);
                }
             
                if (!string.IsNullOrEmpty(tempRecord?.JsonPath))
                {
                    try
                    {
                        Uri urin = new Uri(tempRecord.JsonPath);
                        string TempFileName = Path.GetFileName(urin.AbsolutePath);
                        string JsonFilePath = Path.Combine(ImgFold, TempFileName);

                        if (System.IO.File.Exists(JsonFilePath))
                        {
                            System.IO.File.Delete(JsonFilePath);
                        }
                    }
                    catch(Exception ex)
                    {
                        res = new
                        {
                            message = ex.Message
                        };
                    }
                }
                res = new
                {
                    message = "Template delete from database and fileManager.",
                    state = true
                };
            }
            catch(Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            return Ok(res);
        }

        // As per File Name temp Url will be set into db along with save into fileManager
        [HttpPut]
        [Route("Update_ImeTemp")]
        public async Task<IActionResult> Update(string FileName, IFormFile tempName) 
        {
            dynamic res;
            try
            {
                if (tempName != null && Path.GetExtension(tempName.FileName).ToLower() == ".json")
                {

                    // save new JSON file into Folder
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "TempManager");
                    var fileName = Path.GetFileName(tempName.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);
                    var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == FileName);
                    if (exists)
                    {
                        // DB table is update -- DONE
                        var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.FileName == FileName);
                        ReturnDetails.JsonPath = filePath;
                        await _dbContext.SaveChangesAsync();

                        // delete old File 
                        var delOld = ReturnDetails.JsonPath;
                        if (System.IO.File.Exists(delOld))
                        {
                            System.IO.File.Delete(delOld);
                        }


                        res = new
                        {
                            message = "JSON Template JSON SAVE",
                            state = true
                        };
                    }
                    else
                    {
                        res = new
                        {
                            message = "Template not found in database.",
                            state = false
                        };
                    }
                }
                else
                {
                    res = new
                    {
                        message = "Template not JSON.",
                        state = false
                    };
                }
            }catch(Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false,
                };
            }
            return Ok(res);
        }
    }
}
