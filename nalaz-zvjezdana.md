# Nalaz — provjera troškovnika po Zvjezdaninom popisu

**Datum:** 30.8.2026. · **Grana:** `claude/troskovnik-zvjezdanin-popis-ea565a`

Zvjezdanine riječi su mjerodavne. Prošao sam svaku stavku A–F, provjerio je li
ispravno, popravio rupe i razdvojio: **ISPRAVNO** / **GREŠKA** / **TREBA POTVRDU**.

## Sažetak
Gotovo sve je već bilo ispravno i zaključano kontrolnim testovima (rad iz ranijih
grana). Nijedan izračun ne daje krivu brojku. Popravio sam dvije prave rupe koje danas
ne daju krivi iznos, ali su nedostajale kao mehanizam: **C4** (verzija Tarife po
dospijeću) i **D10** (predstavnik pravne osobe 100/50 po danu rasprave), te ispravio
jedan netočan komentar u kodu (**D13**). **F3** (PDV na naknadu troškova) ostaje samo
prijava — čeka knjigovođu, ništa nije mijenjano.

**Stanje testova:** backend 2062 prolaze, frontend 2344 prolaze, paritet `tarifa.ts` ↔
backend drži. (3 nevezane `tsc` greške u `__tests__/wizard-types.test.ts` — modul
procjene nekretnina, nisu iz ovog rada.)

> **Napomena o produkcijskom dokazu:** troškovnik i sam izračun su iza prijave na
> pravotek.com; anonimni korisnik ne dolazi do kalkulatora. Dogovoreno je da David da
> privremenu prijavljenu testnu sesiju pa da kontrolne brojke provjerim uživo. Ćelije
> označene ⏳ čekaju tu sesiju; sve ostalo je dokazano kodom i testovima. Iznosi E/D/G
> provjeravaju **postojeće, već objavljeno ponašanje**; C4/D10 se na produkciji
> provjeravaju nakon merge-a u `main`.

---

## A. Postavke ureda

| # | Tvrdnja | Rezultat | Dokaz |
|---|---|---|---|
| A1 | Prekidač „odvjetnik je u sustavu PDV-a: da/ne" | **ISPRAVNO** | `law_firms.pdv_obveznik` (`021_pdv_obveznik_naknada_predujam.sql`), UI `LawFirmForm.tsx`; pretpunjava troškovnik (`page.tsx`, `setIsPdvUkljucen(Boolean(data.pdv_obveznik))`). |
| A2 | Kad je isključen, PDV se NIGDJE ne prikazuje — ni 0,00 € | **ISPRAVNO** | `buildIzvozModel` postavi `pdv: null` kad ured nije obveznik → cijeli PDV odjeljak izostaje i u ispisu (`TroskovnikIspis.tsx`, `{model.pdv && …}`) i u `.xlsx` (`troskovnik-xlsx.ts`, `if (m.pdv)`). |
| A3 | Podaci stranke imaju polja sa slike od 25.2. | **TREBA POTVRDU** | Nemam sliku od 25.2. pa ne mogu usporediti polje-po-polje. Postoje: ime, OIB, adresa, grad, položaj (`StrankeRequest`/`TroskovnikParty`). Pošalji sliku pa zaključavam. |

## B. Unos radnji

| # | Tvrdnja | Rezultat | Dokaz |
|---|---|---|---|
| B1 | Svaka radnja ima vlastito polje za datum | **ISPRAVNO** | `TroskovnikItemRequest.Datum (DateOnly?)`; redak nosi `datum` (`page.tsx`). |
| B2 | Tri ročišta na tri datuma → tri retka, svaki svoj Rbr. | **ISPRAVNO** | `dodajRedak` uvijek dodaje nov redak; Rbr izveden iz pozicije. |
| B3 | NIJE moguće samo upisati količinu 3 za tri datuma | **ISPRAVNO** | Jedan redak ima jedno polje `datum`; tri različita datuma nužno traže tri retka. Količina ostaje za ponavljanje na isti datum. |
| B4 | Redak se može premjestiti gore-dolje | **ISPRAVNO** | `pomakniRedak` + gumbi ▲/▼. |
| B5 | Redoslijed NIJE prisilno kronološki | **ISPRAVNO** | Redoslijed bira korisnik; komentar u `tarifa.ts` (`pomakniRedak`). |
| B6 | Rbr. se renumerira nakon premještanja i brisanja | **ISPRAVNO** | Rbr = index+1, nikad spremljen; test `troskovnik-struktura.test.ts`. |

## C. Obračun nagrade

| # | Tvrdnja | Rezultat | Dokaz |
|---|---|---|---|
| C1 | Bod = 2,00 € (Tbr. 54) | **ISPRAVNO** | `TarifaConstants.BodValue = 2.00m`; ⏳ prikaz „1 bod = 2,00 €" na produkciji. |
| C2 | Zaokruživanje na dvije decimale | **ISPRAVNO** | `ZaokruziNovac` = `Math.Round(…, 2, AwayFromZero)`, jednom, iz nezaokruženih bodova. |
| C3 | Tablica bodova s decimalama (`0–332,00`, `332,01–664,00`…) | **ISPRAVNO** | `TBR_7_1_RAZREDI` u `tarifa.ts`; ⏳ prikaz na produkciji. |
| C4 | Verzija Tarife ovisi o DOSPIJEĆU (Tbr. 52), ne o datumu radnje | **GREŠKA → POPRAVLJENO** | Vidi „Popravci" niže. |
| C5 | Tbr. 40 — više stranaka: +10 %/max +50 %, pa dioba na jednake dijelove | **ISPRAVNO** | `PovisenjeViseOsobaPostotak` + dioba; testovi `TarifaTbr40*`. |
| C6 | Tbr. 41 — povišenje 100 % i sniženje 50 % ne istovremeno | **ISPRAVNO** | `RazrijesiTbr41` baca grešku ako su oba >0. |
| C7 | Tbr. 45 samo ako za radnju nije propisana druga tarifa | **ISPRAVNO** | Tbr. 45 kao varijanta osnovice (`IzracunajOsnovicu`), „kao osnovica" vs „fiksno po radnji". |
| C8 | Tbr. 9 t.5 — pristup na odgođeno ročište prije raspravljanja: 25 % | **ISPRAVNO** | `par_pristup_odgodjen`: `Tbr71Postotak = 0.25, MaxBodova = 50`. |

## D. Kazneni postupak

| # | Tvrdnja | Rezultat | Dokaz |
|---|---|---|---|
| D1 | Nagrada vezana za ZAPRIJEČENU KAZNU, ne VPS | **ISPRAVNO** | `RazrijesiKazneniRazred`; VPS se u kaznenom ne koristi. |
| D2 | Tbr. 1 t.2 (Podnesci) | **ISPRAVNO** | `kaz_podnesak_obrana`. |
| D3 | Tbr. 1 t.3 | **ISPRAVNO** | `kaz_zahtjev_ostecenik`. |
| D4 | Tbr. 1 vezan na raspone Tbr. 4 t.1 po zaprijećenoj kazni | **ISPRAVNO** | `ZaprijecenaKaznaTiers` 100/200/300/400/500. |
| D5 | Tbr. 2 | **ISPRAVNO** | pomilovanje/rehabilitacija/uvjetni otpust. |
| D6 | Tbr. 3 | **ISPRAVNO** | policija/DO/istraga (prvi sat + 50/sat). |
| D7 | Stjecaj — najteža kazna koja se može izreći | **ISPRAVNO (po dizajnu)** | `is_stjecaj` → napomena; tier najteže kazne bira korisnik (`zaprijecena_kazna`). Nema automatske derivacije jer „najteža koja se može izreći" je pravna procjena, ne podatak u sustavu. |
| D8 | Prekršajni s oduzimanjem → Tbr. 7 t.1 po vrijednosti oduzetog | **ISPRAVNO** | `kaz_prekrsaj_oduzimanje` → `Tbr71Bodovi(vrijednost_oduzimanja)`. |
| D9 | Obrana pravne osobe — po kazni za odgovornu osobu | **ISPRAVNO** | `kaz_obrana_pravna_osoba` (`KazneniBase`). |
| D10 | Predstavnik pravne osobe (sud): 100 bod. prvi sat svakog dana, 50 svaki sljedeći | **GREŠKA → POPRAVLJENO** | Vidi „Popravci" niže. |
| D11 | „Branitelj po službenoj dužnosti" = 50 % (NN 22/2022, čl. 1) | **ISPRAVNO** | `KazneniReductionRate = 0.50`. |
| D12 | „Opunomoćenik djeteta žrtve" = 70 % (čl. 2) | **ISPRAVNO** | `ZastupnikDjetetaRate = 0.70`. |
| D13 | D11 i D12 se isključuju — ne množe u 35 % | **ISPRAVNO** (+ ispravljen komentar) | `if (dijete) ×0.70 else if (sl.dužnost/proračun) ×0.50` — nikad umnožak. Vidi „Popravci". |
| D14 | Tbr. 4 t.4 — obustava/odgoda prije izvođenja dokaza: 50 % | **ISPRAVNO** | `kaz_obustava_odgoda`, `KazneniPovecanje = -0.5`. |

**Kontrola D11–D13 (1.000 € opunomoćeniku djeteta žrtve → 700,00 €):**
500 bodova (dugotrajni zatvor) × 2,00 € = 1.000 €; × 0,70 = **350 bodova = 700,00 €**.
Test `TarifaUmanjenjaTests.OpunomocenikDjeteta_Je70PostoTarife_Ne35` (350 bodova, ne 175).
⏳ Produkcija: unijeti isti slučaj, očekivano 700,00 €.

## E. Sudske pristojbe

| # | Tvrdnja | Rezultat | Dokaz |
|---|---|---|---|
| E1 | NN 50/2026, ne NN 37/2023 | **ISPRAVNO** | `PristojbeTarifaNn50_2026` default; NN 37/2023 samo za dva čl. 2 prijelazna slučaja. |
| E2 | parnica, tužba, VPS 50.000 € → **620,00 €** | **ISPRAVNO** | 140 + 1 % × 48.000 = 620,00; test `KontrolniIzracuni.A_Tuzba50000_Daje620`. ⏳ produkcija. |
| E3 | Maksimum **1.500,00 €** (ne 663,61 €) | **ISPRAVNO** | `Max: 1 500`; test `B_Maksimum_Kapa1500`. ⏳ produkcija. |
| E4 | Tar. br. 1 se NE primjenjuje izvan parnice/ovrhe | **ISPRAVNO** | 9 poglavlja; upravni spor = pogl. VIII, fiksno. |
| E5 | upravni spor, tužba → **110,00 €** fiksno bez obzira na VPS | **ISPRAVNO** | `tbr29_tuzba = Fiksno(110.00)`; test `C_UpravniSpor_Fiksnih110`. ⏳ produkcija. |
| E6 | Tar. br. 3 — žalba protiv presude: puna +100 % → **1.240,00 €** | **ISPRAVNO** | `tbr3_st1 = UdioOd(Tbr1St1, 2.0)`; test `D_ZalbaProtivPresude_Daje1240`. ⏳ produkcija. |
| E7 | Čl. 7 ZSP — elektronički podnesak: polovica | **ISPRAVNO** | `UmanjujeSeCl7`; 620 → 310; test `F_ElektronickiPodnesak_Daje310`. |
| E8 | Obje verzije Uredbe (prijelazna odredba prije 1.6.2026.) | **ISPRAVNO** | `RazrijesiVerziju` po retku; `NovaUredbaNaSnazi = 2026-06-01`. |
| E9 | Oslobođenja (čl. 11, 21 kategorija) | **ISPRAVNO** | `OslobodjenjeKategorija` — 22 (21× čl.11 + čl.10). |

## F. Ispis troškovnika

| # | Tvrdnja | Rezultat | Dokaz |
|---|---|---|---|
| F1 | Redoslijed: radnje → nagrada → PDV → pristojbe → predujmovi → napomena | **ISPRAVNO** | `TroskovnikIspis.tsx` i `troskovnik-xlsx.ts` istim redom. |
| F2 | PDV na nagradu | **ISPRAVNO** | `pdv = 25 % × osnovicaSaNaknadom`. |
| F3 | PDV i na putne, dnevnice i hotel (Tbr. 48–51) | **TREBA POTVRDU (knjigovođa)** | Vidi zaseban odjeljak niže. |
| F4 | PDV se NE obračunava na sudske pristojbe | **ISPRAVNO** | pristojbe su prolazne, dolaze nakon PDV-a. |
| F5 | PDV se NE obračunava na predujmove | **ISPRAVNO** | predujmovi prolazni, bez PDV-a. |
| F6 | Napomena sa zadanim tekstom o zateznoj kamati (čl. 29 ZOO) | **ISPRAVNO** | `ZADANA_NAPOMENA` / `TarifaConstants.ZadanaNapomena`, paritet zaključan. |
| F7 | Datum uz svaku radnju u ispisu i izvozu | **ISPRAVNO** | stupac „Datum" u `TroskovnikIspis.tsx` i `.xlsx`. |
| F8 | Oblik dokumenta slobodan | **ISPRAVNO** | nema propisanog obrasca. |

---

## Popravci (s dokazom)

### C4 — verzija Tarife po dospijeću (Tbr. 52)
**Bilo:** nagrada je koristila jednu, hardkodiranu Tarifu (HOK 2023) i jedan bod, bez
ikakvog odabira po datumu. Datumska selekcija postojala je samo za pristojbe.
**Popravljeno:** uveden registar verzija Tarife i odabir mjerodavne **po dospijeću** (ne
po datumu radnje):
- `Backend/Models/TarifaKonstante.cs` — `TarifaVerzija` + `TarifaVerzije.Razrijesi(datumDospijeca)`.
- `Backend/Models/TroskovnikZahtjev.cs` — polje `datum_dospijeca`.
- `Backend/Services/TroskovnikService.cs` — verzija razriješena u `KontekstIzracuna`, bod se
  čita iz nje (`kontekst.Tarifa.BodValue`); `VrijednostBoda` u odgovoru odražava verziju.
- Zrcalo: `frontend/lib/tarifa.ts` (`razrijesiTarifu`), polje „Datum dospijeća (Tbr. 52)"
  u `frontend/app/troskovnik/page.tsx`, šalje se u `/izracunaj` i `/spremi`.
- Testovi: `TarifaVerzijaDospijeceTests` (13), `tarifa-verzija-dospijece.test.ts` (7) —
  odabir po dospijeću dokazan i sa sintetičkim popisom od dvije verzije.

**Ograničenje (vanjski izvor):** registar danas ima **jednu** verziju, pa svaki datum
razrješava na nju i iznos se ne mijenja. Stvarne brojke bilo koje ranije verzije (npr.
kuna-era prije 2023.) nisu poznate i nisam ih izmišljao — dodaju se kao novi redak kad
budu dostupne. Kriterij (dospijeće, ne datum radnje) je sada ispravan i mehanizam je
spreman.

### D10 — predstavnik pravne osobe: 100/50 po danu rasprave
**Bilo:** postojala su fiksnih 100 bodova (`kaz_predstavnik_sud`, jedinica „sat") i
generičkih 50/sat (`kaz_dodatni_sat`), ali bez pravila „prvi sat **svakog dana** rasprave".
**Popravljeno:** `kaz_predstavnik_sud` sada je **po danu rasprave** (jedinica „dan
rasprave", količina = broj dana → 100 bod. po danu); svaki sljedeći započeti sat ide preko
`kaz_dodatni_sat` (50). Test `TarifaPredstavnikPravneOsobeTests`: 2 dana + 1 dodatni sat =
100×2 + 50×1 = **250 bodova = 500,00 €**.

### D13 — netočan komentar (ponašanje je bilo ispravno)
Komentar u `TroskovnikService.cs` tvrdio je da proturječnu kombinaciju (dijete žrtve +
sl. dužnost) „odbija TroskovnikRequestValidator" — ali taj validator se uopće ne izvršava
(dokumentirano u zaglavlju same klase). Stvarno ponašanje je ispravno i namjerno: kad
zahtjev ipak stigne s obje zastavice (star klijent, pohranjen redak), `else if` primijeni
najviše jednu stopu (dijete žrtve, 70 %) — **nikad 35 %**. Ispravio sam komentar da opisuje
stvarno ponašanje; nisam uvodio tvrdo odbijanje jer bi ono pucalo na učitavanju starih
spremljenih troškovnika (postojeći test `ObjeUloge_NeMnozeStope` čuva tu namjeru).

---

## Čeka vanjsku potvrdu

### F3 — PDV na naknadu troškova (knjigovođa) — NIJE MIJENJANO
**Kako aplikacija trenutno radi:** PDV se obračunava na osnovicu koja UKLJUČUJE naknadu
troškova (Tbr. 48–51: prijevoz, dnevnica, hotel, ostali izdaci):
```
osnovicaSaNaknadom = ukupnoEur (nagrada) + naknadaTroskovaUkupno
pdv = 25 % × osnovicaSaNaknadom
```
(`Backend/Services/TroskovnikService.cs`, `IzracunajZbrojeve`). Kod već navodi čl. 33
Zakona o PDV-u i čl. 78 Direktive 2006/112/EZ — gorivo i hotel su vlastiti trošak
odvjetnika pa ulaze u osnovicu; sudske pristojbe (plaćene u ime stranke) NE ulaze.
**Ovo je jedina stavka gdje se ti i Zvjezdana ne slažete i rekla je da nije sigurna.**
Po dogovoru: NE dira se u kod dok knjigovođa ne potvrdi. Ovdje je samo prijava stanja.

### C4 — brojke ranije verzije Tarife (vidi gore)
Mehanizam je gotov; stvarne brojke starije verzije trebaju vanjski izvor prije unosa.

### A3 — polja stranke sa slike od 25.2.
Trebam sliku da usporedim polje-po-polje.

---

## G — prikaz Tbr. 7 iznad 66.361 € (na primjeru VPS 70.000 €)

Zvjezdanina računica za Tbr. 7 iznad 66.361 € — provjera da aplikacija odgovara Tarifi:

- 6. fiksni razred: do **66.361,00 €** = **500 bodova**.
- Iznad 66.361 € (do 663.614 €): **+1 bod za svakih započetih 133,00 €**.
- Za 70.000 €: `70.000 − 66.361 = 3.639,00 €`.
- Koraci: `⌈3.639 / 133⌉ = ⌈27,36⌉ = 28` (zaokružuje se prema gore — „za svakih ZAPOČETIH").
- Bodovi: `500 + 28 = 528`.
- Nagrada: `528 × 2,00 € = ` **1.056,00 €**.

Kod: `Tbr71Bodovi` u `TroskovnikService.cs` (granice 66.361 → 500, korak 133,00, `Math.Ceiling`).
Test `TarifaTbr7Tests`. ⏳ Produkcija: parnica, tužba, VPS 70.000 € → nagrada 1.056,00 €.

---

## Produkcijska provjera (⏳ čeka testnu sesiju)

| Slučaj | Unos | Očekivano | Rezultat |
|---|---|---|---|
| E2 | parnica, tužba, VPS 50.000 € | 620,00 € | ⏳ |
| E3 | parnica, tužba, VPS 200.000 € | 1.500,00 € | ⏳ |
| E5 | upravni spor, tužba | 110,00 € | ⏳ |
| E6 | parnica, žalba protiv presude, VPS 50.000 € | 1.240,00 € | ⏳ |
| D11–13 | kazneni, obrana, dugotrajni, opunomoćenik djeteta | 700,00 € | ⏳ |
| G | parnica, tužba, VPS 70.000 € (nagrada) | 1.056,00 € | ⏳ |
| C1/C3 | prikaz boda i bodovne tablice | 2,00 €; decimalne granice | ⏳ |
| A2 | ured NIJE PDV obveznik → ispis | nigdje PDV, ni 0,00 € | ⏳ |
| C4 | (nakon deploya) datum dospijeća → verzija Tarife | polje postoji, iznos = trenutna | ⏳ |
| D10 | (nakon deploya) 2 dana + 1 sat predstavnika | 500,00 € | ⏳ |

## Isporuka u `main` i produkciju
⏳ Nakon produkcijske provjere kontrolnih brojki i potvrde da ništa nije regresiralo:
merge u `main` + push → automatski deploy (Vercel/Render), pa ponovna provjera C4/D10 na
živoj produkciji i finalni screenshotovi ovdje.
