using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Menu.Utilities;

public static class ApplicationDiagnostics
{

   public static readonly string DataAccessSourceName = "SkExample";

   public static readonly ActivitySource ActivitySource = new(DataAccessSourceName);

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


