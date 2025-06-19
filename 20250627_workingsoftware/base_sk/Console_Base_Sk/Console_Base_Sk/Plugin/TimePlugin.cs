

using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Console_Base_Sk.Plugin;


public class TimePlugin
{

   [KernelFunction]
   [Description("Ottiene l'ora attuale")]
   public async Task<DateTime> GetCurrentSystemTime(Kernel kernel)
   {
      Console.WriteLine("Ottengo l'ora attuale usando la funzione TimePlugin...");
      await Task.Delay(1000);
      var result = DateTime.UtcNow;

      Console.WriteLine($"Ora attuale: {result.ToString()}");
      return result;
   }
   [KernelFunction]
   [Description("Ottiene il fuso orario locale")]
   public async Task<string> GetCurrentTimezone(Kernel kernel)
   {

      await Task.Delay(500);
      TimeZoneInfo result = TimeZoneInfo.Local;

      return result.ToSerializedString();
   }

   [KernelFunction]
   [Description("Analizza il sentimento del testo e restituisce un punteggio da -1 (molto negativo) a 1 (molto positivo)")]
   public async Task<string> AnalyzeSentiment(Kernel kernel, string text)
   {
      var function = kernel.CreateFunctionFromPrompt(
          @"Analizza il sentimento del seguente testo e assegna un punteggio da -1 (molto negativo) a 1 (molto positivo).
                Restituisci solo il numero del punteggio.
                
                Testo: {{$text}}");

      var result = await kernel.InvokeAsync(
          function,
          new KernelArguments { ["text"] = text }
      );

      return result.ToString();
   }

   [KernelFunction]
   [Description("Estrae le parole chiave dal testo fornito")]
   public async Task<string> ExtractKeywords(Kernel kernel, string text)
   {
      var function = kernel.CreateFunctionFromPrompt(
          @"Estrai le 5 parole chiave più importanti dal seguente testo. 
                Restituisci solo le parole chiave separate da virgole, senza numerazione o punti.
                
                Testo: {{$text}}");

      var result = await kernel.InvokeAsync(
          function,
          new KernelArguments { ["text"] = text }
      );

      return result.ToString();
   }

   [KernelFunction]
   [Description("Categorizza il testo in una delle seguenti categorie: Tecnologia, Salute, Finanza, Intrattenimento, Sport, Altro")]
   public async Task<string> CategorizeText(Kernel kernel, string text)
   {
      var function = kernel.CreateFunctionFromPrompt(
          @"Categorizza il seguente testo in una delle seguenti categorie: 
                Tecnologia, Salute, Finanza, Intrattenimento, Sport, Altro.
                Restituisci solo il nome della categoria.
                
                Testo: {{$text}}");

      var result = await kernel.InvokeAsync(
          function,
          new KernelArguments { ["text"] = text }
      );

      return result.ToString();
   }
}