using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class StationPrices
    {
        [Key]
        public int price_id { get; set; }

        [Required]
        [Display(Name = "ID станції відправлення")]
        public int from_sequence_id { get; set; }

        [Required]
        [Display(Name = "ID станції прибуття")]
        public int to_sequence_id { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Display(Name = "Ціна (грн)")]
        public decimal price { get; set; }

        [Required]
        [Display(Name = "Дата встановлення ціни")]
        public DateTime curr_date { get; set; } = DateTime.Now;

        // ИЗМЕНЕНО: Навігаційні властивості теперь ссылаются на Stations
        [ForeignKey("from_sequence_id")]
        public virtual Stations FromStation { get; set; }

        [ForeignKey("to_sequence_id")]
        public virtual Stations ToStation { get; set; }
    }
}
