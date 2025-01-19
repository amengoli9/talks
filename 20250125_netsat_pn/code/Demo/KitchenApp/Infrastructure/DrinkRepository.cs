﻿using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using KitchenApp.Domain.Models;
using KitchenApp.Infrastructure.Data;
using KitchenApp.Utilities;

namespace KitchenApp.Infrastructure;

public class DrinkRepository(ILogger<DrinkRepository> logger, KitchenContext context, IOptions<ConnectionStringsOptions> options) : IDrinkRepository
{
   public IEnumerable<Drink> GetAll()
   {
      var dishes = context.Drinks.ToList();
      return dishes;
   }

   public async Task<IEnumerable<Drink>> GetAllAsync()
   {
      var dishes = await context.Drinks.ToListAsync();
      return dishes;
   }

   public async Task<IEnumerable<Drink>> GetAllNoEFAsync()
   {
      var drinks = new List<Drink>();

      using (var connection = new SqlConnection(options.Value.MainConnection))
      {
         await connection.OpenAsync();

         using (var command = connection.CreateCommand())
         {
            command.CommandText = @"
                    SELECT Id, Name
                    FROM Drinks";

            using (var reader = await command.ExecuteReaderAsync())
            {
               while (await reader.ReadAsync())
               {
                  drinks.Add(new Drink
                  {
                     Id = reader.GetInt32(reader.GetOrdinal("Id")),
                     Name = reader.GetString(reader.GetOrdinal("Name")),
                  });
               }
            }
         }
      }

      return drinks;
   }

   public async Task<IEnumerable<Drink>> GetAllNoEFErrorAsync()
   {
      var drinks = new List<Drink>();
      try
      {
         using (var connection = new SqlConnection(options.Value.MainConnection))
         {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
               command.CommandText = @"
                    SELECT Id, Name, Descr
                    FROM Drinks";

               using (var reader = await command.ExecuteReaderAsync())
               {
                  while (await reader.ReadAsync())
                  {
                     drinks.Add(new Drink
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
      

      return drinks;
   }

}

