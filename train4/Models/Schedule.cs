using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class Schedule
    {
        [Key]
        public int schedule_id { get; set; }

        [Required]
        [Display(Name = "ID поезда")]
        public int train_id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Дата отправления")]
        public DateTime date { get; set; }

        [StringLength(50)]
        [Display(Name = "Дни недели")]
        public string weekdays { get; set; }

        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "Время отправления")]
        public TimeSpan departure_time { get; set; }

        // Навигационные свойства
        [ForeignKey("train_id")]
        public virtual Train Train { get; set; }

        public virtual ICollection<Ticket> Tickets { get; set; }
    }

}
