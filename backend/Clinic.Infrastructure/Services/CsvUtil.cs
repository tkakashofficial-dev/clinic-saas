using System.Text;

namespace Clinic.Infrastructure.Services;

/// <summary>
/// Minimal RFC 4180 CSV reader/writer. Hand-rolled on purpose: our needs are
/// tiny (export tables, import patient lists) and a dependency would be
/// heavier than these 60 lines. Excel-compatible: quotes fields containing
/// commas/quotes/newlines and doubles embedded quotes.
/// </summary>
public static class CsvUtil
{
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var mustQuote = value.Contains(',') || value.Contains('"')
            || value.Contains('\n') || value.Contains('\r');
        return mustQuote ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    public static string Row(params string?[] fields)
        => string.Join(",", fields.Select(Escape));

    /// <summary>Parses CSV text into rows of fields. Handles quoted fields,
    /// embedded commas/newlines, doubled quotes, CRLF/LF and a UTF-8 BOM.</summary>
    public static List<string[]> Parse(string text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];   // strip BOM

        var rows = new List<string[]>();
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(current.ToString());
                current.Clear();
                // Skip rows that are entirely empty (trailing newlines etc.)
                if (fields.Any(f => f.Length > 0)) rows.Add(fields.ToArray());
                fields.Clear();
            }
            else current.Append(c);
        }

        fields.Add(current.ToString());
        if (fields.Any(f => f.Length > 0)) rows.Add(fields.ToArray());
        return rows;
    }
}
