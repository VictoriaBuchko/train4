using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Transactions;

namespace train2.Models
{
    public class Ticket
    {
        [Key]
        public int ticket_id { get; set; }

        [Required]
        [Display(Name = "ID места")]
        public int seat_id { get; set; }

        [Required]
        [Display(Name = "ID клиента")]
        public int client_id { get; set; }

        [Required]
        [Display(Name = "ID расписания")]
        public int schedule_id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Дата бронирования")]
        public DateTime booking_date { get; set; }

        // НОВЫЕ ПОЛЯ
        [Required]
        [Range(0, double.MaxValue)]
        [Display(Name = "Общая стоимость")]
        public decimal total_price { get; set; }

        [Required]
        [Display(Name = "ID станции отправления")]
        public int from_sequence_id { get; set; }

        [Required]
        [Display(Name = "ID станции прибытия")]
        public int to_sequence_id { get; set; }

        // Навигационные свойства
        [ForeignKey("seat_id")]
        public virtual Seat Seat { get; set; }

        [ForeignKey("client_id")]
        public virtual Client Client { get; set; }

        [ForeignKey("schedule_id")]
        public virtual Schedule Schedule { get; set; }

        // НОВЫЕ навигационные свойства для станций
        [ForeignKey("from_sequence_id")]
        public virtual Stations FromStation { get; set; }

        [ForeignKey("to_sequence_id")]
        public virtual Stations ToStation { get; set; }

        public virtual ICollection<Transactions> Transactions { get; set; }
    }
}
