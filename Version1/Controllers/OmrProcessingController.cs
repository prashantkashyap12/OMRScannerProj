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

namespace Version1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AllowAnyOrigin")]
    public class OmrProcessingController : ControllerBase
    {
        private readonly OmrProcessingService _omrService;
        private readonly IWebHostEnvironment _env;
        private readonly WebSoketHandler _socketHandler;

        private readonly WebSocketConnectionManager _connectionManager;


        public OmrProcessingController(OmrProcessingService omrService, IWebHostEnvironment env, WebSocketConnectionManager connectionManager, WebSoketHandler socketHandler)
        {
            _omrService = omrService;
            _env = env;
            _connectionManager = connectionManager;
            _socketHandler = socketHandler;
        }

        [HttpGet("/ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                _connectionManager.AddSocket(socket);

                var buffer = new byte[1024 * 4];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }


        //  OMR Processing + WebSocket notification
        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(IFormFile template, string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return BadRequest("Folder path is invalid.");
            }

            var imageFiles = Directory.GetFiles(folderPath, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg")) .ToList();

            // Save template once
            string templatePath = Path.Combine(_env.WebRootPath, template.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(templatePath)!);
            using (var stream = new FileStream(templatePath, FileMode.Create))
            {
                await template.CopyToAsync(stream);
            }
            var results = new List<OmrResult>();
            int count = 1;

            foreach (var imagePath in imageFiles)
            {
                // Create Uploads folder path on root
                string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");

                // Make unique file name with scanFile_ prefix.
                // String fileExtension = imagePath.
                // Make new destination image path in Uploads.
                
                string newImagePath = Path.Combine(uploadsFolder, Path.GetFileName(imagePath));
                var res = await _omrService.ProcessOmrSheet(newImagePath, templatePath);
                results.Add(res);
                await _socketHandler.BroadcastMessageAsync($"Image processed");
                count++;
            }
            await _socketHandler.BroadcastMessageAsync("OMR Processing Complete");
            return Ok(results);
        } // Trigers, storage procesger, 
    }
}