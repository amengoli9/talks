using Newtonsoft.Json;

// Questo progetto referenzia di proposito Newtonsoft.Json 12.0.3,
// che ha una CVE nota (GHSA-5crp-9r3c-p9vr).
//
// Fitness function di sicurezza (atomic · triggered/temporal · static · automated):
//   dotnet list package --vulnerable --include-transitive
// la segnala. In CI fa fallire il quality gate.

var piada = new { Farcitura = "squacquerone e rucola", Prezzo = 5.0 };
Console.WriteLine(JsonConvert.SerializeObject(piada));
