using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using KitchenApp.Domain.Models;
using System.Collections.Generic;

namespace KitchenApp.Infrastructure.Data;

public class KitchenContext : DbContext
{
   public KitchenContext(DbContextOptions<KitchenContext> options) : base(options) { }

   public DbSet<Dish> Dishes { get; set; }
   public DbSet<Drink> Drinks { get; set; }
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
      base.OnModelCreating(modelBuilder);

      // Configurazioni per Db_Dish
      modelBuilder.Entity<Db_Dish>(entity =>
      {
         entity.HasKey(d => d.Id); // Imposta Id come chiave primaria
         entity.Property(d => d.Name)
               .IsRequired()
               .HasMaxLength(100); // Nome obbligatorio con max 100 caratteri
      });

      // Configurazioni per Db_Beer
      modelBuilder.Entity<Db_Drink>(entity =>
      {
         entity.HasKey(b => b.Id); // Imposta Id come chiave primaria
         entity.Property(b => b.Name)
               .IsRequired()
               .HasMaxLength(100); // Nome obbligatorio con max 100 caratteri
      });
   }

}
