using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Version1.Data;
using Version1.Modal;
using Version1.Services;
using Microsoft.AspNetCore;
using Vintasoft.Imaging.Codecs.ImageFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using SQCScanner.Services;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Dapper;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Microsoft.AspNetCore.SignalR;
using SQCScanner.websoketManager;
using NLog.Targets;

namespace Version1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AllowAnyOrigin")]
    public class OmrProcessingController : ControllerBase
    {
        private readonly OmrProcessingService _omrService;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _dbContext;
        private readonly RecordDBClass _recordTable;
        private readonly WebSoketHandler _webSocketHandler;


        public OmrProcessingController(OmrProcessingService omrService, IWebHostEnvironment env, ApplicationDbContext dbContext,RecordDBClass recordTable, WebSoketHandler webSocketHandler)
        {
            _omrService = omrService;
            _env = env;
            _dbContext = dbContext;
            _recordTable = recordTable;
            _webSocketHandler = webSocketHandler;
        }

        //  Process OMR Sheet  
        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(string folderPath, int idTemp)
        {
            // exist path
            folderPath = Path.Combine(_env.WebRootPath, folderPath);
            if (!Directory.Exists(folderPath))
            {
                return BadRequest("Folder path is invalid.");
            }
            var imageFiles = Directory.GetFiles(folderPath, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg")).ToList();

            // Save template once
            var Targetjson = string.Empty;
            var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == idTemp);
            if (ReturnDetails != null)
            {
                if (!string.IsNullOrEmpty(ReturnDetails.JsonPath))
                {
                    Targetjson = ReturnDetails.JsonPath.Replace("\\", "/");
                }
                else
                {
                    BadRequest("Template not found");
                }
            }
            else
            {
                BadRequest("Id is invalid please add Template first");
            }
            string templatePath = Path.Combine(_env.WebRootPath, Targetjson);
           
            var results = new List<OmrResult>();
            var crttb = 1;
            foreach (var imagePath in imageFiles)
            {
                // Create Uploads folder path on root
                string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
                // Make unique file name with scanFile_ prefix
                string fileExtension = imagePath;
                // Make new destination image path in Uploads
                string newImagePath = Path.Combine(uploadsFolder, fileExtension);
                if (crttb == 1)
                {
                    var tableCrt = await _recordTable.TableCreation(newImagePath, templatePath);
                }
                
                var res = await _omrService.ProcessOmrSheet(newImagePath, templatePath);
                results.Add(res);
                string jsonResult = JsonSerializer.Serialize(res);
                // 0.11ms - 0.20ms-mx
                await _webSocketHandler.BroadcastMessageAsync(jsonResult);
                crttb++;
            }
            return Ok(results);
        }
    }
}