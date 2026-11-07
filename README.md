# WTR DC mobile (step 1: sign-in + Document Entry)

1. Supabase: run supabase/schema.sql, add the users (see the end of that file), then `python tools/sync_documents.py <path to data_store.db>`.
2. Put your Project URL and anon key in Config.cs.
3. On the PC (Visual Studio 2022 with ".NET Multi-platform App UI development", or `dotnet workload install maui-android`):
   `dotnet publish -f net8.0-android -c Release`  -> the .apk is under bin/Release/net8.0-android/publish (or Build > Archive in Visual Studio).
   Copy the .apk to the phone and open it (allow "install unknown apps").

## Build on GitHub (no Visual Studio needed)
1. Create a PRIVATE repository and push this folder to it.
2. Repository -> Settings -> Secrets and variables -> Actions -> New secret: `SUPABASE_URL` and `SUPABASE_ANON_KEY`.
3. Actions tab -> "Build APK" -> Run workflow. When it is green, open the run and download the artifact `WtrDC-apk`.
