//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//public class AppService : IHostedService
//{
//   private readonly IGreetingService _greetingService;
//   private readonly IAIService _aiService;
//   private readonly ILogger<AppService> _logger;
//   private readonly IHostApplicationLifetime _lifetime;

//   public AppService(
//       IGreetingService greetingService,
//       IAIService aiService,
//       ILogger<AppService> logger,
//       IHostApplicationLifetime lifetime)
//   {
//      _greetingService = greetingService;
//      _aiService = aiService;
//      _logger = logger;
//      _lifetime = lifetime;
//   }

//   public async Task StartAsync(CancellationToken cancellationToken)
//   {
//      _logger.LogInformation("Applicazione avviata alle {Time}", DateTime.Now);

//      try
//      {
//         // Saluto tradizionale
//         await _greetingService.SayHelloAsync("Mondo");

//         Console.WriteLine("\n" + new string('=', 50));
//         Console.WriteLine("DEMO SEMANTIC KERNEL");
//         Console.WriteLine(new string('=', 50));

//         // Avvia interfaccia interattiva
//         await DemoSemanticKernel();
//      }
//      catch (Exception ex)
//      {
//         _logger.LogError(ex, "Errore durante l'esecuzione dell'applicazione");
//      }
//      finally
//      {
//         _lifetime.StopApplication();
//      }
//   }

//   private async Task DemoSemanticKernel()
//   {
//      ShowMenu();

//      while (true)
//      {
//         Console.Write("\n> ");
//         var input = Console.ReadLine()?.Trim();

//         if (string.IsNullOrEmpty(input))
//            continue;

//         try
//         {
//            await ProcessUserInput(input);
//         }
//         catch (Exception ex)
//         {
//            _logger.LogError(ex, "Errore durante l'elaborazione del comando");
//            Console.WriteLine("❌ Errore durante l'elaborazione. Riprova.");
//         }
//      }
//   }

//   private void ShowMenu()
//   {
//      Console.WriteLine("\n🎯 COMANDI DISPONIBILI:");
//      Console.WriteLine("1. 'chat [messaggio]' - Chatta con l'AI");
//      Console.WriteLine("2. 'traduci [testo] in [lingua]' - Traduci testo");
//      Console.WriteLine("3. 'riassumi [testo]' - Riassumi un testo");
//      Console.WriteLine("4. 'help' - Mostra questo menu");
//      Console.WriteLine("5. 'demo' - Esegui demo automatica");
//      Console.WriteLine("6. 'exit' - Esci dall'applicazione");
//      Console.WriteLine("\nEsempi:");
//      Console.WriteLine("• chat Spiegami cos'è Semantic Kernel");
//      Console.WriteLine("• traduci Buongiorno mondo in inglese");
//      Console.WriteLine("• riassumi L'AI sta cambiando il mondo...");
//   }

//   private async Task ProcessUserInput(string input)
//   {
//      var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
//      var command = parts[0].ToLower();

//      switch (command)
//      {
//         case "exit":
//         case "quit":
//         case "uscita":
//            Console.WriteLine("👋 Arrivederci!");
//            _lifetime.StopApplication();
//            return;

//         case "help":
//         case "aiuto":
//            ShowMenu();
//            break;

//         case "demo":
//            await RunAutomaticDemo();
//            break;

//         case "chat":
//            if (parts.Length > 1)
//            {
//               var message = string.Join(" ", parts.Skip(1));
//               Console.WriteLine("🤖 Elaborando...");
//               var response = await _aiService.GenerateResponseAsync(message);
//               Console.WriteLine($"💬 AI: {response}");
//            }
//            else
//            {
//               Console.WriteLine("❓ Inserisci un messaggio dopo 'chat'");
//            }
//            break;

//         case "traduci":
//            await HandleTranslateCommand(input);
//            break;

//         case "riassumi":
//            if (parts.Length > 1)
//            {
//               var textToSummarize = string.Join(" ", parts.Skip(1));
//               Console.WriteLine("📝 Riassumendo...");
//               var summary = await _aiService.SummarizeTextAsync(textToSummarize);
//               Console.WriteLine($"📋 Riassunto: {summary}");
//            }
//            else
//            {
//               Console.WriteLine("❓ Inserisci il testo da riassumere dopo 'riassumi'");
//            }
//            break;

//         default:
//            // Se non è un comando specifico, trattalo come chat
//            Console.WriteLine("🤖 Elaborando come messaggio chat...");
//            var chatResponse = await _aiService.GenerateResponseAsync(input);
//            Console.WriteLine($"💬 AI: {chatResponse}");
//            break;
//      }
//   }

//   private async Task HandleTranslateCommand(string input)
//   {
//      // Esempio: "traduci Ciao mondo in inglese"
//      var match = System.Text.RegularExpressions.Regex.Match(
//          input, @"traduci\s+(.+?)\s+in\s+(.+)",
//          System.Text.RegularExpressions.RegexOptions.IgnoreCase);

//      if (match.Success)
//      {
//         var textToTranslate = match.Groups[1].Value.Trim();
//         var targetLanguage = match.Groups[2].Value.Trim();

//         Console.WriteLine($"🌍 Traducendo '{textToTranslate}' in {targetLanguage}...");
//         var translation = await _aiService.TranslateTextAsync(textToTranslate, targetLanguage);
//         Console.WriteLine($"🔤 Traduzione: {translation}");
//      }
//      else
//      {
//         Console.WriteLine("❓ Formato: traduci [testo] in [lingua]");
//         Console.WriteLine("Esempio: traduci Ciao mondo in inglese");
//      }
//   }

//   private async Task RunAutomaticDemo()
//   {
//      Console.WriteLine("\n🎬 Avviando demo automatica...");

//      try
//      {
//         // 1. Generazione di una storia
//         Console.WriteLine("\n🤖 Generando una breve storia...");
//         var story = await _aiService.GenerateResponseAsync(
//             "Scrivi una breve storia fantasy di 2-3 frasi su un drago gentile.");
//         Console.WriteLine($"📖 Storia: {story}");

//         // 2. Traduzione
//         Console.WriteLine("\n🌍 Traducendo la storia in inglese...");
//         var translation = await _aiService.(story, "inglese");
//         Console.WriteLine($"🔤 Traduzione: {translation}");

//         // 3. Riassunto
//         var longText = @"L'intelligenza artificiale sta rivoluzionando il modo in cui lavoriamo e viviamo. 
//                Dalle auto a guida autonoma agli assistenti virtuali, l'AI è diventata parte integrante 
//                della nostra vita quotidiana. Tuttavia, con questi progressi arrivano anche nuove sfide 
//                etiche e sociali che dobbiamo affrontare.";

//         Console.WriteLine("\n📝 Riassumendo un testo lungo...");
//         var summary = await _aiService.SummarizeTextAsync(longText);
//         Console.WriteLine($"📋 Riassunto: {summary}");

//         Console.WriteLine("\n✅ Demo completata!");

//      }
//      catch (Exception ex)
//      {
//         _logger.LogError(ex, "Errore durante la demo automatica");
//         Console.WriteLine("❌ Errore durante la demo AI. Controlla la configurazione della API key.");
//      }
//   }

//   public Task StopAsync(CancellationToken cancellationToken)
//   {
//      _logger.LogInformation("Applicazione terminata alle {Time}", DateTime.Now);
//      return Task.CompletedTask;
//   }
//}