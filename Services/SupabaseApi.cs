using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WtrMobile.Models;

namespace WtrMobile.Services;

public sealed class Profile
{
    public string Username = "";
    public bool ReadOnly;
    public string[] Projects = Array.Empty<string>();
}

public enum SaveResult { Saved, Changed, Failed, Offline }

/// <summary>Supabase over plain REST (no SDK). Sign-in = Supabase Auth with the e-mail user@wtr.app; the rights come from the profiles table; the data from documents.</summary>
public sealed class SupabaseApi
{
    public static SupabaseApi I { get; } = new();
    readonly HttpClient _h = new() { Timeout = TimeSpan.FromSeconds(20) };
    string? _token;
    public Profile? Me { get; private set; }

    static string Email(string user) => user.Trim().ToLowerInvariant().Replace(' ', '.') + "@wtr.app";

    HttpRequestMessage Req(HttpMethod m, string path, string? json = null, string? prefer = null)
    {
        var r = new HttpRequestMessage(m, Config.SupabaseUrl + path);
        r.Headers.Add("apikey", Config.AnonKey);
        if (_token != null) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);   // the new sb_publishable key is not a JWT: it goes only in apikey
        if (prefer != null) r.Headers.Add("Prefer", prefer);
        if (json != null) r.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return r;
    }

    public async Task<(bool Ok, string Message)> SignInAsync(string user, string password)
    {
        try
        {
            using var r = Req(HttpMethod.Post, "/auth/v1/token?grant_type=password", JsonSerializer.Serialize(new { email = Email(user), password }));
            using var resp = await _h.SendAsync(r);
            if (!resp.IsSuccessStatusCode) return (false, "Wrong user or password.");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            _token = doc.RootElement.GetProperty("access_token").GetString();
            var id = doc.RootElement.GetProperty("user").GetProperty("id").GetString();

            using var pr = Req(HttpMethod.Get, $"/rest/v1/profiles?id=eq.{id}&select=username,read_only,projects");
            using var presp = await _h.SendAsync(pr);
            using var pd = JsonDocument.Parse(await presp.Content.ReadAsStringAsync());
            if (pd.RootElement.ValueKind != JsonValueKind.Array || pd.RootElement.GetArrayLength() == 0) return (false, "This user has no access profile yet. Ask the owner.");
            var e = pd.RootElement[0];
            Me = new Profile
            {
                Username = e.GetProperty("username").GetString() ?? user,
                ReadOnly = e.GetProperty("read_only").GetBoolean(),
                Projects = e.GetProperty("projects").EnumerateArray().Select(x => x.GetString() ?? "").ToArray(),
            };
            return (true, "");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (false, "No connection. Check the internet and try again."); }
    }

    public void SignOut() { _token = null; Me = null; }

    static string Clean(string s) => new string((s ?? "").Where(c => c is not (',' or '(' or ')' or '*' or '"' or '\\' or '%')).ToArray()).Trim();

    /// <summary>Old rule: DOC NO. exact; REF RTCC / REF WTCO partial (case-insensitive).</summary>
    public Task<List<DocRecord>> FindAsync(ProjectDef p, string term)
    {
        var t = Clean(term);
        if (t.Length == 0) return Task.FromResult(new List<DocRecord>());
        var or = $"(doc_no.eq.\"{t.ToUpperInvariant()}\",data_json->>{p.RefCol}.ilike.\"*{t}*\",data_json->>{p.RefWtcoCol}.ilike.\"*{t}*\")";
        return GetDocs(p, "&or=" + Uri.EscapeDataString(or) + "&order=excel_row.desc&limit=200");
    }

    /// <summary>Rows where one of the two date columns is the typed day (iso = yyyy-MM-dd).</summary>
    public Task<List<DocRecord>> FindByDateAsync(ProjectDef p, string iso)
    {
        var or = $"(data_json->>{p.DateCol}.eq.{iso},data_json->>{p.WtcoDateCol}.eq.{iso})";
        return GetDocs(p, "&or=" + Uri.EscapeDataString(or) + "&order=excel_row.desc&limit=500");
    }

    public Task<List<DocRecord>> RevisionsAsync(ProjectDef p, string docNo)
        => GetDocs(p, "&doc_no=eq." + Uri.EscapeDataString(docNo.Trim().ToUpperInvariant()) + "&order=excel_row.asc&limit=100");

    async Task<List<DocRecord>> GetDocs(ProjectDef p, string filter)
    {
        using var r = Req(HttpMethod.Get, $"/rest/v1/documents?project=eq.{p.Key}&select=excel_row,doc_no,data_json,updated_at{filter}");
        using var resp = await _h.SendAsync(r);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(DocRecord.From).ToList();
    }

    /// <summary>Replaces the cells of one row. The write only goes through when nobody changed the row since it was read (updated_at).</summary>
    public async Task<SaveResult> SaveAsync(ProjectDef p, DocRecord rec, Dictionary<string, string> data)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { data_json = data });
            var path = $"/rest/v1/documents?project=eq.{p.Key}&excel_row=eq.{rec.ExcelRow.ToString(CultureInfo.InvariantCulture)}";
            if (rec.UpdatedAt.Length > 0) path += "&updated_at=eq." + Uri.EscapeDataString(rec.UpdatedAt);
            using var r = Req(new HttpMethod("PATCH"), path, body, "return=representation");
            using var resp = await _h.SendAsync(r);
            if (!resp.IsSuccessStatusCode) return SaveResult.Failed;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.GetArrayLength() == 0) return SaveResult.Changed;
            var fresh = DocRecord.From(doc.RootElement[0]);
            rec.UpdatedAt = fresh.UpdatedAt; rec.Data = fresh.Data;
            return SaveResult.Saved;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return SaveResult.Offline; }
    }
}
