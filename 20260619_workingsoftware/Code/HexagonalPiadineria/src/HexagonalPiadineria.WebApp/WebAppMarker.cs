namespace HexagonalPiadineria.WebApp;

/// <summary>
/// Tipo "segnaposto" pubblico usato come <c>TEntryPoint</c> da
/// <c>WebApplicationFactory&lt;T&gt;</c> nel progetto di load test (Piadineria.Load).
/// Serve solo a identificare l'assembly della WebApp senza dipendere dalla classe
/// <c>Program</c> generata dai top-level statement (che andrebbe in conflitto con il
/// <c>Program</c> del runner NBomber).
/// </summary>
public sealed class WebAppMarker;
