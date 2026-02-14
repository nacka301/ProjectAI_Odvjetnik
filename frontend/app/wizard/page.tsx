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
  FileSearch,
  Cpu,
  Bot,
  Save,
  TrendingUp,
  BarChart3,
  Target,
  MapPin,
  RefreshCw,
  Shield,
  Activity,
  Layers,
  Hash,
  Percent,
} from "lucide-react";
import { saveEvaluation } from "@/lib/supabase";
import LocationSelector from "./LocationSelector";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";

// --- Types mirroring backend enums ---

type PropertyType = "Apartment" | "House" | "Land";
type PropertyCondition = "New" | "Renovated" | "Good" | "Original" | "NeedsWork";
type HouseFloors = "OneStory" | "TwoStory" | "MultiStory";
type LandUse = "Construction" | "Agricultural" | "Mixed";
type Orientation = "Unknown" | "North" | "NorthEast" | "East" | "SouthEast" | "South" | "SouthWest" | "West" | "NorthWest";
type ViewType = "Unknown" | "Street" | "Courtyard" | "Park" | "City" | "Sea" | "Panoramic";
type ViewState = "INPUT" | "PROCESSING" | "RESULT";
type ResultTab = "overview" | "comparables";

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
  orientation: Orientation;
  view_type: ViewType;
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
  orientation: Orientation;
  view_type: ViewType;
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

interface UsedComparable {
  title: string;
  url: string;
  price_per_m2: number;
  similarity_score: number;
  is_outlier: boolean;
  // BrightData-enriched fields
  total_price?: number;
  area_m2?: number;
  rooms?: number;
  floor?: string;
  year_built?: number;
  condition?: string;
  energy_class?: string;
  heating?: string;
  location?: string;
  has_garage?: boolean;
  has_parking?: boolean;
}

interface ValuationAdjustment {
  factor: string;
  description: string;
  percent: number;
}

interface ComparisonTableRow {
  index: number;
  title: string;
  url: string;
  area_m2: number;
  total_price: number;
  raw_price_per_m2: number;
  correction_percent: number;
  adjusted_price_per_m2: number;
  similarity_score: number;
  // V20
  deep_dive_status?: string;
  correction_reason?: string;
}

interface PriceRangeEstimate {
  conservative_price_per_m2: number;
  realistic_price_per_m2: number;
  optimistic_price_per_m2: number;
  conservative_total: number;
  realistic_total: number;
  optimistic_total: number;
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
  math_confidence_explanation?: string;
  market_average_asking?: number;
  std_dev_per_m2?: number;
  coefficient_of_variation?: number;
  total_adjustment_percent?: number;
  realization_gap_reasoning?: string;
  used_comparables?: UsedComparable[];
  adjustments?: ValuationAdjustment[];
  pipeline?: string[];
  outlier_log?: string[];
  outlier_method?: string;
  confidence_score?: number;
  confidence_explanation?: string;
  confidence_color?: string;
  price_source?: string;
  region_name?: string;
  note?: string;
  search_phase_used?: string;
  sherlock_insight?: string;
  // V14: Structured valuation output
  comparison_table?: ComparisonTableRow[];
  price_range?: PriceRangeEstimate;
  benchmark_summary?: string;
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

const ORIENTATION_LABELS: Record<Orientation, string> = {
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

const VIEW_TYPE_LABELS: Record<ViewType, string> = {
  Unknown: "Nepoznato",
  Street: "Ulica",
  Courtyard: "Dvorište",
  Park: "Park",
  City: "Grad",
  Sea: "More",
  Panoramic: "Panoramski",
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
    orientation: "Unknown", view_type: "Unknown",
    has_elevator: false, has_parking: false, has_balcony: false, sea_view: false,
  });
  const [house, setHouse] = useState<HouseData>({
    living_area_m2: 0, land_area_m2: 0, floors: "OneStory", condition: "Good",
    orientation: "Unknown", view_type: "Unknown",
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
  const abortRef = useRef<AbortController | null>(null);
  const [stages, setStages] = useState<StreamStage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [result, setResult] = useState<ValuationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [email, setEmail] = useState("");
  const [notes, setNotes] = useState("");
  const [resultTab, setResultTab] = useState<ResultTab>("overview");

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

    // Using object wrapper so TypeScript doesn't narrow the closure-assigned value to `never`
    const state = { streamedResult: null as ValuationResult | null };
    const controller = new AbortController();
    abortRef.current = controller;

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
        signal: controller.signal,
      });

      if (!response.ok || !response.body) throw new Error(`HTTP ${response.status}`);

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      const processEvent = (line: string) => {
        if (!line.startsWith("data: ")) return;
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
            state.streamedResult = evt.data;
            setResult(evt.data);
          }
        } catch { /* skip malformed */ }
      };

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const parts = buffer.split("\n\n");
        buffer = parts.pop()!;
        for (const part of parts) processEvent(part.trim());
      }
      // Flush remaining buffer (last SSE event might lack trailing \n\n)
      if (buffer.trim()) processEvent(buffer.trim());

      setIsStreaming(false);
      if (state.streamedResult) setViewState("RESULT");

      // Save to Supabase (fire-and-forget)
      if (state.streamedResult) {
        saveEvaluation({
          property_type: propertyType || "nekretnina",
          location: `${location.city}, ${location.district}`,
          estimated_value: state.streamedResult.estimated_value_eur,
          pdf_url: null,
          raw_json_output: state.streamedResult as unknown as Record<string, unknown>,
        }).catch(() => { /* non-critical */ });
      }
    } catch (e: unknown) {
      setIsStreaming(false);
      if (e instanceof DOMException && e.name === "AbortError") {
        setError("Procjena je otkazana.");
      } else {
        setError("Server ne odgovara. Pokušajte ponovno za minutu.");
      }
    } finally {
      abortRef.current = null;
    }
  };

  const cancelValuation = () => {
    abortRef.current?.abort();
    setIsStreaming(false);
    setViewState("INPUT");
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
    abortRef.current?.abort();
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
    setResultTab("overview");
  };

  const formatPrice = (n: number) => n.toLocaleString("hr-HR");

  // ═══════════════════════════════════════════
  //  RENDER
  // ═══════════════════════════════════════════

  return (
    <>
    {/* ═══ PROCESSING: Fixed Full-Screen Pipeline ═══ */}
    {viewState === "PROCESSING" && (
      <div
        className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden"
        style={{ background: "linear-gradient(135deg, #f0f4ff 0%, #e8ecf8 50%, #f5f0ff 100%)" }}
      >
        {/* Animated background blobs — inline styles so Tailwind doesn't purge */}
        <div className="absolute inset-0 pointer-events-none overflow-hidden">
          <div
            className="absolute rounded-full"
            style={{
              top: "-8rem", left: "-8rem", width: "22rem", height: "22rem",
              background: "radial-gradient(circle, rgba(99,102,241,0.2) 0%, rgba(59,130,246,0.08) 70%, transparent 100%)",
              filter: "blur(60px)",
              animation: "float-blob 8s ease-in-out infinite",
            }}
          />
          <div
            className="absolute rounded-full"
            style={{
              bottom: "-6rem", right: "-6rem", width: "20rem", height: "20rem",
              background: "radial-gradient(circle, rgba(139,92,246,0.2) 0%, rgba(124,58,237,0.08) 70%, transparent 100%)",
              filter: "blur(60px)",
              animation: "float-blob 10s ease-in-out 2s infinite",
            }}
          />
          <div
            className="absolute rounded-full"
            style={{
              top: "40%", left: "25%", width: "16rem", height: "16rem",
              background: "radial-gradient(circle, rgba(6,182,212,0.15) 0%, rgba(59,130,246,0.06) 70%, transparent 100%)",
              filter: "blur(60px)",
              animation: "float-blob 12s ease-in-out 4s infinite",
            }}
          />
        </div>

        {/* Card */}
        <div
          className="relative z-10 w-full max-w-lg mx-4"
          style={{
            background: "rgba(255,255,255,0.85)",
            backdropFilter: "blur(24px)",
            WebkitBackdropFilter: "blur(24px)",
            borderRadius: "1.25rem",
            boxShadow: "0 12px 48px rgba(0,0,0,0.08), 0 2px 12px rgba(0,0,0,0.04)",
            border: "1px solid rgba(255,255,255,0.6)",
            padding: "1.75rem 1.5rem",
          }}
        >
          {/* Header */}
          <div className="text-center mb-5">
            <div className="relative mx-auto h-12 w-12 mb-3">
              <div
                className="absolute inset-0 rounded-full"
                style={{
                  background: "linear-gradient(135deg, rgba(59,130,246,0.3), rgba(99,102,241,0.3))",
                  animation: "pulse-glow 2s ease-in-out infinite",
                }}
              />
              <div
                className="relative flex items-center justify-center h-full w-full rounded-full"
                style={{ background: "linear-gradient(135deg, #3b82f6, #6366f1)" }}
              >
                <Loader2 className="h-5 w-5 animate-spin text-white" />
              </div>
            </div>
            <h2
              className="text-xl sm:text-2xl font-bold"
              style={{
                background: "linear-gradient(135deg, #1e40af, #4f46e5, #7c3aed)",
                WebkitBackgroundClip: "text",
                WebkitTextFillColor: "transparent",
                backgroundClip: "text",
              }}
            >
              Procjena u tijeku
            </h2>
            <p className="text-xs text-gray-500 mt-1">
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
                <div key={s.id} className="flex gap-3" style={{ paddingBottom: idx < stages.length - 1 ? "0.75rem" : 0 }}>
                  {/* Icon circle + connector line */}
                  <div className="flex flex-col items-center">
                    <div
                      className="relative h-9 w-9 rounded-full flex items-center justify-center border-2 transition-all duration-500"
                      style={{
                        ...(isDone ? {
                          background: "#f0fdf4", borderColor: "#4ade80",
                          animation: "pop-in 0.4s cubic-bezier(0.34,1.56,0.64,1) forwards",
                        } : isActive ? {
                          background: "linear-gradient(135deg, #eff6ff, #eef2ff)",
                          borderColor: "#818cf8",
                          animation: "pulse-glow 2s ease-in-out infinite",
                        } : isError ? {
                          background: "#fef2f2", borderColor: "#f87171",
                        } : {
                          background: "#f9fafb", borderColor: "#e5e7eb",
                        }),
                      }}
                    >
                      {isActive ? (
                        <span className="text-indigo-600 animate-spin">{icon || <Loader2 className="h-4 w-4" />}</span>
                      ) : isDone ? (
                        <Check className="h-4 w-4 text-green-500" />
                      ) : isError ? (
                        <AlertTriangle className="h-4 w-4 text-red-500" />
                      ) : (
                        <span className="text-gray-300">{icon || <span className="text-sm font-bold">{idx + 1}</span>}</span>
                      )}
                    </div>
                    {idx < stages.length - 1 && (
                      <div className="relative w-0.5 flex-1 bg-gray-200 rounded-full overflow-hidden" style={{ minHeight: "1rem" }}>
                        {isDone && (
                          <div
                            className="absolute inset-0 rounded-full"
                            style={{
                              background: "linear-gradient(to bottom, #4ade80, #86efac)",
                              animation: "line-fill 0.5s ease-out forwards",
                              transformOrigin: "top",
                            }}
                          />
                        )}
                      </div>
                    )}
                  </div>

                  {/* Text content */}
                  <div className="pt-1 flex-1 min-w-0">
                    <p className={`text-sm font-semibold transition-colors duration-300 ${
                      isActive ? "text-indigo-700" :
                      isDone ? "text-green-700" :
                      isError ? "text-red-700" : "text-gray-400"
                    }`}>
                      {s.label}
                    </p>
                    {isActive && microCopy && (
                      <p
                        key={microCopy}
                        className="text-xs text-indigo-400 mt-0.5"
                        style={{ animation: "fade-up 0.3s ease-out forwards" }}
                      >
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
            <div className="mt-5 text-center space-y-2">
              <div className="rounded-lg border border-red-300 bg-red-50 p-4">
                <p className="text-sm text-red-700">{error}</p>
              </div>
              <button onClick={reset} className="rounded-lg bg-gray-100 px-5 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 transition">
                ← Pokušaj ponovno
              </button>
            </div>
          )}

          {/* Cancel button — visible while streaming */}
          {isStreaming && (
            <div className="mt-5 text-center">
              <button
                onClick={cancelValuation}
                className="rounded-lg border border-gray-300 bg-white px-5 py-2 text-sm font-medium text-gray-600 hover:bg-red-50 hover:border-red-300 hover:text-red-700 transition"
              >
                ✕ Odustani
              </button>
            </div>
          )}
        </div>
      </div>
    )}

    {/* Normal page wrapper — hidden during PROCESSING */}
    {viewState !== "PROCESSING" && (
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
                <LocationSelector
                  city={location.city}
                  district={location.district}
                  onCityChange={(city) => setLocation((prev) => ({ ...prev, city, district: "" }))}
                  onDistrictChange={(district) => setLocation((prev) => ({ ...prev, district }))}
                />

                <div className="flex justify-end">
                  <button onClick={() => setStep(2)}
                    disabled={!location.city.trim()}
                    className={`rounded-lg px-6 py-2.5 text-sm font-semibold text-white flex items-center gap-2 transition ${
                      location.city.trim()
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
                <div className="grid grid-cols-2 gap-4">
                  <SelectInput label="Orijentacija" value={apt.orientation}
                    options={Object.entries(ORIENTATION_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                    onChange={(v) => setApt({ ...apt, orientation: v as Orientation })} />
                  <SelectInput label="Pogled" value={apt.view_type}
                    options={Object.entries(VIEW_TYPE_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                    onChange={(v) => setApt({ ...apt, view_type: v as ViewType })} />
                </div>
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
                <div className="grid grid-cols-2 gap-4">
                  <SelectInput label="Orijentacija" value={house.orientation}
                    options={Object.entries(ORIENTATION_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                    onChange={(v) => setHouse({ ...house, orientation: v as Orientation })} />
                  <SelectInput label="Pogled" value={house.view_type}
                    options={Object.entries(VIEW_TYPE_LABELS).map(([k, v]) => ({ value: k, label: v }))}
                    onChange={(v) => setHouse({ ...house, view_type: v as ViewType })} />
                </div>
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

        {/* ═══ VALUATION RESULT ═══ */}
        {viewState === "RESULT" && result && (
          <div className="space-y-6">

            {/* ── Hero: Estimated Value ── */}
            <div className="rounded-2xl bg-gradient-to-br from-blue-600 via-indigo-700 to-purple-800 p-8 text-white relative overflow-hidden">
              <div className="absolute inset-0 opacity-10" style={{ backgroundImage: "radial-gradient(circle at 30% 20%, white 1px, transparent 1px), radial-gradient(circle at 70% 80%, white 1px, transparent 1px)", backgroundSize: "40px 40px" }} />
              <div className="relative z-10 text-center">
                <p className="text-xs font-semibold uppercase tracking-widest text-blue-200">Procijenjena tržišna vrijednost</p>
                <p className="text-5xl md:text-6xl font-extrabold mt-3 tracking-tight">{formatPrice(result.estimated_value_eur)} €</p>

                {/* V14: Price range from server-computed PriceRangeEstimate */}
                {result.price_range ? (
                  <p className="mt-2 text-sm text-blue-200">
                    Raspon: {formatPrice(result.price_range.conservative_total)} € — {formatPrice(result.price_range.optimistic_total)} €
                  </p>
                ) : result.std_dev_per_m2 && result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0 && (() => {
                  const areaM2 = propertyType === "Apartment" ? apt.area_m2 : propertyType === "House" ? house.living_area_m2 : land.land_area_m2;
                  const rangeLow = Math.round((result.calculated_price_per_m2! - result.std_dev_per_m2!) * Math.max(areaM2, 1));
                  const rangeHigh = Math.round((result.calculated_price_per_m2! + result.std_dev_per_m2!) * Math.max(areaM2, 1));
                  return (
                    <p className="mt-2 text-sm text-blue-200">
                      Raspon: {formatPrice(Math.max(rangeLow, 0))} € — {formatPrice(rangeHigh)} €
                    </p>
                  );
                })()}

                <div className="flex flex-wrap items-center justify-center gap-3 mt-4">
                  {result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0 && (
                    <span className="inline-flex items-center gap-1.5 bg-white/15 backdrop-blur-sm rounded-full px-4 py-1.5 text-sm font-medium">
                      <Target className="h-3.5 w-3.5" /> {formatPrice(Math.round(result.calculated_price_per_m2))} €/m²
                    </span>
                  )}
                  {result.math_confidence_score !== undefined && result.math_confidence_score > 0 && (
                    <span className={`inline-flex items-center gap-1.5 rounded-full px-4 py-1.5 text-sm font-semibold ${
                      result.math_confidence_score >= 70 ? "bg-green-400/20 text-green-100" :
                      result.math_confidence_score >= 40 ? "bg-yellow-400/20 text-yellow-100" :
                      "bg-red-400/20 text-red-100"
                    }`}>
                      <Shield className="h-3.5 w-3.5" /> {result.math_confidence_score}/100 — {result.math_confidence_level}
                    </span>
                  )}
                  {result.confidence_level && (
                    <span className="inline-flex items-center gap-1.5 bg-white/15 backdrop-blur-sm rounded-full px-4 py-1.5 text-sm">
                      {result.confidence_level}
                    </span>
                  )}
                </div>
              </div>
            </div>

            {/* ── Tab Navigation ── */}
            <div className="flex border-b border-gray-200 gap-1">
              {([
                { key: "overview" as ResultTab, label: "Pregled", icon: <BarChart3 className="h-4 w-4" /> },
                { key: "comparables" as ResultTab, label: "Usporedbe", icon: <Layers className="h-4 w-4" /> },
              ]).map((tab) => (
                <button key={tab.key} onClick={() => setResultTab(tab.key)}
                  className={`flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
                    resultTab === tab.key
                      ? "border-blue-600 text-blue-700"
                      : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300"
                  }`}>
                  {tab.icon} {tab.label}
                </button>
              ))}
            </div>

            {/* ════════ TAB: Overview ════════ */}
            {resultTab === "overview" && (
              <div className="space-y-6">
                {/* Stats Grid */}
                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
                  <MiniStat icon={<Search className="h-4 w-4 text-blue-500" />} label="Pronađeno" value={result.total_listings_found ?? 0} />
                  <MiniStat icon={<Check className="h-4 w-4 text-green-500" />} label="Korištenih" value={result.usable_price_points ?? 0} />
                  <MiniStat icon={<Hash className="h-4 w-4 text-red-500" />} label="Outlieri" value={result.outliers_removed ?? 0} />
                  <MiniStat icon={<Percent className="h-4 w-4 text-orange-500" />} label="Diskont" value={`${result.realization_discount_percent?.toFixed(1) ?? "15"}%`} />
                  <MiniStat icon={<Target className="h-4 w-4 text-purple-500" />} label="€/m² prosjek" value={result.market_average_asking ? formatPrice(Math.round(result.market_average_asking)) : "—"} />
                  <MiniStat icon={<Activity className="h-4 w-4 text-indigo-500" />} label="Σ korekcija" value={result.total_adjustment_percent !== undefined ? `${result.total_adjustment_percent > 0 ? "+" : ""}${result.total_adjustment_percent.toFixed(1)}%` : "—"} />
                </div>

                {/* V14: Comparison Table — transparent per-comparable breakdown */}
                {result.comparison_table && result.comparison_table.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                      <Layers className="h-4 w-4" /> Tablica usporednih nekretnina
                    </h3>
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-gray-200 text-left text-xs text-gray-500 uppercase tracking-wider">
                            <th className="pb-3 pr-3">#</th>
                            <th className="pb-3 pr-3">Oglas</th>
                            <th className="pb-3 pr-3 text-right">Površina</th>
                            <th className="pb-3 pr-3 text-right">Cijena</th>
                            <th className="pb-3 pr-3 text-right">€/m²</th>
                            <th className="pb-3 pr-3 text-right">Korekcija</th>
                            <th className="pb-3 pr-3 text-right">Prilagođeno €/m²</th>
                            <th className="pb-3 text-right">Sličnost</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {result.comparison_table.map((row) => (
                            <tr key={row.index} className="hover:bg-gray-50 transition">
                              <td className="py-2.5 pr-3 text-gray-400 font-medium">{row.index}</td>
                              <td className="py-2.5 pr-3 max-w-[200px]">
                                {row.url ? (
                                  <a href={row.url} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline truncate block">{row.title.slice(0, 40)}{row.title.length > 40 ? "…" : ""}</a>
                                ) : (
                                  <span className="text-gray-800 truncate block">{row.title.slice(0, 40)}</span>
                                )}
                              </td>
                              <td className="py-2.5 pr-3 text-right text-gray-700">{row.area_m2 > 0 ? `${formatPrice(row.area_m2)} m²` : "—"}</td>
                              <td className="py-2.5 pr-3 text-right text-gray-700">{row.total_price > 0 ? `${formatPrice(Math.round(row.total_price))} €` : "—"}</td>
                              <td className="py-2.5 pr-3 text-right font-medium text-gray-900">{formatPrice(Math.round(row.raw_price_per_m2))} €</td>
                              <td className={`py-2.5 pr-3 text-right font-semibold ${row.correction_percent > 0 ? "text-green-600" : row.correction_percent < 0 ? "text-red-600" : "text-gray-400"}`} title={row.correction_reason || undefined}>
                                {row.correction_percent > 0 ? "+" : ""}{row.correction_percent.toFixed(1)}%
                                {row.correction_reason && (
                                  <div className="text-[9px] font-normal text-gray-400 truncate max-w-[120px]">{row.correction_reason}</div>
                                )}
                              </td>
                              <td className="py-2.5 pr-3 text-right font-bold text-blue-700">{formatPrice(Math.round(row.adjusted_price_per_m2))} €</td>
                              <td className="py-2.5 text-right">
                                <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                                  row.similarity_score >= 70 ? "bg-green-100 text-green-800" :
                                  row.similarity_score >= 40 ? "bg-yellow-100 text-yellow-800" :
                                  "bg-red-100 text-red-800"
                                }`}>{row.similarity_score}%</span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                        {/* Summary row */}
                        <tfoot>
                          <tr className="border-t-2 border-gray-300 bg-gray-50 font-semibold">
                            <td colSpan={4} className="py-3 pr-3 text-right text-gray-600">Prosjek prilagođen:</td>
                            <td className="py-3 pr-3 text-right text-gray-500">
                              {formatPrice(Math.round(result.comparison_table.reduce((s, r) => s + r.raw_price_per_m2, 0) / result.comparison_table.length))} €
                            </td>
                            <td className="py-3 pr-3 text-right text-gray-500">
                              {(() => {
                                const avgCorr = result.comparison_table!.reduce((s, r) => s + r.correction_percent, 0) / result.comparison_table!.length;
                                return `${avgCorr > 0 ? "+" : ""}${avgCorr.toFixed(1)}%`;
                              })()}
                            </td>
                            <td className="py-3 pr-3 text-right text-blue-800 text-base">
                              {formatPrice(Math.round(result.comparison_table.reduce((s, r) => s + r.adjusted_price_per_m2, 0) / result.comparison_table.length))} €/m²
                            </td>
                            <td></td>
                          </tr>
                          {result.realization_discount_percent && (
                            <tr className="bg-gray-50 text-sm">
                              <td colSpan={6} className="py-2 pr-3 text-right text-gray-500">
                                Nakon diskonta ({result.realization_discount_percent.toFixed(1)}%):
                              </td>
                              <td className="py-2 pr-3 text-right font-bold text-indigo-700 text-base">
                                {result.calculated_price_per_m2 ? `${formatPrice(Math.round(result.calculated_price_per_m2))} €/m²` : "—"}
                              </td>
                              <td></td>
                            </tr>
                          )}
                          <tr className="bg-blue-50 text-base">
                            <td colSpan={6} className="py-3 pr-3 text-right font-bold text-blue-900">
                              Konačna procjena:
                            </td>
                            <td colSpan={2} className="py-3 text-right font-extrabold text-blue-900 text-lg">
                              {formatPrice(result.estimated_value_eur)} €
                            </td>
                          </tr>
                        </tfoot>
                      </table>
                    </div>
                  </div>
                )}

                {/* V14: Price Range — Conservative / Realistic / Optimistic */}
                {result.price_range && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                      <BarChart3 className="h-4 w-4" /> Raspon procjene
                    </h3>
                    <div className="space-y-4">
                      {[
                        { label: "Konzervativno", perM2: result.price_range.conservative_price_per_m2, total: result.price_range.conservative_total, color: "bg-amber-500", textColor: "text-amber-700" },
                        { label: "Realno", perM2: result.price_range.realistic_price_per_m2, total: result.price_range.realistic_total, color: "bg-blue-600", textColor: "text-blue-700" },
                        { label: "Optimistično", perM2: result.price_range.optimistic_price_per_m2, total: result.price_range.optimistic_total, color: "bg-green-500", textColor: "text-green-700" },
                      ].map((tier) => {
                        const maxTotal = result.price_range!.optimistic_total || 1;
                        const widthPct = Math.max((tier.total / maxTotal) * 100, 15);
                        return (
                          <div key={tier.label} className="flex items-center gap-4">
                            <span className={`w-28 text-sm font-medium ${tier.textColor}`}>{tier.label}</span>
                            <div className="flex-1 h-8 bg-gray-100 rounded-lg overflow-hidden relative">
                              <div className={`h-full ${tier.color} rounded-lg transition-all duration-700 flex items-center justify-end pr-3`} style={{ width: `${widthPct}%` }}>
                                <span className="text-xs font-bold text-white whitespace-nowrap">{formatPrice(Math.round(tier.perM2))} €/m²</span>
                              </div>
                            </div>
                            <span className={`w-28 text-right text-sm font-bold ${tier.textColor}`}>{formatPrice(tier.total)} €</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* V14: Benchmark Summary */}
                {result.benchmark_summary && (
                  <div className="rounded-xl bg-indigo-50 border border-indigo-100 p-4">
                    <h3 className="text-xs font-semibold text-indigo-400 uppercase tracking-wider mb-2 flex items-center gap-2">
                      <Target className="h-3.5 w-3.5" /> Benchmark regije
                    </h3>
                    <p className="text-sm text-indigo-900">{result.benchmark_summary}</p>
                  </div>
                )}

                {/* V14: Confidence detail */}
                {result.math_confidence_explanation && (
                  <div className={`rounded-xl border p-4 ${
                    (result.math_confidence_score ?? 0) >= 70 ? "bg-green-50 border-green-200" :
                    (result.math_confidence_score ?? 0) >= 40 ? "bg-yellow-50 border-yellow-200" :
                    "bg-red-50 border-red-200"
                  }`}>
                    <h3 className="text-xs font-semibold uppercase tracking-wider mb-2 flex items-center gap-2">
                      <Shield className="h-3.5 w-3.5" /> Pouzdanost procjene: {result.math_confidence_score}/100 ({result.math_confidence_level})
                    </h3>
                    <p className="text-sm leading-relaxed">{result.math_confidence_explanation}</p>
                  </div>
                )}

                {/* Adjustments as chips */}
                {result.adjustments && result.adjustments.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                      <TrendingUp className="h-4 w-4" /> Korekcije primijenjene na usporedbe
                    </h3>
                    <div className="flex flex-wrap gap-2">
                      {result.adjustments.map((a, i) => (
                        <div key={i} className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium border ${
                          a.percent > 0 ? "bg-green-50 border-green-200 text-green-800" :
                          a.percent < 0 ? "bg-red-50 border-red-200 text-red-800" :
                          "bg-gray-50 border-gray-200 text-gray-700"
                        }`}>
                          <span>{a.factor}</span>
                          <span className="font-bold">{a.percent > 0 ? "+" : ""}{a.percent.toFixed(1)}%</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Value Drivers */}
                {result.value_drivers?.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">AI Faktori vrijednosti</h3>
                    <div className="space-y-2">
                      {result.value_drivers.map((d, i) => (
                        <div key={i} className="flex items-center justify-between p-3 rounded-lg bg-gray-50 hover:bg-gray-100 transition">
                          <div className="flex-1 min-w-0">
                            <span className="text-sm font-medium text-gray-800">{d.factor}</span>
                            <p className="text-xs text-gray-500 mt-0.5">{d.impact}</p>
                          </div>
                          <span className={`ml-4 text-sm font-bold whitespace-nowrap ${d.adjustment_percent > 0 ? "text-green-600" : d.adjustment_percent < 0 ? "text-red-600" : "text-gray-400"}`}>
                            {d.adjustment_percent > 0 ? "+" : ""}{d.adjustment_percent}%
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Property Summary */}
                {result.target_property_summary && (
                  <div className="rounded-xl bg-blue-50 border border-blue-100 p-6">
                    <h3 className="text-xs font-semibold text-blue-400 uppercase tracking-wider mb-3 flex items-center gap-2">
                      <Home className="h-4 w-4" /> Sažetak nekretnine
                    </h3>
                    <p className="text-sm text-blue-900 leading-relaxed">{result.target_property_summary}</p>
                  </div>
                )}

                {/* Quick Reasoning Excerpt */}
                {result.valuation_reasoning && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <div className="flex items-center justify-between mb-3">
                      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider flex items-center gap-2">
                        <Brain className="h-4 w-4" /> Stručno mišljenje
                      </h3>
                      <button onClick={() => copyToClipboard(result.valuation_reasoning)}
                        className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg px-3 py-1.5 transition">
                        {copied ? <><Check className="h-3.5 w-3.5 text-green-600" /> Kopirano</> : <><ClipboardCopy className="h-3.5 w-3.5" /> Kopiraj</>}
                      </button>
                    </div>
                    <p className="text-sm text-gray-700 leading-relaxed whitespace-pre-wrap">{result.valuation_reasoning}</p>
                  </div>
                )}
              </div>
            )}

            {/* ════════ TAB: Comparables ════════ */}
            {resultTab === "comparables" && (
              <div className="space-y-6">

                {/* Engine comparables (used_comparables) */}
                {result.used_comparables && result.used_comparables.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                      <Cpu className="h-4 w-4" /> Engine — korištene usporedbe ({result.used_comparables.filter(c => !c.is_outlier).length})
                    </h3>
                    <div className="space-y-3">
                      {[...result.used_comparables]
                        .sort((a, b) => b.similarity_score - a.similarity_score)
                        .map((c, i) => (
                        <div key={i} className={`p-4 rounded-xl border transition ${c.is_outlier ? "border-red-200 bg-red-50/50 opacity-60" : "border-gray-100 bg-gray-50 hover:border-blue-200 hover:bg-blue-50/30"}`}>
                          <div className="flex items-start justify-between gap-3">
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2 flex-wrap">
                                <h4 className="text-sm font-semibold text-gray-900 truncate">{c.title}</h4>
                                {c.is_outlier && (
                                  <span className="text-[10px] font-bold uppercase tracking-wider bg-red-100 text-red-700 px-2 py-0.5 rounded-full">Outlier</span>
                                )}
                              </div>
                              <div className="flex items-center gap-4 mt-1.5">
                                <span className="text-sm font-bold text-blue-700">{formatPrice(Math.round(c.price_per_m2))} €/m²</span>
                                {c.total_price && <span className="text-xs text-gray-500">({formatPrice(Math.round(c.total_price))} €)</span>}
                                <SimilarityBadge score={c.similarity_score} />
                              </div>
                              {/* Enriched feature pills from BrightData */}
                              {(c.area_m2 || c.rooms || c.year_built || c.condition || c.floor || c.energy_class || c.heating || c.has_garage) && (
                                <div className="flex flex-wrap gap-1.5 mt-2">
                                  {c.area_m2 && <span className="text-[10px] bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full">{c.area_m2} m²</span>}
                                  {c.rooms && <span className="text-[10px] bg-purple-50 text-purple-700 px-2 py-0.5 rounded-full">{c.rooms} soba</span>}
                                  {c.floor && <span className="text-[10px] bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded-full">Kat {c.floor}</span>}
                                  {c.year_built && <span className="text-[10px] bg-amber-50 text-amber-700 px-2 py-0.5 rounded-full">God. {c.year_built}</span>}
                                  {c.condition && <span className="text-[10px] bg-green-50 text-green-700 px-2 py-0.5 rounded-full capitalize">{c.condition}</span>}
                                  {c.energy_class && <span className="text-[10px] bg-teal-50 text-teal-700 px-2 py-0.5 rounded-full">Energ. {c.energy_class}</span>}
                                  {c.heating && <span className="text-[10px] bg-orange-50 text-orange-700 px-2 py-0.5 rounded-full">{c.heating}</span>}
                                  {c.has_garage && <span className="text-[10px] bg-gray-100 text-gray-700 px-2 py-0.5 rounded-full">🅿️ Garaža</span>}
                                  {c.has_parking && !c.has_garage && <span className="text-[10px] bg-gray-100 text-gray-700 px-2 py-0.5 rounded-full">🅿️ Parking</span>}
                                </div>
                              )}
                            </div>
                            {c.url && (
                              <a href={c.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:text-blue-700 shrink-0">
                                <ExternalLink className="h-4 w-4" />
                              </a>
                            )}
                          </div>
                          {/* Similarity bar */}
                          <div className="mt-3 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                            <div className="h-full rounded-full transition-all duration-500" style={{
                              width: `${Math.max((c.similarity_score <= 1 ? c.similarity_score * 100 : c.similarity_score), 5)}%`,
                              backgroundColor: (c.similarity_score <= 1 ? c.similarity_score : c.similarity_score / 100) >= 0.7 ? "#22c55e" : (c.similarity_score <= 1 ? c.similarity_score : c.similarity_score / 100) >= 0.4 ? "#eab308" : "#ef4444"
                            }} />
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* AI-selected comparables */}
                {result.selected_comparables?.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                      <Bot className="h-4 w-4" /> AI — odabrane usporedbe ({result.selected_comparables.length})
                    </h3>
                    <div className="space-y-3">
                      {result.selected_comparables.map((c, i) => (
                        <div key={i} className="p-4 border border-gray-100 rounded-xl bg-gray-50 hover:border-blue-200 hover:bg-blue-50/30 transition">
                          <div className="flex items-start justify-between gap-3">
                            <div className="flex-1 min-w-0">
                              <h4 className="text-sm font-semibold text-gray-900 truncate">{c.title}</h4>
                              <div className="flex items-center gap-3 mt-1 text-xs text-gray-500">
                                <span className="font-bold text-blue-700 text-sm">{c.price}</span>
                                <span className="flex items-center gap-1"><MapPin className="h-3 w-3" /> {c.location}</span>
                              </div>
                            </div>
                            {c.url && (
                              <a href={c.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:text-blue-700 shrink-0">
                                <ExternalLink className="h-4 w-4" />
                              </a>
                            )}
                          </div>
                          <p className="text-xs text-gray-500 mt-2 italic">{c.reason_for_selection}</p>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Scatter: Price per m2 */}
                {result.used_comparables && result.used_comparables.filter(c => !c.is_outlier).length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                      <BarChart3 className="h-4 w-4" /> Raspodjela cijena/m²
                    </h3>
                    <PriceDistributionChart
                      comparables={result.used_comparables.filter(c => !c.is_outlier)}
                      calculatedPricePerM2={result.calculated_price_per_m2 ?? 0}
                    />
                  </div>
                )}
              </div>
            )}



            {/* ── Action Buttons ── */}
            <div className="flex flex-wrap items-center justify-center gap-3 pt-2">
              <button onClick={reset} className="rounded-xl bg-gray-100 px-6 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-200 transition flex items-center gap-2">
                <RefreshCw className="h-4 w-4" /> Nova procjena
              </button>
              <button onClick={() => copyToClipboard(
                `Procjena: ${formatPrice(result.estimated_value_eur)} €\n` +
                `Cijena/m²: ${result.calculated_price_per_m2 ? formatPrice(Math.round(result.calculated_price_per_m2)) + " €" : "N/A"}\n` +
                `Pouzdanost: ${result.math_confidence_score ?? "N/A"}/100\n\n` +
                (result.valuation_reasoning ?? "")
              )} className="rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-blue-700 transition flex items-center gap-2">
                <ClipboardCopy className="h-4 w-4" /> Kopiraj izvještaj
              </button>
            </div>

          </div>
        )}
      </div>
    </div>
    )}
    </>
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

function MiniStat({ icon, label, value }: { icon: React.ReactNode; label: string; value: string | number }) {
  return (
    <div className="rounded-xl bg-white border border-gray-200 p-3 flex items-center gap-3">
      <div className="shrink-0">{icon}</div>
      <div className="min-w-0">
        <p className="text-lg font-bold text-gray-900 truncate">{value}</p>
        <p className="text-[10px] text-gray-500 uppercase tracking-wider">{label}</p>
      </div>
    </div>
  );
}

function SimilarityBadge({ score }: { score: number }) {
  const pct = score <= 1 ? score * 100 : score; // handle both 0-1 and 0-100 scales
  const color = pct >= 70 ? "bg-green-100 text-green-800" : pct >= 40 ? "bg-yellow-100 text-yellow-800" : "bg-red-100 text-red-800";
  const label = pct >= 70 ? "Visoka" : pct >= 40 ? "Srednja" : "Niska";
  return (
    <span className={`inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${color}`}>
      {label} {pct.toFixed(0)}%
    </span>
  );
}

function PriceDistributionChart({ comparables, calculatedPricePerM2 }: { comparables: { title: string; price_per_m2: number }[]; calculatedPricePerM2: number }) {
  if (comparables.length === 0) return null;
  const prices = comparables.map(c => c.price_per_m2);
  const minP = Math.min(...prices);
  const maxP = Math.max(...prices);
  const range = maxP - minP || 1;

  return (
    <div className="space-y-3">
      {/* Bar chart */}
      <div className="space-y-2">
        {[...comparables]
          .sort((a, b) => b.price_per_m2 - a.price_per_m2)
          .map((c, i) => {
          const widthPct = Math.max(((c.price_per_m2 - minP * 0.9) / (maxP * 1.1 - minP * 0.9)) * 100, 8);
          return (
            <div key={i} className="flex items-center gap-3">
              <span className="text-xs text-gray-500 w-20 truncate shrink-0" title={c.title}>{c.title.slice(0, 18)}</span>
              <div className="flex-1 h-6 bg-gray-100 rounded-full overflow-hidden relative">
                <div className="h-full rounded-full transition-all duration-700" style={{
                  width: `${widthPct}%`,
                  backgroundColor: Math.abs(c.price_per_m2 - calculatedPricePerM2) / calculatedPricePerM2 < 0.1 ? "#3b82f6" : "#94a3b8"
                }} />
              </div>
              <span className="text-xs font-semibold text-gray-700 w-20 text-right shrink-0">{Math.round(c.price_per_m2).toLocaleString("hr-HR")} €</span>
            </div>
          );
        })}
      </div>
      {/* Legend */}
      <div className="flex items-center justify-center gap-4 text-xs text-gray-500 pt-2 border-t border-gray-100">
        <span className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full" style={{ backgroundColor: "#3b82f6" }} /> Blizu procjene (±10%)
        </span>
        <span className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full" style={{ backgroundColor: "#94a3b8" }} /> Ostale
        </span>
        <span className="font-semibold text-blue-700">Procjena: {Math.round(calculatedPricePerM2).toLocaleString("hr-HR")} €/m²</span>
      </div>
    </div>
  );
}
