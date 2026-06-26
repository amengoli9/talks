using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Microsoft.EntityFrameworkCore;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static HexagonalPiadineria.ArchitectureTests.Architecture;

namespace HexagonalPiadineria.ArchitectureTests;

public class HexagonalTests
{
    [Fact]
    public void Core_should_not_depend_on_adapters()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(WebLayer)
            .Because("il dominio è il centro dell'esagono: niente EF, niente ASP.NET, niente adapter");

        rule.Check(Model);
    }

    [Fact]
    public void Controller_should_not_depend_on_persistence()
    {
        IArchRule rule = Classes().That().HaveNameEndingWith("Controller")
            .Should().NotDependOnAny(typeof(DbContext))
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .Because("un controller non accede mai direttamente al database: passa dalla porta");

        rule.Check(Model);
    }

    [Fact]
    public void Concrete_repositories_live_in_infrastructure()
    {
        IArchRule rule = Classes().That().HaveNameEndingWith("Repository")
            .Should().Be(InfrastructureLayer)
            .Because("le implementazioni concrete dei repository sono dettagli infrastrutturali");

        rule.Check(Model);
    }

    [Fact]
    public void Ports_should_be_interfaces()
    {
        IArchRule rule = Classes().That().ResideInNamespaceMatching("^HexagonalPiadineria\\.Domain\\.Ports")
            .Should().NotExist()
            .Because("le porte dell'esagono sono contratti: solo interfacce, mai classi concrete");

        rule.Check(Model);
    }

    [Fact]
    public void Core_should_be_persistence_ignorant()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore"))
            .Because("il core non conosce EF Core: la persistenza vive solo nell'adapter");

        rule.Check(Model);
    }


}
