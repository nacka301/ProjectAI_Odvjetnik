# Prompt za nastavak — kopiraj sve ispod crte u novi prozor

---

Radiš na `odvjetnik_ai` — hrvatska pravna SaaS aplikacija, modul **troškovnik**
(odvjetnička tarifa + sudske pristojbe).

## PRVO PROČITAJ, PRIJE IJEDNE IZMJENE

1. `git branch -a --list "*tarifa*"` — rad **NIJE na `main`**. Živi na grani
   **`claude/tarifa-parnicni-na-audit`** (worktree:
   `.claude/worktrees/tarifa-civil-implementation-708d38`).
   `main` još ima staru, nepopravljenu verziju. `git log main..HEAD` nije dovoljan
   da nađeš prethodni rad — koristi `git log --all --oneline --grep=tarifa`.
2. `audit-tarifa-popravci.md` u korijenu grane — izvještaj o svemu popravljenom,
   s citatima iz Tarife, `file:line` i imenima testova. Ovo je najbrži put do konteksta.
3. Mjerodavni izvori su PDF-ovi u korijenu repoa:
   - `Tarifa-o-nagradama-i-naknadi-troskova-za-rad-odvjetnika-nesluzbeni-procisceni-tekst.pdf`
     (HOK, 14. listopada 2023., nesl. pročišćeni tekst listopad 2025.)
   - `Uredba-sudske-pristojbe.pdf` (NN 37/2023)
   Čitaj ih s `pdftotext -layout <file> -` — instaliran je i radi.

## PRAVILA RADA (obavezno)

- **Ne izmišljaj nijedan broj.** Svaki iznos, postotak i prag mora imati doslovan
  citat iz Tarife ili Uredbe kao komentar uz kod. Ako Tarifa nešto ne kaže, to je
  nalaz koji prijavljuješ — ne rupa koju popunjavaš pretpostavkom.
- **Test prije popravka.** Napiši test koji pada, popravi, test prolazi. Testovi
  nose ručno izračunate iznose iz PDF-a u komentaru.
- **Zaseban commit po popravku**, poruka na hrvatskom, s citatom odredbe i
  vrijednošću prije/poslije.
- **Piši na hrvatskom** — kod, komentari, commit poruke, odgovori.
- **Kratki odgovori.** David je izričito tražio da opisi budu kraći.
- **Provjeravaj tuđe nalaze.** Prethodne sesije su griješile; ne uzimaj izvještaje
  zdravo za gotovo, provjeri protiv PDF-a i pokreni testove.
- **Ništa ne smije tiho vratiti 0 ni tiho se ograničiti.** Baci grešku s porukom.

## STANJE — ŠTO JE GOTOVO

Backend **1512** testova, frontend tarifa **191**, 0 padova.

Motor: `Backend/Services/TroskovnikService.cs`, modeli
`Backend/Models/TroskovnikModels.cs`, pristojbe
`Backend/Services/SudskePristojbeCalculator.cs`, ispis
`Backend/Services/TroskovnikDocumentService.cs`.
Klijentsko zrcalo: `frontend/lib/tarifa.ts` (mjerodavan je backend; zrcalo postoji
samo da živi pregled bude trenutan, a paritetni testovi hvataju razilaženje).

Pokriveno:

- **Kazneni dio (Tbr. 1–6)** — u cijelosti, uključujući Tbr. 3.2 i Tbr. 4.2 al. 4,
  4.3, 4.4, 4.5, 4.6, 4.8.
- **Parnični dio (Tbr. 7–10)** — 28/28 točaka.
- **Osnovica „Tbr. 7. toč. 1., 4., 7. i 8."** — `IzracunajOsnovicu()`, sedam varijanti
  (toč. 1, toč. 4 autorsko / renta, toč. 7 domaća / međunarodna arbitraža, toč. 8
  mirenje, Tbr. 45). Ovo je bio korijenski uzrok: kod je osnovicu fiksirao na toč. 1.
- **Tbr. 40** — +10 % od druge osobe, kapa 50 %, dioba nagrade na jednake dijelove.
- **Tbr. 41** — po **pojedinoj stavci** (toč. 2 kaže „pojedine"), povišenje 0–100 %,
  sniženje 0–50 %, međusobno isključivi, aditivni na zbroj.
- **Tbr. 45** — 100/200/500 po razini suda, dva čitanja, odabir obavezan.
- **Sudske pristojbe** — NN 37/2023, polovica u ovrsi, ništa u kaznenom.
- **Zaokruživanje** — bodovi decimalni kroz cijeli lanac, novac zaokružen jednom na
  kraju, `AwayFromZero`.
- **`pravniIzvor`** na svakoj stavci, vidljiv u sučelju, CSV-u i HTML ispisu.
- Sučelje za sve navedeno na `/troskovnik`.

## OTVORENO — ODABERI JEDNU STAVKU I PITAJ PRIJE NEGO POČNEŠ

Poredano po važnosti:

1. **Tbr. 23, 26, 29, 38, 42, 43, 44, 47, 48, 49–51, 52** — nisu u katalogu.
   Najkorisniji su Tbr. 38 (25 bodova po satu kad nije drukčije određeno) i
   Tbr. 29 (poništaj upisa u registar).
2. **Tbr. 49–51 (putni troškovi, dnevnica, hotel)** — stavke Tbr. 3.2, 3.4 i 4.6
   ih spominju u opisu, ali se ne obračunavaju. To su stvarni izdaci, ne bodovi,
   pa traže vlastiti mehanizam izvan bodovnog sustava.
3. **Tbr. 10 upućuje na uži skup** — „Tbr. 7. toč. 1., 4. i 7.", bez toč. 8, za
   razliku od Tbr. 8 i Tbr. 9. Sustav primjenjuje jednu osnovicu na cijeli obračun.
   Iznos je isti (toč. 8 upućuje natrag na toč. 1), ali razlika u tekstu je stvarna.
4. **Tbr. 4. toč. 2 al. 4** — „nagrada kao za obranu pred sudom drugog stupnja", bez
   postotka. Uzeto je +25 % (dugotrajni zatvor +50 %) jer al. 3 tako definira obranu
   pred drugim stupnjem. Tumačenje, ne citat — traži mišljenje odvjetnice.
5. **Tbr. 45 — dva čitanja** primjenjuju li se postoci iz Tbr. 8/9 na 100/200/500.
   Tarifa ne kaže; oba su implementirana, odabir je obavezan. Traži mišljenje.
6. **Tbr. 7. toč. 9** je stavka, ne prekidač. Sustav upozorava kad je VPS < 100 €
   a stavka nije odabrana, ali ne mijenja obračun automatski.

**Tbr. 25 (javna nabava) NE IMPLEMENTIRAJ** — ukinut je presudom Visokog upravnog
suda RH Usoz-55/2024-21 od 18. srpnja 2025., iako stoji u pročišćenom tekstu.
Postoji test koji pada ako ga netko doda.

## ZATEČENI KVAROVI KOJI NISU TVOJI

- `frontend/__tests__/delete.test.ts` — 5 padova, nema veze s tarifom, potvrđeno
  na čistom stablu. Ne pokušavaj ih popraviti usput.
- `main` ima staru verziju modula; grana nije spojena.

## PROVJERA

```bash
dotnet test Backend.Tests/Backend.Tests.csproj
```

```bash
cd frontend && npx vitest run __tests__/tarifa
```

Ručno na `/troskovnik`: VPS 3.000 € + „Prijedlog za ovrhu" → pristojba **38,23 €**;
samo kaznene stavke → polje VPS nestaje, pristojba **0,00 €**; VPS 1.000 € +
„Pravni lijek u izvanparnici" → **46,875 bod. = 93,75 €**.

## ZAPOČNI OVAKO

Nemoj odmah kodirati. Prvo:
1. Provjeri granu i pročitaj `audit-tarifa-popravci.md`.
2. Reci mi što si našao i predloži **jednu** stavku iz popisa otvorenog.
3. Pričekaj da potvrdim prije nego dirneš kod.
