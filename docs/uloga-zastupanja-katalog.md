# Uloga zastupanja — pregled kataloga kaznenih i prekršajnih radnji

Za opis PR-a. Izvor: `Backend/Services/TarifaKatalog.cs` (kategorija „Kazneni postupak"), Tarifa NN 138/2023.
Sve četiri uloge (branitelj, privatni tužitelj, oštećenik kao tužitelj, punomoćnik oštećenika) mogu odabrati **svaku** radnju — katalog nema ograničenja po ulozi. Umanjenje od 50% primjenjuje se samo za `PUNOMOCNIK_OSTECENIKA` i samo na tarifne brojeve 3 i 4.

| Radnja (id) | Tbr. | Umanjenje za punomoćnika oštećenika | Točka Tarife / napomena |
|---|---|---|---|
| kaz_privatna_tuzba, kaz_podnesak_obrana, kaz_zahtjev_ostecenik, kaz_imovinskopravni, kaz_ostali_podnesci | 1.1–1.5 | NE (puna) | Tbr. 1 nema umanjenja; Tbr. 1 t. 3 (zahtjev oštećenika) je zasebna stavka |
| kaz_odgoda_izvrsenja, kaz_rehabilitacija, kaz_pomilovanje, kaz_opoziv_uvjetne | 2.1–2.4 | NE | Tbr. 2 ne spominje oštećenika |
| kaz_sudjelovanje_istraga | 3.1 | DA 50% | Tbr. 3 t. 3 |
| kaz_istraga_dodatni_sat (fiksno 50 b → 25 b) | 3.1 | DA 50% | Tbr. 3 t. 3 |
| kaz_istraga_izvan_sjedista | 3.2 | DA 50% | Tbr. 3 t. 3 (+ Tbr. 49 putni troškovi, neumanjeni) |
| kaz_istraga_ostecenik | 3.3 | već u stavci (−50%) | Tbr. 3 t. 3; ne umanjuje se drugi put; skrivena u izborniku, razrješiva za stare troškovnike |
| kaz_istraga_odgoda | 3.4 | DA 50% (uz ugrađenih −50% → 25% Tbr. 4.1) | ⚠ vidi rupe |
| kaz_obrana, kaz_sjednica_drugostupanjska, kaz_rasprava_drugostupanjska, kaz_sjednica_izvanredni, kaz_maloljetnik, kaz_obustava_odgoda, kaz_ocevid_izvan_zgrade | 4.x | DA 50% | Tbr. 4 t. 8 |
| kaz_dodatni_sat, kaz_objava_presude, kaz_razgovor_pritvor, kaz_pritvor_dodatni_sat (fiksno 50 b) | 4.x | DA 50% | Tbr. 4 t. 8 |
| kaz_zastupanje_ostecenika | 4.8 | već u stavci (−50%) | Tbr. 4 t. 8; skrivena u izborniku |
| kaz_obrana_prekrsaj (fiksno 100), kaz_prekrsaj_oduzimanje, kaz_obrana_pravna_osoba, kaz_predstavnik_sud, kaz_predstavnik_pravna_osoba | 4.1 | DA 50% (mehanički, po Tbr.) | ⚠ vidi rupe |
| kaz_zalba_presuda, kaz_odgovor_zalba, kaz_zalba_istrazni_zatvor, kaz_odgovor_zalba_rjesenje, kaz_zalba_troskovi, kaz_odgovor_optuznica | 5.x | NE (puna) | Tbr. 5 ne navodi umanjenje |
| kaz_izvanredni_pravni_lijek, kaz_odgovor_izvanredni | 6.x | NE | Tbr. 6 ne spominje oštećenika |

## Rupe / otvoreno (za odvjetnika)
1. **Opunomoćenik žrtve + Tbr. 4 t. 8:** zbraja li se 50% s Pravilnikom ili ga Pravilnik zamjenjuje — alat ne nudi iznos (greška s objašnjenjem), kako je traženo.
2. **kaz_istraga_odgoda (Tbr. 3 t. 4)** je već 50% od Tbr. 3 t. 1; za punomoćnika oštećenika danas ide još jednom −50% (25% Tbr. 4.1). Nije jasno slijedi li Tarifa to ili Tbr. 3 t. 3 i t. 4 treba tretirati kao alternative.
3. **Branitelj-specifične stavke** (obrana u prekršajnom postupku, obrana/predstavnik pravne osobe, prekršaj s oduzimanjem) dostupne su svim ulogama i umanjuju se mehanički po tarifnom broju; Tarifa za oštećenika u tim stavkama ništa ne propisuje.
4. **Tbr. 1 t. 3:** u katalogu je „Zahtjev oštećenika za dokazne radnje / preuzimanje progona". Točna formulacija t. 3 (zahtjev oštećenika kao tužitelja sucu istrage) nije provjerena u PDF-u Tarife.
5. **Pravilnik na fiksne stavke:** službena dužnost / teret proračuna i dalje NE umanjuju fiksne stavke (postojeće ponašanje, nepromijenjeno).
