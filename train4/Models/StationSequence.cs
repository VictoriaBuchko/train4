using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace train2.Models
{
    public class StationSequence
    {
        [Key]
        public int sequence_id { get; set; }

        [Required]
        [Display(Name = "ID поезда")]
        public int train_id { get; set; }

        [Required]
        [Display(Name = "ID станции")]
        public int station_id { get; set; }

        [Display(Name = "ID следующей станции")]
        public int? next_station_sequence_id { get; set; }

        [Display(Name = "ID предыдущей станции")]
        public int? previous_station_sequence_id { get; set; }

        [Required]
        [Display(Name = "Длительность поездки")]
        public TimeSpan travel_duration { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [Display(Name = "Расстояние (км)")]
        public int distance_km { get; set; }

        // Навигационные свойства
        [ForeignKey("train_id")]
        public virtual Train Train { get; set; }

        [ForeignKey("station_id")]
        public virtual Stations Stations { get; set; }

        [ForeignKey("next_station_sequence_id")]
        public virtual StationSequence NextStation { get; set; }

        [ForeignKey("previous_station_sequence_id")]
        public virtual StationSequence PreviousStation { get; set; }

        public virtual ICollection<StationSequence> NextStations { get; set; }
        public virtual ICollection<StationSequence> PreviousStations { get; set; }
    }
}
