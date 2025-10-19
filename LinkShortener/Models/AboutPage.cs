using System;
using System.ComponentModel.DataAnnotations;

namespace LinkShortener.Models
{
    public class AboutPage
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime LastModified { get; set; }

        public string? ModifiedBy { get; set; }
    }
}
