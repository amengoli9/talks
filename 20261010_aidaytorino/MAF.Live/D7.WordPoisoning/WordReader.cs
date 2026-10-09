using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Torino.Live;

// Stesso esperimento di 20260419_aiday/MAF/Poisoner: testo bianco su pagina bianca.
// Non interpreta stili ereditati, temi, forme, intestazioni o layout: non è un motore di rendering Word.
public static class WordReader
{
    public static (string Visible, string Full) Read(string path)
    {
        using var span = Telemetry.Source.StartActivity("document.read_word");
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("Il Word non contiene un corpo documento.");
        var visible = new StringBuilder();
        var full = new StringBuilder();
        int whiteRuns = 0;
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            foreach (var run in paragraph.Descendants<Run>())
            {
                full.Append(run.InnerText);
                string? color = run.RunProperties?.Color?.Val?.Value;
                bool white = string.Equals(color, "FFFFFF", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(color, "white", StringComparison.OrdinalIgnoreCase);
                if (white) whiteRuns++;
                else visible.Append(run.InnerText);
            }
            full.AppendLine();
            visible.AppendLine();
        }
        span?.SetTag("document.white_runs", whiteRuns);
        span?.SetTag("document.full_characters", full.Length);
        span?.SetTag("document.visible_characters", visible.Length);
        return (visible.ToString(), full.ToString());
    }
}
