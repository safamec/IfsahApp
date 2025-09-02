using System;

namespace IfsahApp.Models
{
    public abstract class BaseEntity
    {
        // Automatically set when a new entity is created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // For entities like Disclosure that need a submitted date
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
