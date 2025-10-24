
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Text;
using System.Text.Json;



public sealed class TimeInformation
{
   [KernelFunction]
   [Description("Retrieves the current time in UTC.")]
   public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");


   [KernelFunction(name: "GetMyCurrentCity"),
   Description("Retrieves my current city")]
   public string GetPos()
   {
      return "Roma";
   }
}

