using System.Collections.Generic;
using System.Reflection.Emit;
using System.Transactions;
using train2.Models;
using Microsoft.EntityFrameworkCore;


namespace train2.Data
{
    public class RailwayDbContext : DbContext
    {
        public RailwayDbContext(DbContextOptions<RailwayDbContext> options)
            : base(options)
        {
        }

        public DbSet<Stations> Stations { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<CarriageTypes> CarriageTypes { get; set; }
        public DbSet<Train> Trains { get; set; }
        public DbSet<TrainCarriageTypes> TrainCarriageTypes { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<StationSequence> StationSequences { get; set; }




        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка таблицы client
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.email)
                .IsUnique();

            modelBuilder.Entity<Client>()
                .HasIndex(c => c.phone_number)
                .IsUnique();

            modelBuilder.Entity<Client>()
                .HasIndex(c => c.login)
                .IsUnique();

            // Настройка таблицы carriage_types
            modelBuilder.Entity<CarriageTypes>()
                .Property(ct => ct.carriage_type)
                .HasConversion<string>();

            // Настройка таблицы train
            modelBuilder.Entity<Train>()
                .HasIndex(t => t.train_number)
                .IsUnique();

            // Настройка таблицы train_carriage_types
            modelBuilder.Entity<TrainCarriageTypes>()
                .HasIndex(tct => new { tct.train_id, tct.carriage_number })
                .IsUnique();

            // Настройка таблицы station_sequence
            modelBuilder.Entity<StationSequence>()
                .HasIndex(ss => new { ss.train_id, ss.station_id })
                .IsUnique();

            // Самоссылочное отношение для StationSequence
            modelBuilder.Entity<StationSequence>()
                .HasOne(ss => ss.NextStation)
                .WithMany(ss => ss.PreviousStations)
                .HasForeignKey(ss => ss.next_station_sequence_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StationSequence>()
                .HasOne(ss => ss.PreviousStation)
                .WithMany(ss => ss.NextStations)
                .HasForeignKey(ss => ss.previous_station_sequence_id)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
