using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal
{
    public class EmpModel
    {
            [Key]
            public int EmpId { get; set; } = 0;
            public string EmpName { get; set; } = string.Empty;
            public string EmpEmail { get; set; } = string.Empty;
            public string password { get; set; } = string.Empty;
            public string contact { get; set; } = string.Empty;
            public string role { get; set; } = string.Empty;
    }
}
