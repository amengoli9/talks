using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Menu.Utilities;

public static class ApplicationDiagnostics
{

   public static readonly string DataAccessSourceName = "KitchenApp.DataAccess";

   public static readonly ActivitySource ActivitySource = new(DataAccessSourceName);

   public static readonly string DishAttribute = "kitchenapp.dish.id";

   public static readonly string MeterName = "Menu.WeatherMetrics";

   public static Meter meter = new Meter(MeterName);

   public static Counter<long> FreezingDaysCounter = meter.CreateCounter<long>("weather.days.freezing", description: "The number of days where the temperature is below freezing");
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


