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
        public OmrProcessingController(OmrProcessingService omrService, IWebHostEnvironment env)
        {
            _omrService = omrService;
            _env = env;
        }

        // Process OMR Sheet -
        [HttpPost("process-omr")]
        public async Task<IActionResult> ProcessOmrSheet(IFormFile template, string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return BadRequest("Folder path is invalid.");
            }
            var imageFiles = Directory.GetFiles(folderPath, "*.*").Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg")).ToList();

            // Save template once
            string templatePath = Path.Combine(_env.WebRootPath, template.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(templatePath)!);
            using (var stream = new FileStream(templatePath, FileMode.Create))
            {
                await template.CopyToAsync(stream);
            }
            var results = new List<OmrResult>();
            foreach (var imagePath in imageFiles)
            {
                // Create Uploads folder path on root
                string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
                // Make unique file name with scanFile_ prefix
                string fileExtension = imagePath;
                // Make new destination image path in Uploads
                string newImagePath = Path.Combine(uploadsFolder, fileExtension);
                var res = await _omrService.ProcessOmrSheet(newImagePath, templatePath);
                results.Add(res);
            }
            return Ok(results);
        }

    }
}