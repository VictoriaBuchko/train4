using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class Seat
    {
        [Key]
        public int seat_id { get; set; }

        [Required]
        [Display(Name = "Номер места")]
        public int seat_number { get; set; }

        [Required]
        [Display(Name = "ID типа вагона поезда")]
        public int train_carriage_types_id { get; set; }

        [ForeignKey("train_carriage_types_id")]
        public virtual TrainCarriageTypes TrainCarriageTypes { get; set; }

        public virtual ICollection<Ticket> Tickets { get; set; }
    }
}
