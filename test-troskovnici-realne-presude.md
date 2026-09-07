# Test-set: 10 realnih troškovnika iz objavljenih presuda

Izvučeno iz javno objavljenih odluka na zakon.hr (sudska praksa). Namjena: unijeti svaki
troškovnik u PravoTek i usporediti izračun red po red.

## Prije nego počneš — dvije stvari koje ovi primjeri dokazuju

**1) Provjeravaj ODVJETNIČKU NAGRADU (bodove), ne pristojbe.**
Tarifa (NN 138/23, 1 bod = 2,00 €) nije se mijenjala, pa se bodovi i nagrada iz ovih
presuda smiju uspoređivati s appom. **Sudske pristojbe u SVIM ovim presudama su po
STAROJ uredbi** (presude su prije 1.6.2026.). Tvoj app računa po **NN 50/2026** (~2×
veće), pa se pristojbe NEĆE poklopiti — njih provjeravaj zasebno protiv teksta NN 50/2026,
ne protiv ovih presuda.

**2) Sud NE daje uvijek pune bodove po radnji.** Vidljivo u primjerima:
- procesno/pripremno ročište zna biti 25 ili 50 umjesto 100 (Tbr. 9.2/9.3),
- „pristup ročištu objave presude" = 25 bodova,
- neki podnesci se odbiju ili priznaju po nižem Tbr. (npr. 8.4 = 50 bodova),
- kod djelomičnog uspjeha trošak se dijeli po postotku uspjeha.
Ako app svakoj radnji mehanički lijepi 100 bodova, razići će se sa sudom. **Bodovi po
radnji moraju biti podesivi** — to je glavni nalaz za testiranje.

## Razredi vrijednosti (Tbr. 7, sastav tužbe) — etalon

| Vrijednost predmeta (€) | Bodova | Nagrada (× 2 €) |
|---|---|---|
| 0 – 332,00 | 25 | 50 € |
| 332,01 – 664,00 | 50 | 100 € |
| 664,01 – 1.327,00 | 75 | 150 € |
| 1.327,01 – 13.272,00 | 100 | 200 € |
| 13.272,01 – 33.181,00 | 250 | 500 € |
| 33.181,01 – 66.361,00 | 500 | 1.000 € |
| 66.361,01 – 663.614,00 | 500 + 1 bod / započetih 133 € iznad 66.361 € | |

> Napomena: iznose sam izvukao automatski iz teksta presuda; kod svakog je link, pa
> kod spornog broja otvori original i provjeri. Presude označene (kn) su po staroj
> Tarifi u kunama — za njih vrijedi logika/struktura, ne aritmetika u eurima.

---

## 1. Naknada štete na vozilu — Trgovački sud Zagreb, P-2064/2022-41
Datum 28.5.2025. · Tarifa NN 138/23 · [izvor](https://www.zakon.hr/c/sudska-praksa/632133/p-2064-2022-41---trgovacki-sud-u-zagrebu)

| Radnja | Bodovi | Nagrada | +PDV 25% |
|---|---|---|---|
| Sastav tužbe (Tbr. 7.1) | 100 | 200 € | 250 € |
| Podnesak 3.4.2023. (Tbr. 8.1) | 100 | 200 € | 250 € |
| Podnesak 31.3.2025. (Tbr. 8.4) | 25 | 50 € | 62,50 € |
| Zastupanje na 4 ročišta (Tbr. 9.1) | 4×100 | 800 € | 1.000 € |
| Predujam vještačenja | — | — | 400 € |
| Pristojba na tužbu | — | — | 66,59 € (stara) |
| Pristojba na presudu | — | — | 130,51 € (stara) |
| **Ukupno** | | | **2.409,60 €** |

Testira: podnesak po sniženom Tbr. 8.4 (50 bod), više ročišta, predujam bez PDV-a.
Sud je jedan podnesak (28.8.2024.) ODBIO — dobar test da app dopušta izbacivanje stavke.

## 2. Parnica s vještačenjem — Općinski sud Koprivnica, P-445/2023-24
Datum 30.9.2025. · Tarifa NN 138/23 · [izvor](https://www.zakon.hr/c/sudska-praksa/710152/p-445-2023-24---opcinski-sud-u-koprivnici)

| Radnja | Datum | Bodovi |
|---|---|---|
| Sastav tužbe | 13.6.2023. | 100 |
| Očitovanje na odgovor na tužbu | 16.10.2024. | 100 |
| Očitovanje na nalaz vještaka | 24.3.2025. | 100 |
| Očitovanje na dopunu nalaza | 20.8.2025. | 100 |
| Zastupanje (procesno) | 2.10.2024. | 50 |
| Zastupanje | 12.11.2024. | 100 |
| Zastupanje | 22.1.2025. | 100 |
| Zastupanje | 28.8.2025. | 100 |
| Pristup ročištu objave presude | — | 25 |

Bodovi 775 → nagrada 1.550 € + PDV 387,50 € + predujam vještačenja 398,17 € =
**2.335,67 €**. Testira: isti tip radnje s RAZLIČITIM bodovima (50/100/25).

## 3. Parnica male vrijednosti, puno ročišta — Općinski sud Split, P-4036/2024-12
Datum 15.7.2025. · Tarifa NN 138/23 · vps 463,53 € · [izvor](https://www.zakon.hr/c/sudska-praksa/561611/p-4036-2024-12---opcinski-sud-u-splitu)

Sastav tužbe **50 bod** (razred 332–664 €), 3 podneska ×50, 6 ročišta ×50 = 500 bod →
nagrada 500 € + PDV 125 € + vještačenje 265,45 € + žalba 53,08 € = **942,53 €**.
Testira: niski razred vrijednosti (50 bod potvrđuje tablicu), velik broj ročišta.

## 4. Ovrha → parnica (prigovor na platni nalog) — Trgovački sud Zadar, Povrv 80/2023-12
Datum 22.3.2024. · Tarifa NN 138/23 · [izvor](https://www.zakon.hr/c/sudska-praksa/154846/povrv-80-2023-12-trgovacki-sud-u-zadru)

Parnični dio: 2 obrazložena podneska ×100, pripremno ročište 25, glavna rasprava 50 =
275 bod → 550 € + PDV 137,50 €. Ovršni dio: javnobilježnička naknada 42,05 € + sastav
ovršnog prijedloga 248,86 €. Pristojba 26,54 € (stara). **Ukupno 1.004,94 €**.
Testira: dvije faze (ovrha + parnica), pripremno ročište 25 vs. glavna 50, javnobilježnička naknada.

## 5. Ovršni postupak — Općinski građanski sud Zagreb, Povrv-2030/2022-15
Datum 30.9.2025. · Tarifa NN 138/23 · [izvor](https://www.zakon.hr/c/sudska-praksa/709256/povrv-2030-2022-15---opcinski-gradanski-sud-u-zagrebu)

Sastav prijedloga za ovrhu 100, pripremna radnja (6.2.2024.) 100, podnesak (8.4.2024.) 25,
ročište (12.2.2025.) 100 + javnobilježnička naknada 12,44 € + vještačenje 200 € +
pristojba na presudu 71,76 €. **Ukupno 1.012,60 €**. Sud odbio jednu stavku dostave
(nedokazana). Testira: ovršne radnje, odbijena nedokazana stavka.

## 6. Potrošački spor (ništetnost odredbe) — Općinski sud Sisak (Glina), P-324/2025-20
Datum 23.4.2026. · Tarifa NN 138/23 · vps 1.327,22 € · [izvor](https://www.zakon.hr/c/sudska-praksa/982083/p-324-2025-20---opcinski-sud-u-sisku)

Sastav tužbe 200 €, podnesak (3.11.2025.) 200 €, tri ročišta, objava presude 100 €,
pristojba 53,10 €. **Ukupno 1.428,10 €**. Testira: vps točno na granici razreda
(1.327 € → još 100 bod, iznad bi bilo 100; provjeri kako app zaokružuje granicu).

## 7. STRESS TEST — Trgovački sud Varaždin, P 4/2024-18
Datum 27.6.2024. · Tarifa NN 138/23 · **djelomični uspjeh 74,54 % / 25,46 %** · [izvor](https://www.zakon.hr/c/sudska-praksa/164713/p-4-2024-18-trgovacki-sud-u-varazdinu)

Honorari punomoćnika **12.627,10 €** (sastav tužbe + ~25 podnesaka po 125–500 €,
8 ročišta po 250–500 €) + PDV 3.156,78 €. Predujmovi vještačenja: 1.832,64 € + 515,50 € +
250 €. Pristojbe: tužba 306,59 €, presuda 304,83 €, žalba 205,58 € (stare). Ukupno
traženo 19.234,85 €, dosuđeno po uspjehu ~5.955 €. Testira: jako dugačak troškovnik,
tri vještačenja, žalbena pristojba, **podjela troška po postotku uspjeha** (ako app to
nema — bitan nedostatak).

## 8. Visoka vrijednost, žalbeni — Županijski sud Slavonski Brod, Gž 186/2023-6 (kn)
Datum 4.9.2023. · **stara Tarifa, u kunama** · [izvor](https://www.zakon.hr/c/sudska-praksa/66811/gz-186-2023-6-zupanijski-sud-u-slavonskom-brodu)

Vrijednost predmeta 5.991.708 kn → **5495 bodova po radnji**; ukupno priznato zastupanje
343.044 kn + PDV 85.761 kn = 428.805 kn (≈ 56.912 €). Testira: gornji kraj skale (500 bod +
1/133 € iznad 66.361 €) — provjeri da app uopće ispravno računa velike vrijednosti.
(Iznose ne uspoređuj 1:1 jer je stara tarifa/kn; provjeri LOGIKU rasta bodova.)

## 9. Radni spor, odbijeno ročište — Općinski sud Osijek, Pr-1107/2021-20 (kn)
Datum 15.2.2023. · **stara Tarifa, u kunama** · [izvor](https://www.zakon.hr/c/sudska-praksa/492010/pr-1107-2021-20-op%C4%87inski-sud-u-osijeku)

Tužba 100, dva obrazložena podneska 100+100, ročište 50. **Sud je smanjio zastupanje na
ročištu jer se raspravljalo samo o procesnim pitanjima.** Testira: radni spor + ručno
sniženje bodova za procesno ročište.

## 10. Baseline (jednostavan) — Općinski građanski sud Zagreb, P-3260/2025-12
Datum 26.1.2026. · Tarifa NN 138/23 · vps 7.500 € · [izvor](https://www.zakon.hr/c/sudska-praksa/900610/p-3260-2025-12---opcinski-gradanski-sud-u-zagrebu)

Sastav tužbe (Tbr. 7.1) **100 bod = 200 €** + PDV 50 € + pristojba na tužbu 60,73 € +
pristojba na presudu 60,73 € = **371,46 €**. Testira: potvrda razreda (7.500 € → 100 bod)
i najjednostavniji slučaj kao kontrola.

---

## Kako testirati

Za svaki: unesi vrijednost predmeta i radnje s bodovima kako ih je sud priznao, pa
provjeri poklapa li se nagrada (bod × 2 €) i PDV. Ako app ne da mijenjati bodove po
radnji (primjeri 1, 2, 4, 5, 9) ili nema podjelu po uspjehu (primjer 7) — to zapiši kao
nedostatak. Pristojbe zanemari u usporedbi (stare); njih provjeri protiv NN 50/2026.
