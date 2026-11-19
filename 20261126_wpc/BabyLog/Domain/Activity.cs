namespace BabyLog.Domain;

public sealed record Activity(Guid Id, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int? Milliliters, string? Notes)
{
    public static Activity Create(string kind, DateTimeOffset start, DateTimeOffset? end, int? milk, string? notes)
    {
        if (kind is not ("milk" or "diaper" or "sleep")) throw new ArgumentException("Tipo di attività non valido.");
        if (start == default) throw new ArgumentException("Indicare data e ora.");
        if (notes?.Length > 200) throw new ArgumentException("Le note possono contenere al massimo 200 caratteri.");
        if (kind == "milk" && milk is not (>= 1 and <= 500)) throw new ArgumentException("Il latte deve essere tra 1 e 500 ml.");
        if (kind != "milk" && milk is not null) throw new ArgumentException("La quantità è prevista solo per il latte.");
        if (kind == "sleep" && (end is null || end <= start)) throw new ArgumentException("La fine del sonno deve seguire l'inizio.");
        if (kind != "sleep" && end is not null) throw new ArgumentException("La fine è prevista solo per il sonno.");
        return new(Guid.NewGuid(), kind, start.ToUniversalTime(), end?.ToUniversalTime(), milk, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
    }
}
