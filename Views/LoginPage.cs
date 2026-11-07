using WtrMobile.Models;
using WtrMobile.Services;

namespace WtrMobile.Views;

/// <summary>Sign-in: user, password, show the password, choose the project. 3 wrong tries pause the button for 3 seconds (like LoginWindow).</summary>
public sealed class LoginPage : ContentPage
{
    static readonly Color Bg = Color.FromArgb("#0F2A33"), Teal = Color.FromArgb("#4FD1C5"), Soft = Color.FromArgb("#CBD5E1");
    readonly Entry _user = new() { Placeholder = "User", TextColor = Colors.White, PlaceholderColor = Soft, AutoCapitalize = false };
    readonly Entry _pw = new() { Placeholder = "Password", IsPassword = true, TextColor = Colors.White, PlaceholderColor = Soft };
    readonly CheckBox _show = new() { Color = Teal };
    readonly Label _msg = new() { TextColor = Color.FromArgb("#FCA5A5"), FontSize = 13, MinimumHeightRequest = 18 };
    readonly Button _go = new() { Text = "Sign in", BackgroundColor = Teal, TextColor = Bg, FontAttributes = FontAttributes.Bold, CornerRadius = 10, HeightRequest = 48 };
    readonly Button[] _chips = new Button[2];
    ProjectDef _proj = ProjectDef.Nspwts2;
    int _fails;

    public LoginPage()
    {
        BackgroundColor = Bg;
        _user.Text = Preferences.Get("user", "");
        _proj = ProjectDef.Get(Preferences.Get("project", "")) ?? ProjectDef.Nspwts2;
        _show.CheckedChanged += (_, e) => _pw.IsPassword = !e.Value;
        _go.Clicked += async (_, _) => await TryAsync();
        _pw.Completed += async (_, _) => await TryAsync();
        _user.Completed += (_, _) => _pw.Focus();

        var chips = new HorizontalStackLayout { Spacing = 8 };
        for (int i = 0; i < 2; i++)
        {
            var p = ProjectDef.All[i];
            var b = new Button { Text = p.Label, CornerRadius = 18, HeightRequest = 38, FontSize = 12, Padding = new Thickness(14, 0) };
            b.Clicked += (_, _) => { _proj = p; Paint(); };
            _chips[i] = b; chips.Add(b);
        }
        Paint();

        var showRow = new HorizontalStackLayout { Spacing = 4, Children = { _show, new Label { Text = "Show the password", TextColor = Soft, FontSize = 13, VerticalOptions = LayoutOptions.Center } } };
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(26, 60, 26, 30), Spacing = 10,
                Children =
                {
                    new Label { Text = "WTR DC", FontSize = 34, FontAttributes = FontAttributes.Bold, TextColor = Teal },
                    new Label { Text = "Document & Submittals", FontSize = 17, TextColor = Soft, Margin = new Thickness(0, 0, 0, 24) },
                    new Label { Text = "Sign in", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    new Label { Text = "User", TextColor = Soft, FontSize = 12, Margin = new Thickness(0, 10, 0, 0) }, _user,
                    new Label { Text = "Password", TextColor = Soft, FontSize = 12 }, _pw, showRow,
                    new Label { Text = "Project", TextColor = Soft, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) }, chips,
                    _msg, _go,
                },
            },
        };
    }

    void Paint()
    {
        for (int i = 0; i < 2; i++)
        {
            bool on = ProjectDef.All[i].Key == _proj.Key;
            _chips[i].BackgroundColor = on ? Teal : Color.FromArgb("#1AFFFFFF");
            _chips[i].TextColor = on ? Bg : Colors.White;
        }
    }

    async Task TryAsync()
    {
        if (!_go.IsEnabled) return;
        var user = (_user.Text ?? "").Trim();
        if (user.Length == 0 || (_pw.Text ?? "").Length == 0) { _msg.Text = "Enter the user and the password."; return; }
        _go.IsEnabled = false; _go.Text = "Signing in..."; _msg.Text = "";
        var (ok, message) = await SupabaseApi.I.SignInAsync(user, _pw.Text!);
        if (ok && !SupabaseApi.I.Me!.Projects.Contains(_proj.Key)) { ok = false; message = $"You have no access to {_proj.Label}."; SupabaseApi.I.SignOut(); }
        if (ok)
        {
            Preferences.Set("user", user); Preferences.Set("project", _proj.Key);
            Application.Current!.MainPage = App.Shell(new EntryPage(_proj));
            return;
        }
        _msg.Text = message; _pw.Text = ""; _go.Text = "Sign in";
        if (message.StartsWith("Wrong")) _fails++;
        if (_fails >= 3) { await Task.Delay(3000); _fails = 0; }
        _go.IsEnabled = true;
    }
}
