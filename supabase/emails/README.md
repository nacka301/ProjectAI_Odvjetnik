# E-mail predlošci i postavke Supabase Auth

Ovi predlošci **nisu dio deploya**. Moraju se ručno zalijepiti u
Supabase → Authentication → Emails → Templates. Dok se to ne napravi, vrijede
Supabaseovi zadani predlošci na engleskom.

## Zašto link više nije `{{ .ConfirmationURL }}`

`@supabase/ssr` koristi PKCE. Kod PKCE-a `ConfirmationURL` vodi na preusmjeravanje
s `?code=`, a razmjena tog koda traži `code_verifier` — kolačić koji postoji
**samo u pregledniku koji je pokrenuo radnju**.

Posljedica: odvjetnik se registrira na računalu, otvori mail na mobitelu, i dobije
„Link je istekao" na linku koji nije istekao. Isto je vrijedilo za reset lozinke.

Predlošci sada šalju `token_hash`, koji `/auth/callback` provjerava preko
`verifyOtp()`. Ništa se ne čita iz preglednika, pa link radi s bilo kojeg uređaja.

Ruta i dalje podržava i stari `?code=` put — zbog Google prijave i zbog mailova
koji su već poslani.

## Što još treba postaviti u nadzornoj ploči

Ovo se ne može napraviti iz repozitorija:

| Gdje | Postavka | Zašto |
|---|---|---|
| Authentication → URL Configuration | **Site URL** = produkcijska adresa | `{{ .SiteURL }}` u predlošcima je izvodi |
| Authentication → URL Configuration | **Redirect URLs** mora sadržavati `<adresa>/auth/callback` | inače Supabase odbija preusmjeravanje |
| Authentication → Emails → SMTP | **Vlastiti SMTP** (Resend, Postmark…) | ugrađeni Supabase mailer je ograničen na nekoliko poruka na sat i nije za produkciju — bez ovoga registracija e-mailom praktički ne radi |
| Authentication → Providers → Email | **Minimum password length = 12** | pravilo od 12 znakova je u `lib/auth-validation.ts` samo UX; poslužitelj vrijedi jedini |
| Authentication → Providers → Email | **Confirm email = ON** | inače se račun aktivira bez potvrde, i spajanje s Googleom ispod ne radi |
| Authentication → Providers → Google | provjeriti spajanje identiteta | ako e-mail nije potvrđen, isti čovjek dobije **dva računa i dva ureda**, i troškovnici jednog nisu vidljivi iz drugog |

Zadnji redak je jedini slučaj iz revizije (br. 7) koji se ne može zatvoriti kodom.
