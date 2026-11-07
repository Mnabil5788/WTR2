using System.Globalization;
using WtrMobile.Models;
using WtrMobile.Services;

namespace WtrMobile.Views;

/// <summary>Document Entry for the phone (step 1): find by DOC / REF or by date, result cards, the record in three cards (Document | References and dates | Review), Edit + Save.
/// Not in this step: new document / new revision, sessions, transmittals, delete.</summary>
public sealed class EntryPage : ContentPage
{
    static readonly Color Ink = Color.FromArgb("#1E293B"), Muted = Color.FromArgb("#64748B"), Line = Color.FromArgb("#E2E8F0"), Blue = Color.FromArgb("#2563EB"), Soft = Color.FromArgb("#F1F5F9"), White = Colors.White;
    static readonly Dictionary<string, string> StatusColors = new() { ["A-AFC"] = "#00468C", ["A"] = "#1E90FF", ["B"] = "#008000", ["C"] = "#E61E1E", ["D"] = "#DC6ED2", ["E"] = "#000000" };

    readonly ProjectDef _p;
    readonly SupabaseApi _api = SupabaseApi.I;
    readonly Entry _term = new() { Placeholder = "DOC NO. or REF", ClearButtonVisibility = ClearButtonVisibility.WhileEditing, AutoCapitalize = false };
    readonly Entry _date = new() { Placeholder = "5-Nov-26" };
    readonly Label _count = new() { Text = "0", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Ink, VerticalOptions = LayoutOptions.Center };
    readonly Label _in = Pill("IN 0", "#F3E8FF", "#7E22CE"), _out = Pill("OUT 0", "#FEE2E2", "#B91C1C");
    readonly Label _status = new() { FontSize = 12, TextColor = Muted, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
    readonly Label _revs = new() { FontSize = 11, TextColor = Muted };
    readonly VerticalStackLayout _results = new() { Spacing = 6 };
    readonly VerticalStackLayout _form = new() { Spacing = 10 };
    readonly Border _actions = new() { BackgroundColor = Color.FromArgb("#DBEAFE"), StrokeThickness = 0, Padding = new Thickness(10, 6), IsVisible = false, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 } };
    readonly Label _actionText = new() { FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1D4ED8"), VerticalOptions = LayoutOptions.Center };
    readonly Button _edit = Small("Edit"), _unlock = Small("Unlock");
    readonly Button _save = new() { Text = "Save", BackgroundColor = Color.FromArgb("#16A34A"), TextColor = White, FontAttributes = FontAttributes.Bold, CornerRadius = 8, HeightRequest = 42, Padding = new Thickness(26, 0), IsVisible = false };
    readonly Dictionary<int, Entry> _boxes = new();
    DocRecord? _sel;
    bool _editing, _unlocked;

    public EntryPage(ProjectDef p)
    {
        _p = p;
        BackgroundColor = Color.FromArgb("#F8FAFC");

        var fetch = Btn("Fetch data", "#FEF3C7", "#92400E"); fetch.Clicked += async (_, _) => await FindAsync();
        var clear = Btn("Clear fields", "#F1F5F9", "#475569"); clear.Clicked += (_, _) => ClearAll();
        var search = Btn("Search", "#DBEAFE", "#1D4ED8"); search.Clicked += async (_, _) => await DateAsync();
        _term.Completed += async (_, _) => await FindAsync();
        _date.Completed += async (_, _) => await DateAsync();
        _edit.Clicked += (_, _) => { _editing = !_editing; if (!_editing) _unlocked = false; Lock(); };
        _unlock.Clicked += (_, _) => { _unlocked = !_unlocked; Lock(); };
        _save.Clicked += async (_, _) => await SaveAsync();

        var head = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(14, 12), BackgroundColor = White };
        head.Add(new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = "Document Entry", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ink },
                new Label { Text = _p.Label + "  ·  " + _api.Me?.Username + (_api.Me?.ReadOnly == true ? "  ·  read only" : ""), FontSize = 11, TextColor = Color.FromArgb(_p.Color) },
            },
        }, 0, 0);
        var so = new Button { Text = "Sign out", FontSize = 12, BackgroundColor = Colors.Transparent, TextColor = Muted };
        so.Clicked += (_, _) => { _api.SignOut(); Application.Current!.MainPage = App.Shell(new LoginPage()); };
        head.Add(so, 1, 0);

        var find = Card("Find or add a document", new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Lab("DOC / REF"), _term,
                new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, Children = { fetch, Pos(clear, 1) } },
                Lab("DATE"),
                new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Children = { _date, Pos(search, 1) } },
            },
        });

        var strip = new Border
        {
            BackgroundColor = Soft, StrokeThickness = 0, Padding = new Thickness(12, 8), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = new HorizontalStackLayout { Spacing = 8, Children = { new Label { Text = "RESULTS", FontSize = 11, TextColor = Muted, VerticalOptions = LayoutOptions.Center }, _count, new Label { Text = "tasks", FontSize = 12, TextColor = Muted, VerticalOptions = LayoutOptions.Center }, _in, _out } },
        };

        _actions.Content = new HorizontalStackLayout { Spacing = 8, Children = { _actionText, _edit, _unlock } };

        var body = new VerticalStackLayout { Padding = new Thickness(10), Spacing = 10, Children = { find, strip, _results, _actions, _revs, _form, _status } };
        var bar = new Grid { Padding = new Thickness(12, 8), BackgroundColor = White, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        bar.Add(new Label { Text = "WTR DC", FontSize = 11, TextColor = Muted, VerticalOptions = LayoutOptions.Center }, 0, 0);
        bar.Add(_save, 1, 0);

        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { head, Row(new ScrollView { Content = body }, 1), Row(bar, 2) },
        };
        BuildForm();
    }

    // ---------------------------------------------------------------- search
    async Task FindAsync()
    {
        var t = (_term.Text ?? "").Trim();
        if (t.Length == 0) { Say("Type a DOC NO. or a REF first."); return; }
        await RunAsync(async () => Show(await _api.FindAsync(_p, t), null));
    }

    async Task DateAsync()
    {
        if (!Dates.TryParse(_date.Text ?? "", out var iso)) { Say("Could not recognize that date - try 12-Sep-26 or 12/09/2026."); return; }
        await RunAsync(async () => Show(await _api.FindByDateAsync(_p, iso), iso));
    }

    async Task RunAsync(Func<Task> work)
    {
        Say("Searching...");
        try { await work(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { Say("No connection. Check the internet and try again."); }
        catch (Exception) { Say("Something went wrong. Try again."); }
    }

    void Show(List<DocRecord> rows, string? iso)
    {
        _results.Children.Clear();
        _count.Text = rows.Count.ToString(CultureInfo.InvariantCulture);
        int nIn = iso is null ? 0 : rows.Count(r => r.Get(_p.WtcoDateCol) == iso), nOut = iso is null ? 0 : rows.Count(r => r.Get(_p.DateCol) == iso);
        _in.Text = "IN " + nIn; _out.Text = "OUT " + nOut;
        foreach (var r in rows.Take(60)) _results.Children.Add(ResultCard(r));
        if (rows.Count > 60) _results.Children.Add(new Label { Text = $"Showing 60 of {rows.Count} - search for more specific text.", FontSize = 11, TextColor = Muted, HorizontalOptions = LayoutOptions.Center });
        Say(rows.Count == 0 ? "Nothing found." : "");
        if (rows.Count == 1) Select(rows[0]);
    }

    View ResultCard(DocRecord r)
    {
        var st = r.Get(_p.StatusCol).Trim();
        var color = StatusColors.TryGetValue(st.ToUpperInvariant(), out var c) ? c : "#64748B";
        var line2 = string.Join("  ·  ", new[] { "Rev " + r.Get(_p.RevCol), r.Get(_p.DisciplineCol), r.Get(_p.RefCol), Dates.Show(r.Get(_p.DateCol)) }.Where(s => s.Trim().Length > 0 && s != "Rev "));
        var g = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, RowSpacing = 2 };
        g.Add(new Label { Text = r.DocNo.Length > 0 ? r.DocNo : r.Get(_p.DocCol), FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Ink, LineBreakMode = LineBreakMode.TailTruncation }, 0, 0);
        if (st.Length > 0) g.Add(new Label { Text = st, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = White, BackgroundColor = Color.FromArgb(color), Padding = new Thickness(8, 2) }, 1, 0);
        var sub = new Label { Text = r.Get(_p.SubjectCol), FontSize = 12, TextColor = Ink, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
        var info = new Label { Text = line2, FontSize = 11, TextColor = Muted };
        var b = new Border { Stroke = Line, StrokeThickness = 1, BackgroundColor = White, Padding = new Thickness(10, 8), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }, Content = new VerticalStackLayout { Spacing = 2, Children = { g, sub, info } } };
        var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => Select(r);
        b.GestureRecognizers.Add(tap);
        return b;
    }

    void ClearAll()
    {
        _term.Text = ""; _date.Text = ""; _results.Children.Clear(); _count.Text = "0"; _in.Text = "IN 0"; _out.Text = "OUT 0";
        _sel = null; _editing = _unlocked = false; _actions.IsVisible = false; _save.IsVisible = false; _revs.Text = ""; BuildForm(); Say("");
    }

    // ---------------------------------------------------------------- the record
    async void Select(DocRecord r)
    {
        _sel = r; _editing = _unlocked = false;
        BuildForm(); Lock(); Say("");
        try
        {
            var revs = await _api.RevisionsAsync(_p, r.DocNo);
            _revs.Text = revs.Count > 1 ? $"Revisions of {r.DocNo}:  " + string.Join("  ·  ", revs.Select(x => x.Get(_p.RevCol))) : "";
        }
        catch (Exception) { _revs.Text = ""; }
    }

    void BuildForm()
    {
        _form.Children.Clear(); _boxes.Clear();
        var (doc, refs, review) = _p.Groups();
        foreach (var (title, fields) in new[] { ("Document", doc), ("References and dates", refs), ("Review", review) })
            _form.Children.Add(Card(title, Fields(fields)));
    }

    View Fields(List<Field> fields)
    {
        var col = new VerticalStackLayout { Spacing = 8 };
        Field? pending = null;
        bool Short(Field f) => f.Col == _p.RevCol || f.Col == _p.SourceCol || f.Col == _p.DisciplineCol || f.Col == _p.StatusCol || f.Col == _p.DelayCol || _p.DateCols.Contains(f.Col) || f.Label is "TYPE" or "ACONEX#";
        void Flush() { if (pending is not null) { col.Children.Add(Cell(pending)); pending = null; } }
        foreach (var f in fields)
        {
            if (!Short(f)) { Flush(); col.Children.Add(Cell(f)); continue; }
            if (pending is null) { pending = f; continue; }
            var g = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
            g.Add(Cell(pending), 0, 0); g.Add(Cell(f), 1, 0); col.Children.Add(g); pending = null;
        }
        Flush();
        return col;
    }

    View Cell(Field f)
    {
        var isDate = _p.DateCols.Contains(f.Col);
        string text = _sel is null ? "" : f.Col == _p.DelayCol ? _sel.DelayText(_p) : isDate ? Dates.Show(_sel.Get(f.Col)) : _sel.Get(f.Col);
        var box = new Entry { Text = text, FontSize = 13, TextColor = Ink, IsReadOnly = true, BackgroundColor = Soft, Placeholder = isDate ? "d-MMM-yy" : "", AutoCapitalize = false };
        _boxes[f.Col] = box;
        var color = f.Col == _p.RefCol ? Color.FromArgb("#DC2626") : f.Col == _p.RefWtcoCol ? Color.FromArgb("#9333EA") : Muted;
        return new VerticalStackLayout { Spacing = 2, Children = { new Label { Text = f.Label, FontSize = 10, TextColor = color }, box } };
    }

    /// <summary>View mode: every box read-only. Edit: the boxes open, except the held ones (DOC NO., SOURCE, DISCIPLINE, REV, REF RTCC) until Unlock (the desktop's F2). DELAY is always derived.</summary>
    void Lock()
    {
        bool canEdit = _api.Me?.ReadOnly != true;
        _actions.IsVisible = _sel is not null;
        _actionText.Text = _sel is null ? "" : _editing ? "Editing row " + _sel.ExcelRow : "Row " + _sel.ExcelRow;
        _edit.IsVisible = canEdit; _edit.Text = _editing ? "Cancel" : "Edit";
        _unlock.IsVisible = canEdit && _editing; _unlock.Text = _unlocked ? "Lock" : "Unlock";
        _save.IsVisible = canEdit && _editing;
        var held = _p.Held;
        foreach (var (col, box) in _boxes)
        {
            bool open = _editing && col != _p.DelayCol && (!held.Contains(col) || _unlocked);
            if (_sel is not null && !_editing) box.Text = Shown(col);
            box.IsReadOnly = !open; box.BackgroundColor = open ? White : Soft;
        }
    }

    string Shown(int col) => _sel is null ? "" : col == _p.DelayCol ? _sel.DelayText(_p) : _p.DateCols.Contains(col) ? Dates.Show(_sel.Get(col)) : _sel.Get(col);

    async Task SaveAsync()
    {
        if (_sel is null || !_editing) return;
        var data = new Dictionary<string, string>(_sel.Data);
        foreach (var (col, box) in _boxes)
        {
            if (col == _p.DelayCol) continue;
            var t = (box.Text ?? "").Trim();
            if (_p.DateCols.Contains(col))
            {
                if (t.Length > 0 && !Dates.TryParse(t, out t)) { Say($"{_p.Header(col)}: could not recognize that date."); return; }
            }
            data[col.ToString(CultureInfo.InvariantCulture)] = t;
        }
        _save.IsEnabled = false; Say("Saving...");
        var res = await _api.SaveAsync(_p, _sel, data);
        _save.IsEnabled = true;
        switch (res)
        {
            case SaveResult.Saved: _editing = _unlocked = false; Lock(); Say("Saved."); break;
            case SaveResult.Changed: Say("Somebody changed this record meanwhile. Fetch it again, then edit."); break;
            case SaveResult.Offline: Say("No connection. Nothing was saved."); break;
            default: Say("Couldn't save. Nothing was changed."); break;
        }
    }

    // ---------------------------------------------------------------- small helpers
    void Say(string s) => _status.Text = s;
    static Label Lab(string t) => new() { Text = t, FontSize = 10, TextColor = Muted };
    static Label Pill(string t, string back, string fore) => new() { Text = t, FontSize = 11, FontAttributes = FontAttributes.Bold, BackgroundColor = Color.FromArgb(back), TextColor = Color.FromArgb(fore), Padding = new Thickness(8, 3), VerticalOptions = LayoutOptions.Center };
    static Button Btn(string t, string back, string fore) => new() { Text = t, BackgroundColor = Color.FromArgb(back), TextColor = Color.FromArgb(fore), FontSize = 12, FontAttributes = FontAttributes.Bold, CornerRadius = 8, HeightRequest = 38 };
    static Button Small(string t) => new() { Text = t, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#1D4ED8"), FontSize = 12, CornerRadius = 8, HeightRequest = 34, Padding = new Thickness(14, 0) };
    static T Pos<T>(T v, int col) where T : BindableObject { Grid.SetColumn(v, col); return v; }
    static T Row<T>(T v, int row) where T : BindableObject { Grid.SetRow(v, row); return v; }

    static View Card(string title, View content) => new Border
    {
        Stroke = Line, StrokeThickness = 1, BackgroundColor = White, Padding = new Thickness(12, 10), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        Content = new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = title, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Ink }, content } },
    };
}
