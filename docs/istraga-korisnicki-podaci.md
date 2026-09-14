# Istraga — gdje se sve čuvaju podaci o korisniku

**Zadatak:** popisati svako mjesto u sustavu gdje ostaje trag o korisniku, prije nego što se napiše
admin gumb za potpuno brisanje računa (da se isti mail može registrirati kao potpuno nov korisnik).

**Ovo je izvještaj, ne implementacija.** Nije mijenjan nijedan drugi file, nema migracija, nije dirana baza.

**Metoda:** čitanje koda u `supabase/`, `Backend/`, `frontend/`. Gdje se nešto ne može utvrditi iz
koda (Supabase Auth postavke, Google Cloud, Vercel/Resend zadržavanje logova, sadržaj Storagea u
produkciji), izrijekom piše **„NE ZNAM — provjeriti ručno"** i kako se to provjerava.

---

## Sažetak (TL;DR) — zašto stanje nije „čisto"

Kad korisnika obrišeš u **Supabase Auth UI**, izbriše se **samo red u `auth.users`**. Zbog dvije
namjerne odluke u shemi, gotovo svi njegovi podaci **ostaju**:

1. **Migracija 015** je vlasništvo podataka prebacila s korisnika na **ured** (`law_firm_id`). Svi
   poslovni podaci (predmeti, stranke, troškovnici, računi…) vezani su na `law_firms`, a ne na
   korisnika. Vidi [`015_firm_scoping.sql`](../supabase/migrations/015_firm_scoping.sql).
2. **Migracija 020** je zato `user_id` na poslovnim tablicama pretvorila iz `ON DELETE CASCADE` u
   **`ON DELETE SET NULL`**. Brisanje korisnika više ne povlači njegove predmete — samo im „obriše
   potpis". Vidi [`020_authorship_not_ownership.sql`](../supabase/migrations/020_authorship_not_ownership.sql).

Posljedica: brisanjem korisnika u Supabase UI ostaje **cijeli ured kao siroče** — `law_firms`,
svi poslovni podaci (`user_id` postane NULL), pretplata, statistika korištenja, pozivnice i
**datoteke u Storageu**. Briše se samo ono što je vezano na `auth.users` kaskadno: profil, 2FA,
obavijesti, legacy pretplata.

**Dobra vijest:** ispravno „potpuno brisanje" **već postoji u kodu** —
[`AccountService.DeleteFirmAsync`](../Backend/Services/AccountService.cs) (ruta `DELETE /api/account/firm`).
Ono je samouslužno (vlasnik briše svoj ured). Za admin gumb treba **admin-ciljanu inačicu iste
logike** (uzme `userId` ili `lawFirmId` i pozove istu proceduru). Detalji u Dijelu 3.

---

# DIO 1 — Svako mjesto gdje živi podatak o korisniku

Legenda stupca „Briše se kad obrišem korisnika u Supabase UI?":
- **DA** = kaskadno nestane s `auth.users`
- **NE** = ostaje (siroče ili namjerno zadržano)
- **NE ZNAM** = treba provjeriti ručno

## 1.1 Supabase — `auth` shema (GoTrue)

Ove tablice nisu u repo migracijama (Supabase ih upravlja interno). Ponašanje je standardno GoTrue,
ali ga nisam mogao provjeriti iz koda — **NE ZNAM sa 100 %, ali standardno je ovako:**

| Mjesto | Što sadrži | Briše se? |
|---|---|---|
| `auth.users` | email, hash lozinke, metapodaci (`raw_user_meta_data`: full_name, firm_name, firm_oib) | **DA** — to je red koji brišeš |
| `auth.identities` | veza korisnik ↔ provider (email, Google `sub`) | **DA** (FK CASCADE na auth.users) |
| `auth.sessions`, `auth.refresh_tokens` | aktivne sesije, refresh tokeni | **DA** (FK CASCADE) |
| `auth.mfa_factors`, `auth.mfa_challenges` | Supabase-native MFA faktori | **DA** (FK CASCADE) — **ali app ne koristi Supabase MFA** (2FA je vlastiti, vidi 1.2 `user_mfa`), pa su ove tablice vjerojatno prazne |
| `auth.one_time_tokens` | tokeni za potvrdu maila / reset lozinke / magic link | **DA** (FK CASCADE) |
| `auth.audit_log_entries` | interni GoTrue dnevnik prijava/registracija; payload sadrži user id i email | **NE** — append-only, nije FK-an na korisnika; ostaje. Aplikacija ga ne čita. NE ZNAM koliko dugo Supabase to čuva. |

**Trigger na `auth.users`:** DA — `on_auth_user_created` → `public.handle_new_user()`
([`015_firm_scoping.sql:360-422`](../supabase/migrations/015_firm_scoping.sql)). Radi na **INSERT**
(dakle pri registraciji, ne pri brisanju):
- Ako postoji otvorena **pozivnica** (`firm_invites`) za taj email → novi korisnik se **priključi
  tom postojećem uredu** kao član (uloga iz pozivnice), i pozivnica se označi iskorištenom.
- Inače → kreira **novi `law_firms`** (naziv/OIB iz metadata ili email) + `user_profiles` s ulogom `owner`.

> **Važno za ponovnu registraciju:** ako za testni email postoji **neiskorištena pozivnica**, novi
> korisnik se NEĆE registrirati „kao nov" — upast će u stari ured. To je skriveni trag koji treba
> uzeti u obzir (vidi Dio 3, rizici).

## 1.2 Supabase — `public` shema

Sve tablice s vezom na korisnika/ured. FK ponašanje očitano iz
[`unified_schema.sql`](../supabase/unified_schema.sql), [`015`](../supabase/migrations/015_firm_scoping.sql),
[`020`](../supabase/migrations/020_authorship_not_ownership.sql).

### A) Osobne tablice — vezane na `auth.users` s `ON DELETE CASCADE` → **brišu se**

| Tablica | Sadrži | Briše se? |
|---|---|---|
| `user_profiles` | ime, telefon, email, uloga, `law_firm_id`, `onboarded_at` | **DA** (PK → auth.users CASCADE) |
| `user_mfa` | TOTP tajna (šifrirana), recovery kodovi | **DA** (FK → `user_profiles(user_id)` CASCADE; lanac vodi do auth.users). Vidi [`017_user_mfa.sql`](../supabase/migrations/017_user_mfa.sql) |
| `user_notification_prefs` | postavke email/push obavijesti | **DA** (PK → auth.users CASCADE) |
| `notifications` | in-app obavijesti korisnika | **DA** (FK → auth.users CASCADE) |
| `subscriptions` (legacy) | stara pretplata po korisniku | **DA** (FK → auth.users CASCADE) — legacy, vjerojatno prazna |

### B) Poslovne tablice — vlasništvo ureda; `user_id` je `ON DELETE SET NULL` → **NE brišu se**

Nakon migracije 020: `law_firm_id` (NOT NULL, `ON DELETE CASCADE` → `law_firms`) = vlasnik;
`user_id` (nullable, `ON DELETE SET NULL`) = autor. Brisanjem korisnika **red ostaje**, samo mu
`user_id` postane NULL:

| Tablica | Sadrži osjetljivo | Briše se? |
|---|---|---|
| `clients` | ime/naziv, OIB, adresa, email, telefon stranaka | **NE** (user_id→NULL, ostaje pod uredom) |
| `cases` | predmeti: broj, sud, sudac, vrijednost spora | **NE** |
| `troskovnici` | troškovnici + JSONB `odvjetnik/predmet/stranke` (OIB, IBAN, adrese) | **NE** |
| `case_events` | radnje po predmetu (upisnik) | **NE** |
| `invoices` | računi: izdavatelj/klijent, OIB-ovi, iznosi, UBL XML | **NE** (i pravno se čuvaju — vidi Dio 3.4) |
| `evaluations` | procjene nekretnina | **NE** |
| `contract_analyses` | analize ugovora | **NE** |
| `chronologies` | kronologije | **NE** |
| `legal_strategies` | pravne strategije | **NE** |
| `zk_analyses` | ZK analize: vlasnici, tereti, čestice | **NE** |
| `case_activities` | dnevnik aktivnosti na predmetu | **NE** |
| `documents` | metapodaci datoteka + `storage_path` | **NE** |
| `troskovnik_templates` | predlošci troškovnika | **NE** |

### C) Tablice s `user_id` = `ON DELETE SET NULL` od početka (revizijski/AI trag) → **NE brišu se (namjerno)**

| Tablica | Sadrži | Briše se? |
|---|---|---|
| `valuation_audits` | revizije procjena | **NE** (user_id→NULL) |
| `audit_logs` | EU AI Act trag odluka (module, reasoning, model) | **NE** (user_id→NULL) |
| `api_audit_logs` | tko je zvao koji endpoint, kad, status; `ip_hash` | **NE** (`user_id` i `tenant_id` oba SET NULL) — bez tijela zahtjeva (GDPR) |

### D) Tablice vezane na **ured**, ne na korisnika → **NE brišu se** dok postoji `law_firms`

To je glavni izvor „siročadi": brisanjem korisnika ured ostaje, pa ostaje i sve pod njim.

| Tablica | Sadrži | Briše se brisanjem korisnika? |
|---|---|---|
| `law_firms` | naziv ureda, OIB, adresa, IBAN | **NE.** `user_profiles.law_firm_id` je `ON DELETE RESTRICT` (015), a ništa ne kaskadira ured iz `auth.users`. Ostaje kao siroče. |
| `tenant_subscriptions` | pretplata/proba ureda | **NE** (FK → law_firms CASCADE; ovisi o uredu, ne o korisniku) |
| `tenant_usage_stats` | mjesečna potrošnja | **NE** (→ law_firms CASCADE) |
| `firm_usage` | Beta krediti po modulu | **NE** (→ law_firms CASCADE) |
| `firm_invites` | pozivnice u ured (email, `invited_by`, `accepted_by`) | **NE** (→ law_firms CASCADE; `invited_by/accepted_by` → auth.users SET NULL). **Otvorena pozivnica na testni email = skriveni trag** |

### E) Ostalo

| Mjesto | Sadrži | Briše se? |
|---|---|---|
| `waitlist` | email s landinga (prije javnog izlaska) | **NE.** Nema FK na korisnika, ključ je `lower(email)` UNIQUE. Testni email ostaje zauvijek; ne blokira registraciju, ali je trag. [`2026_08_23_add_waitlist.sql`](../supabase/migrations/2026_08_23_add_waitlist.sql) |
| `norms`, `law_chunks` | statički pravni tekstovi / RAG embeddings | Nisu korisnički podaci |
| `monthly_revenue` (MATERIALIZED VIEW) | agregat prihoda **po `user_id`** | **NE** dok se ne osvježi. Zadržava user_id iz `troskovnici`/`invoices`. Osvježava se cron-om (`REFRESH MATERIALIZED VIEW`), pa nakon brisanja podataka retci nestanu tek pri sljedećem refreshu. Trag do refresha. |

## 1.3 Supabase — Storage, Edge Functions, RLS

**Storage bucketi** (privatni, [`018_storage_policies.sql`](../supabase/migrations/018_storage_policies.sql)):

| Bucket | Sadrži | Konvencija putanje | Briše se? |
|---|---|---|---|
| `zk-izvadci` | ZK izvadci (PDF): imena, OIB-ovi, čestice | `{law_firm_id}/{uuid}-{ime}.pdf` | **NE** automatski. `storage.objects` nema FK ni na korisnika ni na ured. Datoteke ostaju dok ih netko izrijekom ne obriše. Frontend upload: [`supabase.ts:79`](../frontend/lib/supabase.ts) |
| `case-documents` | dokumenti predmeta | `{law_firm_id}/…` | **NE** automatski (isto kao gore) |
| `documents`(?) | `unified_schema.sql` spominje bucket „documents" | — | **NE ZNAM** postoji li zaseban bucket „documents" ili tablica `documents` koristi jedan od gornja dva. Provjeriti u Supabase → Storage. Migracija 018 politike postavlja **samo** za `zk-izvadci` i `case-documents`. |

> Datoteke se ne brišu kaskadno **nigdje** — Postgres FK-ovi ne dosežu u Storage. Moraju se
> eksplicitno počistiti (kao što `DeleteFirmAsync` i radi za dva poznata bucketa).

**Edge Functions:** **NEMA** ih (`supabase/functions/` ne postoji). Nema tog traga.

**RLS koje bi mogle blokirati brisanje servisnim ključem:** RLS **ne** blokira `service_role` —
sve tablice imaju `service_role_full_access` politiku, a `service_role` ionako zaobilazi RLS. FK
ograničenja (`RESTRICT` na `user_profiles→law_firms`) diktiraju **redoslijed** brisanja, ne pravo
pristupa. To je jedini „blokator" i njime se upravlja redoslijedom (Dio 3.1).

## 1.4 .NET backend (`Backend/`)

- **Vlastita baza / tablice odvojene od Supabasea?** **NE.** Provjereno: nema `DbContext`, `Npgsql`,
  `AddDbContext` ni connection stringa. Sav pristup podacima ide preko **Supabase PostgREST-a**
  (HTTP), servisnim ključem gdje treba. Backend nema svoje trajno spremište.
- **In-memory / distribuirani cache, Redis?** Nema Redisa ni `IDistributedCache`. Postoje **procesni
  (in-memory) cacheovi**, svi TTL-ograničeni i nestaju restartom (Render):
  - `TenantResolver` — `ConcurrentDictionary` po **`userId`** → (tenantId, uloga), 5 min
    ([`TenantResolver.cs:27`](../Backend/Services/TenantResolver.cs)). Nakon brisanja korisnika stari
    userId može ostati mapiran do 5 min; ima `InvalidateCache(userId)`.
  - `SubscriptionGate` — `ConcurrentDictionary` po **`lawFirmId`** → pristup
    ([`SubscriptionGate.cs:62`](../Backend/Services/SubscriptionGate.cs)); `InvalidateCache(lawFirmId)`.
  - `UsageService` — cache po `tenantId` ([`UsageService.cs:27`](../Backend/Services/UsageService.cs)).
  - JWKS (Supabase javni ključevi) — nije korisnički podatak.
- **Session state / API ključevi / rate-limit brojači po korisniku?** Autentikacija je **stateless**
  (JWT provjera u [`SupabaseJwtMiddleware`], nema server-side sesija). Rate limiting
  ([`Program.cs:102-137`](../Backend/Program.cs)) je fiksni prozor po limiteru (`valuation`/`general`/
  `auth`) — **nije particioniran po korisniku** (unatoč komentarima „per IP"), pa ne drži korisnikov trag.
- **Logovi s mailom / user id — gdje završavaju?**
  - Aplikacijski logovi (`_logger.Log…`) pišu `userId`, ponekad `lawFirmId`; e-mail se u logovima
    pojavljuje u `AdminGuardMiddleware` (npr. „Non-admin user {Email}…"). Ovi logovi idu na **Render**
    stdout → Render log retention. **NE ZNAM koliko dugo Render čuva logove** — provjeriti u Render
    dashboardu. Ne mogu se obrisati iz aplikacije.
  - Strukturirani revizijski trag ide u `api_audit_logs` (bez tijela zahtjeva) — vidi 1.2 C.
- **Pozadinski poslovi (scheduler / hosted services) s referencom na korisnika?** Jedini
  `IHostedService` je `SupabaseJwksWarmUp` (grije JWKS na startu) — nema korisničkih podataka. Ostalo
  su Supabase pg_cron poslovi (refresh `monthly_revenue`, `expire_overdue_subscriptions`,
  `purge_old_api_audit_logs`) — rade nad tablicama, ne drže zaseban trag.

## 1.5 Next.js frontend (`frontend/`)

Klijentski tragovi (u pregledniku korisnika). Bitni za „čisto stanje pri ponovnom testiranju na
istom pregledniku"; **NISU** relevantni za admin brisanje tuđeg računa (druga sesija/uređaj).

| Mjesto | Ključ / naziv | Sadrži | Čisti se? |
|---|---|---|---|
| Cookie (Supabase SSR) | `sb-<ref>-auth-token` (i sl.) | JWT access + refresh token | Na odjavi (`supabase.auth.signOut()` + server ruta). Tokeni su u **cookieima**, ne u localStorage ([`supabase/client.ts`](../frontend/lib/supabase/client.ts)). |
| Cookie (HttpOnly, potpisan) | `pt_mfa` (`MFA_COOKIE`) | biljeg da je 2FA prošao, 12 h | Briše ga **server** ruta `/api/auth/sign-out` ([`route.ts:35`](../frontend/app/api/auth/sign-out/route.ts)); klijent ga ne može (HttpOnly). |
| Cookie (čitljiv) | `is-admin=1` | UI hint da je korisnik admin | Briše se na odjavi ([`route.ts:36`]). Ne daje ovlasti (samo prikaz). |
| localStorage | `pravotek_last_activity`, `pravotek_session_start` | idle/session-cap među karticama ([`session-timeout.ts:38-39`](../frontend/lib/session-timeout.ts)) | Briše ih `SessionGuard` na odjavi/isteku ([`SessionGuard.tsx:100,308`]) |
| sessionStorage | `pravotek_login_attempts` | klijentsko brojanje pokušaja prijave ([`auth-validation.ts:45`](../frontend/lib/auth-validation.ts)) | Tab-scoped; nestaje sa zatvaranjem kartice |
| sessionStorage | `troskovnik_draft:{userId}` | nacrt troškovnika ([`troskovnik-draft.ts`](../frontend/lib/troskovnik-draft.ts)) | Tab-scoped; svjesno NE u localStorage (privilegirani sadržaj) |
| localStorage | ~~`pravotek_tour_seen`~~ | **VIŠE SE NE KORISTI** — onboarding je sada u bazi (`user_profiles.onboarded_at`, migr. 022). Stari ključ se u kodu više ne čita ni ne piše ([`onboarding.ts`](../frontend/lib/onboarding.ts)). Može zaostati u pregledniku od prije, ali je mrtav. |
| IndexedDB / Cache Storage | — | — | **Nema.** Supabase SSR ne koristi IndexedDB za tokene ovdje. |
| Service worker / PWA | — | — | **Nema** (`sw.js`/`manifest.json` ne postoje). Nema cache-storage traga. |
| Middleware / server sesija | [`frontend/middleware.ts`] | postavlja `is-admin` cookie iz provjerenog emaila; nema vlastite sesije | — |

**„Onboarding gotov" flag koji preživi odjavu:** riješeno — sada je u bazi (`onboarded_at`), ne u
pregledniku. Nema klijentskog flaga koji laže za novog korisnika (to je bio bug iz prompta 9, uklonjen).

## 1.6 Vercel i vanjski servisi

| Servis | Trag | Može li se obrisati iz aplikacije? |
|---|---|---|
| **Vercel KV / Blob / Postgres** | Ne koriste se (frontend priča samo sa Supabaseom i backendom). **NE ZNAM sa 100 %** — brzo provjeriti u Vercel projektu da nema priključenog KV/Blob/Postgresa. |
| **Vercel logovi** | Server rute (`/api/auth/sign-out`, `/api/waitlist`, `/auth/callback`) mogu logirati; funkcijski logovi mogu sadržati email/putanje. **NE ZNAM Vercel retention** — provjeriti. Ne briše se iz aplikacije. |
| **Resend** (transakcijski mail) | Šalje se preko **SMTP relaya** (`smtp.resend.com:465`, [`ResendEmailService.cs`](../Backend/Services/ResendEmailService.cs)) — **ne** koristi Resend Contacts/Audiences API, pa nema spremljene liste kontakata. **Ali** Resend čuva **logove poslanih mailova** (s adresom) na svojoj strani. **NE ZNAM retention** — provjeriti u Resend dashboardu. Napomena: potvrdu maila/reset lozinke šalje **sam Supabase Auth** (predlošci u `supabase/emails/`), preko SMTP-a konfiguriranog u Supabaseu — dodatni izvor mail-logova. |
| **Analitika** (GA / PostHog / Plausible) | **NE ZNAM** — u kodu nisam našao integraciju; provjeriti postoji li skripta u produkciji / Vercel env. Ako postoji, drži trag koji se ne briše iz aplikacije. |
| **Sentry / error tracking** | **NE ZNAM** — nije nađeno u kodu; provjeriti env varijable na Renderu/Vercelu. |

## 1.7 Google OAuth

- Prijava ide preko Google Identity Services (`signInWithIdToken`, uz fallback `signInWithOAuth`),
  [`GoogleSignIn.tsx`](../frontend/components/GoogleSignIn.tsx).
- Kad obrišeš korisnika u bazi, **odobrenje koje je korisnik dao Google računu (OAuth consent) OSTAJE**
  na Googleovoj strani (`myaccount.google.com` → „Third-party access"). To je izvan sustava i ne može
  se obrisati iz aplikacije.
- **Za ponovnu „čistu" prijavu:** korisnik u praksi **ne mora** ništa poništiti — sljedeći
  `signInWithIdToken` s istim Google računom napravit će **novi** `auth.users` red (novi UUID), a
  `handle_new_user` novi ured (osim ako postoji pozivnica). Google consent koji ostaje samo znači da
  Google neće opet pitati „dopuštaš li pristup" — to ne prlja stanje u bazi. Poništavanje pristupa na
  Google strani relevantno je samo ako se **testira sam ekran pristanka**.

---

# DIO 2 — Kako se danas može prijaviti, i što nedostaje

## 2.1 Načini prijave koje app trenutno podržava (iz koda)

| Način | Stanje u kodu | Datoteka |
|---|---|---|
| **Email + lozinka** | DA — prijava, registracija, potvrda maila, zaboravljena/reset lozinke | `signInWithPassword` ([login/page.tsx:100]), `signUp` ([register/page.tsx:173]), `resetPasswordForEmail` ([forgot-password]), `updateUser({password})` ([reset-password]), `verifyOtp` ([auth/callback]) |
| **Google OAuth** | DA — One Tap / GIS (`signInWithIdToken`) + fallback redirect (`signInWithOAuth`), PKCE (`exchangeCodeForSession`) | [`GoogleSignIn.tsx`](../frontend/components/GoogleSignIn.tsx) |
| **2FA — TOTP aplikacija** | DA — **vlastita** izvedba (ne Supabase MFA): tablica `user_mfa`, backend `TwoFactorController`, `pt_mfa` cookie, recovery kodovi | [`TwoFactorController.cs`], [`017_user_mfa.sql`] |
| **Pozivnica u ured** | Djelomično — DB + trigger spremni (`firm_invites`, `handle_new_user`); **NE ZNAM ima li potpun UI** za slanje pozivnica (provjeriti `SettingsController`/frontend postavke) |

> **Koji su uključeni u Supabase Auth postavkama:** **NE ZNAM iz koda** — email i Google su očito
> uključeni jer se koriste; treba potvrditi u Supabase → Authentication → Providers (i je li
> „Confirm email" uključen). To se ne vidi iz repozitorija.

## 2.2 Načini koje ozbiljne poslovne aplikacije nude — što bi značili za PravoTek

| Način | Što bi značilo za PravoTek | Izvedivo kroz Supabase Auth bez velike prerade? | Posao |
|---|---|---|---|
| **Email + lozinka (potvrda + reset)** | Već imaš; osnova. | Da (postoji) | — (gotovo) |
| **Magic link** | Prijava linkom na mail bez lozinke — manje frikcije, nema zaboravljenih lozinki; dobro za odvjetnike koji rijetko ulaze | Da — `signInWithOtp` + isti `/auth/callback` (`verifyOtp` već postoji) | Mali |
| **Google OAuth** | Već imaš; većina korisnika ima Google | Da (postoji) | — |
| **Microsoft / Entra ID** | Odvjetnički uredi često rade na Outlooku/Microsoft 365 — velik doseg | Da — Supabase ima Azure provider; treba app registracija u Entra + provider config | Srednji |
| **Apple** | Rijetko za B2B pravni alat; nema jake potrebe | Da (Supabase Apple provider), ali Apple Developer račun + config | Srednji |
| **SSO / SAML** | Za veće urede/tvrtke s vlastitim IdP-om (jedinstvena prijava) | Samo na Supabase **Pro/Enterprise** (SAML SSO); veći setup po klijentu | Velik |
| **2FA — TOTP** | Već imaš (vlastiti) | — | — |
| **2FA — SMS** | Skupo (SMS trošak), slabija sigurnost od TOTP-a; mali dodatak | Supabase podržava phone/SMS uz SMS providera (Twilio…) | Srednji |
| **2FA — backup kodovi** | Već imaš (recovery kodovi u `user_mfa`) | — | — |
| **Passkeys / WebAuthn** | Najsigurnije, bez lozinke; ali Supabase native podrška je ograničena/nova | Djelomično — treba dosta vlastitog koda ili čekati zreliju podršku | Velik |
| **Pozivnica od ureda umjesto samostalne registracije** | Ključno za više-korisničke urede: vlasnik poziva kolege, oni ne otvaraju novi ured | DB + trigger već postoje; treba UI + email pozivnice | Srednji |

## 2.3 Isti mail: prvi put Google, drugi put lozinka — spaja li Supabase u jedan račun?

**NE ZNAM točno bez provjere Supabase postavki**, jer ovisi o opciji **„Enable/prevent automatic
linking"** u Supabase Auth (nije vidljivo iz repozitorija). Ponašanje po zadanim postavkama i što je
ispravno:

- Ako se korisnik prvo prijavi **Googleom**, dobije `auth.users` red **bez lozinke** (samo Google
  identitet). Kasniji `signInWithPassword` s istim mailom **neće raditi** — nema lozinke. (Login
  stranica to već zna: komentar na [login/page.tsx:207] spominje „a Google-created account has no
  password".)
- Ako korisnik zatim pokuša **`signUp`** (registracija lozinkom) s istim, potvrđenim mailom: Supabase
  po zadanom **spaja** identitete s istom **potvrđenom** email adresom u **jedan** račun (dodaje email
  identitet postojećem korisniku) — ne radi drugi račun. Uz uključeno „prevent duplicate emails" to je
  osigurano; NE ZNAM je li to ovdje uključeno.
- **Ispravno ponašanje:** **jedan račun po email adresi.** Preporuka: u Supabaseu osigurati sprječavanje
  duplih emaila, a Google-korisniku koji želi i lozinku ponuditi tijek **„postavi lozinku"**
  (`updateUser({password})` dok je prijavljen) umjesto nove registracije. Dva odvojena računa za isti
  mail su pogrešno stanje i treba ga izbjeći.

---

# DIO 3 — Prijedlog za potpuno brisanje

Cilj: admin (samo `vackadavid@gmail.com`) klikne gumb i korisnik + svi njegovi tragovi u sustavu
nestanu, tako da se **isti mail može registrirati kao potpuno nov korisnik**.

> **Ključna odluka koja prethodi svemu:** „obrisati korisnika" u ovom sustavu gotovo uvijek znači
> **„obrisati njegov ured sa svim podacima"**, jer podaci pripadaju uredu, ne osobi. Za **solo**
> testni račun to je isto. Za ured s više članova brisanje jednog člana NE smije obrisati tuđe
> podatke (vidi rizike). Admin gumb mora razlikovati ta dva slučaja.

## 3.1 Točan redoslijed brisanja (da ne pukne na ograničenjima)

FK-ovi diktiraju red. Provjereno prema shemi; ovo je red koji već koristi `DeleteFirmAsync`:

1. **Zapiši u dnevnik PRIJE brisanja** (`api_audit_logs`/`audit_logs`): `tenant_id`/`user_id` su
   SET NULL, pa zapis preživi; nakon brisanja ne bi bilo koga upisati.
2. **Storage datoteke** ureda: `zk-izvadci/{firmId}/…` i `case-documents/{firmId}/…` (Storage nema
   kaskadu). **Paginirati** ako ima > 1000 datoteka (list limit je 1000).
3. **`user_mfa`** svih članova ureda (FK → `user_profiles`).
4. **`user_profiles`** svih članova (mora prije ureda: `user_profiles.law_firm_id` je
   `ON DELETE RESTRICT`).
5. **`firm_invites`** za taj email — i **otvorene pozivnice na taj email u DRUGIM uredima** (inače
   ponovna registracija upadne u tuđi ured). (Pozivnice vlastitog ureda ionako odu kaskadom u koraku 6.)
6. **`law_firms`** (sam ured). Ovo kaskadno briše: sve poslovne tablice iz Dijela 1.2 B/D
   (`law_firm_id` CASCADE), `tenant_subscriptions`, `tenant_usage_stats`, `firm_usage`, i preostale
   `firm_invites`.
7. **`auth.users`** svih članova (Admin API, service_role) — **ovo oslobađa email.** Kaskadno nosi
   `auth.identities/sessions/refresh_tokens/one_time_tokens`, i preostale osobne tablice
   (`notifications`, `user_notification_prefs`, legacy `subscriptions`).
8. **(Opcionalno) `waitlist`** — obrisati red s tim emailom ako želiš da nestane i taj trag.
9. **Invalidacija cacheva:** `TenantResolver.InvalidateCache(userId)`, `SubscriptionGate` i
   `UsageService` `InvalidateCache(lawFirmId)`.
10. **(Opcionalno) `monthly_revenue`** — `REFRESH MATERIALIZED VIEW` ili čekati noćni cron.

Za varijantu „obriši samo korisnika, ured ostaje" (član ureda, ne solo): koraci 1, 3, 4 (samo taj
korisnik), 7 (samo taj korisnik), 9. To je točno ono što radi `DeleteOwnAccountAsync`.

## 3.2 Mjesta koja se NE mogu obrisati iz aplikacije

| Mjesto | Što s njim |
|---|---|
| `auth.audit_log_entries` (GoTrue interni dnevnik) | Ostaje; app ga ne doseže. Prihvatljivo (interni sigurnosni trag). NE ZNAM Supabase retention. |
| Render / Vercel logovi (email/user id u aplikacijskim logovima) | Ne brišu se iz app-a; ovise o retention politici hostova. Ubuduće: ne logirati email gdje nije nužno. |
| Resend / Supabase mail-logovi (poslani mailovi) | Ostaju na strani servisa; provjeriti retention. |
| Google OAuth consent | Na korisnikovoj Google strani; ne dira se iz app-a (i ne treba za čistu ponovnu registraciju). |
| Analitika/Sentry (ako postoje) | NE ZNAM postoje li; ako da — brisanje po njihovim GDPR alatima, izvan app-a. |

## 3.3 Dvije opcije za izvedbu

### Opcija (a) — FK `ON DELETE CASCADE` prema `auth.users`, pa brisanje korisnika sve povuče

**Kako:** vratiti `user_id` na poslovnim tablicama iz `SET NULL` u `CASCADE`.

- **Prednosti:** brisanje jednim potezom u Supabase UI; manje koda.
- **Mane (ozbiljne):**
  1. **Direktno poništava namjernu odluku migracije 020.** Vratila bi se „bomba": brisanje jednog
     odvjetnika obrisalo bi **cijelu arhivu ureda** i onda kad u uredu radi još troje. To je model
     „vlasništvo po osobi" koji je svjesno napušten (015/020).
  2. **Ne rješava siročad koje ne vise o `auth.users`:** `law_firms`, `tenant_subscriptions`,
     `tenant_usage_stats`, `firm_usage` vise o `law_firms`, ne o korisniku — cascade s `auth.users` ih
     ne dira. Ostali bi.
  3. **Ne dira Storage** (FK ne dosežu u Storage) ni `firm_invites` na tuđi email ni `waitlist`.
  4. Destruktivna migracija na produkciji s pravim korisnicima.

  **Zaključak: NE preporučam.** Rješava krivi problem i ruši više-korisnički model.

### Opcija (b) — jedna servisna ruta koja briše redom, koju admin panel zove ⭐ PREPORUČENO

**Kako:** admin-gate-ana ruta (npr. `DELETE /api/admin/users/{userId}`) koja izvede redoslijed iz
3.1. **Velik dio već postoji** u [`AccountService`](../Backend/Services/AccountService.cs):
`DeleteFirmAsync` (potpuno brisanje ureda) i `DeleteOwnAccountAsync` (samo korisnik). Treba:
- admin-ciljanu inačicu koja umjesto „ja iz svog tokena" prima `userId`/`lawFirmId` (identitet i dalje
  iz **provjerenog admin tokena**, meta preko `AdminGuardMiddleware` — nova ruta pod `/api/admin/*`
  automatski je zaključana na `ADMIN_EMAIL`);
- dodati brisanje `firm_invites` na email i (opcionalno) `waitlist`, te invalidaciju cacheva;
- odluku „briši cijeli ured" vs. „samo člana" (za solo je isto).

- **Prednosti:** čuva 015/020 model; pokriva Storage, ured, pretplatu, pozivnice; jedan atomski,
  kontrolirani, logirani tijek; ne dira shemu/migracije; testira se na dev/preview.
- **Mane:** nešto koda; brisanje kroz više PostgREST poziva nije jedna transakcija (djelomičan
  neuspjeh ostavlja pola-obrisano — ublažiti redoslijedom „prvo dnevnik, zadnje auth.users" i jasnim
  logom, kao što `DeleteFirmAsync` već radi). Alternativa: **jedna Postgres `SECURITY DEFINER`
  funkcija** koja odradi korake 3–6 u jednoj transakciji, a backend nakon nje pozove Admin API za
  `auth.users` (korak 7) — to daje atomičnost baznog dijela.

## 3.4 Podaci koje treba ZADRŽATI iz pravnih/računovodstvenih razloga

- **Izdani računi** (`invoices` sa statusom `ISSUED`/`SENT`/`PAID`) — po računovodstvenim/poreznim
  propisima čuvaju se godinama. **Testni** računi (DRAFT, testni podaci) mogu se slobodno brisati.
- **Revizijski tragovi** (`audit_logs`, `api_audit_logs`, `valuation_audits`) — namjerno preživljavaju
  (SET NULL), EU AI Act / sigurnosni trag.

**Kako zadržati, a osloboditi mail:** e-mail adresa živi u `auth.users` i `user_profiles`; brisanjem
`auth.users` (korak 7) mail je **slobodan bez obzira** na zadržane račune (računi nose
`client_name`/`issuer_name`/OIB kao tekst, ne login-email). Za produkciju, ako želiš zadržati izdane
račune ureda koji se gasi: umjesto brisanja tog ureda, **anonimiziraj** (odvezati od osobe, zadržati
račune pod „zatvoreni ured") ili premjestiti u zaseban retencijski prostor. **Za testiranje ovo nije
prepreka** — testni podaci se brišu u cijelosti.

## 3.5 Popis rizika i kako se zaštititi

| Rizik | Zaštita |
|---|---|
| **Brisanje previše** — obrisati korisnika koji je član ureda s više ljudi → nestanu tuđi podaci | Prije brisanja prebrojati članove ureda; ako > 1, tražiti izričitu potvrdu „briši cijeli ured" ili nuditi samo „briši člana" (kao `DeleteOwnAccountAsync`). |
| **Krivi korisnik** (admin klikne pogrešnog) | Potvrda s prepisivanjem (email ili naziv ureda), kao `DeleteFirmAsync` (traži točan naziv ureda). Prikazati što će se obrisati (broj predmeta/stranaka/računa). |
| **Opcija (a) ruši model** | Ne raditi (a); ići (b). |
| **Otvorena pozivnica na testni email** → ponovna registracija upadne u tuđi/stari ured | U brisanju obrisati `firm_invites` za taj email (u svim uredima). |
| **Djelomičan neuspjeh** (PostgREST nije transakcija) | Redoslijed „dnevnik prvo, `auth.users` zadnje"; logirati svaki korak; po mogućnosti bazni dio kao jedna `SECURITY DEFINER` funkcija/transakcija. |
| **Storage list limit 1000** | Paginirati listanje/brisanje datoteka po prefiksu ureda. |
| **Zastarjeli cache** (TenantResolver/SubscriptionGate/Usage) | Invalidirati po `userId`/`lawFirmId` nakon brisanja. |
| **Admin ovlast** | Ruta pod `/api/admin/*` → `AdminGuardMiddleware` provjerava `ADMIN_EMAIL` iz provjerenog JWT-a (fail-closed). Ne oslanjati se na `is-admin` cookie (samo UI). |
| **`monthly_revenue` zaostatak** | Osvježiti materijalizirani pogled ili prihvatiti da noćni cron počisti. |
| **Testiranje na produkciji** | Po `CLAUDE.md`: brisanje/testni podaci **samo na dev bazi/preview**, nikad prod dok su korisnici unutra. |

---

## Zaključak

- Podaci o korisniku žive na puno više mjesta od `auth.users`; glavnina „siročadi" je posljedica
  **namjernog** modela „vlasništvo po uredu" (015) + „autorstvo, ne vlasništvo" (020).
- **Ispravan mehanizam potpunog brisanja već postoji** (`DeleteFirmAsync`); nedostaje mu **admin-ciljana
  inačica** (gumb u [`frontend/app/admin/page.tsx`] → `DELETE /api/admin/users/{userId}`), plus
  čišćenje `firm_invites`/`waitlist` i invalidacija cacheva.
- Preporuka: **Opcija (b)**. Ne dirati FK-ove/model (Opcija a).
- Mjesta izvan dosega app-a (GoTrue audit, hosting/mail logovi, Google consent) prihvatiti/riješiti
  vanjski; ne blokiraju oslobađanje maila.

**Otvorena pitanja za ručnu provjeru (NE ZNAM iz koda):**
1. Supabase Auth postavke: koji provideri uključeni, „Confirm email", automatsko spajanje identiteta / sprječavanje duplih emaila.
2. Supabase Storage: postoji li treći bucket („documents") osim `zk-izvadci`/`case-documents`.
3. Vercel: ima li priključen KV/Blob/Postgres; retention funkcijskih logova.
4. Render: retention aplikacijskih logova.
5. Resend / Supabase: retention mail-logova.
6. Postoji li u produkciji analitika (GA/PostHog/Plausible) ili Sentry (nisu nađeni u kodu).
7. Ima li frontend potpun UI za slanje pozivnica u ured (`firm_invites`).
