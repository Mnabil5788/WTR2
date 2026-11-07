namespace WtrMobile.Models;

public sealed record Field(string Label, int Col);

/// <summary>Port of the two DocProject configs of Wtr.Core/Docs/DocModels.cs (only what the Document Entry screen needs). Columns are Excel column numbers, like data_json.</summary>
public sealed class ProjectDef
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Color { get; init; } = "#2563eb";
    public int DocCol, SubjectCol, RevCol, StatusCol, RemarksCol, DelayCol, NotesCol, DateCol, WtcoDateCol, RefCol, RefWtcoCol, SourceCol, DisciplineCol;
    public int[] ShowColumns { get; init; } = Array.Empty<int>();
    public Dictionary<int, string> Headers { get; init; } = new();

    public int[] DateCols => new[] { DateCol, WtcoDateCol };
    public string Header(int c) => Headers.TryGetValue(c, out var h) ? h : $"Column {c}";
    /// <summary>Fields the desktop keeps locked until F2.</summary>
    public HashSet<int> Held => new[] { DocCol, SourceCol, DisciplineCol, RevCol, RefCol }.Where(c => c > 0).ToHashSet();

    /// <summary>DocEntryData.FormGroups: Document | References and dates | Review.</summary>
    public (List<Field> Doc, List<Field> Refs, List<Field> Review) Groups()
    {
        var all = ShowColumns.Select(c => new Field(Header(c), c)).ToList();
        if (NotesCol > 0 && !ShowColumns.Contains(NotesCol)) all.Add(new Field(Header(NotesCol), NotesCol));
        var refs = new HashSet<int>(new[] { RefCol, RefWtcoCol }.Where(c => c > 0));
        var dates = new HashSet<int>(DateCols.Where(c => c > 0));
        var review = new HashSet<int>(new[] { RemarksCol, StatusCol, DelayCol, NotesCol }.Where(c => c > 0));
        List<Field> doc = new(), rf = new(), rv = new();
        foreach (var f in all)
        {
            if (refs.Contains(f.Col) || dates.Contains(f.Col)) rf.Add(f);
            else if (review.Contains(f.Col)) rv.Add(f);
            else doc.Add(f);
        }
        var first = new[] { DocCol, SubjectCol };
        int Rank(Field f) { int i = Array.IndexOf(first, f.Col); return i >= 0 ? i : first.Length; }
        return (doc.OrderBy(Rank).ToList(), rf, rv);
    }

    public static readonly ProjectDef Nspwts2 = new()
    {
        Key = "nspwts2", Label = "NSPWTS PHASE 2", Color = "#2563eb",
        DocCol = 4, SubjectCol = 5, SourceCol = 7, DisciplineCol = 8, RevCol = 9, RefCol = 10, DateCol = 12, RefWtcoCol = 13, WtcoDateCol = 15, RemarksCol = 16, StatusCol = 17, DelayCol = 18, NotesCol = 2,
        ShowColumns = new[] { 4, 5, 7, 8, 9, 10, 12, 13, 15, 16, 17, 18 },
        Headers = new() { [2] = "Notes", [4] = "Doc Number", [5] = "Subject", [7] = "Source", [8] = "DISCIPLINE", [9] = "Rev", [10] = "REF RTCC", [12] = "DATE (RTCC)", [13] = "REF WTCO", [15] = "DATE (WTCO)", [16] = "Remarks", [17] = "Status", [18] = "Delay" },
    };

    public static readonly ProjectDef Samta1 = new()
    {
        Key = "samta1", Label = "SAMTA PHASE 1", Color = "#16a34a",
        DocCol = 3, SubjectCol = 4, SourceCol = 5, DisciplineCol = 7, RevCol = 8, RefCol = 9, DateCol = 11, RefWtcoCol = 12, WtcoDateCol = 14, RemarksCol = 15, StatusCol = 16, DelayCol = 17, NotesCol = 0,
        ShowColumns = new[] { 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 14, 15, 16, 17 },
        Headers = new() { [2] = "ACONEX#", [3] = "Doc Number", [4] = "Subject", [5] = "Source", [6] = "TYPE", [7] = "DISCIPLINE", [8] = "Rev", [9] = "REF RTCC", [11] = "DATE (RTCC)", [12] = "REF WTCO", [14] = "DATE (WTCO)", [15] = "Remarks", [16] = "Status", [17] = "Delay" },
    };

    public static readonly ProjectDef[] All = { Nspwts2, Samta1 };
    public static ProjectDef? Get(string key) => All.FirstOrDefault(p => p.Key == key);
}
