# Supabase Migrations — Upute za primjenu

> **Zastarjelo ispod ove bilješke:** tablica niže opisuje samo 003–014. Numerirane
> migracije idu do `025`; pravila primjene su u `CLAUDE.md` (prvo dev baza, provjera,
> backup, tek onda produkcija), a puna shema za svježu dev bazu je `../dev_bootstrap.sql`.
>
> | # | Datoteka | Što radi |
> |---|----------|----------|
> | 025 | `025_pravna_zastita.sql` | Verzija propisa uz troškovnik (6 stupaca na `troskovnici`), evidencija potvrde pravnih tekstova (`potvrde_pravnih_tekstova` + RPC `potvrdi_pravne_tekstove`), prijave grešaka u izračunu (`prijave_gresaka`, samo service_role). Plan: `docs/plan-pravna-zastita.md`. |

## Pregled svih 10 novih migracija

| # | Datoteka | Što radi | Prioritet |
|---|----------|----------|-----------|
| 1 | `003_consolidated_troskovnici_columns.sql` | Osigurava da **sve** kolone na `troskovnici` tablici postoje (konsolidacija svih prethodnih migracija) | 🔴 Visok |
| 2 | `004_auto_updated_at_triggers.sql` | Automatski `updated_at` triggeri na svim tablicama | 🟡 Srednji |
| 3 | `005_performance_composite_indexes.sql` | Composite indexi za brže query-je (uključuje `pg_trgm` za fuzzy search) | 🟡 Srednji |
| 4 | `006_dashboard_stats_rpc.sql` | RPC funkcija `get_dashboard_stats()` — vraća svu statistiku za dashboard u jednom pozivu | 🟢 Feature |
| 5 | `007_client_search_rpc.sql` | RPC funkcija `search_clients()` — fuzzy search po imenu/OIB-u/adresi klijenata | 🟢 Feature |
| 6 | `008_case_activity_log.sql` | `case_activities` tablica + auto-triggeri za praćenje timeline-a predmeta | 🟢 Feature |
| 7 | `009_document_attachments.sql` | `documents` tablica za metapodatke uploadanih dokumenata (PDF, slike, itd.) | 🟢 Feature |
| 8 | `010_troskovnik_templates.sql` | `troskovnik_templates` tablica za spremanje predložaka troškovnika | 🟢 Feature |
| 9 | `011_monthly_revenue_view.sql` | Materijalizirani view + RPC za mjesečno izvješće prihoda | 🟡 Srednji |
| 10 | `012_data_validation_constraints.sql` | CHECK constraints za validaciju podataka na DB razini (OIB format, enumi, pozitivni iznosi) | 🔴 Visok |
| bonus | `013_notifications.sql` | In-app notifikacije + korisničke preference za obavijesti | 🟢 Feature |
| bonus | `014_soft_delete_archive.sql` | Soft delete/arhiviranje umjesto brisanja — `is_archived` flag na svim tablicama | 🟢 Feature |

---

## Kako primijeniti

### Opcija A: Supabase SQL Editor (preporučeno za produkciju)

1. Otvori **Supabase Dashboard** → **SQL Editor**
2. Pokreni migracije **redom** (003 → 004 → ... → 014)
3. Kopiraj sadržaj svake `.sql` datoteke i klikni **Run**
4. Provjeri da nema errora u output-u

### Opcija B: Supabase CLI (lokalni razvoj)

```bash
# Ako koristiš Supabase CLI s lokalnim dev stackom:
supabase db reset     # Resetira i primjenjuje sve migracije iz /migrations
# ILI za pojedinačnu migraciju:
supabase db push      # Primjenjuje neprimijenjene migracije na remote
```

### Opcija C: psql direktno

```bash
psql "$DATABASE_URL" -f supabase/migrations/003_consolidated_troskovnici_columns.sql
psql "$DATABASE_URL" -f supabase/migrations/004_auto_updated_at_triggers.sql
# ... itd.
```

---

## Redoslijed je bitan!

Migracije se moraju pokrenuti **po redu** jer:
- `004` koristi `set_updated_at()` funkciju koju sam definira
- `009`, `010`, `013` koriste `set_updated_at()` iz `004`
- `011` ovisi o `troskovnici` i `invoices` tablicama
- `012` dodaje constraints na kolone koje `003` osigurava da postoje

---

## Što svaka migracija omogućuje u aplikaciji

### 003 — Konsolidacija kolona
Ako si ikad imao error "column X does not exist" na troskovnici tablici, ova migracija to fixa jednom zauvijek.

### 004 — Auto updated_at
Ne moraš više ručno slati `updated_at` iz backend-a — baza ga sama postavlja pri svakom UPDATE-u.

### 005 — Performance indexi
Ubrzava listing stranice (klijenti, predmeti, troškovnici) posebno kad naraste broj zapisa.

### 006 — Dashboard stats
Iz frontend-a pozovi:
```ts
const { data } = await supabase.rpc('get_dashboard_stats', { p_user_id: userId })
// data = { total_clients: 42, active_cases: 7, total_eur_s_pdv: 15000, ... }
```

### 007 — Client search
```ts
const { data } = await supabase.rpc('search_clients', {
  p_user_id: userId,
  p_query: 'Horvat',
  p_limit: 20
})
```

### 008 — Case activity log
Automatski se kreira aktivnost kad:
- Kreiraš novi predmet → `"Predmet kreiran"`
- Promijeniš status → `"Status: aktivan → zatvoren"`
- Ručno dodaš bilješku ili linkiraš dokument

### 009 — Document attachments
1. Kreiraj **Storage bucket** `documents` u Supabase Dashboardu (private, 50 MB limit)
2. Upload file → dobij storage_path
3. Insert u `documents` tablicu s metapodacima

### 010 — Troškovnik templates
Lawyer sprema česte kombinacije stavki kao template:
```ts
await supabase.from('troskovnik_templates').insert({
  user_id, name: 'Standardni kazneni',
  stavke: [{ item_id: 'kaz_obrana', kolicina: 1 }, ...],
  zaprijecena_kazna: 'do_5'
})
```

### 011 — Monthly revenue view
Za graf prihoda na dashboardu. ⚠️ Materialized view treba refresh:
```sql
-- Postavi cron job u Supabase (Extensions → pg_cron):
SELECT cron.schedule('refresh-monthly-revenue', '0 2 * * *',
    'REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue');
```

### 012 — Data validation
Sprečava unos nevalidnih podataka na DB razini:
- OIB mora biti 11 znamenki
- Case status mora biti jedan od: aktivan, zatvoren, mirovanje, arhiviran
- Iznosi ne smiju biti negativni

### 013 — Notifications
In-app notification sustav s bell icon-om:
```ts
// Dohvati nepročitane
const { data: count } = await supabase.rpc('get_unread_notification_count', { p_user_id: userId })
// Označi kao pročitane
await supabase.rpc('mark_notifications_read', { p_user_id: userId, p_ids: [...] })
```

### 014 — Soft delete
Umjesto `DELETE` koristi:
```ts
await supabase.rpc('toggle_archive', {
  p_table: 'troskovnici', p_id: recordId, p_user_id: userId, p_archived: true
})
```
Backend dodaj `&is_archived=eq.false` na default SELECT upite.

---

## Nakon primjene

1. ✅ Provjeri da sve tablice postoje: `SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY 1;`
2. ✅ Provjeri RPC funkcije: `SELECT routine_name FROM information_schema.routines WHERE routine_schema = 'public';`
3. ✅ Testiraj RLS: pozovi API s anon ključem — ne smije vratiti tuđe podatke
4. ✅ Postavi cron za materialized view ako koristiš `011`
