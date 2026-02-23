import type { ReactNode } from "react";
import {
  Search,
  Brain,
  FileSearch,
  Cpu,
  ListChecks,
  Save,
  Eye,
  Sparkles,
} from "lucide-react";
import type {
  PropertyCondition,
  HouseFloors,
  LandUse,
  Orientation,
  ViewType,
  HeatingType,
  EnergyClass,
  SewageType,
  LandCategory,
  LocationType,
  StreamStage,
} from "./types";

// ═══════════════════════════════════════════
//  INITIAL SSE STAGES
// ═══════════════════════════════════════════

export const INITIAL_STAGES: StreamStage[] = [
  { id: "reading", label: "Pripremam podatke za procjenu...", status: "pending" },
  { id: "searching", label: "Pretražujem Njuškalo i tržište nekretnina...", status: "pending" },
  { id: "ai_features", label: "Analiziram značajke oglasa...", status: "pending" },
  { id: "sherlock", label: "Dubinska analiza tekstova oglasa...", status: "pending" },
  { id: "engine", label: "Engine izračunava tržišnu vrijednost...", status: "pending" },
  { id: "agent", label: "Analitički modul procjenjuje nekretninu...", status: "pending" },
  { id: "ai_opinion", label: "Generiram stručno mišljenje...", status: "pending" },
  { id: "saving", label: "Spremam rezultate...", status: "pending" },
];

// ═══════════════════════════════════════════
//  LABEL MAPS
// ═══════════════════════════════════════════

export const CONDITION_LABELS: Record<PropertyCondition, string> = {
  New: "Novogradnja",
  Renovated: "Renovirano",
  Good: "Dobro stanje",
  Original: "Izvorno stanje",
  NeedsWork: "Potrebna adaptacija",
};

export const FLOORS_LABELS: Record<HouseFloors, string> = {
  OneStory: "Prizemnica",
  TwoStory: "Katnica",
  MultiStory: "Višekatnica",
};

export const LAND_USE_LABELS: Record<LandUse, string> = {
  Construction: "Građevinsko",
  Agricultural: "Poljoprivredno",
  Mixed: "Mješovito",
};

export const ORIENTATION_LABELS: Record<Orientation, string> = {
  Unknown: "Nepoznato",
  North: "Sjever",
  NorthEast: "Sjeveroistok",
  East: "Istok",
  SouthEast: "Jugoistok",
  South: "Jug",
  SouthWest: "Jugozapad",
  West: "Zapad",
  NorthWest: "Sjeverozapad",
};

export const VIEW_TYPE_LABELS: Record<ViewType, string> = {
  Unknown: "Nepoznato",
  Street: "Ulica",
  Courtyard: "Dvorište",
  Park: "Park",
  City: "Grad",
  Sea: "More",
  Panoramic: "Panoramski",
};

// ═══════════════════════════════════════════
//  V40: ELABORAT-STYLE LABEL MAPS
// ═══════════════════════════════════════════

export const HEATING_TYPE_LABELS: Record<HeatingType, string> = {
  Unknown: "Nepoznato",
  CentralHeating: "Centralno grijanje",
  Gas: "Plin",
  Wood: "Drva / čvrsto gorivo",
  Electric: "Električno",
  HeatPump: "Dizalica topline",
  FloorHeating: "Podno grijanje",
  None: "Bez grijanja",
};

export const ENERGY_CLASS_LABELS: Record<EnergyClass, string> = {
  Unknown: "Nepoznato",
  APlus: "A+",
  A: "A",
  B: "B",
  C: "C",
  D: "D",
  E: "E",
  F: "F",
  G: "G",
};

export const SEWAGE_TYPE_LABELS: Record<SewageType, string> = {
  Unknown: "Nepoznato",
  Public: "Javna kanalizacija",
  SepticTank: "Septička jama",
  None: "Bez kanalizacije",
};

export const LAND_CATEGORY_LABELS: Record<LandCategory, string> = {
  Unknown: "Nepoznato",
  Ktg1: "1. KTG — najpovoljnija",
  Ktg2: "2. KTG",
  Ktg3: "3. KTG",
  Ktg4_1: "4.1. KTG — oranice, vrtovi",
  Ktg4_2: "4.2. KTG — livade, pašnjaci",
  Ktg5: "5. KTG — šume",
  Ktg6: "6. KTG — neplodno",
};

export const LOCATION_TYPE_LABELS: Record<LocationType, string> = {
  Unknown: "Nepoznato",
  CityCenter: "A — Centar grada",
  UrbanResidential: "B — Urbano stanovanje",
  Suburban: "C — Predgrađe",
  SmallTown: "D — Manji grad / Općina",
  Rural: "E — Ruralno / Selo",
};

// ═══════════════════════════════════════════
//  STAGE ICONS & MICRO-COPY
// ═══════════════════════════════════════════

export const STAGE_ICONS: Record<string, ReactNode> = {
  reading: <FileSearch className="h-5 w-5" />,
  searching: <Search className="h-5 w-5" />,
  ai_features: <Brain className="h-5 w-5" />,
  sherlock: <Eye className="h-5 w-5" />,
  engine: <Cpu className="h-5 w-5" />,
  agent: <Sparkles className="h-5 w-5" />,
  ai_opinion: <ListChecks className="h-5 w-5" />,
  saving: <Save className="h-5 w-5" />,
};

export const STAGE_MICROCOPY: Record<string, string[]> = {
  reading: [
    "Učitavam parametre nekretnine...",
    "Provjeravam lokaciju i vrstu...",
    "Parsiram ključne značajke...",
    "Pripremam podatke za analitički modul...",
  ],
  searching: [
    "Spajam se na izvore podataka...",
    "Pretražujem Njuškalo oglase...",
    "Skeniram Index nekretnine...",
    "Filtriram relevantne oglašivače...",
    "Prikupljam cijene za usporedbu...",
  ],
  ai_features: [
    "Analiziram kvalitetu oglasa...",
    "Ekstrahiram značajke nekretnina...",
    "Detektiram renovacije i opremu...",
    "Klasificiram stanja objekata...",
  ],
  sherlock: [
    "Čitam opise oglasa...",
    "Detektiram hitnost prodaje...",
    "Analiziram tip zemljišta...",
    "Provjeravam konzistentnost podataka...",
  ],
  engine: [
    "Računam baznu cijenu po m²...",
    "Primjenjujem korekcijske faktore...",
    "Uklanjam statističke outliere...",
    "Izračunavam diskont tražene cijene...",
    "Validam pouzdanost procjene...",
  ],
  agent: [
    "Analiziram kontekst nekretnine...",
    "Provjeravam pravne specifičnosti...",
    "Uspoređujem s benchmarkom...",
    "Primjenjujem kontekstualne korekcije...",
  ],
  ai_opinion: [
    "Generiram stručno mišljenje...",
    "Analiziram tržišne trendove...",
    "Uspoređujem s lokalnim podacima...",
    "Formiram završnu ocjenu...",
  ],
  saving: [
    "Spremam rezultate u bazu...",
    "Finaliziram izvještaj...",
  ],
};

// ═══════════════════════════════════════════
//  UTILITIES
// ═══════════════════════════════════════════

export const floorNumericValue = (f: string): number => {
  const m = f.match(/^Floor(\d+)$/);
  if (m) return parseInt(m[1]);
  return ({ Subbasement: -2, SemiBasement: -1, Ground: 0, Attic: 100, Penthouse: 101 } as Record<string, number>)[f] ?? 0;
};

export const formatPrice = (n: number | null | undefined) =>
  n != null && Number.isFinite(n) ? n.toLocaleString("hr-HR") : "—";
