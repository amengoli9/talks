using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Piadineria.Analyzers;

/// <summary>
/// §7b — La fitness function più "a sinistra" possibile: gira NEL COMPILATORE.
/// Stessa regola del controller→DbContext, ma il feedback è una squiggle rossa
/// nell'IDE e una build rotta — prima ancora della CI.
///
/// Sugli assi: atomic · triggered (a compile-time) · static · automated · intentional.
/// </summary>
// [DiagnosticAnalyzer] dice a Roslyn: "questa classe è un analyzer, caricala
// durante la compilazione". LanguageNames.CSharp = la attivo solo su codice C#.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerDbContextAnalyzer : DiagnosticAnalyzer
{
    // L'ID del diagnostic: è ciò che vedi nell'output ("error ARCH001"), nei
    // file .editorconfig per regolarne la severità, e nelle #pragma di soppressione.
    public const string DiagnosticId = "ARCH001";

    // Il "template" del messaggio di errore. Lo definisco una volta sola (static
    // readonly) perché Roslyn istanzia l'analyzer di continuo: zero allocazioni inutili.
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Un Controller non deve dipendere da DbContext",
        // {0} è un segnaposto: viene riempito dall'argomento passato a Diagnostic.Create.
        messageFormat: "Il controller '{0}' espone un DbContext: viola il layering dell'esagonale",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,   // ERROR = fa fallire la build (Warning = solo squiggle)
        isEnabledByDefault: true,
        description: "I controller parlano con il dominio, mai direttamente col database.");

    // Roslyn chiede in anticipo TUTTI i diagnostic che questo analyzer può emettere.
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    // Punto d'ingresso: qui dico A COSA voglio reagire durante l'analisi.
    public override void Initialize(AnalysisContext context)
    {
        // Non analizzo codice auto-generato (es. .Designer.cs, source generators).
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Permetto a Roslyn di analizzare file diversi in parallelo: più veloce.
        context.EnableConcurrentExecution();
        // Mi "iscrivo" a livello di SIMBOLO (modello semantico, non testo): mi
        // sveglio per ogni campo e ogni proprietà del progetto analizzato.
        context.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext ctx)
    {
        // ctx.Symbol è il campo/proprietà su cui mi sono svegliato. Ne estraggo il TIPO.
        ITypeSymbol? memberType = ctx.Symbol switch
        {
            IFieldSymbol f => f.Type,
            IPropertySymbol p => p.Type,
            _ => null
        };
        if (memberType is null) return;

        // ContainingType = la classe che possiede il campo. Mi interessa solo se è
        // un Controller (convenzione di naming ASP.NET Core).
        var owner = ctx.Symbol.ContainingType;
        if (owner is null || !owner.Name.EndsWith("Controller", StringComparison.Ordinal)) return;

        // Cuore della regola: un Controller che tiene un DbContext → diagnostic.
        // Locations[0] = dove disegnare la squiggle rossa nell'editor.
        // owner.Name finisce nel {0} del messageFormat.
        if (InheritsFromDbContext(memberType))
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Symbol.Locations[0], owner.Name));
    }

    // Risalgo la catena di ereditarietà: il tipo è (o deriva da) EF Core DbContext?
    // Uso ToDisplayString() per il match sul nome completo, senza dover referenziare
    // l'assembly di EF dentro l'analyzer.
    private static bool InheritsFromDbContext(ITypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.ToDisplayString() == "Microsoft.EntityFrameworkCore.DbContext")
                return true;
        return false;
    }
}
