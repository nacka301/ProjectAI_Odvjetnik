# Pravila projekta PravoTek / odvjetnik_ai

Ovo čita Claude Code na početku svake sesije. Drži se ovoga uvijek.

## ZAKON O GRANAMA (najvažnije)

- `main` = **PRODUKCIJA**. S nje se objavljuje `pravotek.com` i na njoj su
  **stvarni korisnici i njihovi podaci**.
- **NIKAD ne commitaj i NIKAD ne pushaj izravno na `main`.**
- Sav rad — svaki commit i push — ide na granu **`razvoj`** (ili na granu koju
  David izrijekom navede u tom zadatku).
- **Ne otvaraj novu `claude/...` granu po zadatku.** Radi na `razvoj`, osim ako
  David izričito ne traži zasebnu granu.
- U `main` se spaja **isključivo** kad David to izričito kaže riječima poput
  „spoji u main", „pusti u produkciju". Do tada `main` se ne dira.
- Ako se zatekneš na `main`, **stani** i prebaci se na `razvoj` prije bilo kakve
  izmjene (`git switch razvoj`).

## Tijek rada (točan redoslijed)

1. Provjeri granu: `git branch --show-current`. Mora biti `razvoj` (ili
  dogovorena grana). Ako nije — prebaci se.
2. Napravi izmjenu → commit → **push na `razvoj`**.
3. Vercel automatski radi **Preview** za `razvoj`. David provjerava tamo, na
  živom preview linku (ne lokalni build).
4. Tek na Davidovu izričitu naredbu: `razvoj` → merge u `main` → push `main`
  (to je objava u produkciju).
5. Nakon spajanja u `main`, **poravnaj `razvoj` s `main`** (merge/rebase `main`
  u `razvoj`) da grane ne divergiraju.

## Baza (Supabase)

- Prod baza (`ai-odvjetnik-prod`) sadrži prave korisnike. **Ne diraj je** za
  testiranje.
- Testiranje, testni podaci, brisanje korisnika i probne migracije idu u
  **dev bazu** (preview okruženje), nikad u prod.
- Migracije: **prvo dev → provjeri → tek onda prod**. Prije prod migracije:
  backup. Ništa ručno nad prod bazom dok su korisnici unutra.

## Provjera

- Dokaz je **živa stranica**, ne lokalni build: `razvoj` se provjerava na
  Vercel Preview linku, `main` na `pravotek.com` (anonimni prozor).
- Za promjene sučelja priloži snimke (desktop + mobilni).

## Ostala pravila

- Sav tekst u aplikaciji na **hrvatskom**.
- Prije implementacije objasni plan (Ask/Plan), pa tek onda kod — detalji u
  `START-novi-chat.md` i memorijskim pravilima (ai-development-rules).
- Jedna značajka = jedan zadatak. Ne pakiraj više nepovezanih stvari zajedno.
