using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KitchenApp.Utilities;

public static class ApplicationDiagnostics
{

   public static readonly string DataAccessSourceName = "KitchenApp.DataAccess";

   public static readonly ActivitySource ActivitySource = new(DataAccessSourceName);

   public static readonly string DishAttribute = "kitchenapp.dish.id";

   public static readonly string MeterName = "KitchenApp.Metrics";

   public static Meter meter = new Meter(MeterName);

   public static readonly Gauge<int> cappellettiGauge = meter.CreateGauge<int>("cappelletti_gauge");

}

public static class ActivityExtensions
{

}

public static class ActivitySourceExtensions
{
   public static Activity? StartActivityWithTags(this ActivitySource source, string name, List<KeyValuePair<string, object?>> tags)
   {
      return source.StartActivity(
                name,
                ActivityKind.Internal,
                Activity.Current?.Context ?? new ActivityContext(),
                tags: tags);
   }
}

public static class KitchenDiagnosticsSemanticNames
{
   public const string ServiceName = "service.name";
   public const string OperationType = "kitchen.operation.type";
   public const string ItemCategory = "kitchen.item.category";
   public const string ItemCount = "kitchen.item.count";

   // Activity names
   public const string GetDishesActivityName = "KitchenService.GetDishes";
   public const string GetDrinksActivityName = "KitchenService.GetDrinks";
}

public static class KitchenDiagnosticsValues
{
   public const string ServiceNameValue = "menu-service";

   public static class Operations
   {
      public const string GetDishes = "get_dishes";
      public const string GetDrinks = "get_drinks";
   }
   public static class Categories
   {
      public const string Dish = "dish";
      public const string Drink = "drink";
   }
}

