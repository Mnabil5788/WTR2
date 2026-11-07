-- Run once in Supabase -> SQL editor.

create table if not exists profiles (
  id        uuid primary key references auth.users on delete cascade,
  username  text unique not null,
  read_only boolean not null default false,
  projects  text[] not null default '{nspwts2,samta1}'
);

-- Same shape as the desktop data_store.db "documents" table (one row per document revision, cells as {column number: text}).
create table if not exists documents (
  project    text not null,
  excel_row  int  not null,
  doc_no     text,
  data_json  jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now(),
  primary key (project, excel_row)
);
create index if not exists documents_doc_no on documents (project, doc_no);

create or replace function touch_updated_at() returns trigger language plpgsql as $$
begin new.updated_at = now(); return new; end $$;
drop trigger if exists documents_touch on documents;
create trigger documents_touch before update on documents for each row execute function touch_updated_at();

alter table profiles  enable row level security;
alter table documents enable row level security;

create policy "own profile" on profiles for select to authenticated using (id = auth.uid());

create policy "read my projects" on documents for select to authenticated
  using (exists (select 1 from profiles p where p.id = auth.uid() and documents.project = any (p.projects)));

create policy "edit my projects" on documents for update to authenticated
  using      (exists (select 1 from profiles p where p.id = auth.uid() and not p.read_only and documents.project = any (p.projects)))
  with check (exists (select 1 from profiles p where p.id = auth.uid() and not p.read_only and documents.project = any (p.projects)));

-- Users: Authentication -> Users -> Add user, e-mail = <username>@wtr.app (lower case, spaces become dots), a password, "Auto confirm".
-- Then one profile row for each, e.g.:
--   insert into profiles (id, username, read_only, projects)
--   values ('<the user id>', 'Mohamed', false, '{nspwts2,samta1}');
