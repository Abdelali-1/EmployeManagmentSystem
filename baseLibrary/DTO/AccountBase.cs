using System.ComponentModel.DataAnnotations;

namespace baseLibrary.DTO
{
    public class AccountBase
    {
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        [Required]
        public string Email { get; set; }

        [DataType(DataType.EmailAddress)]
        [Required]
        public string Password { get; set; }
    }
}
