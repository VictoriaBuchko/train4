using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class TrainCarriageTypes
    {
        [Key]
        public int train_carriage_types_id { get; set; }

        [Required]
        [Display(Name = "ID поезда")]
        public int train_id { get; set; }

        [Required]
        [Display(Name = "ID типа вагона")]
        public int carriage_type_id { get; set; }

        [Required]
        [Display(Name = "Номер вагона")]
        public int carriage_number { get; set; }

        // Навигационные свойства
        [ForeignKey("train_id")]
        public virtual Train Train { get; set; }

        [ForeignKey("carriage_type_id")]
        public virtual CarriageTypes CarriageTypes { get; set; }

        public virtual ICollection<Seat> Seats { get; set; }
    }
}
