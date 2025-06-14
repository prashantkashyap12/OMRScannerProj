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
        private readonly WebSoketHandler _webSocketHandler;
        private readonly OmrProcessingControlService _controlService;
        private readonly RecordSave _SaveOnly;
        private readonly table_gen _recordTable;
        private readonly ImgSave _imgSave;


        public OmrProcessingController(
            OmrProcessingService omrService,
            IWebHostEnvironment env,
            ApplicationDbContext dbContext,
            WebSoketHandler webSocketHandler,
            OmrProcessingControlService controlService,
            RecordSave recordSave,
            table_gen recordTable,
            ImgSave imgSave

            )
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
            _SaveOnly = recordSave;
            _imgSave = imgSave;
        }

        //  Process OMR Sheet  

        // Check
        // Y/N = Save/update data into database.

        // Y/N = ReScan failure Img Folder.

        // Y/N = make web api To get Failure records from db and surve path.

        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(string folderPath, int idTemp, string token, bool IsSaveDb = true, bool failReScan = true)
        {
            // Token handler UserId Extract
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
            var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "unique_name")?.Value;


            // Y/N = ReScan failure Img Folder.
            var sharefolder = "";
            if (failReScan)
            {
                sharefolder = Path.Combine("wFileManager/" + folderPath);
            }
            else
            {
                sharefolder = Path.Combine("RejectImg/" + folderPath);
            }

            // Exist path
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
            foreach (var imagePath in imageFiles)
            {
                // Stop and Continue API Globle 
                _controlService.WaitIfPaused();

                // Scaning to get data from OMR Sheet
                var res = await _omrService.ProcessOmrSheet(imagePath, templatePath, sharefolder);
                results.Add(res);
                
                // Make table design  - Done  (Tables Are created perfactly Scan or ReScan k case)
                if (crttb == 1)
                {
                    var tableCrt = await _recordTable.TableCreation(res, idTemp);
                    Console.WriteLine(tableCrt);
                } crttb++;

                // 1. Save_Record into DB - Done
                var dbRes = null as object;
                if (IsSaveDb)
                {
                    dbRes = await _SaveOnly.RecordSaveVal(res, idTemp, userName);
                }
                Console.WriteLine(dbRes);

                // 2. Save_Sacanned Img Folder - Done  (success images or failure image are save into saprated folder)
                var stat = res.Success;
                var SaveRoot = await _imgSave.ScanedSave(_env.WebRootPath, imagePath, idTemp, stat);
                Console.WriteLine(SaveRoot);

                // 3. WS_Handler - Done
                string jsonResult = JsonSerializer.Serialize(dbRes);              // Object into JSON_STRING
                await _webSocketHandler.UserMessageAsync(userId, jsonResult);
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
