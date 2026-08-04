# Popravci odvjetničke tarife i sudskih pristojbi

Grana: `claude/tarifa-parnicni-na-audit` (stoji na `claude/audit-tarifa-fixes-9e61a6`)
· 22 commita · 29. srpnja 2026.

Mjerodavno:

- **Tarifa o nagradama i naknadi troškova za rad odvjetnika** — Upravni odbor Hrvatske
  odvjetničke komore, 14. listopada 2023.; nesl. pročišćeni tekst (listopad 2025.),
  `Tarifa-o-nagradama-...-nesluzbeni-procisceni-tekst.pdf` u korijenu repoa
- **Uredba o tarifi sudskih pristojbi**, NN 37/2023, `Uredba-sudske-pristojbe.pdf`
- **Pravilnik**, NN 22/2022, br. 271 —
  https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html

> **Napomena o `audit-tarifa.md`:** taj datoteka ne postoji u repozitoriju — ni u
> radnom stablu, ni u `main`, ni u git povijesti. Radio sam po listi od 14 točaka iz
> zadatka, svaku verificiravši protiv gornjih izvora. Gdje se moj nalaz iz izvora
> razlikuje od formulacije u zadatku, to je izričito označeno niže.

Stanje testova nakon svih popravaka:

```
dotnet test Backend.Tests/Backend.Tests.csproj   →  1487 prolaza, 0 padova
cd frontend && npx vitest run __tests__/tarifa   →   164 prolaza, 0 padova
```

---

## 0. Preduvjet — DI registracije (`e45c2f3`)

Nije bilo na listi, ali bez ovoga se nijedan popravak nije mogao provjeriti u
pokrenutoj aplikaciji.

Commit `c04aeeb` uveo je `I*Service` sučelja u konstruktore kontrolera, ali ih nije
registrirao. **11 od 20 kontrolera bacalo je `InvalidOperationException`**, uključujući
cijeli `/api/troskovnik`. Dvije vrste kvara: sučelja bez ijedne registracije, i sučelja
registrirana bez konkretnog tipa dok kontroler injektira konkretnu klasu.

| | |
|---|---|
| Popravak | [Backend/Extensions/DomainServiceCollectionExtensions.cs](Backend/Extensions/DomainServiceCollectionExtensions.cs), poziv na [Backend/Program.cs:166](Backend/Program.cs#L166) |
| Test | `DependencyInjectionTests.SviKontroleri_ImajuRazrjesiveOvisnosti` — refleksijom razrješava svaki parametar konstruktora svakog kontrolera |
| Prije | 8 nerazrješivih ovisnosti |
| Poslije | 0 |

**Priprema (`d542e77`):** klijentski motor izdvojen iz `page.tsx` u
[frontend/lib/tarifa.ts](frontend/lib/tarifa.ts), bez promjene ponašanja, uz paritetni
vitest paket. Sve daljnje izmjene rade se na tom jednom mjestu.

---

## 1. Tbr. 7. toč. 2 i 3 — jednokratna nagrada (`00f8897`)

> Tbr. 7. toč. 2: *"Odvjetniku pripada jednokratna nagrada u visini od 250 bodova, koja
> obuhvaća najviše do četiri radnje za svaki prvostupanjski postupak (…) Za svaku daljnju
> radnju u navedenim postupcima odvjetniku pripada nagrada u visini od 50 bodova."*
> Toč. 3 je isti mehanizam s osnovicom od 200 bodova.

Obje su se stavke množile količinom.

| | |
|---|---|
| Popravak | [TroskovnikService.cs:138](Backend/Services/TroskovnikService.cs#L138) (toč. 2), [:147](Backend/Services/TroskovnikService.cs#L147) (toč. 3), logika u `UkupnoBodovaZaStavku` [:583](Backend/Services/TroskovnikService.cs#L583) |
| Model | `JednokratnaNagrada`, `UkljuceneRadnje`, `DaljnjaRadnjaBodovi` — [TroskovnikModels.cs:541](Backend/Models/TroskovnikModels.cs#L541) |
| Klijent | `ukupnoBodovaZaStavku` — [lib/tarifa.ts:312](frontend/lib/tarifa.ts#L312) |
| Testovi | `TarifaTbr7Tests` (14) · `__tests__/tarifa-jednokratna.test.ts` |

| količina | toč. 2 prije | toč. 2 sada | toč. 3 prije | toč. 3 sada |
|---|---|---|---|---|
| 4 | 1.000 | **250** | 800 | **200** |
| 5 | 1.250 | **300** | 1.000 | **250** |
| 6 | 1.500 | **350** | 1.200 | **300** |

U opis stavke po toč. 2 upisan je i uvjet iz same Tarife: *"osim u procjenjivim
predmetima na koje se primjenjuje Tbr. 7. toč. 1."*

---

## 2. Tbr. 8. toč. 1 — odgovor na tužbu (`fff650f`)

> *"Za sastavljanje odgovora na tužbu, prigovora protiv platnog naloga i četiri
> obrazložena podneska u prvostupanjskom postupku odvjetniku pripada nagrada kao za
> tužbu po Tbr. 7. toč. 1., 4., 7. i 8."*

Stavka nije postojala u katalogu.

| | |
|---|---|
| Popravak | `par_odgovor_tuzba` — [TroskovnikService.cs:157](Backend/Services/TroskovnikService.cs#L157) |
| Testovi | `TarifaTbr8Tests` (9) |
| Provjera | VPS 20.000 €, količina 1 / 3 / 6 / 20 → uvijek **250 bodova** |

Tbr. 8. toč. 3 nije diran; opis mu je dopunjen s *"izvan Tbr. 8.1"*, kako Tarifa i kaže.

---

## 3. Kazneni postupak ne koristi VPS (`ce41fcf`)

U kaznenom se sve veže na zapriječenu kaznu (Tbr. 4. toč. 1). VPS je institut parničnog
postupka.

| | |
|---|---|
| Popravak | `isSamoKazneni` — [TroskovnikService.cs:373](Backend/Services/TroskovnikService.cs#L373); kontroler sprema VPS iz rezultata, ne iz zahtjeva — [TroskovnikController.cs](Backend/Controllers/TroskovnikController.cs) |
| Klijent | `jeSamoKazneni` — [lib/tarifa.ts:90](frontend/lib/tarifa.ts#L90) |
| Testovi | `TarifaKazneniTests` (4) · `__tests__/tarifa-kazneni.test.ts` |
| Provjera | nijedna kaznena stavka u katalogu ne mijenja bodove kad VPS ide s 0 na 1.000.000 €; kazneni predmet s VPS-om 50.000 € vraća `vrijednost_spora = 0` |

Iznimka propisana u **Tbr. 4. toč. 1 al. 8** ostaje: prekršaj s oduzimanjem ide po
Tbr. 7. toč. 1, ali prema vrijednosti **oduzetog predmeta**.

---

## 4. Pristojba se ne obračunava u kaznenom (`a0dc715`)

> Uredba NN 37/23, Prilog, **POGLAVLJE I. — "PARNIČNI I OVRŠNI POSTUPAK"**.
> Kaznenog postupka u Uredbi nema.

| | |
|---|---|
| Popravak | `PristojbaOsnova { Puna, Polovica, Nema }` — [TroskovnikModels.cs:82](Backend/Models/TroskovnikModels.cs#L82); odabir u `OdrediPristojbuOsnovu` — [TroskovnikService.cs:329](Backend/Services/TroskovnikService.cs#L329) |
| Testovi | `SudskePristojbeTests` (17) · `__tests__/tarifa-pristojbe.test.ts` |
| Prije | kazneni predmet, VPS 50.000 € → **546,45 €** |
| Poslije | **0,00 €** |

Osnova putuje u odgovor, pa se *"Uredba ovdje ne propisuje pristojbu"* razlikuje od
oslobođenja stranke po čl. 11. Zakona — oboje daju 0,00 €, ali iz različitih razloga.
Sučelje ispisuje koji je od ta dva razloga posrijedi.

---

## 5. Pristojba u ovrsi je polovica (`94921c9`)

> Tar. br. 1 st. 2: *"(…) prijedlog za ovrhu, protuovrhu i osiguranje (…) žalbu,
> prijedlog za dopuštenje revizije i reviziju plaća se **polovica** pristojbe iz
> stavka 1. ovoga Tar. br."*

| | |
|---|---|
| Popravak | [SudskePristojbeCalculator.cs:71](Backend/Services/SudskePristojbeCalculator.cs#L71); nezaokruženi st. 1 u `IzracunajNezaokruzeno` [:83](Backend/Services/SudskePristojbeCalculator.cs#L83) |
| Klijent | `izracunajSudskuPristojbu` — [lib/tarifa.ts:408](frontend/lib/tarifa.ts#L408) |
| Testovi | `SudskePristojbeTests.OvrsniPostupak_PolovicaPristojbe`, `.Ovrha3000Eura_JeTocno3823` |

| VPS | prije | sada |
|---|---|---|
| 3.000 € | 76,45 € | **38,23 €** |
| 300 € | 13,27 € | **6,64 €** |
| 50.000 € | 546,45 € | **273,23 €** |

Kontrolni izračun: 66,36 + 1 % × (3.000 − 1.990,84) = 76,4516; polovica = 38,2258 →
**38,23**. Traži `MidpointRounding.AwayFromZero` — bankarsko bi dalo 38,22.

**Usput ispravljeno:** klijentska tablica pristojbi držala je 26 razreda iz stare HRK
uredbe s maksimumom **3.981,68 €** umjesto 663,61 €. Živi pregled i odgovor servera
prikazivali su različite iznose na istom ekranu (pri VPS-u 100.000 €: 929,06 € naspram
663,61 €). Napomena ispod troškovnika više ne citira ukinutu NN 95/2015.

---

## 6. Tbr. 40 — zastupanje više osoba (`556af05`)

> *"Kad odvjetnik u istom postupku brani ili zastupa više osoba, pripada mu pravo na
> povišenje tarifnih stavaka za **drugu i svaku sljedeću** osobu za 10 % — uz uvjet da
> to povišenje može iznositi **najviše 50 %**."*

**Odgovor na pitanje iz zadatka:** prva osoba **ne** ulazi u postotak. Osnova je (n − 1).
Uz to Tarifa propisuje kapu od 50 %, koju zadatak ne spominje.

| | |
|---|---|
| Popravak | `PovisenjeViseOsobaPostotak` — [TroskovnikService.cs:566](Backend/Services/TroskovnikService.cs#L566); primjena [:466](Backend/Services/TroskovnikService.cs#L466) |
| Klijent | `povisenjeViseOsobaPostotak` — [lib/tarifa.ts:297](frontend/lib/tarifa.ts#L297) |
| Testovi | `TarifaTbr40Tests` (13) · `__tests__/tarifa-vise-osoba.test.ts` |

| osoba | povišenje |
|---|---|
| 1 | 0 % |
| 2 | 10 % |
| 6 | 50 % |
| 8, 20 | 50 % (kapa) |

Stara stavka `zaj_vise_osoba` s `FiksniBody = 0` uklonjena je iz kataloga — Tarifa ne
govori o zasebnoj nagradi nego o povišenju *tarifnih stavaka*, dakle cijele osnovice.
Zamjenjuje je polje zahtjeva `broj_zastupanih_osoba`, a povišenje se u troškovniku
prikazuje kao vlastiti redak.

---

## 7. Neprocjenjivi upravni spor (`a87fde5`)

> Tbr. 27. toč. 1: *"— u procjenjivim predmetima (…) nagrada iz Tbr. 7. toč. 1.,
> — u neprocjenjivim predmetima (…) nagrada od 500 bodova."*

`usp_rasprava` je imala samo procjenjivu varijantu, pa je u neprocjenjivom predmetu
(VPS = 0) padala na dno tablice iz Tbr. 7. toč. 1 → **25 bodova**.

**Isti kvar imale su i toč. 3 i toč. 4**, jer i one upućuju na toč. 1. Sve tri imaju
neprocjenjivu varijantu:

| ID | bodovi | izvod |
|---|---|---|
| [`usp_rasprava_neproc`](Backend/Services/TroskovnikService.cs#L237) | **500** | toč. 2 → toč. 1 al. 2 |
| [`usp_zalba_neproc`](Backend/Services/TroskovnikService.cs#L242) | **625** | toč. 3: 500 uvećano za 25 % |
| [`usp_izvrsenje_neproc`](Backend/Services/TroskovnikService.cs#L247) | **250** | toč. 4: 50 % od 500 |

Postojeći ID-evi zadržani su za procjenjive varijante, pa spremljeni troškovnici ne
gube stavke.

Testovi: `TarifaUpravniSporTests` (13).

---

## 8. Dugotrajni zatvor: +50 %, ne +25 % (`1e730ae`)

> Tbr. 4. toč. 2 al. 3: *"(…) s povišenjem od 25 %, a na raspravi pred sudom drugog i
> trećeg stupnja za kaznena djela za koja je predviđena kazna dugotrajnog zatvora s
> povišenjem od 50 %."*

| | |
|---|---|
| Popravak | `KazneniPovecanjeDugotrajni` — [TroskovnikModels.cs:527](Backend/Models/TroskovnikModels.cs#L527); stavka [TroskovnikService.cs:92](Backend/Services/TroskovnikService.cs#L92); primjena u `IzracunajKazneneBodove` [:637](Backend/Services/TroskovnikService.cs#L637) |
| Testovi | `TarifaDugotrajniZatvorTests` (9) · `__tests__/tarifa-dugotrajni-zatvor.test.ts` |
| Prije | dugotrajni zatvor → 625 bodova (1.250 €) |
| Poslije | **750 bodova** (1.500 €) |

Ostale kazne zadržavaju +25 %. Žalba po Tbr. 5. toč. 1 zadržava svojih +50 % i ne
zbraja se s ovim. Zatečeni `AUDIT NOTE` koji je slučaj označavao kao "rubni" uklonjen je.

---

## 9. Zaokruživanje (`9f1aa25`)

Tarifa zaokruživanje bodova nigdje ne propisuje. Kod je ipak svaki korak lanca provlačio
kroz `Math.Ceiling`, pa se greška zbrajala.

| | |
|---|---|
| Popravak | `IzracunajBodove` [:606](Backend/Services/TroskovnikService.cs#L606), `IzracunajKazneneBodove` [:637](Backend/Services/TroskovnikService.cs#L637), `ZaokruziNovac` [:547](Backend/Services/TroskovnikService.cs#L547) |
| Model | `BodovaPoJedinici` i `UkupnoBodova` → `decimal` — [TroskovnikModels.cs:432](Backend/Models/TroskovnikModels.cs#L432) |
| Migracija | [supabase/migrations/2026_07_29_troskovnik_decimalni_bodovi.sql](supabase/migrations/2026_07_29_troskovnik_decimalni_bodovi.sql) — `ukupno_bodova INTEGER → NUMERIC(14,3)` |
| Testovi | `TarifaZaokruzivanjeTests` (11) · `__tests__/tarifa-zaokruzivanje.test.ts` |

**Traženi test — VPS 1.000 €, `izv_pravni_lijek`:**

```
prije:  75 × 0,5 = 37,5 → 38;  38 × 1,25 = 47,5 → 48      96,00 €
sada:   75 × 0,5 × 1,25 = 46,875                          93,75 €
```

Vrijednost je **46,875**, a standardno zaokružena **47** — nikako 48, kako je zadatak i
tražio. Po tvojoj odluci bodovi ostaju decimalni do kraja, pa se u sučelju i u ispisu
prikazuje `46,875`; `Math.Round(46,875) = 47` zaključano je zasebnim testom
`StandardnoZaokruzenoJe47`.

Novac se zaokružuje jednom, na kraju, `AwayFromZero`: 23,4375 → 23,44.

**Iznimka:** `Tbr71Bodovi` [:18](Backend/Services/TroskovnikService.cs#L18) zadržava
`Math.Ceiling`. *"1 boda za svakih **započetih** 133,00 eura"* je zaokruživanje koje
Tarifa sama propisuje. Zato sve granice razreda ostaju nepromijenjene (točka 14).

---

## 10. Minimum 200 bodova samo za neprocjenjive (`0d72ee8`)

> Tbr. 34. toč. 2, odlomak 2: *"(…) u **neprocjenjivim** predmetima odvjetniku pripada
> nagrada ne manja od 200 bodova."*

Minimum je stajao na stavci bez obzira na vrstu predmeta.

| | |
|---|---|
| Popravak | [`raz_pisani_savjet`](Backend/Services/TroskovnikService.cs#L287) izgubio `MinBodova`; dodan [`raz_pisani_savjet_neproc`](Backend/Services/TroskovnikService.cs#L291) |
| Testovi | `TarifaPisaniSavjetTests` (9) |
| Prije | VPS 500 € → 200 bodova (400 €) |
| Poslije | **50 bodova** (100 €) |

---

## 11. Izvori za dvije potvrđene stope (`6bcdd63`)

Oba su broja zadržana i dobila izvor u kodu. Uklonjena su dva zatečena `AUDIT NOTE`
komentara, uključujući onaj koji je tvrdio da izvor za 0,70 nije poznat.

| stopa | mjesto | izvor |
|---|---|---|
| **50 %** — branitelj po službenoj dužnosti / na teret proračuna | [TroskovnikModels.cs:35](Backend/Models/TroskovnikModels.cs#L35) | Pravilnik NN 22/2022 br. 271 (Tbr. 4. toč. 7 upućuje na njega) |
| **70 %** od Tbr. 4. toč. 8 — opunomoćenik djeteta žrtve | [TroskovnikModels.cs:53](Backend/Models/TroskovnikModels.cs#L53) | isti Pravilnik |

**Provjereno kako je zadatak tražio:** kod doista računa **35 %**, ne 70 %. Tbr. 4. toč. 8
je već 50 % nagrade iz Tbr. 4, pa je 0,50 × 0,70 = 0,35. Pri dugotrajnom zatvoru
(500 bodova) to je **175 bodova**, a ne 350.

Testovi: `TarifaUmanjenjaTests` (11) · `__tests__/tarifa-umanjenja.test.ts`.

---

## 12. `pravniIzvor` na svakoj stavci (`d062f2b`)

| | |
|---|---|
| Model | [TroskovnikModels.cs:504](Backend/Models/TroskovnikModels.cs#L504) (katalog), [:425](Backend/Models/TroskovnikModels.cs#L425) (redak troškovnika) |
| Prikaz | kartica stavke, redak živog troškovnika, tablica rezultata i CSV — [app/troskovnik/page.tsx](frontend/app/troskovnik/page.tsx); HTML ispis — [TroskovnikDocumentService.cs:124](Backend/Services/TroskovnikDocumentService.cs#L124) |
| Testovi | `TarifaPravniIzvorTests` (10) |

Za **68 stavaka** izvor je upisan izričito, jer se nagrada izvodi iz drugog tarifnog
broja pa sam broj stavke ne govori dovoljno:

```
kaz_prekrsaj_oduzimanje   Tbr. 4. toč. 1 al. 8 — nagrada po Tbr. 7. toč. 1 prema
                          vrijednosti oduzetog predmeta
par_odgovor_tuzba         Tbr. 8. toč. 1 — nagrada kao za tužbu po Tbr. 7. toč. 1., 4., 7. i 8.
izv_pravni_lijek          Tbr. 18. toč. 1 — nagrada iz Tbr. 16 s povišenjem od 25%
usp_zalba_neproc          Tbr. 27. toč. 3 — 500 bodova iz toč. 1 al. 2 uvećano za 25% = 625
```

Ostale stavke izvor sastavljaju iz naziva Tarife i vlastitog tarifnog broja.

**Usput:** HTML ispis dobio je retke koji su mu nedostajali — povišenje po Tbr. 40
[:133](Backend/Services/TroskovnikDocumentService.cs#L133), sudsku pristojbu s
obrazloženjem, i sveukupni trošak [:168](Backend/Services/TroskovnikDocumentService.cs#L168).

---

## 13. Ništa se ne prešućuje (`b177523`)

Izračun je nepoznatu stavku i količinu ≤ 0 preskakao naredbom `continue` — odvjetnik je
dobio troškovnik bez stavke koju je odabrao i bez ijedne poruke. Kaznena stavka bez
odabrane kazne davala je 0 bodova, dakle 0,00 €, što izgleda kao valjan rezultat.

| | |
|---|---|
| Popravak | [TroskovnikValidationException.cs](Backend/Services/TroskovnikValidationException.cs); provjere na [TroskovnikService.cs:363](Backend/Services/TroskovnikService.cs#L363), [:367](Backend/Services/TroskovnikService.cs#L367), [:389](Backend/Services/TroskovnikService.cs#L389), [:394](Backend/Services/TroskovnikService.cs#L394), [:413](Backend/Services/TroskovnikService.cs#L413), [:438](Backend/Services/TroskovnikService.cs#L438); → HTTP 400 na [TroskovnikController.cs:47](Backend/Controllers/TroskovnikController.cs#L47) i [:103](Backend/Controllers/TroskovnikController.cs#L103) |
| Klijent | `provjeriZahtjev` — [lib/tarifa.ts:235](frontend/lib/tarifa.ts#L235) |
| Testovi | `TarifaValidacijaTests` (8) · `__tests__/tarifa-validacija.test.ts` |

Odbija se: nepoznat `item_id`, količina ≤ 0, kaznena stavka bez zapriječene kazne,
nepoznat ključ kazne (npr. stari `do_1`), uključeno oduzimanje bez unesene vrijednosti,
te bilo koja stavka čiji izračun daje 0 bodova.

Prekršaj bez uključenog oduzimanja i dalje prolazi — 100 bodova iz Tbr. 4. toč. 1 al. 7
valjan je iznos.

Sučelje zrcali iste provjere: gumbi "Generiraj" i "Spremi" onemogućeni su dok
nedostatak stoji, razlog piše iznad njih, a redak s 0 bodova se ne prikazuje.

---

## 14. Regresija (`cbe1b4a`)

`Tbr71Bodovi` nije diran nijednim potezom.

| | |
|---|---|
| Testovi | [Backend.Tests/TarifaRazrediTests.cs](Backend.Tests/TarifaRazrediTests.cs) (40) · [frontend/__tests__/tarifa.test.ts](frontend/__tests__/tarifa.test.ts) (37) |

Obje vrijednosti na svakoj granici razreda:

```
0,00 → 25          332,00 → 25        332,01 → 50
664,00 → 50        664,01 → 75        1.327,00 → 75
1.327,01 → 100     13.272,00 → 100    13.272,01 → 250
33.181,00 → 250    33.181,01 → 500    66.361,00 → 500
66.361,01 → 501    66.494,00 → 501    66.494,01 → 502
663.614,00 → 4991  663.614,01 → 4992  663.879,01 → 4993
1.327.228,00 → 7496  1.327.228,01 → 7497  1.327.892,01 → 7498
```

plus negativan VPS → 25, kapa na 10.000 bodova i provjera monotonosti.

**Standardna procjenjiva parnica**, VPS 50.000 € (tužba + dva ročišta + žalba):

| | |
|---|---|
| tužba (Tbr. 7.1) | 500 bodova |
| dva ročišta (Tbr. 9.1) | 1.000 bodova |
| žalba (Tbr. 10.1, +25 %) | 625 bodova |
| **ukupno** | **2.125 bodova = 4.250,00 €** |
| PDV 25 % | 1.062,50 € |
| sudska pristojba (Tar. br. 1 st. 1) | 546,45 € |
| **sveukupno** | **5.858,95 €** |

Ista parnica uz zastupanje tri osobe (Tbr. 40, +20 %) → 2.550 bodova = 5.100,00 €.

---

## Što nije dirano, a vrijedi znati

1. **Pristojba izvan parničnog i ovršnog postupka.** Po tvojoj odluci pristojba je
   svedena na nulu samo u kaznenom. Uredba NN 37/23 u Poglavlju I. pokriva samo parnični
   i ovršni postupak, pa upravni spor, Ustavni sud i kategorija "Razno" i dalje dobivaju
   punu parničnu pristojbu — što po Uredbi nije točno, ali je izvan opsega ovog zadatka.

2. **`__tests__/delete.test.ts` — 5 padova.** Zatečeno stanje, provjereno na čistom
   stablu prije prve izmjene. Nema veze s tarifom.

3. **Ostali kontroleri iz commita `c04aeeb`.** Commit 0 popravio je DI za sve kontrolere,
   ali njihovu poslovnu logiku nisam dirao ni provjeravao.

## 15. Dopune kaznenog dijela (`244b47a`, `064bc42`, `321871d`)

Drugi krug, nakon što je odvjetnica pregledala aplikaciju i prijavila da u kaznenom
dijelu nedostaju cijeli tarifni brojevi.

### Što je provjereno u pročišćenom tekstu

Pročitao sam Tbr. 1, 2, 3 i 4 u cijelosti prije ijedne izmjene. **Prijava je bila
djelomično netočna:**

| prijavljeno | stvarno stanje |
|---|---|
| Tbr. 1 toč. 2 i 3 nedostaju | **postoje** — `kaz_podnesak_obrana`, `kaz_zahtjev_ostecenik`, obje vezane na Tbr. 4. toč. 1 |
| Tbr. 2 potpuno nedostaje | **postoji u cijelosti** — sva četiri točke, iznosi 100 / 50 / po Tbr. 4.1 / 50 poklapaju se s PDF-om |
| Tbr. 3 potpuno nedostaje | toč. 1, 3 i 4 **postoje**; nedostajala je samo toč. 2 |
| "sve stavke iz Tbr. 1 vezane su na Tbr. 4. toč. 1" | **netočno** — toč. 4 je fiksnih 75 bodova, toč. 5 fiksnih 50; katalog je to imao ispravno |

### Sedam odredaba koje su stvarno nedostajale

| Tbr. | ID | nagrada | ručni izračun iz testa |
|---|---|---|---|
| 3. toč. 2 | `kaz_istraga_izvan_sjedista` | nagrada iz toč. 1 **+ naknada po Tbr. 49** | do 5 god. → 200 bod. = 400,00 € |
| 4. toč. 2 al. 4 | `kaz_sjednica_izvanredni` | kao obrana pred sudom drugog stupnja | do 5 god.: 200 × 1,25 = **250 bod.** = 500,00 € |
| 4. toč. 3 | `kaz_maloljetnik` | kao obrana po toč. 1 | do 10 god. → 300 bod. = 600,00 € |
| 4. toč. 4 | `kaz_obustava_odgoda` | 50 % nagrade iz toč. 1 | do 10 god.: 50 % od 300 = **150 bod.** = 300,00 € |
| 4. toč. 5 | `kaz_pritvor_dodatni_sat` | 50 bodova po svakom sljedećem započetom satu | 2 sata × 50 = **100 bod.** = 200,00 € |
| 4. toč. 6 | `kaz_ocevid_izvan_zgrade` | nagrada iz toč. 1 **+ Tbr. 49** | dugotrajni → 500 bod. = 1.000,00 € |
| 4. toč. 8 | `kaz_zastupanje_ostecenika` | 50 % nagrade iz Tbr. 4 | preko 10 god.: 50 % od 400 = **200 bod.** = 400,00 € |

Testovi: `TarifaTbr3Tests` (12) · `TarifaTbr4DopuneTests` (33), uključujući dvije
theory koje provjeravaju da svaka točka Tbr. 4 i svaka alineja toč. 1 ima stavku.

**Tbr. 49 nije bodovna nagrada** nego naknada stvarnih putnih troškova. Stavke koje
ga spominju to kažu u opisu i u `pravniIzvor`; iznos se obračunava zasebno i namjerno
nije sveden na bodove.

### Jedan izmišljen broj uklonjen

`kaz_privatna_tuzba` nosio je `MinBodova = 100`. Tbr. 1. toč. 1 glasi samo *"odvjetniku
pripada nagrada po Tbr. 4. toč. 1."* — nikakav minimum se ne spominje. Taj je clamp
poništavao umanjenje od 50 % za branitelja po službenoj dužnosti: 100 × 0,50 = 50, pa
natrag na 100.

Testovi: `TarifaTbr1Tests` (8). Privatna tužba po službenoj dužnosti, novčana kazna →
**50 bodova**, prije 100.

### Stjecaj — odstupanje od zadatka

Zadatak je tražio da *"korisnik ručno unosi broj radnji"*. **Tarifa to ne poznaje.**
Tbr. 4. toč. 1 al. 6 glasi:

> *"za kaznena djela u stjecaju i za produljena kaznena djela nagrada se određuje
> sukladno rasponima iz alineja od 1. do 5. ovog tarifnog broja, pri čemu se kao
> predviđena kazna smatra najteža kazna koja se u postupku može izreći (…)"*

Alineja ne uvodi ni množitelj ni broj radnji — određuje samo **koji se razred uzima**.
Nagrada ostaje jedna. Uvođenje "broja radnji" značilo bi izmišljanje broja, što je
zadatak izričito zabranio.

Implementirano po tekstu: zastavica ne mijenja izračun, ali sada putuje u odgovor s
obrazloženjem (`napomena_stjecaj`) koje se ispisuje na troškovniku — pa se vidi zašto
je odabran razred koji ne odgovara pojedinačnom djelu. Sučelje je uputu već imalo.

Testovi: `TarifaStjecajTests` (7), uključujući tvrdnju da stjecaj **ne množi** nagradu.

### PDV — potvrđeno, već je postojalo

`is_pdv_ukljucen` postoji, zadana vrijednost je **isključen** (odvjetnik izvan sustava
PDV-a), uz stariji `has_vat` kao rezervu. Zaključano s `TarifaPdvTests` (7):
nagrada 1.000,00 € → s PDV-om 1.250,00 €, bez PDV-a 1.000,00 €; sveukupno izvan sustava
PDV-a 1.546,45 € (nagrada + pristojba).

### Tablica razreda

Sučelje i ispis prikazivali su `0 – 332,00` pa `332,00 – 664,00` — 332,00 je izgledalo
kao da pripada u oba razreda. Progresivni razredi bili su svedeni na `500+` i `4.991+`.

Sada stoji točno kako Tarifa piše:

```
0,00 – 332,00                    25
332,01 – 664,00                  50
664,01 – 1.327,00                75
1.327,01 – 13.272,00             100
13.272,01 – 33.181,00            250
33.181,01 – 66.361,00            500
66.361,01 – 663.614,00           500 + 1 bod / započetih 133,00 €
663.614,01 – 1.327.228,00        4.991 + 1 bod / započetih 265,00 €
iznad 1.327.228,00               7.496 + 1 bod / započetih 664,00 €, najviše 10.000
```

Testovi: `TarifaTablicaPrikazTests` (7) · `__tests__/tarifa-tablica-prikaz.test.ts` —
svaka donja granica je točno cent iznad prethodne gornje, a prikazane granice daju
bodove koje motor doista računa.

---

## Što još nije pokriveno

Sustavna usporedba kataloga s Tbr. 1–54 iz pročišćenog teksta.

### Kazneni i prekršajni postupak — pokriven u cijelosti

Tbr. 1 do Tbr. 6 sada nemaju neimplementiranih odredaba. Jedina iznimka je
**Tbr. 4. toč. 7** (branitelj po službenoj dužnosti), koja nije stavka nego zastavica
zahtjeva i upućuje na Pravilnik NN 22/2022 br. 271 — vidi odjeljak 11.

### Parnični postupak — nedostaju pojedine točke

| Tbr. | odredba |
|---|---|
| 7. toč. 4 | autorsko pravo i industrijsko vlasništvo (Tbr. 7.1, ne manje od 100 bodova); renta, zakup i najam — kao VPS uzima se **petogodišnji iznos** |
| 7. toč. 6 | mjenični, čekovni i platni nalozi na temelju drugih vrijednosnih papira |
| 7. toč. 8 | zastupanje u postupcima mirnog rješavanja sporova |
| 7. toč. 9 | VPS manji od 100,00 € → jednokratno **50 bodova** za cijeli prvostupanjski postupak |
| 7. toč. 10 | obiteljski sporovi s međunarodnim elementom (Haška konvencija i dr.) → **250 bodova** |
| 8. toč. 2 | prijedlog za povrat u prijašnje stanje → 50 % iz toč. 1, najviše 100 bodova |
| 9. toč. 4 | ročište pred arbitražom |
| 9. toč. 6 | rasprava pred sudom drugog stupnja |
| 9. toč. 8 | zastupanje u postupcima po Haškoj konvenciji |
| 10. toč. 2 | redovni pravni lijekovi u parnicama iz Tbr. 7. toč. 2 i 3 |
| 10. toč. 3 | redovni pravni lijekovi u parnicama iz Tbr. 7. toč. 9 → **100 bodova** |

### Tarifni brojevi kojih uopće nema u katalogu

| Tbr. | sadržaj | napomena |
|---|---|---|
| 23 | zastupanje u ispitnom postupku, osiguranju dokaza, na usmenoj raspravi i očevidu u prvostupanjskom upravnom postupku | nagrada iz Tbr. 22 + naknada po Tbr. 39 |
| 25 | žalba u postupcima javne nabave | **UKINUT** presudom Visokog upravnog suda RH Usoz-55/2024-21 od 18. srpnja 2025. — ne smije se implementirati |
| 26 | izvlaštenje | |
| 29 | tužba na poništaj upisa u registar | Tbr. 7. toč. 1, ne manje od 100 bodova; za druge registre po Tbr. 27 |
| 38 | nagrada za utrošeni sat kad nije drukčije određeno | **25 bodova** |
| ~~41~~ | povišenje do 100 % za posebnu stručnost, sniženje do 50 % | **implementirano** u `50a7e4b`, sučelje u `f0ba06d` |
| 42 | paušalni ugovor o pružanju pravne pomoći | |
| 43 | ugovorena nagrada | ne obvezuje sud pri dosuđivanju troška |
| 44 | pravna pomoć u inozemstvu | po tarifi zemlje zastupanja |
| ~~45~~ | neprocjenjivi predmeti koje Tarifa ne predviđa | **implementirano** u `50a7e4b`: 100 / 200 / 500 po razini suda, sučelje u `f0ba06d` |
| 46 | PDV | implementirano kao zastavica, nije zasebna stavka |
| 47 | pisani obračun nagrade na zahtjev stranke | |
| 48 | stvarni izdaci (poštanski, telekomunikacijski i dr.) | |
| 49, 50, 51 | putni troškovi, prijevoz, dnevnica i hotelski smještaj | Tbr. 49 se spominje u opisima stavaka Tbr. 3.2, 3.4 i 4.6, ali se ne obračunava |
| 52 | mjerodavna tarifa i vrijednost boda u trenutku dospijeća | |
| 53 | donositelj Tarife | nema računskog učinka |
| 54 | vrijednost boda | implementirano kao konstanta `BodValue = 2,00 €` |

> **Zastarjelo od `50a7e4b` i `f0ba06d`.** Ovaj je popis nastao prije parničnog kruga.
> **Tbr. 41 i Tbr. 45 su u međuvremenu implementirani**, kao i cijeli parnični dio
> (Tbr. 7 toč. 4, 6, 8, 9, 10; Tbr. 8 toč. 2 i 5; Tbr. 9 toč. 4, 6, 8; Tbr. 10 toč. 2,
> 3, 5 st. 2 i 6 st. 2) — vidi dopunu na kraju dokumenta. Ostatak tablice i dalje vrijedi:
> Tbr. 23, 26, 29, 38, 42, 43, 44, 47, 48, 49–51 i 52 nisu pokriveni, a Tbr. 25 je ukinut
> i namjerno se ne implementira.

### Jedno tumačenje, ne doslovan citat

Tbr. 4. toč. 2 al. 4 kaže *"nagrada kao za obranu pred sudom drugog stupnja"*, ne
navodeći postotak. Uzeo sam da je taj pojam definiran u al. 3 (toč. 1 + 25 %, kod
dugotrajnog zatvora + 50 %), jer je al. 3 jedina alineja koja nagradu pripisuje
obrani pred drugim stupnjem — dok al. 2 sjednicu vijeća drugog stupnja izjednačuje s
prvim stupnjem. Drugo bi čitanje dalo nagradu bez povišenja. Zabilježeno je u
komentaru uz stavku; ako odvjetnica smatra suprotno, izmjena je jedan broj.

---

## Kako provjeriti rukom

```bash
dotnet test Backend.Tests/Backend.Tests.csproj
```

```bash
cd frontend && npm test
```

U pokrenutoj aplikaciji, na `/troskovnik`:

1. VPS 3.000 €, "Prijedlog za ovrhu" → pristojba **38,23 €**
2. samo kaznene stavke, VPS 50.000 € → polje VPS-a nestaje, pristojba **0,00 €** uz
   obrazloženje
3. "Tužba – smetanje posjeda", količina 6 → **350 bodova**, ne 1.500
4. kaznena stavka bez odabrane zapriječene kazne → gumbi onemogućeni, poruka iznad njih
5. VPS 1.000 €, "Pravni lijek u izvanparnici" → **46,875 bod. = 93,75 €**
6. broj zastupanih osoba 8 → povišenje **+50 %**, ne +70 %
7. svaka stavka i svaki redak troškovnika prikazuju pravni izvor
8. tablica bodova u desnom stupcu → `0,00 – 332,00` pa `332,01 – 664,00`, bez
   preklapanja, a progresivni razredi pišu formulu iz Tarife
9. "Sjednica povodom izvanrednih pravnih lijekova", kazna do 5 godina → **250 bodova**
10. "Zastupanje oštećenika", kazna preko 10 godina → **200 bodova**
11. uključi "Stjecaj / produljeno kazneno djelo" → iznos se ne mijenja, ali na
    troškovniku se pojavi obrazloženje odabranog razreda

---

# Dopuna — parnični dio zatvoren (Tbr. 7. toč. 4, 6, 9, 10; Tbr. 8, 9, 10; Tbr. 41, 45)

Nastavak rada na parničnom dijelu. Testovi: **1487 prošlo, 0 palo** (1433 zatečeno + 54 nova).

## Korijenski uzrok: osnovica „Tbr. 7. toč. 1., 4., 7. i 8."

Tarifa na **devet mjesta** upućuje na taj skup točaka — Tbr. 8. toč. 1, 3, 4;
Tbr. 9. toč. 1, 2, 3, 5, 6; Tbr. 10. To **nisu četiri različita iznosa** nego
**četiri varijante iste osnovice**; koja se primjenjuje ovisi o vrsti postupka.

Kod je osnovicu fiksirao na `Tbr71Bodovi(VrijednostSpora)` — uvijek toč. 1. Posljedica:
Tbr. 8. toč. 1 (dodan commitom `fff650f`) davao je ispravan iznos samo za običnu parnicu,
a Tbr. 8. toč. 3 i 4 bili su krivi u arbitraži i autorskopravnim sporovima.

Uvedena je `TroskovnikService.IzracunajOsnovicu(request)`:

| `osnovica_tbr7` | Izračun | Odredba |
|---|---|---|
| `toc1` *(zadano)* | `Tbr71Bodovi(VPS)` | Tbr. 7. toč. 1 |
| `toc4_autorsko` | `max(Tbr71Bodovi(VPS), 100)` | Tbr. 7. toč. 4 al. 1 |
| `toc4_renta` | `Tbr71Bodovi(petogodišnji iznos)` | Tbr. 7. toč. 4 al. 2 |
| `toc7_domaca_arbitraza` | `Tbr71Bodovi(VPS)` | Tbr. 7. toč. 7 st. 1 |
| `toc7_medunarodna_arbitraza` | `Tbr71Bodovi(VPS) × 2` | Tbr. 7. toč. 7 st. 2 |
| `toc8_mirenje` | `Tbr71Bodovi(VPS)` | Tbr. 7. toč. 8 |
| `tbr45_neprocjenjiv` | 100 / 200 / 500 po razini suda | Tbr. 45. toč. 1–3 |

**Provjera Tbr. 8. toč. 1 na sve četiri upućene točke** (VPS 20.000 € → toč. 1 daje 250 bod.):

| Slučaj | Osnovica | Tbr. 8.1 |
|---|---|---|
| toč. 1 | 250 | 250 bod. |
| toč. 4, VPS 20.000 (prag ne djeluje) | max(250, 100) = 250 | 250 bod. |
| **toč. 4, VPS 1.000 (prag djeluje)** | **max(75, 100) = 100** | **100 bod.** |
| toč. 7 domaća | 250 | 250 bod. |
| **toč. 7 međunarodna** | **250 × 2 = 500** | **500 bod.** |
| toč. 8 mirenje | 250 | 250 bod. |

## Novi tarifni brojevi

| Tbr. | Stavka | Iznos |
|---|---|---|
| 7. toč. 6 | mjenični / čekovni nalozi i drugi vrijednosni papiri | puna nagrada iz toč. 1 (bez stropa iz toč. 5) |
| 7. toč. 9 | parnica vrijednosti < 100,00 € | jednokratno 50 bod. za cijeli prvostupanjski postupak |
| 7. toč. 10 | zahtjev po Haškoj konvenciji / Uredbama Vijeća | 250 bod. |
| 8. toč. 2 | povrat u prijašnje stanje | 50 % iz Tbr. 8.1, max 100 |
| 8. toč. 5 | podnesak u haškom postupku | 250 bod. / ostali 125 bod. |
| 9. toč. 4 | ročište pred arbitražom | puna / 50 % nagrade iz Tbr. 7. toč. 7 |
| 9. toč. 6 | rasprava pred sudom drugog stupnja | puna nagrada iz toč. 1 |
| 9. toč. 8 | zastupanje u haškom postupku | 250 bod. |
| 10. toč. 2 | pravni lijek u parnicama iz Tbr. 7.2 i 7.3 | jednokratno 100 bod. |
| 10. toč. 3 | pravni lijek u parnicama iz Tbr. 7.9 | jednokratno 30 bod. |
| 10. toč. 5 st. 2 | prijedlog za reviziju u tim predmetima | 150 odn. 100 bod. |
| 10. toč. 6 st. 2 | odgovor na žalbu u tim predmetima | 30 bod. |

Parnični dio (Tbr. 7.–10.) time je pokriven u cijelosti: **28 / 28 točaka**.

## Tbr. 45 — neprocjenjivi predmeti

Prije: neprocjenjiv predmet (VPS = 0) padao je na `Tbr71Bodovi(0)` = **25 bodova**.
Pred Vrhovnim sudom Tarifa propisuje **500** — greška do dvadeseterostruke.

Sada `razina_suda`: općinski 100 / županijski i trgovački 200 / Vrhovni 500.
Bez odabrane razine izračun baca `TroskovnikValidationException` — ne postoji razumna
zadana vrijednost kad je raspon 1:5.

### NEJASNO U TARIFI

Tbr. 45 daje iznos „za svaku poduzetu radnju", ali **ne kaže** primjenjuju li se na njega
postoci iz Tbr. 8 (50 % / 25 %) i Tbr. 9. Oba čitanja su obranjiva, pa korisnik bira
(`tbr45_nacin`), a sustav ne pretpostavlja:

| Način | Ponašanje | Primjer: Tbr. 8.3, općinski sud |
|---|---|---|
| `kao_osnovica` | 100/200/500 je osnovica, postoci se primjenjuju | 50 bodova |
| `fiksno_po_radnji` | svaka radnja = puni iznos, bez postotaka i stropova | 100 bodova |

Odabir je obavezan; bez njega izračun baca grešku.

## Tbr. 41 — povisivanje i snižavanje

- Toč. 1 — povišenje za posebnu stručnost, **0–100 %**, zadano **0**
- Toč. 2 — sniženje, **0–50 %**, zadano **0**

Djeluje na zbroj stavaka, kao i Tbr. 40, i **zbraja se** s njim:
`250 × (1 + 0,20 + 1,00 − 0,50) = 425 bodova`. Sudska pristojba se ne uvećava —
nije odvjetnička nagrada. Bez korisnikova zahvata iznos je nepromijenjen.

## Tbr. 7. toč. 4 al. 2 — petogodišnji iznos

„Kao vrijednost spora uzima se petogodišnji iznos rente, zakupa, najma i drugih sličnih
davanja." Traži se izričit unos iznosa **i** razdoblja (`mjesecno` × 60 / `godisnje` × 5).
Bez razdoblja se ne preračunava ništa — koristi se uneseni VPS.

Primjer: najam 400 €/mj × 60 = 24.000 € → **250 bodova**. Bez preračuna bio bi 400 € → 50.

## Tbr. 25 — i dalje neimplementiran

Ukinut presudom Visokog upravnog suda RH **Usoz-55/2024-21 od 18. srpnja 2025.**
Stoji u pročišćenom tekstu, ali nije na snazi. Test `Tbr25_JavnaNabava_NeSmijePostojatiUKatalogu`
pada ako ga netko doda.

## Otvoreno

1. **Tbr. 10 upućuje na uži skup** — „Tbr. 7. toč. 1., 4. i 7.", bez toč. 8, za razliku od
   Tbr. 8 i Tbr. 9. Sustav primjenjuje jednu osnovicu na cijeli obračun. Praktično je
   bezopasno (toč. 8 ionako upućuje natrag na toč. 1, pa je iznos isti), ali razlika u
   tekstu je stvarna.
2. **Tbr. 7. toč. 9 ostaje stavka, ne prekidač** — `par_mali_spor` odvjetnik bira sam.
   Sustav sada upozorava kad je VPS ispod 100 € a stavka nije odabrana (vidi niže), ali
   ne mijenja obračun automatski.

---

# Dopuna — sučelje za osnovicu, Tbr. 41 i Tbr. 45 (`f0ba06d`)

Backend je od `50a7e4b` prihvaćao osnovicu, razinu suda i Tbr. 41, ali su ta polja bila
dostupna samo preko API-ja — kroz `/troskovnik` se do njih nije moglo. Ovo zatvara rupu.

## Klijentski motor

`frontend/lib/tarifa.ts` zrcali `IzracunajOsnovicu`, `EfektivniVps` i `Tbr41Udio`.
Ključna promjena potpisa: `izracunajBodove(def, osnovica)` umjesto `izracunajBodove(def,
baseBodovi)` — isto kao na backendu, pa „fiksno po radnji" iz Tbr. 45 radi i u živom
pregledu, gdje se postoci i stropovi iz Tbr. 8./9. ne smiju primijeniti.

| funkcija | zrcali |
|---|---|
| `izracunajOsnovicu` | `TroskovnikService.IzracunajOsnovicu` |
| `efektivniVps` | `TroskovnikService.EfektivniVps` |
| `tbr41Udio` | `TroskovnikService.Tbr41Udio` |
| `provjeriZahtjev` | provjere Tbr. 45 iz `Izracunaj` |

## Sučelje

- **Osnovica** — padajući izbornik sa svih sedam varijanti, uz ispis pravnog izvora
  odabrane varijante ispod polja.
- **Tbr. 7. toč. 4 al. 2** — unos iznosa rente/zakupa/najma i razdoblja (mjesečno × 60,
  godišnje × 5), uz prikaz izračunatog petogodišnjeg iznosa. Bez odabranog razdoblja
  ne preračunava se ništa.
- **Tbr. 45** — razina suda i način obračuna, oboje obavezno. Dok nisu odabrani, iznos
  se ne prikazuje, stavke pišu „— dovrši osnovicu —", a gumbi su onemogućeni s
  obrazloženjem. Raspon je 1:5, pa nema razumne zadane vrijednosti.
- **Tbr. 41** — dva klizača: povišenje 0–100 % (toč. 1) i sniženje 0–50 % (toč. 2), oba
  zadano 0. Prikazuje se kao zaseban redak u živom troškovniku i u tablici rezultata.

## Tbr. 7. toč. 9 — upozorenje

Kad je vrijednost spora ispod 100,00 €, osnovica je toč. 1 i odabrane su parnične stavke
koje se obračunavaju po toč. 1, sučelje javlja da toč. 9 za takve postupke propisuje
jednokratnih 50 bodova za cijeli prvostupanjski postupak.

Upozorenje, ne greška: odredba se veže uz toč. 1, a odvjetnik odlučuje primjenjuje li se.

## Testovi

`frontend/__tests__/tarifa-osnovica.test.ts` — 34 provjere, isti presudni slučajevi kao
na backendu:

| slučaj | očekivano |
|---|---|
| VPS 1.000 € uz toč. 4 al. 1 | **100** bodova (tablica bi dala 75) |
| međunarodna arbitraža pri VPS-u 20.000 € | **500** bodova (toč. 1 daje 250) |
| renta 500 €/mj. | petogodišnji iznos **30.000 €** |
| Tbr. 45, Vrhovni sud, „kao osnovica" | obrazloženi podnesak → **100** (50 % od 500, strop) |
| Tbr. 45, Vrhovni sud, „fiksno po radnji" | isti podnesak → **500** |
| Tbr. 41 povišenje 250 % | ograničeno na **+100 %** |
| Tbr. 41 sniženje 90 % | ograničeno na **−50 %** |

`tarifa-zaokruzivanje.test.ts` prilagođen novom potpisu; test iz audita i dalje daje
46,875 bodova i 93,75 €.

```
dotnet test Backend.Tests/Backend.Tests.csproj   →  1487 prolaza, 0 padova
cd frontend && npx vitest run __tests__/tarifa   →   164 prolaza, 0 padova
```

Zatečenih 5 padova u `__tests__/delete.test.ts` nema veze s tarifom — provjereno na
čistom stablu prije prve izmjene.

## Nove provjere

```bash
dotnet test Backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName~TarifaOsnovica|FullyQualifiedName~TarifaTbr45|FullyQualifiedName~TarifaTbr41"
```

- `TarifaOsnovicaTests` — Tbr. 8.1 na sve četiri točke, petogodišnji iznos, apsolutni iznosi, Tbr. 25
- `TarifaTbr45Tests` — tri razine suda × oba čitanja, greška bez odabira
- `TarifaTbr41Tests` — povišenje/sniženje, ograničenja raspona, zbrajanje s Tbr. 40

---

# Dopuna kataloga — Tbr. 23, 28.2, 29, 31.4, 32.3–32.5

Šest commita · nastavak na `196b43c`.

Popis otvorenog iz `PROMPT-NASTAVAK.md` navodio je jedanaest „nepokrivenih" tarifnih
brojeva. Provjera protiv Tarife pokazala je da većina njih **nisu obračunske stavke**, a
da tri rupe koje jesu **nisu bile na popisu**. Ovaj odjeljak bilježi i popravke i ispravke
prethodnih nalaza.

## Ispravci prethodnih nalaza

### Tbr. 38 nije stavka — brief ga je krivo označio „najkorisnijim"

> Tbr. 38: *„Ako pojedinim tarifnim odredbama, prema kojima se određeni poslovi naknađuju
> po utrošenom satu, nije drugačije određeno, nagrada za utrošeni sat iznosi 25 bodova."*

Tbr. 38 je **rezidualno pravilo**, ne obračunska stavka. Provjerena je svaka odredba
Tarife koja posao naknađuje po satu — **svaka propisuje vlastitu satnicu**:

| odredba | satnica |
|---|---|
| Tbr. 2 | 50 bodova |
| Tbr. 4. toč. 1 al. 10 | 100 bodova (prvi sat), 50 (svaki sljedeći) |
| Tbr. 4. toč. 3 | 50 bodova |
| Tbr. 29. toč. 4 | najmanje 100 bodova |
| Tbr. 34. toč. 1 | 50 bodova |
| Tbr. 35 | 50 bodova |
| Tbr. 36 | 25 bodova |
| Tbr. 39 | 25 bodova |

Ne postoji dakle nijedna odredba na koju bi se Tbr. 38 danas primijenio. Stavka
„25 bodova po satu" izmislila bi mu primjenu i omogućila obračun rada koji Tarifa ne
priznaje kao zasebnu radnju.

| | |
|---|---|
| Brana | `TarifaKatalogDopunaTests.Tbr38_NijeStavkaKataloga` |
| Dopuna | `SatneStavke_IzvodeIznosIzVlastiteOdredbe` — svaka satna stavka mora iznos izvoditi iz vlastite odredbe |

Isti obrazac kao postojeća brana za ukinuti Tbr. 25, iako je razlog drukčiji: Tbr. 25 je
ukinut presudom, Tbr. 38 je na snazi ali nije stavka.

### Tbr. 42, 43, 44, 47, 48, 52 također nisu obračunske stavke

Paušalni ugovor (42), ugovorena nagrada (43), inozemna tarifa (44), odnos odvjetnik–stranka
(47), stvarni izdaci općenito (48), vremenska primjena Tarife (52). Nijedna ne daje bodove
za obračun. Tbr. 46 (PDV) je već implementiran.

### Tri rupe koje nisu bile na popisu

Tbr. **28. toč. 2**, Tbr. **31. toč. 4**, Tbr. **32. toč. 3, 4 i 5** — sve tri su
obračunske, s fiksnim iznosima, i sve su nedostajale.

## 1. Tbr. 23 — zastupanje i obrana u upravnom postupku (`353bfff`)

> Toč. 1: *„Za zastupanje u ispitnom postupku, u postupku osiguranja dokaza, na usmenoj
> raspravi te očevidu u prvostupanjskom upravnom postupku odvjetniku pripada nagrada iz
> Tbr. 22. te naknada po Tbr. 39."*
> Toč. 2: *„Za obranu u postupcima radi poreznih, carinskih ili deviznih prekršaja
> odvjetniku pripada nagrada po Tbr. 22., te naknada po Tbr. 39."*

Tbr. 22. toč. 1 ima dvije alineje, pa svaka točka daje dvije stavke.

| stavka | Tbr. | bodovi |
|---|---|---|
| `upr_zastupanje_neproc` | 23.1 | 50 |
| `upr_zastupanje_proc` | 23.1 | Tbr. 7. toč. 1 |
| `upr_obrana_prekrsaj_neproc` | 23.2 | 50 |
| `upr_obrana_prekrsaj_proc` | 23.2 | Tbr. 7. toč. 1 |

„naknada po Tbr. 39" **nije uključena** — Tbr. 39 je već zasebna stavka `zaj_cekanje` i
obračunava se po započetom satu.

Toč. 2 govori o prekršajima, ali Tarifa je smješta u glavu VII. UPRAVNI POSTUPAK i nagradu
veže uz Tbr. 22, ne uz Tbr. 4. Kategorija je obračunski bitna: u kategoriji
„Kazneni postupak" tiho bi ugasila sudsku pristojbu. Test to zaključava.

| | |
|---|---|
| Popravak | `TroskovnikService.cs`, blok VII. UPRAVNI POSTUPAK |
| Testovi | `TarifaTbr23Tests` (15) |

## 2. Tbr. 28. toč. 2 — osnivačke isprave (`4e9e9a1`)

> *„Za sastavljanje isprava (izjava o osnivanju društva, društveni ugovor, statut, te
> ostale isprave) potrebnih za upis osnivanja ili promjena u registru, odvjetniku pripada
> nagrada prema Tbr. 33. toč. 1. i 2."*

Katalog je imao 28.1, 28.3 i 28.4, ali ne i 28.2.

| stavka | izvor | iznos |
|---|---|---|
| `reg_isprave_proc` | Tbr. 33. toč. 1 | Tbr. 7. toč. 1 + 25 % → VPS 50.000 € = 625 bod. = 1.250,00 € |
| `reg_isprave_neproc` | Tbr. 33. toč. 2 al. 3 | 200 bod. = 400,00 € |

Test uspoređuje nove stavke s postojećima po Tbr. 33 (`raz_ugovor`, `raz_ugovor_neproc`) —
moraju dati isto, jer Tbr. 28. toč. 2 na njih upućuje.

| | |
|---|---|
| Testovi | `TarifaTbr282Tests` (7) |

## 3. Tbr. 29 — poništaj upisa u registar (`c9503aa`)

Cijeli tarifni broj je nedostajao. Toč. 1 daje **tri osnovice**, toč. 2 i 3 **pet radnji**
— 15 stavki, plus toč. 4 (skupština) = **16**.

> Toč. 1: *„(...) nagrada iz Tbr. 7. toč. 1. kod tužbi na poništaj upisa u sudski registar,
> a ne manje od 100 bodova, odnosno po Tbr. 27. kod tužbi na poništaj upisa u drugi
> registar."*

### Prag od 100 bodova skalira se s postotkom

Ovo je bilo jedino netrivijalno mjesto. `ClampBodovi` primjenjuje `MinBodova` **nakon**
postotka, a toč. 2 i 3 uzimaju postotak nagrade **iz toč. 1**, u kojoj prag već stoji.
Kako je prag konstanta, vrijedi

```
max(x, 100) × p  ≡  max(p·x, 100p)
```

pa je u svaku stavku upisan prag pomnožen istim postotkom: 100 → **50** (50 %) odnosno
**125** (125 %). Naivan prag od 100 na svakoj stavci u malim bi predmetima dao previše.

Uz VPS 200 € (25 bodova po Tbr. 7. toč. 1), sudski registar:

| radnja | prije | sada | naivni prag 100 dao bi |
|---|---|---|---|
| toč. 1 — tužba / odgovor / podnesak | — | **100** bod. = 200,00 € | 100 |
| toč. 2 — ročište o glavnoj stvari | — | **100** bod. = 200,00 € | 100 |
| toč. 2 — ostala ročišta | — | **50** bod. = 100,00 € | 100 ✗ |
| toč. 3 — pravni lijek protiv presude | — | **125** bod. = 250,00 € | 100 ✗ |
| toč. 3 — pravni lijek protiv rješenja | — | **50** bod. = 100,00 € | 100 ✗ |

Prag **ne vrijedi** za drugi registar — toč. 1 ga veže izrijekom uz sudski. Uz VPS 200 €
tužba u drugi registar daje 25 bodova, ne 100.

Neprocjenjivi drugi registar ide po Tbr. 27. toč. 1 al. 2: 500 · 250 · 625 · 250.

### Toč. 4 — skupština

> *„(...) nagrada prema dogovoru sa strankom, ali ne manje od 100 bodova za svaki započeti
> sat skupštine."*

Tarifa određuje samo **donju** granicu; gornju prepušta dogovoru, pa sustav obračunava
minimum (`reg_skupstina`, 100 bod./sat). Povišenje iznad toga ide kroz Tbr. 41 ili ugovor
po Tbr. 43.

| | |
|---|---|
| Testovi | `TarifaTbr29Tests` (34), uz odnosne provjere da toč. 2 i 3 ostaju točan postotak toč. 1 i ispod i iznad praga |
| Zrcalo | `frontend/__tests__/tarifa-tbr29-prag.test.ts` (6) — živi pregled mora dati iste iznose |

## 4. Tbr. 31. toč. 4 i Tbr. 32. toč. 3–5 (`da77548`)

> Tbr. 31. toč. 4: *„(...) odvjetniku pripada 50% nagrade iz toč. 1. ovog tarifnog broja."*

Toč. 1 = 1000 bodova → **500** bod. = 1.000,00 €. Katalog je imao 31.1, 31.2 i 31.3.

Tbr. 32. toč. 3, 4 i 5 sve se izvode iz toč. 2 (100 bodova); katalog je imao samo 32.1 i 32.2:

| stavka | Tbr. | iznos |
|---|---|---|
| `ost_stegovni_ostali` | 32.3 | 25 % × 100 = **25** bod. = 50,00 € |
| `ost_stegovni_rasprava` | 32.4 | **100** bod. = 200,00 € |
| `ost_stegovni_rasprava_procesna` | 32.5 | 25 % × 100 = **25** bod. = 50,00 € |

| | |
|---|---|
| Testovi | `TarifaKatalogDopunaTests` (14) |

## Što nije dirano

Motor obračuna (`IzracunajBodove`, `ClampBodovi`, `UkupnoBodovaZaStavku`), određivanje
osnove sudske pristojbe, sučelje i klijentsko zrcalo. Sve 26 stavki izražene su postojećim
poljima `TarifnaStavka` i ulaze u postojeće kategorije, pa se katalog na `/troskovnik`
grupira sam.

## Nalaz za odluku — `Jedinica = "sat"` na prekršajnim stavkama

Test `SatneStavke_IzvodeIznosIzVlastiteOdredbe` otkrio je **zatečeno** stanje, nije ga
uzrokovao:

- `kaz_obrana_prekrsaj` (Tbr. 4. toč. 1 al. 7) — *„za obranu u prekršajnom postupku
  100 bodova"*
- `kaz_prekrsaj_oduzimanje` (Tbr. 4. toč. 1 al. 8) — *„nagrada po Tbr. 7. toč. 1., ovisno
  o vrijednosti oduzetog predmeta odnosno imovinske koristi"*

Nijedna od dvije alineje **ne spominje sat**. Satnica u Tbr. 4. toč. 1 pripada samo
al. 10 (*„za prvi sat svakog dana rasprave (...) 100 bodova"*, uz 50 za svaki sljedeći).
Obje stavke ipak nose `Jedinica = "sat"`, pa ih sučelje množi brojem sati.

**Nije popravljeno namjerno.** Kazneni blok koristi „sat" i kao oznaku „prvi sat", uz
zasebne stavke `kaz_dodatni_sat` i `kaz_istraga_dodatni_sat` za daljnje sate, pa nije jasno
je li ovo greška ili dio te konvencije. Odluka traži pregled cijelog kaznenog bloka i
mišljenje odvjetnice — nije nešto što se popravlja usput.

## Stanje testova

```
dotnet test Backend.Tests/Backend.Tests.csproj   →  1582 prolaza, 0 padova   (bilo 1512)
cd frontend && npx vitest run __tests__/tarifa   →   197 prolaza, 0 padova   (bilo 191)
```

Zatečenih 5 padova u `__tests__/delete.test.ts` i dalje nema veze s tarifom.

## Preostalo nepokriveno

| tarifni broj | zašto nije ovdje |
|---|---|
| **Tbr. 26** — izvlaštenje | vlastita tablica po tržišnoj vrijednosti nekretnine (0–1.000 → 50; do 10.000 → 100; do 30.000 → 250; preko → 500). Traži novo polje u zahtjevu i sučelju, po uzoru na `VrijednostOduzimanja`. |
| **Tbr. 49–51** — putni troškovi, dnevnica, hotel | stvarni izdaci izvan bodovnog sustava. Tarifa ne fiksira ni cijenu benzina ni iznos dnevnice (veže je uz dnevnicu suca županijskog suda), pa oboje mora biti unos, ne pretpostavka. |
| **Osnovica Tbr. 22** | Tarifa za procjenjive upravne predmete propisuje osnovicu *„ukupan prihod, odnosno vrijednost imovine, odnosno iznos jednogodišnje najamnine ili zakupnine"*, a sustav koristi opću osnovicu Tbr. 7. Nove stavke po Tbr. 23 nasljeđuju to jer preslikavaju postojeći `upr_procjenjiv`. Isti razred problema kao Tbr. 10 i uži skup osnovica. |
