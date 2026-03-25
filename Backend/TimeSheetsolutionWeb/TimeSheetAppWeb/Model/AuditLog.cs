using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeSheetAppWeb.Model
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TableName { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE

        [Required]
        public string KeyValues { get; set; } = string.Empty; // e.g. Id = 1

        public string? OldValues { get; set; } // JSON
        public string? NewValues { get; set; } // JSON

        public int? UserId { get; set; } // Who did it

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}