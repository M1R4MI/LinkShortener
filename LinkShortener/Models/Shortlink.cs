using System.ComponentModel.DataAnnotations;

namespace LinkShortener.Models
{
    public class Shortlink
    {
        [Key]
        public int ID { get; set; }
        [Required]
        public string? OriginalURL { get; set; }
        [Required]
        [MaxLength(20)]
        public string? ShortURL { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public int RedirectCount { get; set; }

    }
}
