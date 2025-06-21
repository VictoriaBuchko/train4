using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    // Оновіть клас Client в AllModels.cs

    public class Client
    {
        [Key]
        public int client_id { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Ім'я")]
        public string client_name { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Прізвище")]
        public string client_surname { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "По батькові")]
        public string client_patronymic { get; set; }

        [StringLength(50)]
        [EmailAddress]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
        [Display(Name = "Email")]
        public string email { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^\+380\((50|63|67|68|93|95|96|97|98)\)[0-9]{7}$")]
        [Display(Name = "Номер телефону")]
        public string phone_number { get; set; }

        [StringLength(255)]
        [Display(Name = "Платіжна інформація")]
        public string payment_info { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Логін")]
        public string login { get; set; }

        // Навигационные свойства
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
