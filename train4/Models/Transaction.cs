using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class Transactions
    {
        [Key]
        public int transaction_id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Дата транзакции")]
        public DateTime transaction_date { get; set; }

        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "Время транзакции")]
        public TimeSpan transaction_time { get; set; }

        [Required]
        [Display(Name = "ID билета")]
        public int ticket_id { get; set; }

        // Навигационные свойства
        [ForeignKey("ticket_id")]
        public virtual Ticket Ticket { get; set; }
    }
}
