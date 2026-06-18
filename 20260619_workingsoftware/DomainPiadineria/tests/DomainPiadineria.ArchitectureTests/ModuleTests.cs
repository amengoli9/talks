using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;
using static DomainPiadineria.ArchitectureTests.Architecture;

namespace DomainPiadineria.ArchitectureTests;

public class ModuleTests
{
    [Fact]
    public void Cucina_should_not_depend_on_Sala()
    {
        IArchRule rule = Types().That().Are(Cucina)
            .Should().NotDependOnAny(Sala)
            .Because("la Cucina prepara le piade senza sapere nulla di tavoli e conti");

        rule.Check(Model);
    }

    [Fact]
    public void Sala_should_not_depend_on_Cucina()
    {
        IArchRule rule = Types().That().Are(Sala)
            .Should().NotDependOnAny(Cucina)
            .Because("la Sala gestisce il conto con uno snapshot della piada, non con l'entità della Cucina");

        rule.Check(Model);
    }

    [Fact]
    public void Domains_should_be_free_of_cycles()
    {
        Slices().Matching("DomainPiadineria.Domain.(*)")
            .Should().BeFreeOfCycles()
            .Check(Model);
    }

    [Fact]
    public void Domains_should_be_persistence_ignorant()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching("^DomainPiadineria\\.Domain")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore"))
            .Because("i due domini sono puri: nessuna conoscenza di EF o della persistenza");

        rule.Check(Model);
    }

    [Fact]
    public void Domain_aggregates_should_be_sealed()
    {
        IArchRule rule = Classes().That().ResideInNamespaceMatching("^DomainPiadineria\\.Domain")
            .Should().BeSealed()
            .Because("gli aggregati di dominio non sono pensati per essere estesi per ereditarietà");

        rule.Check(Model);
    }
}
