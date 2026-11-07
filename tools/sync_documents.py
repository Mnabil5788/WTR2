"""Copies the documents of the desktop database (data_store.db) to Supabase. Run it on the PC that has the file.
   set SUPABASE_URL=https://xxxx.supabase.co   set SUPABASE_SERVICE_KEY=<service_role key - never put it in the app>
   python sync_documents.py "%LOCALAPPDATA%\\RTCC_DocSystem\\data_store.db"
"""
import json, os, sqlite3, sys, urllib.request

url, key = os.environ["SUPABASE_URL"].rstrip("/"), os.environ["SUPABASE_SERVICE_KEY"]
con = sqlite3.connect("file:" + sys.argv[1] + "?mode=ro", uri=True)
rows = []
for project, excel_row, doc_no, data_json in con.execute("SELECT project, excel_row, doc_no, data_json FROM documents"):
    try: data = json.loads(data_json)
    except ValueError: continue
    rows.append({"project": project, "excel_row": excel_row, "doc_no": (doc_no or "").strip().upper(), "data_json": data})

for i in range(0, len(rows), 500):
    req = urllib.request.Request(url + "/rest/v1/documents", data=json.dumps(rows[i:i + 500]).encode(), method="POST",
        headers={"apikey": key, "Authorization": "Bearer " + key, "Content-Type": "application/json", "Prefer": "resolution=merge-duplicates,return=minimal"})
    urllib.request.urlopen(req).read()
    print("sent", min(i + 500, len(rows)), "of", len(rows))
