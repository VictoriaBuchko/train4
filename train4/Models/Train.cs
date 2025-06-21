using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class Train
    {
        [Key]
        public int train_id { get; set; }

        [Required]
        [Display(Name = "Номер поезда")]
        public int train_number { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        [Display(Name = "Количество вагонов")]
        public int carriage_count { get; set; }

        [Display(Name = "Активный")]
        public bool is_active { get; set; } = true;

        // Навигационные свойства
        public virtual ICollection<TrainCarriageTypes> TrainCarriageTypes { get; set; }
        public virtual ICollection<Schedule> Schedules { get; set; }
        public virtual ICollection<StationSequence> StationSequences { get; set; }
    }
}
