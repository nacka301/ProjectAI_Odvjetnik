"use client";

import { useState } from "react";
import {
  Check,
  Search,
  Brain,
  ExternalLink,
  ClipboardCopy,
  Cpu,
  Bot,
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
  Home,
  FileDown,
} from "lucide-react";
import type { ValuationResult, ResultTab } from "./types";
import { formatPrice } from "./constants";
import { MiniStat, SimilarityBadge, PriceDistributionChart } from "./FormInputs";
import { generateValuationPdf } from "@/lib/generatePdf";

interface ResultViewProps {
  result: ValuationResult;
  propertyType: string | null;
  location: { city: string; district: string };
  areaM2: number;
  onReset: () => void;
}

export default function ResultView({ result, propertyType, location, areaM2, onReset }: ResultViewProps) {
  const [resultTab, setResultTab] = useState<ResultTab>("overview");
  const [copied, setCopied] = useState(false);
  const [pdfLoading, setPdfLoading] = useState(false);

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

  const handlePdfExport = async () => {
    setPdfLoading(true);
    try {
      await generateValuationPdf({
        result,
        propertyType: propertyType || "nekretnina",
        location: `${location.city}${location.district ? ", " + location.district : ""}`,
        areaM2,
      });
    } catch (e) {
      console.error("PDF generation failed:", e);
    } finally {
      setPdfLoading(false);
    }
  };

  return (
    <div className="space-y-6">

      {/* â”€â”€ Hero: Estimated Value â”€â”€ */}
      <div className="rounded-2xl bg-gradient-to-br from-blue-600 via-indigo-700 to-purple-800 p-5 sm:p-8 text-white relative overflow-hidden">
        <div className="absolute inset-0 opacity-10" style={{ backgroundImage: "radial-gradient(circle at 30% 20%, white 1px, transparent 1px), radial-gradient(circle at 70% 80%, white 1px, transparent 1px)", backgroundSize: "40px 40px" }} />
        <div className="relative z-10 text-center">
          <p className="text-xs font-semibold uppercase tracking-widest text-blue-200">Procijenjena trĹľiĹˇna vrijednost</p>
          <p className="text-3xl sm:text-5xl md:text-6xl font-extrabold mt-3 tracking-tight">{formatPrice(result.estimated_value_eur)} â‚¬</p>

          {/* Price range */}
          {result.price_range ? (
            <p className="mt-2 text-sm text-blue-200">
              Raspon: {formatPrice(result.price_range.conservative_total)} â‚¬ â€” {formatPrice(result.price_range.optimistic_total)} â‚¬
            </p>
          ) : result.std_dev_per_m2 && result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0 && (() => {
            const rangeLow = Math.round((result.calculated_price_per_m2! - result.std_dev_per_m2!) * Math.max(areaM2, 1));
            const rangeHigh = Math.round((result.calculated_price_per_m2! + result.std_dev_per_m2!) * Math.max(areaM2, 1));
            return (
              <p className="mt-2 text-sm text-blue-200">
                Raspon: {formatPrice(Math.max(rangeLow, 0))} â‚¬ â€” {formatPrice(rangeHigh)} â‚¬
              </p>
            );
          })()}

          <div className="flex flex-wrap items-center justify-center gap-3 mt-4">
            {result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0 && (
              <span className="inline-flex items-center gap-1.5 bg-white/15 backdrop-blur-sm rounded-full px-4 py-1.5 text-sm font-medium">
                <Target className="h-3.5 w-3.5" /> {formatPrice(Math.round(result.calculated_price_per_m2))} â‚¬/mÂ˛
              </span>
            )}
            {result.math_confidence_score !== undefined && result.math_confidence_score > 0 && (
              <span className={`inline-flex items-center gap-1.5 rounded-full px-4 py-1.5 text-sm font-semibold ${
                result.math_confidence_score >= 70 ? "bg-green-400/20 text-green-100" :
                result.math_confidence_score >= 40 ? "bg-yellow-400/20 text-yellow-100" :
                "bg-red-400/20 text-red-100"
              }`}>
                <Shield className="h-3.5 w-3.5" /> {result.math_confidence_score}/100 â€” {result.math_confidence_level}
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

      {/* â”€â”€ Tab Navigation â”€â”€ */}
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

      {/* â•â•â•â•â•â•â•â• TAB: Overview â•â•â•â•â•â•â•â• */}
      {resultTab === "overview" && (
        <div className="space-y-6">
          {/* Stats Grid */}
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
            <MiniStat icon={<Search className="h-4 w-4 text-blue-500" />} label="PronaÄ‘eno" value={result.total_listings_found ?? 0} />
            <MiniStat icon={<Check className="h-4 w-4 text-green-500" />} label="KoriĹˇtenih" value={result.usable_price_points ?? 0} />
            <MiniStat icon={<Hash className="h-4 w-4 text-red-500" />} label="Outlieri" value={result.outliers_removed ?? 0} />
            <MiniStat icon={<Percent className="h-4 w-4 text-orange-500" />} label="Diskont" value={`${result.realization_discount_percent?.toFixed(1) ?? "15"}%`} />
            <MiniStat icon={<Target className="h-4 w-4 text-purple-500" />} label="â‚¬/mÂ˛ prosjek" value={result.market_average_asking ? formatPrice(Math.round(result.market_average_asking)) : "â€”"} />
            <MiniStat icon={<Activity className="h-4 w-4 text-indigo-500" />} label="ÎŁ korekcija" value={result.total_adjustment_percent !== undefined ? `${result.total_adjustment_percent > 0 ? "+" : ""}${result.total_adjustment_percent.toFixed(1)}%` : "â€”"} />
          </div>

          {/* Comparison Table */}
          {result.comparison_table && result.comparison_table.length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <Layers className="h-4 w-4" /> Tablica usporednih nekretnina
              </h3>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-200 text-left text-xs text-gray-500 uppercase tracking-wider">
                      <th className="pb-3 pr-3">#</th>
                      <th className="pb-3 pr-3">Oglas</th>
                      <th className="pb-3 pr-3 text-right">PovrĹˇina</th>
                      <th className="pb-3 pr-3 text-right">Cijena</th>
                      <th className="pb-3 pr-3 text-right">â‚¬/mÂ˛</th>
                      <th className="pb-3 text-right">SliÄŤnost</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {result.comparison_table.map((row) => (
                      <tr key={row.index} className="hover:bg-gray-50 transition">
                        <td className="py-2.5 pr-3 text-gray-400 font-medium">{row.index}</td>
                        <td className="py-2.5 pr-3 max-w-[200px]">
                          {row.url ? (
                            <a href={row.url} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline truncate block">{row.title.slice(0, 40)}{row.title.length > 40 ? "â€¦" : ""}</a>
                          ) : (
                            <span className="text-gray-800 truncate block">{row.title.slice(0, 40)}</span>
                          )}
                        </td>
                        <td className="py-2.5 pr-3 text-right text-gray-700">{row.area_m2 > 0 ? `${formatPrice(row.area_m2)} mÂ˛` : "â€”"}</td>
                        <td className="py-2.5 pr-3 text-right text-gray-700">{row.total_price > 0 ? `${formatPrice(Math.round(row.total_price))} â‚¬` : "â€”"}</td>
                        <td className="py-2.5 pr-3 text-right font-medium text-gray-900">{formatPrice(Math.round(row.raw_price_per_m2))} â‚¬</td>
                        <td className="py-2.5 text-right">
                          <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold cursor-help ${
                            row.similarity_score >= 70 ? "bg-green-100 text-green-800" :
                            row.similarity_score >= 40 ? "bg-yellow-100 text-yellow-800" :
                            "bg-red-100 text-red-800"
                          }`} title={row.similarity_reason || undefined}>{row.similarity_score}%</span>
                          {row.similarity_reason && (
                            <div className="text-[9px] text-gray-400 max-w-[150px] whitespace-normal text-right mt-0.5">{row.similarity_reason}</div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    {/* V27: Ponderirani prosjek â€” weighted median from backend, NOT recalculated */}
                    <tr className="border-t-2 border-gray-300 bg-gray-50 font-semibold">
                      <td colSpan={4} className="py-3 pr-3 text-right text-gray-600">Ponderirani prosjek:</td>
                      <td className="py-3 pr-3 text-right text-blue-800 text-base">
                        {result.market_average_asking ? `${formatPrice(Math.round(result.market_average_asking))} â‚¬/mÂ˛` : "â€”"}
                      </td>
                      <td></td>
                    </tr>
                    {/* V27: After realization gap */}
                    {result.realization_discount_percent && (
                      <tr className="bg-gray-50 text-sm">
                        <td colSpan={4} className="py-2 pr-3 text-right text-gray-500">
                          Nakon diskonta ({result.realization_discount_percent.toFixed(1)}%):
                        </td>
                        <td className="py-2 pr-3 text-right font-bold text-indigo-700 text-base">
                          {result.after_discount_per_m2 ? `${formatPrice(Math.round(result.after_discount_per_m2))} â‚¬/mÂ˛` : (result.calculated_price_per_m2 ? `${formatPrice(Math.round(result.calculated_price_per_m2))} â‚¬/mÂ˛` : "â€”")}
                        </td>
                        <td></td>
                      </tr>
                    )}
                    {/* V27: After contextual adjustments */}
                    {result.total_adjustment_percent !== undefined && result.total_adjustment_percent !== 0 && (
                      <tr className="bg-gray-50 text-sm">
                        <td colSpan={4} className="py-2 pr-3 text-right text-gray-500">
                          ÎŁ Korekcija ({result.total_adjustment_percent > 0 ? "+" : ""}{result.total_adjustment_percent.toFixed(1)}%):
                        </td>
                        <td className="py-2 pr-3 text-right font-bold text-purple-700 text-base">
                          {result.calculated_price_per_m2 ? `${formatPrice(Math.round(result.calculated_price_per_m2))} â‚¬/mÂ˛` : "â€”"}
                        </td>
                        <td></td>
                      </tr>
                    )}
                    <tr className="bg-blue-50 text-base">
                      <td colSpan={4} className="py-3 pr-3 text-right font-bold text-blue-900">
                        KonaÄŤna procjena:
                      </td>
                      <td colSpan={2} className="py-3 text-right font-extrabold text-blue-900 text-lg">
                        {formatPrice(result.estimated_value_eur)} â‚¬
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            </div>
          )}

          {/* Price Range */}
          {result.price_range && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <BarChart3 className="h-4 w-4" /> Raspon procjene
              </h3>
              <div className="space-y-4">
                {[
                  { label: "Konzervativno", perM2: result.price_range.conservative_price_per_m2, total: result.price_range.conservative_total, color: "bg-amber-500", textColor: "text-amber-700" },
                  { label: "Realno", perM2: result.price_range.realistic_price_per_m2, total: result.price_range.realistic_total, color: "bg-blue-600", textColor: "text-blue-700" },
                  { label: "OptimistiÄŤno", perM2: result.price_range.optimistic_price_per_m2, total: result.price_range.optimistic_total, color: "bg-green-500", textColor: "text-green-700" },
                ].map((tier) => {
                  const maxTotal = result.price_range!.optimistic_total || 1;
                  const widthPct = Math.max((tier.total / maxTotal) * 100, 15);
                  return (
                    <div key={tier.label} className="flex items-center gap-4">
                      <span className={`w-28 text-sm font-medium ${tier.textColor}`}>{tier.label}</span>
                      <div className="flex-1 h-8 bg-gray-100 rounded-lg overflow-hidden relative">
                        <div className={`h-full ${tier.color} rounded-lg transition-all duration-700 flex items-center justify-end pr-3`} style={{ width: `${widthPct}%` }}>
                          <span className="text-xs font-bold text-white whitespace-nowrap">{formatPrice(Math.round(tier.perM2))} â‚¬/mÂ˛</span>
                        </div>
                      </div>
                      <span className={`w-28 text-right text-sm font-bold ${tier.textColor}`}>{formatPrice(tier.total)} â‚¬</span>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Benchmark Summary */}
          {result.benchmark_summary && (
            <div className="rounded-xl bg-indigo-50 border border-indigo-100 p-4">
              <h3 className="text-xs font-semibold text-indigo-400 uppercase tracking-wider mb-2 flex items-center gap-2">
                <Target className="h-3.5 w-3.5" /> Benchmark regije
              </h3>
              <p className="text-sm text-indigo-900">{result.benchmark_summary}</p>
            </div>
          )}

          {/* Confidence detail */}
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

          {/* V48: Unified price structure â€” merges Adjustments + Value Drivers */}
          {(() => {
            // Build unified list of all price factors (EXCLUDING realization discount â€”
            // discount is already shown in the table footer as a separate step)
            const factors: { name: string; desc: string; pct: number }[] = [];

            // 1. Engine adjustments (garage, renovation, condition, etc.)
            if (result.adjustments?.length) {
              for (const a of result.adjustments) {
                factors.push({ name: a.factor, desc: a.description, pct: a.percent });
              }
            }

            // 2. AI value drivers (deduplicated â€” skip if already covered by adjustments)
            if (result.value_drivers?.length) {
              const existingNames = new Set(factors.map(f => f.name.toLowerCase()));
              for (const d of result.value_drivers) {
                const key = d.factor.toLowerCase();
                if (!existingNames.has(key)) {
                  factors.push({ name: d.factor, desc: d.impact, pct: d.adjustment_percent });
                  existingNames.add(key);
                }
              }
            }

            if (factors.length === 0) return null;

            // V48e: Use backend's authoritative total â€” matches the table footer's "ÎŁ Korekcija"
            const totalPct = result.total_adjustment_percent ?? factors.reduce((sum, f) => sum + f.pct, 0);

            return (
              <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
                <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                  <TrendingUp className="h-4 w-4" /> Struktura formiranja cijene
                </h3>
                <div className="space-y-2">
                  {factors.map((f, i) => (
                    <div key={i} className={`flex items-start justify-between p-3 rounded-lg border ${f.pct > 0 ? "bg-green-50/50 border-green-100" : f.pct < 0 ? "bg-red-50/50 border-red-100" : "bg-gray-50 border-gray-100"}`}>
                      <div className="flex-1 min-w-0">
                        <span className={`text-sm font-semibold ${f.pct > 0 ? "text-green-800" : f.pct < 0 ? "text-red-800" : "text-gray-700"}`}>{f.name}</span>
                        <p className="text-xs text-gray-500 mt-0.5">{f.desc}</p>
                      </div>
                      <span className={`ml-4 text-sm font-bold whitespace-nowrap ${f.pct > 0 ? "text-green-600" : f.pct < 0 ? "text-red-600" : "text-gray-400"}`}>
                        {f.pct > 0 ? "+" : ""}{f.pct.toFixed(1)}%
                      </span>
                    </div>
                  ))}
                </div>
                {/* Total */}
                <div className={`mt-3 flex items-center justify-between p-3 rounded-lg border-2 ${totalPct > 0 ? "border-green-300 bg-green-50" : totalPct < 0 ? "border-red-300 bg-red-50" : "border-gray-300 bg-gray-50"}`}>
                  <span className="text-sm font-bold text-gray-700">ÎŁ Ukupni utjecaj</span>
                  <span className={`text-sm font-bold ${totalPct > 0 ? "text-green-700" : totalPct < 0 ? "text-red-700" : "text-gray-500"}`}>
                    {totalPct > 0 ? "+" : ""}{totalPct.toFixed(1)}%
                  </span>
                </div>
              </div>
            );
          })()}

          {/* Property Summary */}
          {result.target_property_summary && (
            <div className="rounded-xl bg-blue-50 border border-blue-100 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-blue-400 uppercase tracking-wider mb-3 flex items-center gap-2">
                <Home className="h-4 w-4" /> SaĹľetak nekretnine
              </h3>
              <p className="text-sm text-blue-900 leading-relaxed">{result.target_property_summary}</p>
            </div>
          )}

          {/* Reasoning */}
          {result.valuation_reasoning && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider flex items-center gap-2">
                  <Brain className="h-4 w-4" /> StruÄŤno miĹˇljenje
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

      {/* â•â•â•â•â•â•â•â• TAB: Comparables â•â•â•â•â•â•â•â• */}
      {resultTab === "comparables" && (
        <div className="space-y-6">

          {/* Engine comparables */}
          {result.used_comparables && result.used_comparables.length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <Cpu className="h-4 w-4" /> Engine â€” koriĹˇtene usporedbe ({result.used_comparables.filter(c => !c.is_outlier).length})
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
                          {c.is_outlier && c.outlier_reason && (
                            <span className="text-[10px] text-red-500 italic">{c.outlier_reason}</span>
                          )}
                        </div>
                        <div className="flex items-center gap-4 mt-1.5">
                          <span className="text-sm font-bold text-blue-700">{formatPrice(Math.round(c.price_per_m2))} â‚¬/mÂ˛</span>
                          {c.total_price && <span className="text-xs text-gray-500">({formatPrice(Math.round(c.total_price))} â‚¬)</span>}
                          <SimilarityBadge score={c.similarity_score} />
                        </div>
                        {(c.area_m2 || c.rooms || c.year_built || c.condition || c.floor || c.energy_class || c.heating || c.has_garage) && (
                          <div className="flex flex-wrap gap-1.5 mt-2">
                            {c.area_m2 && <span className="text-[10px] bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full">{c.area_m2} mÂ˛</span>}
                            {c.rooms && <span className="text-[10px] bg-purple-50 text-purple-700 px-2 py-0.5 rounded-full">{c.rooms} soba</span>}
                            {c.floor && <span className="text-[10px] bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded-full">Kat {c.floor}</span>}
                            {c.year_built && <span className="text-[10px] bg-amber-50 text-amber-700 px-2 py-0.5 rounded-full">God. {c.year_built}</span>}
                            {c.condition && <span className="text-[10px] bg-green-50 text-green-700 px-2 py-0.5 rounded-full capitalize">{c.condition}</span>}
                            {c.energy_class && <span className="text-[10px] bg-teal-50 text-teal-700 px-2 py-0.5 rounded-full">Energ. {c.energy_class}</span>}
                            {c.heating && <span className="text-[10px] bg-orange-50 text-orange-700 px-2 py-0.5 rounded-full">{c.heating}</span>}
                            {c.has_garage && <span className="text-[10px] bg-gray-100 text-gray-700 px-2 py-0.5 rounded-full">đź…żď¸Ź GaraĹľa</span>}
                            {c.has_parking && !c.has_garage && <span className="text-[10px] bg-gray-100 text-gray-700 px-2 py-0.5 rounded-full">đź…żď¸Ź Parking</span>}
                          </div>
                        )}
                      </div>
                      {c.url && (
                        <a href={c.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:text-blue-700 shrink-0">
                          <ExternalLink className="h-4 w-4" />
                        </a>
                      )}
                    </div>
                    <div className="mt-3 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                      <div className="h-full rounded-full transition-all duration-500" style={{
                        width: `${Math.max((c.similarity_score <= 1 ? c.similarity_score * 100 : c.similarity_score), 5)}%`,
                        backgroundColor: (c.similarity_score <= 1 ? c.similarity_score : c.similarity_score / 100) >= 0.7 ? "#22c55e" : (c.similarity_score <= 1 ? c.similarity_score : c.similarity_score / 100) >= 0.4 ? "#eab308" : "#ef4444"
                      }} />
                    </div>
                    {/* V48: Similarity reason â€” AI explanation of why this comp is similar/different */}
                    {c.similarity_reason && (
                      <div className="mt-1.5 text-[11px] text-gray-500 italic">đź’ˇ {c.similarity_reason}</div>
                    )}
                    {/* Per-comp correction */}
                    {!c.is_outlier && c.correction_percent !== undefined && c.correction_percent !== 0 && (
                      <div className={`mt-2 text-xs px-3 py-1.5 rounded-md ${c.correction_percent > 0 ? "bg-green-50 text-green-700" : "bg-red-50 text-red-700"}`}>
                        <span className="font-semibold">Korekcija: {c.correction_percent > 0 ? "+" : ""}{c.correction_percent.toFixed(1)}%</span>
                        {c.correction_reason && <span className="ml-1 text-gray-500">â€” {c.correction_reason}</span>}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* AI-selected comparables */}
          {result.selected_comparables?.length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <Bot className="h-4 w-4" /> AI â€” odabrane usporedbe ({result.selected_comparables.length})
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

          {/* Price Distribution Chart */}
          {result.used_comparables && result.used_comparables.filter(c => !c.is_outlier).length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <BarChart3 className="h-4 w-4" /> Raspodjela cijena/mÂ˛
              </h3>
              <PriceDistributionChart
                comparables={result.used_comparables.filter(c => !c.is_outlier)}
                calculatedPricePerM2={result.calculated_price_per_m2 ?? 0}
              />
            </div>
          )}
        </div>
      )}

      {/* â”€â”€ Action Buttons â”€â”€ */}
      <div className="flex flex-wrap items-center justify-center gap-3 pt-2">
        <button onClick={onReset} className="rounded-xl bg-gray-100 px-6 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-200 transition flex items-center gap-2">
          <RefreshCw className="h-4 w-4" /> Nova procjena
        </button>
        <button onClick={handlePdfExport} disabled={pdfLoading}
          className="rounded-xl bg-indigo-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition flex items-center gap-2">
          <FileDown className="h-4 w-4" /> {pdfLoading ? "Generiram PDF..." : "Preuzmi PDF"}
        </button>
        <button onClick={() => copyToClipboard(
          `Procjena: ${formatPrice(result.estimated_value_eur)} â‚¬\n` +
          `Cijena/mÂ˛: ${result.calculated_price_per_m2 ? formatPrice(Math.round(result.calculated_price_per_m2)) + " â‚¬" : "N/A"}\n` +
          `Pouzdanost: ${result.math_confidence_score ?? "N/A"}/100\n\n` +
          (result.valuation_reasoning ?? "")
        )} className="rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-blue-700 transition flex items-center gap-2">
          <ClipboardCopy className="h-4 w-4" /> Kopiraj izvjeĹˇtaj
        </button>
      </div>

    </div>
  );
}
