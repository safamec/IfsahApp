// Web/Controllers/AdminPageViewModels.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace IfsahApp.Web.Controllers
{
    public class ExaminerRowVM
    {
        public int Id { get; set; }
        public string ADUserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Department { get; set; }

        /// <summary> "Admin", "Examiner", or "User" </summary>
        public string Role { get; set; } = "User";

        /// <summary>True if an active Admin delegation exists right now.</summary>
        public bool HasActiveTempAdmin { get; set; }
    }
}




