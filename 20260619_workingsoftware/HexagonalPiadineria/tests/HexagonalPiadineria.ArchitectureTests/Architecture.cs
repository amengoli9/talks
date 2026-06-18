using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace HexagonalPiadineria.ArchitectureTests;

public static class Architecture
{
    public static readonly ArchUnitNET.Domain.Architecture Model =
        new ArchLoader()
            .LoadAssemblies(
                typeof(Domain.Order).Assembly,
                typeof(Infrastructure.PiadineriaDbContext).Assembly,
                typeof(WebApp.Controllers.OrdersController).Assembly)
            .Build();

    public static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespaceMatching("^HexagonalPiadineria\\.Domain").As("Core (Domain)");

    public static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInNamespaceMatching("^HexagonalPiadineria\\.Infrastructure").As("Adapter di persistenza");

    public static readonly IObjectProvider<IType> WebLayer =
        Types().That().ResideInNamespaceMatching("^HexagonalPiadineria\\.WebApp").As("Adapter web");
}
