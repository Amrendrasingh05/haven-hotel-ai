
using HotelManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Api.Data;

public class HotelDbContext : DbContext
{
    public HotelDbContext(DbContextOptions<HotelDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Guest> Guests => Set<Guest>();

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Reservation>()
            .HasOne(reservation => reservation.Room)
            .WithMany()
            .HasForeignKey(reservation => reservation.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(reservation => reservation.Guest)
            .WithMany(guest => guest.Reservations)
            .HasForeignKey(reservation => reservation.GuestId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Reservation>()
            .Property(reservation => reservation.TotalPrice)
            .HasPrecision(12, 2);

        modelBuilder.Entity<Reservation>()
            .HasIndex(reservation => new
            {
                reservation.RoomId,
                reservation.CheckInDate,
                reservation.CheckOutDate
            });

        // Each guest phone number must be unique.
        modelBuilder.Entity<Guest>()
            .HasIndex(guest => guest.Phone)
            .IsUnique();

        modelBuilder.Entity<Guest>()
            .Property(guest => guest.FullName)
            .IsRequired();

        modelBuilder.Entity<Guest>()
            .Property(guest => guest.Phone)
            .IsRequired();

        modelBuilder.Entity<GuestSession>(entity =>
{
    entity.HasIndex(s => s.TokenHash)
        .IsUnique();

    entity.HasOne(s => s.Guest)
        .WithMany()
        .HasForeignKey(s => s.GuestId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne(s => s.Reservation)
        .WithMany()
        .HasForeignKey(s => s.ReservationId)
        .OnDelete(DeleteBehavior.Restrict);
    });
    }
}
