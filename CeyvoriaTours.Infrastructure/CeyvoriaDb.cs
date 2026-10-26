using CeyvoriaTours.Domain;
using Microsoft.EntityFrameworkCore;
namespace CeyvoriaTours.Infrastructure;
public class CeyvoriaDb(DbContextOptions<CeyvoriaDb> options):DbContext(options){
 public DbSet<Tour> Tours=>Set<Tour>();public DbSet<Destination> Destinations=>Set<Destination>();
 public DbSet<Experience> Experiences=>Set<Experience>();public DbSet<TravelStyle> TravelStyles=>Set<TravelStyle>();
 public DbSet<BlogPost> BlogPosts=>Set<BlogPost>();public DbSet<Faq> Faqs=>Set<Faq>();
 public DbSet<ContactMessage> ContactMessages=>Set<ContactMessage>();
 public DbSet<CustomTripRequest> CustomTrips=>Set<CustomTripRequest>();
 public DbSet<Booking> Bookings=>Set<Booking>();public DbSet<PaymentRecord> Payments=>Set<PaymentRecord>();
 public DbSet<AppUser> Users=>Set<AppUser>();
 protected override void OnModelCreating(ModelBuilder model){
  base.OnModelCreating(model);
  model.Entity<Tour>().HasIndex(x=>x.Slug).IsUnique();
  model.Entity<Destination>().HasIndex(x=>x.Slug).IsUnique();
  model.Entity<Experience>().HasIndex(x=>x.Slug).IsUnique();
  model.Entity<TravelStyle>().HasIndex(x=>x.Slug).IsUnique();
  model.Entity<BlogPost>().HasIndex(x=>x.Slug).IsUnique();
  model.Entity<AppUser>().HasIndex(x=>x.Email).IsUnique();
  model.Entity<ContactMessage>().HasIndex(x=>x.Reference).IsUnique();
  model.Entity<CustomTripRequest>().HasIndex(x=>x.Reference).IsUnique();
  model.Entity<Booking>().HasIndex(x=>x.Reference).IsUnique();
  model.Entity<PaymentRecord>().HasIndex(x=>x.ProviderReference);
  model.Entity<Booking>().HasIndex(x=>new{x.Status,x.CreatedAt});
  model.Entity<CustomTripRequest>().HasIndex(x=>new{x.Status,x.CreatedAt});
  model.Entity<PaymentRecord>().Property(x=>x.Amount).HasPrecision(18,2);
  model.Entity<Booking>().Property(x=>x.ConfirmedAmount).HasPrecision(18,2);
  model.Entity<Tour>().Property(x=>x.StartingPrice).HasPrecision(18,2);
  model.Entity<CustomTripRequest>().Property(x=>x.Budget).HasPrecision(18,2);
 }
 public override Task<int> SaveChangesAsync(CancellationToken cancellationToken=default){
  foreach(var entry in ChangeTracker.Entries<Entity>()){
   if(entry.State==EntityState.Modified)entry.Entity.UpdatedAt=DateTimeOffset.UtcNow;
  }
  return base.SaveChangesAsync(cancellationToken);
 }
}
