using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KitchenApp.Domain.Models;
using KitchenApp.Infrastructure.Data;
using KitchenApp.Utilities;
using System.Diagnostics;

namespace KitchenApp.Infrastructure;

public class DishRepository(ILogger<DishRepository> logger, KitchenContext context, IOptions<ConnectionStringsOptions> options) : IDishRepository
{
   //public async Task<IEnumerable<Dish>> GetAllAsync()
   //{
   //   using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("Dishes.GetAllAsync");
   //   var dishes = await context.Dishes.ToListAsync();
   //   return dishes;
   //}

   public async Task<IEnumerable<Dish>> GetAllAsync()
   {
      using var activityA = ApplicationDiagnostics.ActivitySource.StartActivity("Dishes.GetAllAsync");

      //using var activityChild = ApplicationDiagnostics.ActivitySource.StartActivity("Dishes.GetAllAsyncChild");

      #region base activity
      //using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("KitchenService.GetDishes");
      #endregion

      #region activitysourceextension

      //using var activity = ApplicationDiagnostics.ActivitySource.StartActivityWithTags("KitchenService.GetDishes",
      //   new() {
      //      new("kitchen.operation.type", "get_dishes")
      //      });
      #endregion

      #region commonname
      using var activity = ApplicationDiagnostics.ActivitySource.StartActivityWithTags(KitchenDiagnosticsSemanticNames.GetDrinksActivityName,
   new() {
                  new(KitchenDiagnosticsSemanticNames.OperationType, KitchenDiagnosticsValues.Operations.GetDishes)
      });
      #endregion

      var dishes = context.Dishes.ToList();
      logger.LogInformation("Retrieved {DishCount} dishes", dishes.Count);
      foreach (var dish in dishes)
      {
         logger.LogDishInfo("Get dish", dish);
      }
      Activity.Current?.AddTag("dish.count", dishes.Count());

      ApplicationDiagnostics.cappellettiGauge.Record(Random.Shared.Next(1, 200));
      await Task.Delay(100);
      return dishes;
   }



   public async Task<IEnumerable<Dish>> GetAllNoEFAsync()
   {
      var dishes = new List<Dish>();
      using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("Dishes.GetAllAsync");
      using (var connection = new SqlConnection(options.Value.MainConnection))
      {
         await connection.OpenAsync();

         using (var command = connection.CreateCommand())
         {
            command.CommandText = @"
                    SELECT Id, Name
                    FROM Dishes";

            using (var reader = await command.ExecuteReaderAsync())
            {
               while (await reader.ReadAsync())
               {
                  dishes.Add(new Dish
                  {
                     Id = reader.GetInt32(reader.GetOrdinal("Id")),
                     Name = reader.GetString(reader.GetOrdinal("Name")),
                  });
               }
            }
         }
      }

      return dishes;
   }

   public async Task<IEnumerable<Dish>> GetAllNoEFErrorAsync()
   {
      var dishes = new List<Dish>();
      try
      {
         using (var connection = new SqlConnection(options.Value.MainConnection))
         {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
               command.CommandText = @"
                    SELECT Id, Name, Descr
                    FROM Dishes";

               using (var reader = await command.ExecuteReaderAsync())
               {
                  while (await reader.ReadAsync())
                  {
                     dishes.Add(new Dish
                     {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                     });
                  }
               }
            }
         }
      }
      catch (Exception ex)
      {
         //logger.LogAppError(ex.Message, ex);
         throw;
      }
      

      return dishes;
   }

}

