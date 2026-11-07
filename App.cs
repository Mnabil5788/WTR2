using WtrMobile.Views;

namespace WtrMobile;

public class App : Application
{
    public App() { MainPage = Shell(new LoginPage()); }
    public static NavigationPage Shell(Page p) { NavigationPage.SetHasNavigationBar(p, false); return new NavigationPage(p); }
}
