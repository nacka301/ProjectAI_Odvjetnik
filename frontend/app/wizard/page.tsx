"use client";

import { useState, useEffect, useRef } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  ArrowRight,
  Home,
  Building2,
  TreePine,
  Loader2,
  AlertTriangle,
  Check,
  Scale,
  Search,
  Brain,
  FileCheck,
  ExternalLink,
  ClipboardCopy,
  ChevronDown,
  ChevronUp,
  FileSearch,
  Cpu,
  Bot,
  Save,
} from "lucide-react";
import { saveEvaluation } from "@/lib/supabase";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";

// --- Types mirroring backend enums ---

type PropertyType = "Apartment" | "House" | "Land";
type PropertyCondition = "New" | "Renovated" | "Good" | "Original" | "NeedsWork";
type HouseFloors = "OneStory" | "TwoStory" | "MultiStory";
type LandUse = "Construction" | "Agricultural" | "Mixed";
type ViewState = "INPUT" | "PROCESSING" | "RESULT";

interface LocationInfo {
  city: string;
  district: string;
}

interface ApartmentData {
  area_m2: number;
  room_count: number;
  floor: string;
  total_floors: number;
  condition: PropertyCondition;
  year_built?: number;
  distance_to_sea_m?: number;
  has_elevator: boolean;
  has_parking: boolean;
  has_balcony: boolean;
  sea_view: boolean;
}

interface HouseData {
  living_area_m2: number;
  land_area_m2: number;
  floors: HouseFloors;
  condition: PropertyCondition;
  year_built?: number;
  distance_to_sea_m?: number;
  has_garage: boolean;
  has_pool: boolean;
  first_row_to_sea: boolean;
  has_auxiliary_object: boolean;
}

interface LandData {
  land_area_m2: number;
  land_use: LandUse;
  distance_to_sea_m?: number;
  has_access_road: boolean;
  has_infrastructure: boolean;
  first_row_to_sea: boolean;
  is_legalized: boolean;
}

interface ValidationError {
  field: string;
  message: string;
}

interface DraftResponse {
  valid: boolean;
  errors?: ValidationError[];
  warnings?: ValidationError[];
  description: string;
  ready_for_valuation: boolean;
}

// SSE types (same as /valuation page)
interface StreamStage {
  id: string;
  label: string;
  status: "pending" | "active" | "done" | "skipped" | "error";
  detail?: string;
  error?: string;
}

interface SelectedComparable {
  title: string;
  price: string;
  location: string;
  url: string;
  reason_for_selection: string;
}

interface ValueDriver {
  factor: string;
  impact: string;
  adjustment_percent: number;
}

interface ValuationResult {
  target_property_summary: string;
  selected_comparables: SelectedComparable[];
  estimated_value_eur: number;
  value_drivers: ValueDriver[];
  valuation_reasoning: string;
  confidence_level?: string;
  calculated_price_per_m2?: number;
  total_listings_found?: number;
  usable_price_points?: number;
  outliers_removed?: number;
  realization_discount_percent?: number;
  math_confidence_score?: number;
  math_confidence_level?: string;
}

const INITIAL_STAGES: StreamStage[] = [
  { id: "reading", label: "Pripremam podatke za procjenu...", status: "pending" },
  { id: "searching", label: "Pretražujem Njuškalo i tržište nekretnina...", status: "pending" },
  { id: "ai_features", label: "AI analizira značajke oglasa...", status: "pending" },
  { id: "engine", label: "Engine izračunava tržišnu vrijednost...", status: "pending" },
  { id: "ai_opinion", label: "AI generira stručno mišljenje...", status: "pending" },
  { id: "saving", label: "Spremam rezultate...", status: "pending" },
];

const CONDITION_LABELS: Record<PropertyCondition, string> = {
  New: "Novogradnja",
  Renovated: "Renovirano",
  Good: "Dobro stanje",
  Original: "Izvorno stanje",
  NeedsWork: "Potrebna adaptacija",
};

const FLOORS_LABELS: Record<HouseFloors, string> = {
  OneStory: "Prizemnica",
  TwoStory: "Katnica",
  MultiStory: "Višekatnica",
};

const LAND_USE_LABELS: Record<LandUse, string> = {
  Construction: "Građevinsko",
  Agricultural: "Poljoprivredno",
  Mixed: "Mješovito",
};

// Stage icons for PROCESSING view
const STAGE_ICONS: Record<string, React.ReactNode> = {
  reading: <FileSearch className="h-5 w-5" />,
  searching: <Search className="h-5 w-5" />,
  ai_features: <Brain className="h-5 w-5" />,
  engine: <Cpu className="h-5 w-5" />,
  ai_opinion: <Bot className="h-5 w-5" />,
  saving: <Save className="h-5 w-5" />,
};

// Dynamic micro-copy per stage
const STAGE_MICROCOPY: Record<string, string[]> = {
  reading: [
    "Učitavam parametre nekretnine...",
    "Provjeravam lokaciju i vrstu...",
    "Parsiram ključne značajke...",
    "Pripremam podatke za AI model...",
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
  engine: [
    "Računam baznu cijenu po m²...",
    "Primjenjujem korekcijske faktore...",
    "Uklanjam statističke outliere...",
    "Izračunavam diskont tražene cijene...",
    "Validam pouzdanost procjene...",
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

const FLOOR_OPTIONS = [
  { value: "Subbasement", label: "Podrum" },
  { value: "SemiBasement", label: "Suteren" },
  { value: "Ground", label: "Prizemlje" },
  ...Array.from({ length: 30 }, (_, i) => ({
    value: `Floor${i + 1}`,
    label: `${i + 1}. kat`,
  })),
  { value: "Attic", label: "Potkrovlje" },
  { value: "Penthouse", label: "Penthouse" },
];

const floorNumericValue = (f: string): number => {
  const m = f.match(/^Floor(\d+)$/);
  if (m) return parseInt(m[1]);
  return ({ Subbasement: -2, SemiBasement: -1, Ground: 0, Attic: 100, Penthouse: 101 } as Record<string, number>)[f] ?? 0;
};

// ═══════════════════════════════════════════
//  MAIN COMPONENT
// ═══════════════════════════════════════════

export default function WizardPage() {
  // Wizard state
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [propertyType, setPropertyType] = useState<PropertyType | null>(null);

  // Location
  const [location, setLocation] = useState<LocationInfo>({ city: "", district: "" });

  // Type-specific data
  const [apt, setApt] = useState<ApartmentData>({
    area_m2: 0, room_count: 2, floor: "Ground", total_floors: 4, condition: "Good",
    has_elevator: false, has_parking: false, has_balcony: false, sea_view: false,
  });
  const [house, setHouse] = useState<HouseData>({
    living_area_m2: 0, land_area_m2: 0, floors: "OneStory", condition: "Good",
    has_garage: false, has_pool: false, first_row_to_sea: false, has_auxiliary_object: false,
  });
  const [land, setLand] = useState<LandData>({
    land_area_m2: 0, land_use: "Construction",
    has_access_road: false, has_infrastructure: false, first_row_to_sea: false, is_legalized: true,
  });

  // Draft validation
  const [draftLoading, setDraftLoading] = useState(false);
  const [draftResult, setDraftResult] = useState<DraftResponse | null>(null);
  const [draftErrors, setDraftErrors] = useState<ValidationError[]>([]);
  const [draftWarnings, setDraftWarnings] = useState<ValidationError[]>([]);

  // Valuation (SSE)
  const [stages, setStages] = useState<StreamStage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [result, setResult] = useState<ValuationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [email, setEmail] = useState("");
  const [notes, setNotes] = useState("");

  // View state machine: INPUT → PROCESSING → RESULT
  const [viewState, setViewState] = useState<ViewState>("INPUT");

  // Dynamic micro-copy cycling for active stage
  const [microCopy, setMicroCopy] = useState("");
  const microCopyIdx = useRef(0);

  useEffect(() => {
    if (viewState !== "PROCESSING") return;
    const activeStage = stages.find((s) => s.status === "active");
    if (!activeStage) { setMicroCopy(""); return; }

    const copies = STAGE_MICROCOPY[activeStage.id] || [];
    if (copies.length === 0) { setMicroCopy(""); return; }

    microCopyIdx.current = 0;
    setMicroCopy(copies[0]);

    const interval = setInterval(() => {
      microCopyIdx.current = (microCopyIdx.current + 1) % copies.length;
      setMicroCopy(copies[microCopyIdx.current]);
    }, 2500);

    return () => clearInterval(interval);
  }, [viewState, stages]);

  // ── Build JSON payload ──

  const buildPayload = () => {
    const base = { location, property_type: propertyType, notes: notes.trim() || undefined };
    if (propertyType === "Apartment") return { type: "Apartment", ...base, ...apt };
    if (propertyType === "House") return { type: "House", ...base, ...house };
    if (propertyType === "Land") return { type: "Land", ...base, ...land };
    return base;
  };

  // ── Step 2 → Validate draft ──

  const validateDraft = async () => {
    setDraftLoading(true);
    setDraftErrors([]);
    setDraftWarnings([]);
    setDraftResult(null);
    setError(null);

    try {
      const res = await fetch(`${API_BASE}/api/valuation/draft`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(buildPayload()),
      });

      const data = await res.json();

      if (!res.ok || data.valid === false) {
        setDraftErrors(data.errors || [{ field: "", message: "Nepoznata greška" }]);
        setDraftWarnings(data.warnings || []);
      } else {
        setDraftResult(data);
        setDraftWarnings(data.warnings || []);
        setStep(3);
      }
    } catch {
      setError("Server ne odgovara. Pokušajte ponovno.");
    } finally {
      setDraftLoading(false);
    }
  };

  // ── Step 3 → Run valuation (SSE) ──

  const runValuation = async () => {
    if (!draftResult) return;

    setError(null);
    setResult(null);
    setStages(INITIAL_STAGES.map((s) => ({ ...s })));
    setIsStreaming(true);
    setViewState("PROCESSING");

    let streamedResult: ValuationResult | null = null;

    try {
      // Send the description as text to the text/stream endpoint
      const url = `${API_BASE}/api/evaluate/text/stream`;
      const body = JSON.stringify({
        text: draftResult.description,
        city: location.city || undefined,
        neighborhood: location.district || undefined,
        email: email.trim() || undefined,
      });

      const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body,
      });

      if (!response.ok || !response.body) throw new Error(`HTTP ${response.status}`);

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const parts = buffer.split("\n\n");
        buffer = parts.pop()!;

        for (const part of parts) {
          const line = part.trim();
          if (!line.startsWith("data: ")) continue;
          try {
            const evt = JSON.parse(line.slice(6));
            if (evt.type === "progress") {
              const { stage, status, label, detail } = evt.data;
              setStages((prev) =>
                prev.map((s) =>
                  s.id === stage ? { ...s, status, label: label || s.label, detail: detail ?? s.detail } : s
                )
              );
            } else if (evt.type === "error") {
              const { stage, message } = evt.data;
              setStages((prev) =>
                prev.map((s) => (s.id === stage ? { ...s, status: "error", error: message } : s))
              );
              setError(message);
            } else if (evt.type === "result") {
              streamedResult = evt.data;
              setResult(evt.data);
            }
          } catch { /* skip */ }
        }
      }
      setIsStreaming(false);
      if (streamedResult) setViewState("RESULT");

      // Save to Supabase (fire-and-forget)
      if (streamedResult) {
        saveEvaluation({
          property_type: propertyType || "nekretnina",
          location: `${location.city}, ${location.district}`,
          estimated_value: streamedResult.estimated_value_eur,
          pdf_url: null,
          raw_json_output: streamedResult as unknown as Record<string, unknown>,
        }).catch((e) => console.warn("Failed to save evaluation:", e));
      }
    } catch {
      setIsStreaming(false);
      setError("Server ne odgovara. Pokušajte ponovno za minutu.");
    }
  };

  // ── Copy reasoning ──

  const copyToClipboard = async (text: string) => {
    try { await navigator.clipboard.writeText(text); } catch {
      const ta = document.createElement("textarea");
      ta.value = text; ta.style.position = "fixed"; ta.style.opacity = "0";
      document.body.appendChild(ta); ta.select(); document.execCommand("copy");
      document.body.removeChild(ta);
    }
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  // ── Reset ──

  const reset = () => {
    setViewState("INPUT");
    setStep(1);
    setPropertyType(null);
    setDraftResult(null);
    setDraftErrors([]);
    setDraftWarnings([]);
    setStages([]);
    setResult(null);
    setError(null);
    setIsStreaming(false);
    setNotes("");
  };

  const formatPrice = (n: number) => n.toLocaleString("hr-HR");

  // ═══════════════════════════════════════════
  //  RENDER
  // ═══════════════════════════════════════════

  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 font-sans">
      <div className="mx-auto max-w-3xl">
        <Link href="/" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6">
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        {/* Header + Step indicator — INPUT only */}
        {viewState === "INPUT" && (
          <>
            <div className="text-center mb-10">
              <h1 className="text-3xl font-bold text-gray-900">
                🏠 Wizard — Unos nekretnine
              </h1>
              <p className="mt-2 text-gray-500">
                Unesite podatke korak po korak — sustav automatski procjenjuje tržišnu vrijednost
              </p>
            </div>

            <div className="flex items-center justify-center gap-2 mb-8">
              {[1, 2, 3].map((s) => (
                <div key={s} className="flex items-center gap-2">
                  <div className={`h-8 w-8 rounded-full flex items-center justify-center text-sm font-bold ${
                    step >= s ? "bg-blue-600 text-white" : "bg-gray-200 text-gray-500"
                  }`}>{s}</div>
                  {s < 3 && <div className={`w-12 h-0.5 ${step > s ? "bg-blue-600" : "bg-gray-200"}`} />}
                </div>
              ))}
            </div>
          </>
        )}

        {/* Error — INPUT only */}
        {viewState === "INPUT" && error && (
          <div className="mb-6 flex items-center gap-2 rounded-lg border border-red-300 bg-red-50 p-4 text-red-700">
            <AlertTriangle className="h-5 w-5 flex-shrink-0" />
            <span className="text-sm">{error}</span>
          </div>
        )}

        {/* ═══ STEP 1: Choose type + Location ═══ */}
        {viewState === "INPUT" && step === 1 && (
          <div className="space-y-6">
            <div className="rounded-xl bg-white border border-gray-200 p-6">
              <h3 className="text-sm font-semibold text-gray-700 mb-4">Tip nekretnine</h3>
              <div className="grid grid-cols-3 gap-4">
                {([
                  { type: "Apartment" as PropertyType, icon: <Building2 className="h-8 w-8" />, label: "Stan" },
                  { type: "House" as PropertyType, icon: <Home className="h-8 w-8" />, label: "Kuća" },
                  { type: "Land" as PropertyType, icon: <TreePine className="h-8 w-8" />, label: "Zemljište" },
                ]).map((opt) => (
                  <button
                    key={opt.type}
                    onClick={() => setPropertyType(opt.type)}
                    className={`flex flex-col items-center gap-2 rounded-xl border-2 p-6 transition ${
                      propertyType === opt.type
                        ? "border-blue-500 bg-blue-50 text-blue-700"
                        : "border-gray-200 text-gray-500 hover:border-gray-300"
                    }`}
                  >
                    {opt.icon}
                    <span className="text-sm font-semibold">{opt.label}</span>
                  </button>
                ))}
              </div>
            </div>

            {propertyType && (
              <>
                <div className="rounded-xl bg-white border border-gray-200 p-6">
                  <h3 className="text-sm font-semibold text-gray-700 mb-4">📍 Lokacija</h3>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div>
                      <label className="block text-xs font-medium text-gray-500 mb-1">Grad *</label>
                      <input type="text" value={location.city}
                        onChange={(e) => setLocation({ ...location, city: e.target.value })}
                        placeholder="npr. Split" className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
                    </div>
                    <div>
                      <label className="block text-xs font-medium text-gray-500 mb-1">Kvart / Naselje *</label>
                      <input type="text" value={location.district}
                        onChange={(e) => setLocation({ ...location, district: e.target.value })}
                        placeholder="npr. Plokite" className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
                    </div>
                  </div>
                </div>

                <div className="flex justify-end">
                  <button onClick={() => setStep(2)}
                    disabled={!location.city.trim() || !location.district.trim()}
                    className={`rounded-lg px-6 py-2.5 text-sm font-semibold text-white flex items-center gap-2 transition ${
                      location.city.trim() && location.district.trim()
                        ? "bg-blue-600 hover:bg-blue-700" : "bg-gray-300 cursor-not-allowed"
                    }`}>
                    Dalje <ArrowRight className="h-4 w-4" />
                  </button>
                </div>
              </>
            )}
          </div>
        )}

        {/* ═══ STEP 2: Type-specific details ═══ */}
        {viewState === "INPUT" && step === 2 && propertyType && (
          <div className="space-y-6">
            <button onClick={() => setStep(1)} className="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1">
              <ArrowLeft className="h-4 w-4" /> Natrag na korak 1
            </button>

            {/* ── APARTMENT ── */}
            {propertyType === "Apartment" && (
              <div className="rounded-xl bg-white border border-gray-200 p-6 space-y-5">
                <h3 className="text-sm font-semibold text-gray-700">🏢 Detalji stana</h3>
                <div className="grid grid-cols-2 gap-4">
                  <NumberInput label="Površina (m²) *" value={apt.area_m2}
                    onChange={(v) => setApt({ ...apt, area_m2: v })} min={10} max={300} />
                  <NumberInput label="Broj soba *" value={apt.room_count}
                    onChange={(v) => setApt({ ...apt, room_count: v })} min={1} max={10} step={1} />
                  <FloorSelect value={apt.floor}
                    onChange={(v) => setApt({ ...apt, floor: v })} />
                  <NumberInput label="Ukupno katova *" value={apt.total_floors}
                    onChange={(v) => setApt({ ...apt, total_floors: v })} min={1} max={50} step={1} />
                  <NumberInput label="Godina izgradnje" value={apt.year_built || 0}
                    onChange={(v) => setApt({ ...apt, year_built: v || undefined })} min={1900} max={2030} step={1} />
                  <NumberInput label="Udaljenost od mora (m)" value={apt.distance_to_sea_m || 0}
                    onChange={(v) => setApt({ ...apt, distance_to_sea_m: v || undefined })} min={0} max={100000} step={10} />
                </div>
                <SelectInput label="Stanje *" value={apt.condition}
                  options={Object.entries(CONDITION_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                  onChange={(v) => setApt({ ...apt, condition: v as PropertyCondition })} />
                <div className="grid grid-cols-2 gap-3">
                  <Toggle label="Lift" checked={apt.has_elevator} onChange={(v) => setApt({ ...apt, has_elevator: v })} />
                  <Toggle label="Parking" checked={apt.has_parking} onChange={(v) => setApt({ ...apt, has_parking: v })} />
                  <Toggle label="Balkon" checked={apt.has_balcony} onChange={(v) => setApt({ ...apt, has_balcony: v })} />
                  <Toggle label="Pogled na more" checked={apt.sea_view} onChange={(v) => setApt({ ...apt, sea_view: v })} />
                </div>
                {floorNumericValue(apt.floor) >= 2 && !apt.has_elevator && (
                  <div className="rounded-lg bg-yellow-50 border border-yellow-200 p-3 text-sm text-yellow-800">
                    ⚠️ Stan je na visokom katu bez lifta — to utječe na vrijednost.
                  </div>
                )}
              </div>
            )}

            {/* ── HOUSE ── */}
            {propertyType === "House" && (
              <div className="rounded-xl bg-white border border-gray-200 p-6 space-y-5">
                <h3 className="text-sm font-semibold text-gray-700">🏠 Detalji kuće</h3>
                <div className="grid grid-cols-2 gap-4">
                  <NumberInput label="Stambena površina (m²) *" value={house.living_area_m2}
                    onChange={(v) => setHouse({ ...house, living_area_m2: v })} min={20} max={2000} />
                  <NumberInput label="Okućnica (m²) *" value={house.land_area_m2}
                    onChange={(v) => setHouse({ ...house, land_area_m2: v })} min={1} max={100000} />
                  <NumberInput label="Godina izgradnje" value={house.year_built || 0}
                    onChange={(v) => setHouse({ ...house, year_built: v || undefined })} min={1900} max={2030} step={1} />
                  <NumberInput label="Udaljenost od mora (m)" value={house.distance_to_sea_m || 0}
                    onChange={(v) => setHouse({ ...house, distance_to_sea_m: v || undefined })} min={0} max={100000} step={10} />
                </div>
                <SelectInput label="Katnost *" value={house.floors}
                  options={Object.entries(FLOORS_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                  onChange={(v) => setHouse({ ...house, floors: v as HouseFloors })} />
                <SelectInput label="Stanje *" value={house.condition}
                  options={Object.entries(CONDITION_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                  onChange={(v) => setHouse({ ...house, condition: v as PropertyCondition })} />
                <div className="grid grid-cols-2 gap-3">
                  <Toggle label="Garaža" checked={house.has_garage} onChange={(v) => setHouse({ ...house, has_garage: v })} />
                  <Toggle label="Bazen" checked={house.has_pool} onChange={(v) => setHouse({ ...house, has_pool: v })} />
                  <Toggle label="1. red do mora" checked={house.first_row_to_sea} onChange={(v) => setHouse({ ...house, first_row_to_sea: v })} />
                  <Toggle label="Pomoćni objekt" checked={house.has_auxiliary_object} onChange={(v) => setHouse({ ...house, has_auxiliary_object: v })} />
                </div>
              </div>
            )}

            {/* ── LAND ── */}
            {propertyType === "Land" && (
              <div className="rounded-xl bg-white border border-gray-200 p-6 space-y-5">
                <h3 className="text-sm font-semibold text-gray-700">🌿 Detalji zemljišta</h3>
                <div className="grid grid-cols-2 gap-4">
                  <NumberInput label="Površina (m²) *" value={land.land_area_m2}
                    onChange={(v) => setLand({ ...land, land_area_m2: v })} min={50} max={1000000} />
                  <NumberInput label="Udaljenost od mora (m)" value={land.distance_to_sea_m || 0}
                    onChange={(v) => setLand({ ...land, distance_to_sea_m: v || undefined })} min={0} max={100000} step={10} />
                </div>
                <SelectInput label="Namjena *" value={land.land_use}
                  options={Object.entries(LAND_USE_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                  onChange={(v) => setLand({ ...land, land_use: v as LandUse })} />
                <div className="grid grid-cols-2 gap-3">
                  <Toggle label="Pristupni put" checked={land.has_access_road} onChange={(v) => setLand({ ...land, has_access_road: v })} />
                  <Toggle label="Infrastruktura (voda/struja)" checked={land.has_infrastructure} onChange={(v) => setLand({ ...land, has_infrastructure: v })} />
                  <Toggle label="1. red do mora" checked={land.first_row_to_sea} onChange={(v) => setLand({ ...land, first_row_to_sea: v })} />
                  <Toggle label="Legalizirano" checked={land.is_legalized} onChange={(v) => setLand({ ...land, is_legalized: v })} />
                </div>
              </div>
            )}

            {/* Validation errors */}
            {draftErrors.length > 0 && (
              <div className="rounded-lg border border-red-300 bg-red-50 p-4 space-y-1">
                {draftErrors.map((e, i) => (
                  <p key={i} className="text-sm text-red-700">❌ {e.message}</p>
                ))}
              </div>
            )}
            {draftWarnings.length > 0 && (
              <div className="rounded-lg border border-yellow-300 bg-yellow-50 p-4 space-y-1">
                {draftWarnings.map((w, i) => (
                  <p key={i} className="text-sm text-yellow-800">⚠️ {w.message}</p>
                ))}
              </div>
            )}

            {/* Dodatne informacije */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-2">✍️ Dodatne informacije <span className="text-xs font-normal text-gray-400">(opcionalno)</span></h4>
              <p className="text-xs text-gray-400 mb-2">Navedite sve što može utjecati na vrijednost: renovacije, posebnosti, blizina sadržaja...</p>
              <textarea value={notes} onChange={(e) => setNotes(e.target.value)}
                rows={3} maxLength={500}
                placeholder="npr. Stan je potpuno renoviran 2023., ima podne grijanje, klima u svim sobama, u blizini škole i tramvaja..."
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 resize-none" />
              <p className="text-xs text-gray-400 mt-1 text-right">{notes.length}/500</p>
            </div>

            {/* Email */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-2">✉️ Email obavijest <span className="text-xs font-normal text-gray-400">(opcionalno)</span></h4>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)}
                placeholder="ime@gmail.com" className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            </div>

            <div className="flex justify-end">
              <button onClick={validateDraft} disabled={draftLoading}
                className="rounded-lg bg-blue-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-50 transition flex items-center gap-2">
                {draftLoading ? <><Loader2 className="h-4 w-4 animate-spin" /> Validiram...</> : <>Validiraj i dalje <ArrowRight className="h-4 w-4" /></>}
              </button>
            </div>
          </div>
        )}

        {/* ═══ STEP 3: Confirmation + Valuation ═══ */}
        {viewState === "INPUT" && step === 3 && draftResult && (
          <div className="space-y-6">
            <button onClick={() => setStep(2)} className="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1">
              <ArrowLeft className="h-4 w-4" /> Natrag na korak 2
            </button>

            <div className="rounded-xl bg-white border border-gray-200 p-6">
              <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Potvrda — opis nekretnine</h3>
              <p className="text-gray-800 leading-relaxed text-lg font-medium">{draftResult.description}</p>
              {draftWarnings.length > 0 && (
                <div className="mt-4 space-y-1">
                  {draftWarnings.map((w, i) => (
                    <p key={i} className="text-sm text-yellow-700">⚠️ {w.message}</p>
                  ))}
                </div>
              )}
            </div>

            <div className="flex justify-end">
              <button onClick={runValuation}
                className="rounded-lg bg-green-600 px-8 py-3 text-sm font-bold text-white hover:bg-green-700 transition flex items-center gap-2">
                <Scale className="h-5 w-5" /> Pokreni procjenu
              </button>
            </div>
          </div>
        )}

        {/* ═══ PROCESSING: Premium Pipeline Animation ═══ */}
        {viewState === "PROCESSING" && (
          <div className="relative flex flex-col items-center justify-center min-h-[70vh] -mx-4 px-4 overflow-hidden">
            {/* Animated background blobs */}
            <div className="absolute inset-0 pointer-events-none overflow-hidden">
              <div className="absolute -top-32 -left-32 h-80 w-80 rounded-full bg-gradient-to-br from-blue-200/40 to-indigo-300/30 blur-3xl animate-float-blob" />
              <div className="absolute -bottom-24 -right-24 h-72 w-72 rounded-full bg-gradient-to-br from-violet-200/40 to-purple-300/30 blur-3xl animate-float-blob-delayed" />
              <div className="absolute top-1/2 left-1/3 h-56 w-56 rounded-full bg-gradient-to-br from-cyan-200/30 to-blue-200/20 blur-3xl animate-float-blob-slow" />
            </div>

            {/* Card container */}
            <div className="relative z-10 w-full max-w-lg bg-white/80 backdrop-blur-xl rounded-2xl shadow-[0_10px_40px_rgba(0,0,0,0.08)] border border-white/60 p-8 sm:p-10">
              {/* Header */}
              <div className="text-center mb-10">
                <div className="relative mx-auto h-16 w-16 mb-5">
                  <div className="absolute inset-0 rounded-full bg-gradient-to-br from-blue-400 to-indigo-500 opacity-20 animate-ping" />
                  <div className="absolute inset-0 rounded-full animate-pulse-glow" />
                  <div className="relative flex items-center justify-center h-full w-full rounded-full bg-gradient-to-br from-blue-500 to-indigo-600">
                    <Loader2 className="h-7 w-7 animate-spin text-white" />
                  </div>
                </div>
                <h2 className="text-2xl sm:text-3xl font-bold bg-gradient-to-r from-blue-700 via-indigo-600 to-violet-600 bg-clip-text text-transparent">
                  AI Procjena u tijeku
                </h2>
                <p className="text-sm text-gray-500 mt-2">
                  Analiziramo tržište nekretnina za vašu lokaciju
                </p>
              </div>

              {/* Vertical Step Pipeline */}
              <div className="relative">
                {stages.map((s, idx) => {
                  const icon = STAGE_ICONS[s.id];
                  const isDone = s.status === "done";
                  const isActive = s.status === "active";
                  const isError = s.status === "error";

                  return (
                    <div key={s.id} className="flex gap-4 pb-7 last:pb-0">
                      {/* Icon circle + connector line */}
                      <div className="flex flex-col items-center">
                        <div className={`relative h-11 w-11 rounded-full flex items-center justify-center border-2 transition-all duration-500 ${
                          isDone ? "bg-green-50 border-green-400 animate-pop-in" :
                          isActive ? "bg-gradient-to-br from-blue-50 to-indigo-50 border-indigo-400 animate-pulse-glow" :
                          isError ? "bg-red-50 border-red-400" :
                          "bg-gray-50 border-gray-200"
                        }`}>
                          {isActive ? (
                            <span className="text-indigo-600 animate-spin">{icon || <Loader2 className="h-5 w-5" />}</span>
                          ) : isDone ? (
                            <Check className="h-5 w-5 text-green-500" />
                          ) : isError ? (
                            <AlertTriangle className="h-5 w-5 text-red-500" />
                          ) : (
                            <span className="text-gray-300">{icon || <span className="text-sm font-bold">{idx + 1}</span>}</span>
                          )}
                        </div>
                        {idx < stages.length - 1 && (
                          <div className="relative w-0.5 flex-1 min-h-[1.25rem] bg-gray-200 rounded-full overflow-hidden">
                            {isDone && (
                              <div className="absolute inset-0 bg-gradient-to-b from-green-400 to-green-300 animate-line-fill rounded-full" />
                            )}
                          </div>
                        )}
                      </div>

                      {/* Text content */}
                      <div className="pt-2.5 flex-1 min-w-0">
                        <p className={`text-sm font-semibold transition-colors duration-300 ${
                          isActive ? "text-indigo-700" :
                          isDone ? "text-green-700" :
                          isError ? "text-red-700" : "text-gray-400"
                        }`}>
                          {s.label}
                        </p>
                        {/* Dynamic micro-copy for active stage */}
                        {isActive && microCopy && (
                          <p key={microCopy} className="text-xs text-indigo-400 mt-0.5 animate-fade-up">
                            {microCopy}
                          </p>
                        )}
                        {s.detail && !isActive && <p className="text-xs text-gray-500 mt-0.5">{s.detail}</p>}
                        {s.error && <p className="text-xs text-red-600 mt-0.5">{s.error}</p>}
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* Error recovery */}
              {error && !isStreaming && (
                <div className="mt-8 text-center space-y-3">
                  <div className="rounded-lg border border-red-300 bg-red-50 p-4">
                    <p className="text-sm text-red-700">{error}</p>
                  </div>
                  <button onClick={reset} className="rounded-lg bg-gray-100 px-5 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 transition">
                    ← Pokušaj ponovno
                  </button>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ═══ VALUATION RESULT ═══ */}
        {viewState === "RESULT" && result && (
          <div className="space-y-6">
            {/* Main Value */}
            <div className="rounded-xl bg-gradient-to-r from-blue-600 to-indigo-700 p-8 text-white text-center">
              <p className="text-sm font-medium text-blue-100 uppercase tracking-wide">Procijenjena tržišna vrijednost</p>
              <p className="text-5xl font-bold mt-2">{formatPrice(result.estimated_value_eur)} €</p>
              {result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0 && (
                <p className="mt-2 text-blue-200">
                  {formatPrice(Math.round(result.calculated_price_per_m2))} €/m²
                  {result.confidence_level && ` • ${result.confidence_level}`}
                </p>
              )}
              {result.math_confidence_score !== undefined && result.math_confidence_score > 0 && (
                <div className="mt-3 inline-flex items-center gap-2 bg-white/20 rounded-full px-4 py-1.5">
                  <span className="text-sm font-medium">
                    Pouzdanost: {result.math_confidence_score}/100 ({result.math_confidence_level})
                  </span>
                </div>
              )}
            </div>

            {/* Stats */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard label="Pronađeno oglasa" value={result.total_listings_found ?? 0} />
              <StatCard label="Korištenih" value={result.usable_price_points ?? 0} />
              <StatCard label="Outlieri" value={result.outliers_removed ?? 0} />
              <StatCard label="Diskont" value={`${result.realization_discount_percent?.toFixed(1) ?? "15"}%`} />
            </div>

            {/* Comparables */}
            {result.selected_comparables?.length > 0 && (
              <div className="rounded-xl bg-white border border-gray-200 p-6">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4 flex items-center gap-2">
                  <Search className="h-4 w-4" /> Usporedive nekretnine
                </h3>
                <div className="space-y-3">
                  {result.selected_comparables.map((c, i) => (
                    <div key={i} className="p-4 border border-gray-100 rounded-lg">
                      <div className="flex items-start justify-between">
                        <div className="flex-1 min-w-0">
                          <h4 className="text-sm font-semibold text-gray-900 truncate">{c.title}</h4>
                          <div className="flex items-center gap-3 mt-1 text-xs text-gray-500">
                            <span className="font-semibold text-blue-700">{c.price}</span>
                            <span>{c.location}</span>
                          </div>
                        </div>
                        {c.url && (
                          <a href={c.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:text-blue-700 ml-2">
                            <ExternalLink className="h-4 w-4" />
                          </a>
                        )}
                      </div>
                      <p className="text-xs text-gray-500 mt-2">{c.reason_for_selection}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Value Drivers */}
            {result.value_drivers?.length > 0 && (
              <div className="rounded-xl bg-white border border-gray-200 p-6">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4">Faktori</h3>
                <div className="space-y-2">
                  {result.value_drivers.map((d, i) => (
                    <div key={i} className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
                      <div>
                        <span className="text-sm font-medium text-gray-700">{d.factor}</span>
                        <p className="text-xs text-gray-500">{d.impact}</p>
                      </div>
                      <span className={`text-sm font-bold ${d.adjustment_percent > 0 ? "text-green-600" : d.adjustment_percent < 0 ? "text-red-600" : "text-gray-500"}`}>
                        {d.adjustment_percent > 0 ? "+" : ""}{d.adjustment_percent}%
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Reasoning */}
            {result.valuation_reasoning && (
              <div className="rounded-xl bg-white border border-gray-200 p-6">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide flex items-center gap-2">
                    <Brain className="h-4 w-4" /> Stručno mišljenje
                  </h3>
                  <button onClick={() => copyToClipboard(result.valuation_reasoning)}
                    className="flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700">
                    {copied ? <><Check className="h-4 w-4 text-green-600" /> Kopirano</> : <><ClipboardCopy className="h-4 w-4" /> Kopiraj</>}
                  </button>
                </div>
                <div className="prose prose-sm max-w-none text-gray-800 leading-relaxed whitespace-pre-wrap">
                  {result.valuation_reasoning}
                </div>
              </div>
            )}

            {/* Reset */}
            <div className="text-center">
              <button onClick={reset} className="rounded-lg bg-gray-100 px-6 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-200 transition">
                Nova procjena
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════
//  REUSABLE INPUT COMPONENTS
// ═══════════════════════════════════════════

function NumberInput({ label, value, onChange, min, max, step = 0.1 }: {
  label: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-500 mb-1">{label}</label>
      <input type="number" value={value || ""} onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
        min={min} max={max} step={step}
        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500" />
    </div>
  );
}

function SelectInput({ label, value, options, onChange }: {
  label: string; value: string; options: { value: string; label: string }[]; onChange: (v: string) => void;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-500 mb-1">{label}</label>
      <select value={value} onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </div>
  );
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-3 p-3 rounded-lg border border-gray-200 cursor-pointer hover:bg-gray-50 transition">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
      <span className="text-sm text-gray-700">{label}</span>
    </label>
  );
}

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-lg bg-white border border-gray-200 p-4 text-center">
      <p className="text-2xl font-bold text-gray-900">{value}</p>
      <p className="text-xs text-gray-500 mt-1">{label}</p>
    </div>
  );
}

function FloorSelect({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-500 mb-1">Kat *</label>
      <select value={value} onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
        <optgroup label="Posebne etaže">
          <option value="Subbasement">Podrum</option>
          <option value="SemiBasement">Suteren</option>
          <option value="Ground">Prizemlje</option>
          <option value="Attic">Potkrovlje</option>
          <option value="Penthouse">Penthouse</option>
        </optgroup>
        <optgroup label="Katovi">
          {Array.from({ length: 30 }, (_, i) => (
            <option key={i + 1} value={`Floor${i + 1}`}>{i + 1}. kat</option>
          ))}
        </optgroup>
      </select>
    </div>
  );
}
