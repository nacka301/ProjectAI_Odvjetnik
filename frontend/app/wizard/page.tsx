"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import dynamic from "next/dynamic";
import {
  ArrowLeft,
  ArrowRight,
  Home,
  Building2,
  TreePine,
  Loader2,
  AlertTriangle,
  Scale,
} from "lucide-react";
import LocationSelector from "@/components/LocationSelector";

import type {
  PropertyType,
  PropertyCondition,
  HouseFloors,
  LandUse,
  Orientation,
  ViewType,
  ViewState,
  ApartmentData,
  HouseData,
  LandData,
  DraftResponse,
  StreamStage,
  ValidationError,
  ValuationResult,
} from "./types";

import {
  INITIAL_STAGES,
  CONDITION_LABELS,
  FLOORS_LABELS,
  LAND_USE_LABELS,
  ORIENTATION_LABELS,
  VIEW_TYPE_LABELS,
  floorNumericValue,
} from "./constants";

import {
  NumberInput,
  SelectInput,
  Toggle,
  FloorSelect,
} from "./FormInputs";

import ProcessingView from "./ProcessingView";

// Lazy-load the heavy ResultView (tabs, charts, PDF)
const ResultView = dynamic(() => import("./ResultView"), {
  loading: () => (
    <div className="flex items-center justify-center py-20">
      <Loader2 className="h-8 w-8 animate-spin text-indigo-500" />
    </div>
  ),
});

import { API_BASE } from "@/lib/api";
import { useUserId } from "@/lib/useUserId";

// ═══════════════════════════════════════════
//  MAIN COMPONENT
// ═══════════════════════════════════════════

export default function WizardPage() {
  const userId = useUserId();
  // Wizard state
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [propertyType, setPropertyType] = useState<PropertyType | null>(null);

  // Location
  const [location, setLocation] = useState<{ city: string; district: string }>({ city: "", district: "" });

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
  const [email, setEmail] = useState("");
  const [notes, setNotes] = useState("");

  // View state machine: INPUT → PROCESSING → RESULT
  const [viewState, setViewState] = useState<ViewState>("INPUT");

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
        headers: { "Content-Type": "application/json", ...(userId ? { "x-user-id": userId } : {}) },
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
        headers: { "Content-Type": "application/json", ...(userId ? { "x-user-id": userId } : {}) },
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
  };

  const areaM2 = propertyType === "Apartment" ? apt.area_m2 : propertyType === "House" ? house.living_area_m2 : land.land_area_m2;

  // ═══════════════════════════════════════════
  //  RENDER
  // ═══════════════════════════════════════════

  return (
    <>
    {/* ═══ PROCESSING: Full-Screen Pipeline ═══ */}
    {viewState === "PROCESSING" && (
      <ProcessingView
        stages={stages}
        isStreaming={isStreaming}
        error={error}
        onCancel={cancelValuation}
        onReset={reset}
      />
    )}

    {/* Normal page wrapper — hidden during PROCESSING */}
    {viewState !== "PROCESSING" && (
    <div className="min-h-screen bg-gray-50 py-6 sm:py-12 px-4 font-sans">
      <div className="mx-auto max-w-3xl">
        <Link href="/" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6">
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        {/* Header + Step indicator — INPUT only */}
        {viewState === "INPUT" && (
          <>
            <div className="text-center mb-6 sm:mb-10">
              <h1 className="text-2xl sm:text-3xl font-bold text-gray-900">
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
              <div className="grid grid-cols-3 gap-2 sm:gap-4">
                {([
                  { type: "Apartment" as PropertyType, icon: <Building2 className="h-6 w-6 sm:h-8 sm:w-8" />, label: "Stan" },
                  { type: "House" as PropertyType, icon: <Home className="h-6 w-6 sm:h-8 sm:w-8" />, label: "Kuća" },
                  { type: "Land" as PropertyType, icon: <TreePine className="h-6 w-6 sm:h-8 sm:w-8" />, label: "Zemljište" },
                ]).map((opt) => (
                  <button
                    key={opt.type}
                    onClick={() => setPropertyType(opt.type)}
                    className={`flex flex-col items-center gap-1.5 sm:gap-2 rounded-xl border-2 p-3 sm:p-6 transition ${
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
              <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6 space-y-5">
                <h3 className="text-sm font-semibold text-gray-700">🏢 Detalji stana</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
              <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6 space-y-5">
                <h3 className="text-sm font-semibold text-gray-700">🏡 Detalji kuće</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
              <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6 space-y-5">
                <h3 className="text-sm font-semibold text-gray-700">🌳 Detalji zemljišta</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
                ))}n              </div>
            )}

            {/* Dodatne informacije */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-2">📝 Dodatne informacije <span className="text-xs font-normal text-gray-400">(opcionalno)</span></h4>
              <p className="text-xs text-gray-400 mb-2">Navedite sve što može utjecati na vrijednost: renovacije, posebnosti, blizina sadržaja...</p>
              <textarea value={notes} onChange={(e) => setNotes(e.target.value)}
                rows={3} maxLength={500}
                placeholder="npr. Stan je potpuno renoviran 2023., ima podne grijanje, klima u svim sobama, u blizini škole i tramvaja..."
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 resize-none" />
              <p className="text-xs text-gray-400 mt-1 text-right">{notes.length}/500</p>
            </div>

            {/* Email */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-2">📧 Email obavijest <span className="text-xs font-normal text-gray-400">(opcionalno)</span></h4>
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
          <ResultView
            result={result}
            propertyType={propertyType}
            location={location}
            areaM2={areaM2}
            onReset={reset}
          />
        )}
      </div>
    </div>
    )}
    </>
  );
}
