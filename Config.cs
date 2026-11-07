namespace WtrMobile;

public static class Config
{
    // Supabase dashboard -> Project Settings -> API. The anon key is meant to be inside the app: the data is protected by the RLS policies in supabase/schema.sql.
    public const string SupabaseUrl = "https://YOUR-PROJECT.supabase.co";
    public const string AnonKey = "YOUR-ANON-KEY";
}
