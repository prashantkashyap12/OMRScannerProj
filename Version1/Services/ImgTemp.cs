using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Services
{
    public class ImgTemp
    {
        [Key]
        public int Id {get; set;}
        public string FileName { get; set; }
        public string imgPath { get; set; }
        public string JsonPath { get; set; }
    }
}
