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
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        private readonly OmrProcessingControlService _controlService;


        public OmrProcessingController(
            OmrProcessingService omrService, 
            IWebHostEnvironment env, 
            ApplicationDbContext dbContext, 
            RecordDBClass recordTable, 
            WebSoketHandler webSocketHandler,  
            OmrProcessingControlService controlService)
        {
            _omrService = omrService;
            _env = env;
            _dbContext = dbContext;
            _recordTable = recordTable;
            _webSocketHandler = webSocketHandler;
            if (controlService == null)
            {
                throw new ArgumentNullException(nameof(controlService), "OmrProcessingControlService is not injected properly.");
            }
            _controlService = controlService;
        }

        //  Process OMR Sheet  
        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(string folderPath, int idTemp, string token)
        {
            // Token handler UserId Extract
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;



            // Exist path
            var sharefolder = Path.Combine("wFileManager/" + folderPath);
            folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wFileManager/" + folderPath);
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
            foreach (var imagePath  in imageFiles)
            {
                 _controlService.WaitIfPaused();
                var res = await _omrService.ProcessOmrSheet(imagePath, templatePath, sharefolder);

                results.Add(res);

                string jsonResult = JsonSerializer.Serialize(res);

                await _webSocketHandler.UserMessageAsync(userId, jsonResult);

                // Make table design 
                if (crttb == 1)
                {
                    var tableCrt = await _recordTable.TableCreation(res, idTemp);
                }
                crttb++;
            }
            return Ok(results);
        }

        // Data Push procesing
        [HttpPost("pause-processing")]
        public IActionResult PauseProcessing()
        {
            _controlService.PauseProcessing();
            return Ok("Processing paused.");
        }

        [HttpPost("resume-processing")]
        public IActionResult ResumeProcessing()
        {
            _controlService.ResumeProcessing();
            return Ok("Processing resumed.");
        }

    }
}
