
using Microsoft.SemanticKernel;
using System.ComponentModel;

public sealed class TimeInformation
{
   [KernelFunction]
   [Description("Retrieves the current time in UTC.")]
   public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");


   [KernelFunction(name: "GetMyTimeZone"),
   Description("etrieves the current timezone")]
   public string GetMyTimeZone()
   {
      // Puoi usare TimeZoneInfo.Local oppure impostare in base utente
      return TimeZoneInfo.Local.StandardName;
   }
}
