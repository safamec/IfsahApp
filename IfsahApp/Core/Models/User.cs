using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IfsahApp.Core.Enums;

namespace IfsahApp.Core.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string ADUserName { get; set; } = string.Empty; // sAMAccountName or UPN

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Department { get; set; }

        [Required]
        public Role Role { get; set; } // enum from Core.Enums

        public bool IsActive { get; set; } = true;

        // Disclosures submitted by this user
        public ICollection<Disclosure> SubmittedDisclosures { get; set; } = new List<Disclosure>();

        // Disclosures assigned to this user
        public ICollection<Disclosure> AssignedDisclosures { get; set; } = new List<Disclosure>();
    }
}
