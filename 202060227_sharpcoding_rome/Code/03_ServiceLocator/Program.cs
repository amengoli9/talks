using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 3. SERVICE LOCATOR PATTERN ===\n");

#region anti-pattern
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- ANTI-PATTERN: Service Locator ---");
Console.ResetColor();

var services = new ServiceCollection();
services.AddSingleton<IEmailSender, FakeEmailSender>();
services.AddSingleton<IUserRepository, FakeUserRepository>();
services.AddTransient<BadNotificationService>();
services.AddTransient<GoodNotificationService>();

var provider = services.BuildServiceProvider();

Console.WriteLine("  BadNotificationService(IServiceProvider provider)");
Console.WriteLine("  -> Cosa serve a questa classe? Devi leggere TUTTO il codice!");
Console.WriteLine("  -> Per il test devi mockare l'intero IServiceProvider\n");

var bad = provider.GetRequiredService<BadNotificationService>();
bad.SendNotification("mario", "Ciao!");

#endregion

#region solution
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\n--- SOLUZIONE: Constructor Injection ---");
Console.ResetColor();

Console.WriteLine("  GoodNotificationService(IEmailSender, IUserRepository)");
Console.WriteLine("  -> Le dipendenze sono chiare dal costruttore");
Console.WriteLine("  -> Il test e' semplice: passa i mock direttamente\n");

var good = provider.GetRequiredService<GoodNotificationService>();
good.SendNotification("luigi", "Ciao!");

//Console.ForegroundColor = ConsoleColor.Yellow;
//Console.WriteLine("\n  Ricorda: la DI e' un'ALTERNATIVA ai pattern di accesso statico/globale.");
//Console.WriteLine("  Se inietti IServiceProvider ovunque, vanifichi i benefici della DI!");
//Console.ResetColor();
#endregion


#region interfaces and implementations

interface IEmailSender
{
    void Send(string to, string message);
}

class FakeEmailSender : IEmailSender
{
    public void Send(string to, string message)
        => Console.WriteLine($"  [Email] To: {to}, Message: {message}");
}

interface IUserRepository
{
    (string Name, string Email) GetById(string id);
}

class FakeUserRepository : IUserRepository
{
    public (string Name, string Email) GetById(string id)
        => (id, $"{id}@example.com");
}

class BadNotificationService(IServiceProvider provider)
{
    public void SendNotification(string userId, string message)
    {
        var emailSender = provider.GetRequiredService<IEmailSender>();
        var userRepo = provider.GetRequiredService<IUserRepository>();

        var user = userRepo.GetById(userId);
        emailSender.Send(user.Email, message);
        Console.WriteLine($"  [BadService] Notifica inviata a {userId}");
    }
}

class GoodNotificationService(IEmailSender emailSender, IUserRepository userRepo)
{
    public void SendNotification(string userId, string message)
    {
        var user = userRepo.GetById(userId);
        emailSender.Send(user.Email, message);
        Console.WriteLine($"  [GoodService] Notifica inviata a {userId}");
    }
}

#endregion