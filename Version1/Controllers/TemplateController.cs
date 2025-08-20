using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using SQCScanner.Modal;
using SQCScanner.Services;
using Syncfusion.EJ2.Notifications;
using Version1.Data;
using Version1.Modal;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static OpenCvSharp.XImgProc.CvXImgProc;

namespace SQCScanner.Controllers
{
    //[Authorize]
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

        // DONE - - yh 
        [HttpPost]
        [Route("Create_ImeTemp")]
        public async Task<IActionResult> imgRec(IFormFile ImgTemp, string TempName)
        {
            dynamic res;
            bool exist = true;
            if (ImgTemp == null || ImgTemp.Length == 0)
            {
                //return BadRequest("No file uploaded.");
                res = new
                {
                    state = false,
                    message = "No file uploaded."
                };
            }
            else
            {
                string extension = Path.GetExtension(ImgTemp.FileName).ToLower();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    res = new
                    {
                        state = false,
                        message = "Invalid file type. Only .jpg, .jpeg, or .png are allowed."
                    };
                }
                else
                {
                    try
                    {
                        string fileName = Path.GetFileName(ImgTemp.FileName);
                        var TempNameUnq = _dbContext.ImgTemplate.Select(x => x.FileName).ToList();
                        foreach (var tempUnq in TempNameUnq)
                        {
                            if (tempUnq == TempName)
                            {
                                exist = false;
                            }
                        }
                        if (exist == true)
                        {
                            string uploadsFolder = Path.Combine(_env.WebRootPath, "ImageManager");
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }
                            var ImgfileName = Path.GetFileName(ImgTemp.FileName);
                            string filePath = Path.Combine(uploadsFolder, ImgfileName);
                            string relativeImgPath = Path.Combine("ImageManager", ImgfileName).Replace("\\", "/");
                            var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == fileName);
                            var results = new List<ImgTemp>();
                            if (!exists)
                            {
                                using (var TempSet = new FileStream(filePath, FileMode.Create))
                                {
                                    await ImgTemp.CopyToAsync(TempSet);
                                }
                                // Save into DB
                                DateTime Date = DateTime.Now;
                                string toDay = Date.ToString();
                                var resp = _dbContext.Add(new ImgTemp
                                {
                                    //Date = toDay,
                                    FileName = TempName,
                                    imgPath = relativeImgPath,
                                    JsonPath = "",
                                    CreateAt = toDay
                                });
                                await _dbContext.SaveChangesAsync();
                                results.Add(resp.Entity);
                                res = new
                                {
                                    message = "File Save into Table and Folder",
                                    status = true,
                                    data = results
                                };
                            }
                            else
                            {
                                res = new
                                {
                                    message = "File Not Exist",
                                    status = false,
                                };
                            }
                        }
                        else
                        {
                            res = new
                            {
                                state = false,
                                message = "FileName is Already Exist"
                            };
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        res = new
                        {
                            message = ex.Message,
                            status = false,
                        };
                    }
                }
            }
            bool currentState = res.status;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        // DONE - -
        [HttpGet]
        [Route("Single_ImeTem")]
        public async Task<IActionResult> getImgTemp(int id)
        {
            dynamic res;
            try
            {
                var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == id);
                if (ReturnDetails != null)
                {
                    if (!string.IsNullOrEmpty(ReturnDetails.imgPath))
                    {
                        ReturnDetails.imgPath = ReturnDetails.imgPath.Replace("\\", "/");
                    }
                    if(!string.IsNullOrEmpty(ReturnDetails.JsonPath))
                    {
                        ReturnDetails.JsonPath = ReturnDetails.JsonPath.Replace("\\", "/");
                    }
                    res = new
                    {
                        data = ReturnDetails,
                        state = true,
                        Message = "Record Found"
                    };
                }
                else
                {
                    res = new
                    {
                        state = false,
                        Message = "Record Not Found",
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
                };
            }
            bool currentStatex = res.state;
            if (currentStatex)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
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
                        message = "Record Not Found"
                    };
                }
                else
                {
                    foreach (var item in results)
                    {
                        if (!string.IsNullOrEmpty(item.imgPath))
                        {
                            item.imgPath = item.imgPath.Replace("\\", "/");
                        }
                        if (!string.IsNullOrEmpty(item.JsonPath))
                        {
                            item.JsonPath = item.JsonPath.Replace("\\", "/");
                        }
                    }
                    res = new
                    {
                        state = true,
                        message = "Record Found",
                        body = results
                    };
                }
            }catch(Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        // File will be deleted from DB and File Manager too
        [HttpDelete]
        [Route("Del_ImeTemp")]
        public async Task<IActionResult> deleteTemp(int id)
        {
            dynamic res;
            try
            {
                // Remove perticular id_Data from DB
                var tempRecord = await _dbContext.ImgTemplate.FindAsync(id);
                if (tempRecord == null)
                {
                    res = new
                    {
                        state = false,
                        message = "Template not found."
                    };
                }
                else
                {
                    _dbContext.ImgTemplate.Remove(tempRecord);
                    await _dbContext.SaveChangesAsync();

                    // Img Del  -- Root folder
                    string ImgFold = Path.Combine(_env.WebRootPath, "ImageManager");
                    var imgTemp = tempRecord.imgPath;
                    string ImgFileName = Path.GetFileName(imgTemp);
                    string ImgFilePath = Path.Combine(ImgFold, ImgFileName);
                    if (System.IO.File.Exists(ImgFilePath))
                    {
                        System.IO.File.Delete(ImgFilePath);
                    }
                    if (!string.IsNullOrEmpty(tempRecord?.JsonPath))
                    {
                        string TempFold = Path.Combine(_env.WebRootPath, "TempManager");
                        var JsonTemp = tempRecord.JsonPath;
                        string TempFileName = Path.GetFileName(JsonTemp);
                        string JsonFilePath = Path.Combine(TempFold, TempFileName);
                        if (System.IO.File.Exists(JsonFilePath))
                        {
                            System.IO.File.Delete(JsonFilePath);
                        }
                    }
                    res = new
                    {
                        message = "Template delete from database and fileManager.",
                        state = true
                    };
                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
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
                    string relativePath = Path.Combine("TempManager", fileName).Replace("\\", "/");

                    var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == FileName);
                    if (exists)
                    {
                        // DB table is update -- DONE
                        var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.FileName == FileName);
                        var fileNameDel = ReturnDetails.JsonPath;    // update
                        ReturnDetails.JsonPath = relativePath;
                        await _dbContext.SaveChangesAsync();

                        // delete old File 
                        var delOldRelative = fileNameDel;
                        var delOldFullPath = Path.Combine(_env.WebRootPath, delOldRelative.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (System.IO.File.Exists(delOldFullPath))
                        {
                            System.IO.File.Delete(delOldFullPath);
                        }

                        // save into root folder
                        using (var TempSet = new FileStream(filePath, FileMode.Create))
                        {
                            await tempName.CopyToAsync(TempSet);
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
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }
    }
}
