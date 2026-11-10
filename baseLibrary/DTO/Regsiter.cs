using System.ComponentModel.DataAnnotations;

namespace baseLibrary.DTO
{
    public class Regsiter : AccountBase
    {
        [MaxLength(100)]
        [MinLength(5)]
        [Required]
        public string FullName { get; set; }

        [Compare(nameof(Password))]
        [DataType(DataType.Password)]
        [Required]
        public string ConfirmPassword { get; set; }
    }
}
