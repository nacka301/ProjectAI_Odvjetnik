# Popravci odvjetničke tarife i sudskih pristojbi

Grana: `claude/audit-tarifa-fixes-9e61a6` · 16 commitova · 29. srpnja 2026.

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
dotnet test Backend.Tests/Backend.Tests.csproj   →  1347 prolaza, 0 padova
cd frontend && npx vitest run __tests__/tarifa   →   123 prolaza, 0 padova
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
