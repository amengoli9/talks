using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Menu.Utilities;

public static class ApplicationDiagnostics
{

   public static readonly string DataAccessSourceName = "KitchenApp.DataAccess";

   public static readonly ActivitySource ActivitySource = new(DataAccessSourceName);

   public static readonly string DishAttribute = "kitchenapp.dish.id";

   public static readonly string MeterName = "Menu.Consumations";

   public static Meter meter = new Meter(MeterName);

   public static Counter<long> PiadinaConsumed = meter.CreateCounter<long>("piadina.consumed", description: "The number of piadina consumed");
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


