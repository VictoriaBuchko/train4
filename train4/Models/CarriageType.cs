using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class CarriageTypes
    {
        [Key]
        public int carriage_type_id { get; set; }

        [Required]
        [StringLength(15)]
        [Display(Name = "Тип вагона")]
        public string carriage_type { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        [Display(Name = "Количество мест")]
        public int seat_count { get; set; }

        // Навигационные свойства
        public virtual ICollection<TrainCarriageTypes> TrainCarriageTypes { get; set; }
    }


}
