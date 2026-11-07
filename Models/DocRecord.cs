using System.Globalization;
using System.Text.Json;

namespace WtrMobile.Models;

/// <summary>One row of the documents table: one revision of one document, its cells as {column number: text} (dates as yyyy-MM-dd).</summary>
public sealed class DocRecord
{
    public int ExcelRow;
    public string DocNo = "";
    public string UpdatedAt = "";
    public Dictionary<string, string> Data = new();

    public string Get(int col) => col > 0 && Data.TryGetValue(col.ToString(CultureInfo.InvariantCulture), out var v) ? v : "";

    public static DocRecord From(JsonElement e)
    {
        var r = new DocRecord { ExcelRow = e.GetProperty("excel_row").GetInt32() };
        if (e.TryGetProperty("doc_no", out var d) && d.ValueKind == JsonValueKind.String) r.DocNo = d.GetString() ?? "";
        if (e.TryGetProperty("updated_at", out var u) && u.ValueKind == JsonValueKind.String) r.UpdatedAt = u.GetString() ?? "";
        if (e.TryGetProperty("data_json", out var j) && j.ValueKind == JsonValueKind.Object)
            foreach (var p in j.EnumerateObject())
                r.Data[p.Name] = p.Value.ValueKind switch { JsonValueKind.String => p.Value.GetString() ?? "", JsonValueKind.Null => "", _ => p.Value.GetRawText() };
        return r;
    }

    /// <summary>DELAY is never stored: (reply date or today) - submission date, in days (EnrichDelay of the desktop).</summary>
    public string DelayText(ProjectDef p)
    {
        var stored = Get(p.DelayCol);
        if (stored.Length > 0) return stored;
        if (!Dates.TryIso(Get(p.DateCol), out var sub)) return "";
        var end = Dates.TryIso(Get(p.WtcoDateCol), out var rep) ? rep : DateOnly.FromDateTime(DateTime.Today);
        return (end.DayNumber - sub.DayNumber).ToString(CultureInfo.InvariantCulture);
    }
}

public static class Dates
{
    static readonly string[] In = { "yyyy-MM-dd", "d-MMM-yy", "d-MMM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d MMM yyyy" };
    public static bool TryIso(string text, out DateOnly d) => DateOnly.TryParseExact((text ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    /// <summary>Any of the usual ways to type a date -> yyyy-MM-dd.</summary>
    public static bool TryParse(string text, out string iso)
    {
        iso = "";
        if (!DateOnly.TryParseExact((text ?? "").Trim(), In, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        iso = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); return true;
    }
    /// <summary>yyyy-MM-dd -> 5-Nov-26 (the desktop's format).</summary>
    public static string Show(string iso) => TryIso(iso, out var d) ? $"{d.Day}-{d.ToString("MMM", CultureInfo.InvariantCulture)}-{d.ToString("yy", CultureInfo.InvariantCulture)}" : iso;
}
