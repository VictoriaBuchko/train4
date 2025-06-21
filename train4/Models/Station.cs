using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class Stations
    {
        [Key]
        public int station_id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Название станции")]
        public string station_name { get; set; }

        // Навигационные свойства
        public virtual ICollection<StationSequence> StationSequences { get; set; }

        // Новые навигационные свойства для билетов
        public virtual ICollection<Ticket> TicketsFrom { get; set; }
        public virtual ICollection<Ticket> TicketsTo { get; set; }

        // Новые навигационные свойства для цен
        public virtual ICollection<StationPrices> PricesFrom { get; set; }
        public virtual ICollection<StationPrices> PricesTo { get; set; }
    }
}
