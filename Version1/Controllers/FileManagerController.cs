using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Syncfusion.EJ2.FileManager.Base;
using Syncfusion.EJ2.FileManager.PhysicalFileProvider;

namespace SQCScanner.Controllers
{



    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    public class FileManagerController : Controller
    {

        private readonly Syncfusion.EJ2.FileManager.PhysicalFileProvider.PhysicalFileProvider _operation;
        private readonly string _root = Environment.CurrentDirectory + "\\wwwroot";

        public FileManagerController(IWebHostEnvironment hostingEnvironment)
        {
            _operation = new Syncfusion.EJ2.FileManager.PhysicalFileProvider.PhysicalFileProvider();
            _operation.RootFolder(Path.Combine(_root));
        }

        [HttpGet("folders")]
        public IActionResult GetFolders([FromQuery] string path)
        {
            try
            {
                var fullPath = Path.Combine(_root, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                {
                    return NotFound("Directory not found");
                }

                var directories = Directory.GetDirectories(fullPath)
                    .Select(d => new { Name = Path.GetFileName(d), FullPath = d })
                    .ToList();

                return Ok(directories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("folders")]
        public IActionResult CreateFolder([FromQuery] string path, [FromQuery] string folderName)
        {
            try
            {
                var fullPath = Path.Combine(_root, path ?? string.Empty, folderName);
                if (Directory.Exists(fullPath))
                {
                    return BadRequest("Directory already exists");
                }

                Directory.CreateDirectory(fullPath);
                return Ok("Directory created successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        
        [HttpPost]
        [Route("FileOperations")]
        public IActionResult FileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Action == "delete" || args.Action == "rename")
            {
                if (args.TargetPath == null && string.IsNullOrEmpty(args.Path))
                {
                    var response = new FileManagerResponse
                    {
                        Error = new ErrorDetails { Code = "401", Message = "Restricted to modify the root folder." }
                    };
                    return StatusCode(401, response);
                }
            }

            switch (args.Action)
            {
                case "read":
                    return Ok(_operation.ToCamelCase(_operation.GetFiles(args.Path, args.ShowHiddenItems)));
                case "delete":
                    return Ok(_operation.ToCamelCase(_operation.Delete(args.Path, args.Names)));
                case "copy":
                    return Ok(_operation.ToCamelCase(_operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData)));
                case "move":
                    return Ok(_operation.ToCamelCase(_operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData)));
                case "details":
                    return Ok(_operation.ToCamelCase(_operation.Details(args.Path, args.Names, args.Data)));
                case "create":
                    return Ok(_operation.ToCamelCase(_operation.Create(args.Path, args.Name)));
                case "search":
                    return Ok(_operation.ToCamelCase(_operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive)));
                case "rename":
                    return Ok(_operation.ToCamelCase(_operation.Rename(args.Path, args.Name, args.NewName, false, args.ShowFileExtension, args.Data)));
                default:
                    return BadRequest("Invalid action.");
            }
        }







        [Route("Upload")]
        [HttpPost]
        public IActionResult Upload([FromForm] string path, [FromForm] IList<IFormFile> uploadFiles, [FromForm] string action)
        {
            try
            {
                foreach (var file in uploadFiles)
                {
                    var folders = file.FileName.Split('/');
                    if (folders.Length > 1)
                    {
                        for (var i = 0; i < folders.Length - 1; i++)
                        {
                            string newDirectoryPath = Path.Combine(_root + path, folders[i]);
                            if (Path.GetFullPath(newDirectoryPath) != Path.GetDirectoryName(newDirectoryPath) + Path.DirectorySeparatorChar + folders[i])
                            {
                                throw new UnauthorizedAccessException("Access denied for Directory-traversal");
                            }
                            if (!Directory.Exists(newDirectoryPath))
                            {
                                _operation.Create(path, folders[i]);
                            }
                            path += folders[i] + "/";
                        }
                    }
                }

                var uploadResponse = _operation.Upload(path, uploadFiles, action);    
                if (uploadResponse.Error != null)
                {
                    return StatusCode(Convert.ToInt32(uploadResponse.Error.Code), uploadResponse);
                }

                return Ok(uploadResponse);
            }
            catch (Exception e)
            {
                var errorResponse = new ErrorDetails
                {
                    Message = "Access denied for Directory-traversal",
                    Code = "417"
                };
                return StatusCode(Convert.ToInt32(errorResponse.Code), errorResponse);
            }
        }










        [Route("Download")]
        [HttpPost]
        public IActionResult Download([FromForm] string downloadInput)
        {
            var args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return _operation.Download(args.Path, args.Names, args.Data);
        }

        [Route("GetImage")]
        [HttpGet]
        public IActionResult GetImage([FromQuery] string path, [FromQuery] string id)
        {
            var args = new FileManagerDirectoryContent
            {
                Path = path,
                Id = id
            };
            return _operation.GetImage(args.Path, args.Id, false, null, null);
        }
    }
}
