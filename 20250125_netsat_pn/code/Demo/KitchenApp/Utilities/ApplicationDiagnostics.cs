using System.Diagnostics;

namespace KitchenApp.Utilities;

public static class ApplicationDiagnostics
{

   public static readonly string DataAccessSourceName = "KitchenApp.DataAccess";

   public static readonly ActivitySource ActivitySource = new(DataAccessSourceName);



}