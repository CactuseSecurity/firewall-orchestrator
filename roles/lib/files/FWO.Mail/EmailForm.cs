using System.ComponentModel.DataAnnotations;

namespace FWO.Mail
{

    public class EmailForm
    {
        // [Required]
        public string? EmailSrvAddress { get; set; }

        // [Required]
        // [Range(1, 65535, ErrorMessage = "Port range must be in (1-65535)")]
        public int EmailSrvPort { get; set; }

        public string? EmailAuthUser { get; set; }
        public string? EmailAuthPassword { get; set; }
        public string? EmailSenderAddress { get; set; }
    }

}