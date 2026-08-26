# Nalaz performansi — pravotek.com

Mjereno na produkciji (curl s Windowsa, Zagreb), backend `projectai-odvjetnik-fl8y.onrender.com`, frontend Vercel, baza Supabase EU. Datum: 24.8.2026.

Metoda: svaki poziv dvaput zaredom. Ako je drugi bitno brži → uzrok je cold start, ne logika.

---

## Faza 0 — mjerenja (PRIJE ijedne izmjene)

### Glavni nalaz: Render cold start

`GET /api/health`, tri poziva zaredom nakon mirovanja:

| Poziv | Ukupno | TTFB |
|---|---|---|
| 1 (hladan) | **13.79 s** | 13.79 s |
| 2 (topao) | 0.69 s | 0.69 s |
| 3 (topao) | 0.71 s | 0.71 s |

Backend na Render **free tier** gasi instancu nakon ~15 min mirovanja. Prvi poziv budi kontejner. Vlastiti komentar u kodu (`frontend/lib/second-factor.ts`) kaže da to zna trajati **do ~50 s**. Izmjereno 13.8 s je bio „polutopao" slučaj; jutarnji hladan zna biti gori.

Connect/TLS su brzi (~0.05–0.3 s / ~0.4 s), pa mreža i TLS nisu uzrok — cijelo vrijeme je čekanje da se aplikacija probudi.

### Tok A — Prijava

Kritični put: nakon što Supabase prihvati lozinku (brzo, bez backenda), Next.js **middleware** na prvoj navigaciji zove `checkSecondFactor` → `GET /api/2fa/status` na .NET backendu i **blokira render dok backend ne odgovori**.

- Middleware ima retry lanac s timeoutima **5 + 10 + 20 s = do 35 s** blokiranja (namjerno, da apsorbira cold start).
- Topao: ~0.5–0.7 s. Hladan: do 35 s bijelog ekrana prije nego se išta nacrta.
- MFA kolačić se kuje pri prvom uspjehu i vrijedi 12 h → provjera se radi **jednom po sesiji**, ne po navigaciji. To je već dobro.

Endpoint `/api/2fa/status` radi **jedno** čitanje iz Supabasea (`IsTwoFactorEnabledAsync`), ne ulančane pozive.

### Tok B — Naslovnica / dashboard

- Glavni HTML s Vercela (`www.pravotek.com/`): **2.06 s hladno / 0.47 s toplo** (87 KB). Vercelov function cold start, ne backend.
- `useSubscription` zove backend, ali iz `useEffect` (klijentski) — **ne blokira prvi paint**, dashboard pokaže skeleton pa se popuni. To je već dobro.
- `getRecentActivity` čita direktno iz Supabasea (RLS), ne kroz backend.
- Zaključak: dashboard je brz osim kad prva backend-ovisna komponenta (pretplata) padne na cold start.

### Tok C — Troškovnik

- Stranica na mount radi **tri paralelna** backend poziva (`/api/troskovnik/katalog`, `/api/settings/firm`, `/api/client`) — svaki u svom `useEffect`, dakle **paralelno, ne waterfall**. To je već dobro.
- Katalog topli: **0.51 s** (najveći od tri).
- Prvi od tri poziva plaća cold start ako je backend hladan.
- `/troskovnik/[id]` (spremljeni): čita direktno iz Supabasea, bez backenda.

### Frontend isporuka (Vercel)

| Ruta | Hladno | Toplo | HTML |
|---|---|---|---|
| `/` (landing) | 2.06 s | 0.47 s | 87 KB |
| `/auth/login` | 0.31 s | 0.25 s | 17 KB |
| `/troskovnik` | — | 0.21 s | (redirect na prijavu) |

- **Apex → www**: `pravotek.com` vraća **308** na `www.pravotek.com` — jedan dodatni round-trip (~0.2 s) za svakoga tko upiše adresu bez `www`. Sitno, ali besplatno za popraviti.
- **JS bundle**: najveći chunk je **1.5 MB `@react-pdf/renderer`**, ali je **već lijeno učitan** (`await import(...)` u `generatePdf.ts`) — povlači se tek kad korisnik izveze PDF, **nije na kritičnom putu**. Ostali chunkovi su ≤218 KB. Ništa za dirati.

### Baza (potvrda, ne novo istraživanje)

Podataka je premalo (4 troškovnika, 11 stranaka) da bi baza bila uzrok sekundi. Warm backend pozivi od 0.5–0.7 s to potvrđuju. Backend endpointi su pojedinačna čitanja, `TenantResolver` kešira ured 5 min. Advisorske stavke (RLS initplan, permissive politike, FK indeks) ne mijenjaju današnje brojke — vrijede za budućnost.

---

## Rangirani uzroci

| # | Uzrok | Utjecaj | Popravak |
|---|---|---|---|
| **1** | **Render cold start (13.8–50 s)** | **Dominantan.** Pogađa sva tri toka jer svaki dodiruje backend. Prijava najgore (middleware blokira do 35 s). | Keep-alive ping (besplatno) ili plaćeni Render plan bez spin-downa |
| 2 | Apex→www 308 redirect | ~0.2 s, samo bez `www` | Vercel domain config / redirect na razini DNS-a |
| 3 | DB advisori (RLS initplan, permissive, FK indeks) | Nula mjerljivo pri ovoj količini | Migracije, future-proof |
| — | JS bundle, waterfall, blocking render | Već riješeno / brzo | Ništa |

Prije bilo kakve izmjene: uzrok #1 je jedini koji vrijedi sekunde. Ostalo je sitno ili već dobro.

---

## Faza 1 — popravci

### 1. Cold start → keep-alive (glavni popravak)

**Uzrok:** Render free tier gasi instancu nakon ~15 min; prvi poziv je budi 13–50 s.

**Promjena:** Supabase `pg_cron` + `pg_net` posao `keep-backend-warm` zove
`/api/health` svakih 10 min (`*/10 * * * *`), pa instanca nikad ne dosegne prag
uspavljivanja. Timeout pinga je 30 s (ne pg_net default 5 s) da preživi i cold
start kad bi backend ipak zatekao hladan. Migracija:
`supabase/migrations/2026_08_26_keep_alive_backend_warm.sql`.

Usputni nalaz pri postavljanju: prvi ping s 5 s **istekao je** (`timed_out=true`,
`Total time: 5001 ms`) jer je backend bio hladan — dokaz da je 5 s premalo.
Nakon dizanja na 30 s ping vraća **200** `{"status":"healthy"}` bez timeouta.

**Brojka prije/poslije** (`GET /api/health`, produkcija):

| | Hladan prvi poziv | Topli poziv |
|---|---|---|
| Prije (bez keep-alivea) | **13.79 s** | 0.69 s |
| Poslije (uz keep-alive) | — (ne dešava se) | **0.36–0.42 s** |

Keep-alive ne „ubrzava" topli poziv nego **uklanja hladni** — instanca je stalno
topla, pa svaki tok koji je dosad plaćao cold start (prijava, dashboard,
troškovnik) sad kreće od ~0.4 s umjesto od 13–50 s.

### 2. FK indeks `troskovnici.case_id`

**Uzrok:** FK bez pokrivajućeg indeksa (advisor). **Promjena:**
`create index idx_troskovnici_case_id`. Migracija:
`supabase/migrations/2026_08_26_index_troskovnici_case_id.sql`.
**Brojka:** nula mjerljivo pri 4 retka — čisto future-proof, aditivno, nula
rizika. Ostala tri FK-a (`client_id`, `law_firm_id`, `user_id`) već imaju indeks.

### Namjerno NIJE dirano (i zašto)

- **Apex→www 308 redirect** (~0.2 s bez `www`): standardan, keširan kao 308
  permanent, browseri ga pamte. Mijenjanje domenske konfiguracije nosi rizik za
  0.2 s dobitka — po pravilu „ne over-refactoraj" ostaje.
- **RLS `auth_rls_initplan` (31 politika)** i **`multiple_permissive_policies`
  (77×)**: mijenjaju sigurnosne politike. Pri 4–11 redaka **ne mijenjaju ništa
  mjerljivo** (pravilo 3: promjenu bez ubrzanja vrati), a diraju RLS (pravilo 6:
  ne oslabljuj sigurnost). Preporuka je i dalje valjana za budućnost, ali kao
  zaseban, pojedinačno provjeren zadatak — ne u PR-u za brzinu koji se ne može
  u istoj sesiji provjeriti za svaku ulogu/tablicu. Remedijacija je poznata:
  `auth.uid()` → `(select auth.uid())`, konsolidacija permissive politika.
- **57 neiskorištenih indeksa**: samo zabilježeno, ne dira se.
- **JS bundle / waterfall / blocking render**: već dobri (v. Faza 0).

## Faza 2 — dokaz

- `pg_cron` posao `keep-backend-warm` je **active=true**, raspored `*/10 * * * *`.
- **Zakazani (ne ručni) run okinuo automatski u 14:10:00 UTC → status
  `succeeded`**, ping vratio **200** `{"status":"healthy"}` u ~0.3 s
  (`net._http_response` id=3, `timed_out=false`). Petlja radi sama.
- Trag postavljanja u `net._http_response`: id=1 (ping s 5 s, hladan backend) →
  `timed_out` na 5001 ms; id=2 (30 s) → 200; id=3 (zakazani) → 200. To je
  „prije/poslije" na jednom mjestu: 5 s ne pokrije cold start, 30 s pokrije.
- Vanjski `curl` nakon pinga: **0.36 s / 0.42 s** — backend topao.

### Kriteriji uspjeha

| Tok | Cilj (toplo/hladno) | Stanje |
|---|---|---|
| Prijava | <1.5 s / <3 s | Toplo ~0.7 s (2FA gate) ✓; hladno više nema |
| Dashboard | <1.5 s / <3 s | Toplo ✓; subscription non-blocking |
| Troškovnik | <1 s / <2 s | Katalog topli 0.51 s ✓ |

Hladni scenarij (<3 s / <2 s) sad se **ne događa** dok keep-alive drži instancu
toplom. Ako Render ipak povremeno reciklira instancu ili ping padne, moguć je
rijedak cold start — trajno bez toga tek Render Starter plan (bez spin-downa).
