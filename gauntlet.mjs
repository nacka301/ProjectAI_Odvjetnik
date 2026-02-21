/**
 * ═══════════════════════════════════════════════════════════════════
 *  GAUNTLET v1.0 — 100-Scenario Autonomous Valuation Stress Test
 * ═══════════════════════════════════════════════════════════════════
 *
 *  Sends 100 extreme Croatian property descriptions to the backend
 *  /api/evaluate/text/stream SSE endpoint and validates that:
 *    1) The server responds without errors
 *    2) EstimatedValueEur > 0
 *    3) EstimatedValueEur falls within a sane range for the scenario
 *
 *  Usage:  node gauntlet.mjs [startIndex] [endIndex]
 *          node gauntlet.mjs           → runs all 100
 *          node gauntlet.mjs 1 10      → runs scenarios 1-10
 */

const API_URL = 'http://localhost:5078/api/evaluate/text/stream';
const TIMEOUT_MS = 600_000; // 10 min per scenario (BrightData scraping is slow)

// ── 100 nightmare scenarios ──────────────────────────────────────
const SCENARIOS = [
  // ══════════════════════════════════════
  // COASTAL LUXURY  (1-15)
  // ══════════════════════════════════════
  { id: 1, text: "Luksuzni stan u centru Dubrovnika, 85 m², 3 sobe, potpuno renoviran 2023., pogled na more, 2. kat, klima, parking mjesto, blizina Straduna", city: "Dubrovnik", neighborhood: "Centar", minEur: 150_000, maxEur: 900_000 },
  { id: 2, text: "Penthouse stan na Bačvicama u Splitu, 120 m², terasa 40 m² s panoramskim pogledom na more, 2 parking mjesta, novogradnja 2024.", city: "Split", neighborhood: "Bačvice", minEur: 200_000, maxEur: 1_200_000 },
  { id: 3, text: "Apartman u Rovinju, stara jezgra, 55 m², kameni zidovi, renoviran, pogled na luku, 1. kat", city: "Rovinj", neighborhood: "Stari grad", minEur: 100_000, maxEur: 600_000 },
  { id: 4, text: "Stan na otoku Hvaru, grad Hvar, 70 m², 2 sobe, klima, terasa, blizina plaže, potpuno namješten", city: "Hvar", neighborhood: "Hvar", minEur: 100_000, maxEur: 700_000 },
  { id: 5, text: "Luksuzna vila u Cavtatu, 250 m², bazen, vrt 500 m², garaža za 2 auta, pogled na more, prvi red do mora", city: "Cavtat", neighborhood: "", minEur: 400_000, maxEur: 3_000_000 },
  { id: 6, text: "Garsonijera u Zadru, Poluotok, 28 m², renovirana, 3. kat, klima, blizina Morskih orgulja", city: "Zadar", neighborhood: "Poluotok", minEur: 40_000, maxEur: 250_000 },
  { id: 7, text: "Stan u Opatiji, 95 m², 3 sobe, pogled na Kvarner, balkon, garažno mjesto, lift, Slatina", city: "Opatija", neighborhood: "Slatina", minEur: 150_000, maxEur: 800_000 },
  { id: 8, text: "Kuća u Makarskoj, 180 m² stambeno + 100 m² okućnica, 4 sobe, pogled na Biokovo, 300 m od plaže", city: "Makarska", neighborhood: "", minEur: 150_000, maxEur: 1_000_000 },
  { id: 9, text: "Stan u Poreču, 60 m², novogradnja, prizemlje s vrtom 30 m², parking, 500 m od mora", city: "Poreč", neighborhood: "", minEur: 80_000, maxEur: 400_000 },
  { id: 10, text: "Luksuzni stan u Šibeniku, 100 m², pogled na katedralu sv. Jakova, 2 balkona, renoviran 2022., stara jezgra", city: "Šibenik", neighborhood: "Stari grad", minEur: 100_000, maxEur: 600_000 },
  { id: 11, text: "Vila na Korčuli, grad Korčula, 200 m², okućnica 400 m², bazen, pogled na more, kamena kuća potpuno obnovljena", city: "Korčula", neighborhood: "Korčula", minEur: 200_000, maxEur: 1_500_000 },
  { id: 12, text: "Apartman u Novalji na Pagu, 45 m², 2 sobe, terasa, 200 m od plaže Zrće, namješten", city: "Novalja", neighborhood: "", minEur: 50_000, maxEur: 350_000 },
  { id: 13, text: "Stan u Trogiru, UNESCO zona, 75 m², 2. kat, kameni zidovi, renoviran, pogled na kanal", city: "Trogir", neighborhood: "Stari grad", minEur: 100_000, maxEur: 500_000 },
  { id: 14, text: "Moderna vila u Primošten, 300 m² + bazen + garaža, parcela 600 m², pogled na more, novogradnja", city: "Primošten", neighborhood: "", minEur: 300_000, maxEur: 2_000_000 },
  { id: 15, text: "Kuća na otoku Braču, Bol, 150 m², okućnica 300 m², pogled na Zlatni rat, djelomično renovirana", city: "Bol", neighborhood: "", minEur: 150_000, maxEur: 1_200_000 },

  // ══════════════════════════════════════
  // ZAGREB & INLAND  (16-30)
  // ══════════════════════════════════════
  { id: 16, text: "Trosoban stan u Zagrebu, Trnje, 75 m², 4. kat, lift, balkon, renoviran, blizina Bundeka", city: "Zagreb", neighborhood: "Trnje", minEur: 100_000, maxEur: 350_000 },
  { id: 17, text: "Stan u Zagrebu, Centar, Ilica, 90 m², 3. kat, visoki stropovi, potrebna renovacija, bez lifta", city: "Zagreb", neighborhood: "Centar", minEur: 100_000, maxEur: 400_000 },
  { id: 18, text: "Novogradnja stan Zagreb, Buzin, 55 m², 2 sobe, parking, terasa, energetski razred A+", city: "Zagreb", neighborhood: "Buzin", minEur: 80_000, maxEur: 250_000 },
  { id: 19, text: "Kuća u Zagrebu, Maksimir, 220 m² stambeno, parcela 500 m², garaža, 5 soba, renovirana 2020.", city: "Zagreb", neighborhood: "Maksimir", minEur: 200_000, maxEur: 800_000 },
  { id: 20, text: "Garsonijera Zagreb, Jarun, 25 m², 2. kat, namještena, blizina jezera i tramvaja", city: "Zagreb", neighborhood: "Jarun", minEur: 40_000, maxEur: 150_000 },
  { id: 21, text: "Stan u Varaždinu, centar, 65 m², 2 sobe, renoviran, balkon, 1. kat, blizina Starog grada", city: "Varaždin", neighborhood: "Centar", minEur: 50_000, maxEur: 200_000 },
  { id: 22, text: "Kuća u Osijeku, Gornji grad, 160 m², parcela 400 m², 4 sobe, garaža, obnovljena fasada", city: "Osijek", neighborhood: "Gornji grad", minEur: 60_000, maxEur: 300_000 },
  { id: 23, text: "Stan u Rijeci, Korzo, 80 m², 2. kat, pogled na more, potrebna obnova, austrougarski stil", city: "Rijeka", neighborhood: "Centar", minEur: 50_000, maxEur: 300_000 },
  { id: 24, text: "Trosoban stan u Puli, Veruda, 72 m², 5. kat, lift, balkon, pogled na Arenu, renoviran", city: "Pula", neighborhood: "Veruda", minEur: 80_000, maxEur: 300_000 },
  { id: 25, text: "Kuća u Karlovcu, 140 m², parcela 800 m², 3 sobe, garaža, blizina rijeke Korane, dobro stanje", city: "Karlovac", neighborhood: "", minEur: 50_000, maxEur: 250_000 },
  { id: 26, text: "Stan u Čakovcu, 58 m², 2 sobe, 3. kat, lift, balkon, novogradnja 2023.", city: "Čakovec", neighborhood: "", minEur: 40_000, maxEur: 150_000 },
  { id: 27, text: "Kuća u Koprivnici, 180 m², parcela 1000 m², 4 sobe, garaža za 2 auta, bazen, renovirana", city: "Koprivnica", neighborhood: "", minEur: 60_000, maxEur: 300_000 },
  { id: 28, text: "Stan u Sisku, centar, 70 m², 2. kat, potrebna obnova nakon potresa, konstrukcijski siguran", city: "Sisak", neighborhood: "Centar", minEur: 20_000, maxEur: 120_000 },
  { id: 29, text: "Penthouse u Zagrebu, Novi Zagreb, 110 m², terasa 50 m², 2 garažna mjesta, novogradnja, pogled na Savu", city: "Zagreb", neighborhood: "Novi Zagreb", minEur: 150_000, maxEur: 500_000 },
  { id: 30, text: "Kuća u Samoboru, 200 m², parcela 1200 m², pogled na Samoborsko gorje, renovirana, 4 sobe", city: "Samobor", neighborhood: "", minEur: 100_000, maxEur: 500_000 },

  // ══════════════════════════════════════
  // ISLANDS & REMOTE  (31-45)
  // ══════════════════════════════════════
  { id: 31, text: "Kamena kuća na otoku Visu, 80 m², parcela 200 m², djelomično obnovljena, pogled na more", city: "Vis", neighborhood: "", minEur: 80_000, maxEur: 500_000 },
  { id: 32, text: "Ruševina na otoku Lastovu, kamena kuća 60 m², parcela 300 m², potrebna kompletna obnova", city: "Lastovo", neighborhood: "", minEur: 15_000, maxEur: 200_000 },
  { id: 33, text: "Apartman na Malom Lošinju, 50 m², pogled na luku, 2. kat, renoviran, klima, 100 m od mora", city: "Mali Lošinj", neighborhood: "", minEur: 80_000, maxEur: 400_000 },
  { id: 34, text: "Kuća na Murteru, 120 m², okućnica 250 m², pogled na Kornate, blizina plaže, dobro stanje", city: "Murter", neighborhood: "", minEur: 100_000, maxEur: 500_000 },
  { id: 35, text: "Stan na Cresu, grad Cres, 65 m², prizemlje s vrtom, kamena kuća, renoviran, stari grad", city: "Cres", neighborhood: "", minEur: 60_000, maxEur: 350_000 },
  { id: 36, text: "Vikendica na otoku Šolti, 90 m², parcela 500 m², pogled na more, potrebna obnova", city: "Šolta", neighborhood: "", minEur: 60_000, maxEur: 400_000 },
  { id: 37, text: "Kuća na Ugljanu, 100 m², okućnica 300 m², blizina trajektne luke, dobro stanje, pogled na Zadar", city: "Ugljan", neighborhood: "", minEur: 60_000, maxEur: 350_000 },
  { id: 38, text: "Apartman na Rabu, 40 m², centar grada, renoviran, 1. kat, blizina plaže Rajska", city: "Rab", neighborhood: "", minEur: 50_000, maxEur: 250_000 },
  { id: 39, text: "Kamena kuća na Pelješcu, Orebić, 130 m², parcela 400 m², pogled na Korčulu, obnovljena", city: "Orebić", neighborhood: "", minEur: 100_000, maxEur: 600_000 },
  { id: 40, text: "Kuća na otoku Iž, 70 m², parcela 500 m², pogled na more, originalno stanje, bez pristupne ceste", city: "Iž", neighborhood: "", minEur: 15_000, maxEur: 150_000 },
  { id: 41, text: "Vila na Krku, Malinska, 180 m², bazen, okućnica 600 m², garaža, novogradnja, pogled na more", city: "Malinska", neighborhood: "", minEur: 200_000, maxEur: 1_200_000 },
  { id: 42, text: "Apartman na Pašmanu, 35 m², prizemlje, 50 m od plaže, namješten, potrebna obnova", city: "Pašman", neighborhood: "", minEur: 25_000, maxEur: 180_000 },
  { id: 43, text: "Kuća na Dugom otoku, Sali, 100 m², parcela 800 m², maslinik, pogled na more, originalno stanje", city: "Dugi Otok", neighborhood: "Sali", minEur: 40_000, maxEur: 300_000 },
  { id: 44, text: "Kamena kuća na Šipanu, 90 m², parcela 300 m², obnovljena 2021., pogled na more, mir i tišina", city: "Šipan", neighborhood: "", minEur: 80_000, maxEur: 500_000 },
  { id: 45, text: "Stan na Ciovu, Okrug Gornji, 55 m², pogled na more, 1. kat, parking, novogradnja", city: "Okrug Gornji", neighborhood: "", minEur: 80_000, maxEur: 350_000 },

  // ══════════════════════════════════════
  // LAND — CONSTRUCTION  (46-55)
  // ══════════════════════════════════════
  { id: 46, text: "Građevinsko zemljište u Splitu, Žnjan, 500 m², urbanistička dozvola, komunalno opremljeno, blizina plaže", city: "Split", neighborhood: "Žnjan", minEur: 100_000, maxEur: 800_000 },
  { id: 47, text: "Građevinsko zemljište u Dubrovniku, Lapad, 800 m², pogled na more, infrastruktura, pristupna cesta", city: "Dubrovnik", neighborhood: "Lapad", minEur: 150_000, maxEur: 1_500_000 },
  { id: 48, text: "Građevinsko zemljište u Zagrebu, Remete, 1200 m², mirna lokacija, sva infrastruktura, ravno", city: "Zagreb", neighborhood: "Remete", minEur: 80_000, maxEur: 500_000 },
  { id: 49, text: "Građevinsko zemljište u Rovinju, 600 m², pogled na more, 2 km od centra, pristupna cesta", city: "Rovinj", neighborhood: "", minEur: 100_000, maxEur: 700_000 },
  { id: 50, text: "Građevinsko zemljište na Krku, Njivice, 450 m², 300 m od mora, infrastruktura, ravno", city: "Njivice", neighborhood: "", minEur: 50_000, maxEur: 400_000 },
  { id: 51, text: "Građevinsko zemljište u Zadru, Diklo, 700 m², pogled na more, infrastruktura, regulirano", city: "Zadar", neighborhood: "Diklo", minEur: 80_000, maxEur: 500_000 },
  { id: 52, text: "Građevinsko zemljište u Makarskoj, 400 m², panoramski pogled na more, strmo, bez infrastrukture", city: "Makarska", neighborhood: "", minEur: 40_000, maxEur: 400_000 },
  { id: 53, text: "Građevinsko zemljište u Puli, Štinjan, 350 m², ravno, infrastruktura, blizina Nacionalnog parka Brijuni", city: "Pula", neighborhood: "Štinjan", minEur: 30_000, maxEur: 250_000 },
  { id: 54, text: "Građevinsko zemljište u Opatiji, 1000 m², pogled na Kvarner, strmo, pristupna cesta", city: "Opatija", neighborhood: "", minEur: 100_000, maxEur: 800_000 },
  { id: 55, text: "Građevinsko zemljište u Umagu, 500 m², ravno, komunalno opremljeno, blizina centra", city: "Umag", neighborhood: "", minEur: 40_000, maxEur: 350_000 },

  // ══════════════════════════════════════
  // LAND — AGRICULTURAL  (56-60)
  // ══════════════════════════════════════
  { id: 56, text: "Poljoprivredno zemljište u Baranji, 50000 m², oranica, ravno, pristupna cesta, blizina Osijeka", city: "Osijek", neighborhood: "Baranja", minEur: 10_000, maxEur: 200_000 },
  { id: 57, text: "Maslinik na Hvaru, 8000 m², 200 stabala maslina, pogled na more, pristupna cesta", city: "Hvar", neighborhood: "", minEur: 20_000, maxEur: 300_000 },
  { id: 58, text: "Vinograd u Slavoniji, Kutjevo, 15000 m², plodna zemlja, infrastruktura, ravno", city: "Kutjevo", neighborhood: "", minEur: 10_000, maxEur: 150_000 },
  { id: 59, text: "Poljoprivredno zemljište u Lici, 30000 m², livada, bez infrastrukture, bez pristupne ceste", city: "Gospić", neighborhood: "", minEur: 3_000, maxEur: 60_000 },
  { id: 60, text: "Mješovito zemljište na Pelješcu, 5000 m², maslinik + vinograd, pogled na more, pristupna cesta", city: "Pelješac", neighborhood: "", minEur: 15_000, maxEur: 200_000 },

  // ══════════════════════════════════════
  // EDGE CASES & UNUSUAL  (61-75)
  // ══════════════════════════════════════
  { id: 61, text: "Garaža u centru Zagreba, Donji grad, 15 m², suteren, sigurnosna vrata, blizina Trga bana Jelačića", city: "Zagreb", neighborhood: "Donji grad", minEur: 10_000, maxEur: 80_000 },
  { id: 62, text: "Poslovni prostor u Splitu, Riva, 50 m², prizemlje, izlog, ugostiteljska namjena, renoviran", city: "Split", neighborhood: "Centar", minEur: 80_000, maxEur: 500_000 },
  { id: 63, text: "Etaža kuće u Dubrovniku, Gruž, 100 m², 1. kat, balkon, potrebna obnova, blizina luke", city: "Dubrovnik", neighborhood: "Gruž", minEur: 80_000, maxEur: 500_000 },
  { id: 64, text: "Stan u Vukovaru, centar, 80 m², obnovljen nakon rata, 2. kat, lift, parking", city: "Vukovar", neighborhood: "Centar", minEur: 30_000, maxEur: 150_000 },
  { id: 65, text: "Ruševina u unutrašnjosti Istre, kamena kuća, 100 m², parcela 2000 m², bez infrastrukture", city: "Buzet", neighborhood: "", minEur: 15_000, maxEur: 200_000 },
  { id: 66, text: "Stan u Slavonskom Brodu, 90 m², 3 sobe, 4. kat, lift, balkon, dobro stanje, centar", city: "Slavonski Brod", neighborhood: "Centar", minEur: 30_000, maxEur: 150_000 },
  { id: 67, text: "Kuća u Požegi, 200 m², parcela 1500 m², 5 soba, garaža, voćnjak, renovirana", city: "Požega", neighborhood: "", minEur: 40_000, maxEur: 200_000 },
  { id: 68, text: "Mali stan u Dubrovniku, Pile, 22 m², garsonijera, renovirana, pogled na Lovrijenac", city: "Dubrovnik", neighborhood: "Pile", minEur: 50_000, maxEur: 400_000 },
  { id: 69, text: "Kuća na selu u Zagorju, Krapina, 250 m², parcela 3000 m², stara kuća, originalno stanje, potrebna obnova", city: "Krapina", neighborhood: "", minEur: 20_000, maxEur: 150_000 },
  { id: 70, text: "Dvoetažni stan u Rijeci, Trsat, 130 m², pogled na Kvarner, garaža, renoviran 2019., terasa", city: "Rijeka", neighborhood: "Trsat", minEur: 100_000, maxEur: 500_000 },
  { id: 71, text: "Stan u Biogradu na Moru, 50 m², 1. kat, blizina marine, renoviran, klima, namješten", city: "Biograd na Moru", neighborhood: "", minEur: 50_000, maxEur: 250_000 },
  { id: 72, text: "Kuća u Vinkovcima, 150 m², parcela 600 m², 3 sobe, garaža, dobro stanje, mirna ulica", city: "Vinkovci", neighborhood: "", minEur: 30_000, maxEur: 150_000 },
  { id: 73, text: "Apartman u Vodicama, 40 m², prizemlje s malim vrtom, 200 m od plaže, potrebna obnova", city: "Vodice", neighborhood: "", minEur: 40_000, maxEur: 200_000 },
  { id: 74, text: "Vila u Motovunu, 180 m², parcela 1000 m², pogled na istarsko podnožje, renovirana, kamena", city: "Motovun", neighborhood: "", minEur: 150_000, maxEur: 800_000 },
  { id: 75, text: "Kuća u Bjelovaru, 170 m², parcela 700 m², 4 sobe, garaža, centralno grijanje, renovirana", city: "Bjelovar", neighborhood: "", minEur: 40_000, maxEur: 200_000 },

  // ══════════════════════════════════════
  // EXTREME SIZES & VALUES  (76-85)
  // ══════════════════════════════════════
  { id: 76, text: "Mikrostan u Zagrebu, Trešnjevka, 18 m², garsonijera, 5. kat, bez lifta, originalno stanje", city: "Zagreb", neighborhood: "Trešnjevka", minEur: 20_000, maxEur: 100_000 },
  { id: 77, text: "Ogromni penthouse u Zagrebu, centar, 250 m², terasa 100 m², jacuzzi, 3 garažna mjesta, luksuz", city: "Zagreb", neighborhood: "Centar", minEur: 300_000, maxEur: 1_500_000 },
  { id: 78, text: "Velika vila u Dubrovniku, 500 m², parcela 2000 m², infinity bazen, privatna plaža, heliodrom", city: "Dubrovnik", neighborhood: "", minEur: 800_000, maxEur: 8_000_000 },
  { id: 79, text: "Malena kamena kuća u Istri, Grožnjan, 45 m², parcela 100 m², originalno stanje, bez vode", city: "Grožnjan", neighborhood: "", minEur: 10_000, maxEur: 150_000 },
  { id: 80, text: "Stan u Križevcima, 100 m², 3 sobe, prizemlje, vrt, dobro stanje, mirna lokacija", city: "Križevci", neighborhood: "", minEur: 30_000, maxEur: 150_000 },
  { id: 81, text: "Velika kuća u Splitu, Meje, 350 m², parcela 800 m², 6 soba, bazen, garaža, pogled na more", city: "Split", neighborhood: "Meje", minEur: 400_000, maxEur: 3_000_000 },
  { id: 82, text: "Mikro garsonijera u Puli, 16 m², adaptirani podrumski prostor, centralna lokacija", city: "Pula", neighborhood: "Centar", minEur: 10_000, maxEur: 70_000 },
  { id: 83, text: "Kuća u Đakovu, 200 m², parcela 2000 m², 5 soba, pomoćni objekt, ekonomsko dvorište", city: "Đakovo", neighborhood: "", minEur: 30_000, maxEur: 180_000 },
  { id: 84, text: "Luksuzni stan u Opatiji, Lungomare, 150 m², pogled na more, terasa, garaža, lift, renoviran", city: "Opatija", neighborhood: "Centar", minEur: 250_000, maxEur: 1_500_000 },
  { id: 85, text: "Kuća za renovaciju u Zagrebu, Gornji grad, 180 m², parcela 300 m², zaštićena kulturna baština", city: "Zagreb", neighborhood: "Gornji grad", minEur: 100_000, maxEur: 600_000 },

  // ══════════════════════════════════════
  // CONTINENTAL & RURAL  (86-95)
  // ══════════════════════════════════════
  { id: 86, text: "Seoska kuća u Slavoniji, Đakovština, 250 m², parcela 5000 m², gospodarski objekt, originalno stanje", city: "Đakovo", neighborhood: "", minEur: 10_000, maxEur: 120_000 },
  { id: 87, text: "Kuća u Virovitici, 130 m², parcela 400 m², 3 sobe, centralno grijanje, dobro stanje", city: "Virovitica", neighborhood: "", minEur: 25_000, maxEur: 130_000 },
  { id: 88, text: "Stan u Dubrovniku, Mokošica, 65 m², 3. kat, lift, parking, klima, pogled na Rijeku dubrovačku", city: "Dubrovnik", neighborhood: "Mokošica", minEur: 80_000, maxEur: 400_000 },
  { id: 89, text: "Kuća u Mariji Bistrici, 180 m², parcela 2000 m², blizina svetišta, renovirana, 4 sobe", city: "Marija Bistrica", neighborhood: "", minEur: 40_000, maxEur: 200_000 },
  { id: 90, text: "Stan u Senju, 60 m², 1. kat, pogled na more, renoviran, klima, blizina centra", city: "Senj", neighborhood: "", minEur: 30_000, maxEur: 150_000 },
  { id: 91, text: "Kuća u Našicama, 160 m², parcela 800 m², 4 sobe, garaža, voćnjak, dobro stanje", city: "Našice", neighborhood: "", minEur: 25_000, maxEur: 130_000 },
  { id: 92, text: "Stan u Crikvenici, 55 m², 2. kat, blizina plaže, renoviran, klima, parking", city: "Crikvenica", neighborhood: "", minEur: 50_000, maxEur: 250_000 },
  { id: 93, text: "Kuća u Petrinji, 140 m², parcela 600 m², potrebna obnova nakon potresa 2020., konstrukcijski siguran", city: "Petrinja", neighborhood: "", minEur: 15_000, maxEur: 100_000 },
  { id: 94, text: "Stan u Splitu, Solin, 70 m², novogradnja, 2. kat, lift, parking, blizina Salona", city: "Solin", neighborhood: "", minEur: 80_000, maxEur: 350_000 },
  { id: 95, text: "Kuća u Imotskom, 200 m², parcela 1500 m², pogled na Modro jezero, renovirana, 5 soba", city: "Imotski", neighborhood: "", minEur: 50_000, maxEur: 300_000 },

  // ══════════════════════════════════════
  // FINAL STRESS TESTS  (96-100)
  // ══════════════════════════════════════
  { id: 96, text: "Luksuzna nekretnina na otoku Lopudu, 200 m², parcela 1000 m², privatna plaža, potpuno renovirana, bez auta na otoku", city: "Lopud", neighborhood: "", minEur: 200_000, maxEur: 1_500_000 },
  { id: 97, text: "Industrijski prostor u Zagrebu, Žitnjak, 500 m², hala + ured, rampa, parking za kamione", city: "Zagreb", neighborhood: "Žitnjak", minEur: 80_000, maxEur: 600_000 },
  { id: 98, text: "Stara mlinica u Istri, Pazin, kamena zgrada 100 m², parcela 3000 m², potok, bez infrastrukture, potrebna kompletna obnova", city: "Pazin", neighborhood: "", minEur: 15_000, maxEur: 200_000 },
  { id: 99, text: "Super luksuzni stan u Rovinju, 200 m², prvi red do mora, terasa s jacuzzijem, 2 garažna mjesta, concierge servis", city: "Rovinj", neighborhood: "", minEur: 400_000, maxEur: 3_000_000 },
  { id: 100, text: "Tradicionalna dalmatinska kamena kuća u Stonu, 120 m², parcela 500 m², pogled na solanu, obnovljena, blizina zidina", city: "Ston", neighborhood: "", minEur: 80_000, maxEur: 500_000 },
];

// ── SSE parser ──────────────────────────────────────────────────
async function callValuationAPI(scenario) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);

  try {
    const res = await fetch(API_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        text: scenario.text,
        city: scenario.city || null,
        neighborhood: scenario.neighborhood || null,
        email: null
      }),
      signal: controller.signal
    });

    if (!res.ok) {
      return { ok: false, error: `HTTP ${res.status} ${res.statusText}` };
    }

    // Read SSE stream
    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let result = null;
    let lastError = null;

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // Parse SSE lines
      const lines = buffer.split('\n');
      buffer = lines.pop(); // keep incomplete line in buffer
      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        try {
          const parsed = JSON.parse(line.slice(6));
          if (parsed.type === 'result') {
            result = parsed.data;
          } else if (parsed.type === 'error') {
            lastError = parsed.data?.message || JSON.stringify(parsed.data);
          }
        } catch { /* skip malformed lines */ }
      }
    }

    // Process remaining buffer
    if (buffer.startsWith('data: ')) {
      try {
        const parsed = JSON.parse(buffer.slice(6));
        if (parsed.type === 'result') result = parsed.data;
        if (parsed.type === 'error') lastError = parsed.data?.message || JSON.stringify(parsed.data);
      } catch { /* ignore */ }
    }

    clearTimeout(timer);

    if (result) {
      return {
        ok: true,
        estimatedValue: result.estimated_value_eur,
        pricePerM2: result.calculated_price_per_m2,
        confidence: result.confidence_level || result.math_confidence_level,
        usableComps: result.usable_price_points,
        totalListings: result.total_listings_found,
        region: result.region_name,
        searchPhase: result.search_phase_used,
      };
    } else if (lastError) {
      return { ok: false, error: `SSE error: ${lastError}` };
    } else {
      return { ok: false, error: 'No result event received' };
    }
  } catch (err) {
    clearTimeout(timer);
    if (err.name === 'AbortError') {
      return { ok: false, error: `TIMEOUT after ${TIMEOUT_MS / 1000}s` };
    }
    return { ok: false, error: err.message };
  }
}

// ── Main runner ─────────────────────────────────────────────────
async function main() {
  const args = process.argv.slice(2);
  let startIdx = 0;
  let endIdx = SCENARIOS.length;

  if (args.length >= 2) {
    startIdx = parseInt(args[0]) - 1;
    endIdx = parseInt(args[1]);
  } else if (args.length === 1) {
    startIdx = parseInt(args[0]) - 1;
    endIdx = startIdx + 1;
  }

  const subset = SCENARIOS.slice(startIdx, endIdx);
  console.log(`\n${'═'.repeat(70)}`);
  console.log(`  GAUNTLET v1.0 — Running scenarios ${startIdx + 1} to ${endIdx} (${subset.length} total)`);
  console.log(`${'═'.repeat(70)}\n`);

  let passed = 0;
  let failed = 0;
  const failures = [];
  const results = [];

  for (const scenario of subset) {
    const label = `[${scenario.id}/100]`;
    process.stdout.write(`${label} ${scenario.city}${scenario.neighborhood ? '/' + scenario.neighborhood : ''} — "${scenario.text.substring(0, 60)}..." `);

    const startTime = Date.now();
    const result = await callValuationAPI(scenario);
    const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);

    if (!result.ok) {
      failed++;
      console.log(`\n  ❌ FAIL (${elapsed}s): ${result.error}`);
      failures.push({ id: scenario.id, city: scenario.city, error: result.error, elapsed });
      results.push({ ...scenario, status: 'FAIL', error: result.error, elapsed });
      continue;
    }

    const value = result.estimatedValue;
    const inRange = value >= scenario.minEur && value <= scenario.maxEur;
    const isPositive = value > 0;

    if (isPositive && inRange) {
      passed++;
      console.log(`✅ ${value.toLocaleString('de-DE')} € (${elapsed}s) [${result.confidence}] (${result.usableComps} comps)`);
      results.push({ ...scenario, status: 'PASS', value, elapsed, confidence: result.confidence, comps: result.usableComps });
    } else if (isPositive && !inRange) {
      // Value is positive but out of expected range — soft fail
      const direction = value < scenario.minEur ? 'TOO LOW' : 'TOO HIGH';
      failed++;
      console.log(`\n  ⚠️  SOFT FAIL (${elapsed}s): ${value.toLocaleString('de-DE')} € — ${direction} (expected ${scenario.minEur.toLocaleString('de-DE')}-${scenario.maxEur.toLocaleString('de-DE')} €) [${result.confidence}] (${result.usableComps} comps, phase=${result.searchPhase})`);
      failures.push({ id: scenario.id, city: scenario.city, value, direction, expected: `${scenario.minEur}-${scenario.maxEur}`, elapsed, confidence: result.confidence });
      results.push({ ...scenario, status: 'SOFT_FAIL', value, direction, elapsed, confidence: result.confidence, comps: result.usableComps });
    } else {
      // Value is 0 or negative
      failed++;
      console.log(`\n  ❌ ZERO VALUE (${elapsed}s): Engine returned 0 €`);
      failures.push({ id: scenario.id, city: scenario.city, error: 'EstimatedValueEur = 0', elapsed });
      results.push({ ...scenario, status: 'FAIL', error: 'ZERO', elapsed });
    }
  }

  // ── Summary ──
  console.log(`\n${'═'.repeat(70)}`);
  console.log(`  GAUNTLET RESULTS: ${passed} PASSED / ${failed} FAILED / ${subset.length} TOTAL`);
  console.log(`${'═'.repeat(70)}`);

  if (failures.length > 0) {
    console.log(`\n  FAILURES:`);
    for (const f of failures) {
      if (f.value) {
        console.log(`    #${f.id} ${f.city}: ${f.value.toLocaleString('de-DE')} € ${f.direction} (expected ${f.expected}) [${f.confidence}]`);
      } else {
        console.log(`    #${f.id} ${f.city}: ${f.error}`);
      }
    }
  }

  // Write JSON report
  const report = {
    timestamp: new Date().toISOString(),
    total: subset.length,
    passed,
    failed,
    passRate: `${((passed / subset.length) * 100).toFixed(1)}%`,
    failures,
    results: results.map(r => ({
      id: r.id,
      city: r.city,
      status: r.status,
      value: r.value || null,
      expected: `${r.minEur}-${r.maxEur}`,
      elapsed: r.elapsed,
      confidence: r.confidence || null,
      comps: r.comps || null,
    }))
  };
  const fs = await import('fs');
  fs.writeFileSync('gauntlet-report.json', JSON.stringify(report, null, 2));
  console.log(`\n  Report saved to gauntlet-report.json`);

  process.exit(failed > 0 ? 1 : 0);
}

main().catch(err => { console.error('Fatal:', err); process.exit(2); });
