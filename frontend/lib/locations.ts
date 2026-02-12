// ══════════════════════════════════════════════════════════
//  CROATIAN LOCATION HIERARCHY
//  County → City/Municipality → Settlement (Naselje)
// ══════════════════════════════════════════════════════════

export interface Settlement {
  name: string;
}

export interface CityMunicipality {
  name: string;
  settlements: string[];
}

export interface County {
  name: string;
  cities: CityMunicipality[];
}

export const LOCATIONS: County[] = [
  // ── Grad Zagreb ──
  {
    name: "Grad Zagreb",
    cities: [
      {
        name: "Zagreb",
        settlements: [
          "Centar", "Črnomerec", "Dubrava", "Gornja Dubrava", "Donja Dubrava",
          "Maksimir", "Medveščak", "Novi Zagreb - istok", "Novi Zagreb - zapad",
          "Peščenica - Žitnjak", "Podsljeme", "Podsused - Vrapče",
          "Sesvete", "Stenjevec", "Trešnjevka - sjever", "Trešnjevka - jug",
          "Trnje", "Brezovica", "Buzin", "Lučko", "Stupnik",
          "Velika Gorica okolica", "Jankomir", "Rudeš", "Špansko",
          "Voltino", "Gajnice", "Malešnica", "Jarun", "Prečko",
          "Sloboština", "Sopot", "Savski Gaj", "Travno", "Utrina",
          "Zapruđe", "Folnegovićevo", "Volovčica", "Borovje",
          "Kozari Bok", "Klara", "Sveta Klara",
        ],
      },
    ],
  },

  // ── Splitsko-dalmatinska ──
  {
    name: "Splitsko-dalmatinska županija",
    cities: [
      {
        name: "Split",
        settlements: [
          "Bačvice", "Bol", "Brda", "Duilovo", "Firule", "Gripe",
          "Kman", "Kocunar", "Lokve", "Lovret", "Lučac", "Manuš",
          "Meje", "Mertojak", "Neslanovac", "Plokite", "Pujanke",
          "Ravne njive", "Sirobuja", "Skalice", "Smokvin",
          "Spinut", "Split 3", "Sućidar", "Šine", "Trstenik",
          "Varoš", "Visoka", "Žnjan", "Stobreč", "Kamen",
        ],
      },
      {
        name: "Kaštela",
        settlements: [
          "Kaštel Gomilica", "Kaštel Kambelovac", "Kaštel Lukšić",
          "Kaštel Novi", "Kaštel Stari", "Kaštel Sućurac", "Kaštel Štafilić",
        ],
      },
      {
        name: "Solin",
        settlements: [
          "Solin", "Kučine", "Mravince", "Vranjic", "Japirko",
        ],
      },
      {
        name: "Trogir",
        settlements: ["Trogir", "Čiovo", "Arbanija", "Okrug Gornji", "Okrug Donji"],
      },
      {
        name: "Omiš",
        settlements: ["Omiš", "Dugi Rat", "Jesenice", "Lokva Rogoznica", "Mimice", "Pisak"],
      },
      {
        name: "Makarska",
        settlements: [
          "Makarska", "Baska Voda", "Brela", "Gradac", "Podgora",
          "Tučepi", "Živogošće",
        ],
      },
      {
        name: "Sinj",
        settlements: ["Sinj", "Trilj", "Dicmo", "Hrvace", "Otok"],
      },
      {
        name: "Supetar",
        settlements: ["Supetar", "Bol", "Milna", "Nerežišća", "Pučišća", "Selca", "Sutivan"],
      },
      {
        name: "Hvar",
        settlements: ["Hvar", "Jelsa", "Stari Grad", "Vrboska", "Sućuraj"],
      },
      {
        name: "Vis",
        settlements: ["Vis", "Komiža"],
      },
      {
        name: "Imotski",
        settlements: ["Imotski", "Zmijavci", "Glavina Donja", "Vinjani Donji", "Runovići"],
      },
    ],
  },

  // ── Primorsko-goranska ──
  {
    name: "Primorsko-goranska županija",
    cities: [
      {
        name: "Rijeka",
        settlements: [
          "Centar", "Belveder", "Bulevard", "Drenova", "Gornja Vežica",
          "Kantrida", "Kozala", "Krnjevo", "Krimeja", "Martinkovac",
          "Mlaka", "Pašac", "Pećine", "Podmurvice", "Srdoči",
          "Sušak", "Škurinje", "Škurinjsko Plase", "Trsat", "Turnić",
          "Zamet", "Banderovo", "Vežica",
        ],
      },
      {
        name: "Opatija",
        settlements: ["Opatija", "Ičići", "Ika", "Lovran", "Medveja", "Volosko", "Pobri"],
      },
      {
        name: "Crikvenica",
        settlements: ["Crikvenica", "Dramalj", "Jadranovo", "Selce"],
      },
      {
        name: "Krk",
        settlements: ["Krk", "Baška", "Malinska", "Njivice", "Omišalj", "Punat", "Vrbnik"],
      },
      {
        name: "Rab",
        settlements: ["Rab", "Banjol", "Barbat", "Kampor", "Lopar", "Supetarska Draga"],
      },
      {
        name: "Kastav",
        settlements: ["Kastav", "Rubeši", "Brnčići"],
      },
      {
        name: "Matulji",
        settlements: ["Matulji", "Jušići", "Rukavac"],
      },
      {
        name: "Delnice",
        settlements: ["Delnice", "Brod na Kupi", "Crni Lug"],
      },
    ],
  },

  // ── Osječko-baranjska ──
  {
    name: "Osječko-baranjska županija",
    cities: [
      {
        name: "Osijek",
        settlements: [
          "Centar", "Gornji grad", "Donji grad", "Tvrđa", "Retfala",
          "Jug II", "Sjenjak", "Vijenac Slavka Kolara",
          "Višnjevac", "Josipovac", "Briješće", "Nemetin",
          "Sarvaš", "Tenja", "Klisa", "Brijest",
        ],
      },
      {
        name: "Đakovo",
        settlements: ["Đakovo", "Budrovci", "Đurđanci", "Kuševac", "Piškorevci", "Semeljci", "Strizivojna"],
      },
      {
        name: "Beli Manastir",
        settlements: ["Beli Manastir", "Darda", "Bilje", "Kneževi Vinogradi", "Popovac"],
      },
      {
        name: "Belišće",
        settlements: ["Belišće", "Valpovo", "Petrijevci"],
      },
      {
        name: "Donji Miholjac",
        settlements: ["Donji Miholjac", "Podravska Moslavina", "Marijanci"],
      },
      {
        name: "Našice",
        settlements: ["Našice", "Đurđenovac", "Koška", "Podgorač"],
      },
    ],
  },

  // ── Bjelovarsko-bilogorska ──
  {
    name: "Bjelovarsko-bilogorska županija",
    cities: [
      {
        name: "Daruvar",
        settlements: [
          "Daruvar", "Daruvarski Brestovac", "Doljani", "Ljudevit Selo",
          "Vrbovac", "Daruvarski Vinogradi", "Lipovac Majur",
          "Prekopakra", "Batinjani", "Končanica", "Sirač",
          "Đulovac", "Uljanik",
        ],
      },
      {
        name: "Bjelovar",
        settlements: [
          "Bjelovar", "Gudovac", "Klokočevac", "Kokinac",
          "Nove Plavnice", "Stare Plavnice", "Prgomelje",
          "Prespa", "Ždralovi",
        ],
      },
      {
        name: "Garešnica",
        settlements: ["Garešnica", "Kaniška Iva", "Tomašica", "Veliki Prokop"],
      },
      {
        name: "Čazma",
        settlements: ["Čazma", "Dapci", "Grabovnica", "Sišćani", "Vrtlinska"],
      },
      {
        name: "Grubišno Polje",
        settlements: ["Grubišno Polje", "Ivanovo Selo", "Velika Dapčevica", "Velika Peratovica"],
      },
    ],
  },

  // ── Zagrebačka županija ──
  {
    name: "Zagrebačka županija",
    cities: [
      {
        name: "Velika Gorica",
        settlements: [
          "Velika Gorica", "Petrovina", "Kuče", "Mraclin",
          "Bukevje", "Novo Čiče", "Vukovina",
        ],
      },
      {
        name: "Samobor",
        settlements: ["Samobor", "Bregana", "Mala Rakovica", "Rakov Potok", "Sveta Nedelja"],
      },
      {
        name: "Zaprešić",
        settlements: ["Zaprešić", "Bistra", "Brdovec", "Dubravica", "Luka", "Marija Gorica"],
      },
      {
        name: "Dugo Selo",
        settlements: ["Dugo Selo", "Brckovljani", "Rugvica"],
      },
      {
        name: "Ivanić-Grad",
        settlements: ["Ivanić-Grad", "Kloštar Ivanić", "Križ", "Novoselec"],
      },
      {
        name: "Jastrebarsko",
        settlements: ["Jastrebarsko", "Pisarovina", "Krašić"],
      },
      {
        name: "Vrbovec",
        settlements: ["Vrbovec", "Dubrava", "Gradec", "Preseka", "Rakovec", "Farkaševac"],
      },
    ],
  },

  // ── Istarska županija ──
  {
    name: "Istarska županija",
    cities: [
      {
        name: "Pula",
        settlements: [
          "Pula", "Medulin", "Ližnjan", "Marčana",
          "Vodnjan", "Fažana", "Šišan", "Premantura",
          "Štinjan", "Verudela", "Veruda", "Stoja",
        ],
      },
      {
        name: "Rovinj",
        settlements: ["Rovinj", "Bale", "Kanfanar", "Rovinjsko Selo"],
      },
      {
        name: "Poreč",
        settlements: ["Poreč", "Tar", "Vabriga", "Funtana", "Vrsar", "Kaštelir"],
      },
      {
        name: "Umag",
        settlements: ["Umag", "Novigrad", "Buje", "Brtonigla", "Grožnjan", "Oprtalj"],
      },
      {
        name: "Labin",
        settlements: ["Labin", "Rabac", "Raša", "Sveta Nedelja", "Kršan"],
      },
      {
        name: "Pazin",
        settlements: ["Pazin", "Motovun", "Buzet", "Cerovlje", "Lupoglav", "Tinjan"],
      },
    ],
  },

  // ── Dubrovačko-neretvanska ──
  {
    name: "Dubrovačko-neretvanska županija",
    cities: [
      {
        name: "Dubrovnik",
        settlements: [
          "Dubrovnik", "Lapad", "Gruž", "Babin Kuk", "Mokošica",
          "Župa Dubrovačka", "Srebreno", "Mlini", "Plat",
          "Cavtat", "Čilipi",
        ],
      },
      {
        name: "Metković",
        settlements: ["Metković", "Opuzen", "Ploče", "Kula Norinska"],
      },
      {
        name: "Korčula",
        settlements: ["Korčula", "Vela Luka", "Blato", "Lumbarda", "Smokvica"],
      },
    ],
  },

  // ── Zadarska županija ──
  {
    name: "Zadarska županija",
    cities: [
      {
        name: "Zadar",
        settlements: [
          "Zadar", "Arbanasi", "Bili Brig", "Bokanjac", "Brodarica",
          "Crno", "Diklo", "Dračevac", "Gaženica", "Jazine",
          "Kolovare", "Poluotok", "Puntamika", "Relja",
          "Stanovi", "Višnjik", "Voštarnica",
          "Nin", "Privlaka", "Vir", "Sukošan",
        ],
      },
      {
        name: "Biograd na Moru",
        settlements: ["Biograd na Moru", "Sv. Filip i Jakov", "Pakoštane", "Turanj"],
      },
      {
        name: "Benkovac",
        settlements: ["Benkovac", "Lišane Ostrovičke", "Pristeg"],
      },
      {
        name: "Pag",
        settlements: ["Pag", "Novalja", "Kolan", "Povljana"],
      },
    ],
  },

  // ── Šibensko-kninska ──
  {
    name: "Šibensko-kninska županija",
    cities: [
      {
        name: "Šibenik",
        settlements: [
          "Šibenik", "Brodarica", "Crnica", "Jadrija",
          "Martinska", "Raslina", "Solaris", "Vodice",
          "Tribunj", "Pirovac", "Primošten",
        ],
      },
      {
        name: "Knin",
        settlements: ["Knin", "Biskupija", "Civljane", "Kistanje"],
      },
      {
        name: "Drniš",
        settlements: ["Drniš", "Oklaj", "Promina", "Ružić", "Unešić"],
      },
    ],
  },

  // ── Vukovarsko-srijemska ──
  {
    name: "Vukovarsko-srijemska županija",
    cities: [
      {
        name: "Vukovar",
        settlements: ["Vukovar", "Borovo", "Trpinja", "Bogdanovci", "Negoslavci"],
      },
      {
        name: "Vinkovci",
        settlements: ["Vinkovci", "Ivankovo", "Jarmina", "Nuštar", "Stari Mikanovci", "Rokovci"],
      },
      {
        name: "Županja",
        settlements: ["Županja", "Babina Greda", "Bošnjaci", "Cerna", "Gradište", "Gunja"],
      },
      {
        name: "Ilok",
        settlements: ["Ilok", "Lovas", "Šarengrad", "Tovarnik"],
      },
    ],
  },

  // ── Varaždinska županija ──
  {
    name: "Varaždinska županija",
    cities: [
      {
        name: "Varaždin",
        settlements: [
          "Varaždin", "Črnec Biškupečki", "Jalkovec", "Kućan Marof",
          "Nedeljanec", "Sračinec", "Sveti Ilija", "Trnovec Bartolovečki",
          "Vidovec", "Vinica",
        ],
      },
      {
        name: "Ivanec",
        settlements: ["Ivanec", "Bednja", "Klenovnik", "Lepoglava", "Maruševec"],
      },
      {
        name: "Ludbreg",
        settlements: ["Ludbreg", "Mali Bukovec", "Sveti Đurđ", "Veliki Bukovec"],
      },
      {
        name: "Novi Marof",
        settlements: ["Novi Marof", "Breznički Hum", "Visoko"],
      },
    ],
  },

  // ── Karlovačka županija ──
  {
    name: "Karlovačka županija",
    cities: [
      {
        name: "Karlovac",
        settlements: [
          "Karlovac", "Duga Resa", "Ozalj", "Draganić",
          "Generalski Stol", "Kamanje", "Netretić", "Ribnik",
        ],
      },
      {
        name: "Ogulin",
        settlements: ["Ogulin", "Josipdol", "Plaški", "Saborsko", "Tounj"],
      },
      {
        name: "Slunj",
        settlements: ["Slunj", "Cetingrad", "Rakovica"],
      },
    ],
  },

  // ── Sisačko-moslavačka ──
  {
    name: "Sisačko-moslavačka županija",
    cities: [
      {
        name: "Sisak",
        settlements: [
          "Sisak", "Caprag", "Galdovo", "Greda",
          "Letovanić", "Martinska Ves", "Odra", "Topolovac",
        ],
      },
      {
        name: "Kutina",
        settlements: ["Kutina", "Lipovljani", "Novska", "Popovača"],
      },
      {
        name: "Petrinja",
        settlements: ["Petrinja", "Glina", "Topusko", "Gvozd", "Hrvatska Kostajnica"],
      },
    ],
  },

  // ── Koprivničko-križevačka ──
  {
    name: "Koprivničko-križevačka županija",
    cities: [
      {
        name: "Koprivnica",
        settlements: [
          "Koprivnica", "Đelekovec", "Drnje", "Hlebine",
          "Koprivnički Bregi", "Koprivnički Ivanec", "Legrad",
          "Novigrad Podravski", "Peteranec", "Rasinja", "Sokolovac",
        ],
      },
      {
        name: "Križevci",
        settlements: ["Križevci", "Gornja Rijeka", "Kalnik", "Sveti Ivan Žabno", "Sveti Petar Orehovec"],
      },
      {
        name: "Đurđevac",
        settlements: ["Đurđevac", "Ferdinandovac", "Kalinovac", "Kloštar Podravski", "Molve", "Virje"],
      },
    ],
  },

  // ── Međimurska županija ──
  {
    name: "Međimurska županija",
    cities: [
      {
        name: "Čakovec",
        settlements: [
          "Čakovec", "Ivanovec", "Kuršanec", "Mačkovec",
          "Mihovljan", "Nedelišće", "Pribislavec", "Savska Ves",
          "Šenkovec", "Strahoninec",
        ],
      },
      {
        name: "Mursko Središće",
        settlements: ["Mursko Središće", "Sveti Martin na Muri", "Podturen"],
      },
      {
        name: "Prelog",
        settlements: ["Prelog", "Belica", "Dekanovec", "Domašinec", "Goričan", "Kotoriba"],
      },
    ],
  },

  // ── Požeško-slavonska ──
  {
    name: "Požeško-slavonska županija",
    cities: [
      {
        name: "Požega",
        settlements: ["Požega", "Brestovac", "Jakšić", "Kaptol", "Kutjevo", "Velika"],
      },
      {
        name: "Pakrac",
        settlements: ["Pakrac", "Lipik", "Prekopakra", "Badljevina"],
      },
      {
        name: "Pleternica",
        settlements: ["Pleternica", "Buk", "Gradac", "Kuzmanovac"],
      },
    ],
  },

  // ── Brodsko-posavska ──
  {
    name: "Brodsko-posavska županija",
    cities: [
      {
        name: "Slavonski Brod",
        settlements: [
          "Slavonski Brod", "Brodski Stupnik", "Bukovlje",
          "Garčin", "Gornja Vrba", "Klakar", "Podcrkavlje",
          "Sibinj", "Slavonski Šamac",
        ],
      },
      {
        name: "Nova Gradiška",
        settlements: ["Nova Gradiška", "Cernik", "Okučani", "Rešetari", "Stara Gradiška", "Staro Petrovo Selo"],
      },
    ],
  },

  // ── Virovitičko-podravska ──
  {
    name: "Virovitičko-podravska županija",
    cities: [
      {
        name: "Virovitica",
        settlements: ["Virovitica", "Lukač", "Špišić Bukovica", "Gradina", "Suhopolje"],
      },
      {
        name: "Slatina",
        settlements: ["Slatina", "Mikleuš", "Nova Bukovica", "Sopje", "Čačinci", "Voćin"],
      },
      {
        name: "Orahovica",
        settlements: ["Orahovica", "Čačinci", "Crnac", "Zdenci"],
      },
    ],
  },

  // ── Ličko-senjska ──
  {
    name: "Ličko-senjska županija",
    cities: [
      {
        name: "Gospić",
        settlements: ["Gospić", "Lički Osik", "Perušić", "Brinje"],
      },
      {
        name: "Senj",
        settlements: ["Senj", "Vratnik", "Sveti Juraj"],
      },
      {
        name: "Otočac",
        settlements: ["Otočac", "Vrhovine", "Ličko Lešće"],
      },
      {
        name: "Novalja",
        settlements: ["Novalja", "Stara Novalja", "Zubovići", "Caska"],
      },
    ],
  },

  // ── Krapinsko-zagorska ──
  {
    name: "Krapinsko-zagorska županija",
    cities: [
      {
        name: "Krapina",
        settlements: ["Krapina", "Đurmanec", "Jesenje", "Petrovsko", "Radoboj"],
      },
      {
        name: "Zabok",
        settlements: ["Zabok", "Bedekovčina", "Sveti Križ Začretje", "Zlatar", "Zlatar Bistrica"],
      },
      {
        name: "Pregrada",
        settlements: ["Pregrada", "Desinić", "Hum na Sutli", "Kumrovec", "Tuhelj"],
      },
      {
        name: "Klanjec",
        settlements: ["Klanjec", "Kraljevec na Sutli", "Zagorska Sela"],
      },
      {
        name: "Donja Stubica",
        settlements: ["Donja Stubica", "Gornja Stubica", "Marija Bistrica", "Oroslavje", "Stubičke Toplice"],
      },
    ],
  },
];

// ══════════════════════════════════════════════════════════
//  HELPER: Flat list of all cities (for search)
// ══════════════════════════════════════════════════════════

export interface FlatCity {
  city: string;
  county: string;
  settlements: string[];
}

let _flatCitiesCache: FlatCity[] | null = null;

export function getFlatCities(): FlatCity[] {
  if (_flatCitiesCache) return _flatCitiesCache;
  const flat: FlatCity[] = [];
  for (const county of LOCATIONS) {
    for (const city of county.cities) {
      flat.push({
        city: city.name,
        county: county.name,
        settlements: city.settlements,
      });
    }
  }
  _flatCitiesCache = flat;
  return flat;
}

export function getSettlements(cityName: string): string[] {
  const flat = getFlatCities();
  const match = flat.find(
    (c) => c.city.toLowerCase() === cityName.toLowerCase()
  );
  return match?.settlements ?? [];
}
