using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DomainPiadineria.ArchitectureTests;

public static class Architecture
{
    public static readonly ArchUnitNET.Domain.Architecture Model =
        new ArchLoader()
            .LoadAssemblies(typeof(Domain.Sala.Order).Assembly)
            .Build();

    public static readonly IObjectProvider<IType> Cucina =
        Types().That().ResideInNamespaceMatching("^DomainPiadineria\\.Domain\\.Cucina").As("Dominio Cucina");

    public static readonly IObjectProvider<IType> Sala =
        Types().That().ResideInNamespaceMatching("^DomainPiadineria\\.Domain\\.Sala").As("Dominio Sala");
}
