"use client";

import { useState } from "react";
import {
  Check,
  Search,
  Brain,
  ExternalLink,
  ClipboardCopy,
  Cpu,
  ListChecks,
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
  Scale,
  Calculator,
  FlaskConical,
  Gauge,
  ArrowDownRight,
  ArrowRight,
  CheckCircle2,
  XCircle,
  Info,
} from "lucide-react";
import type { ValuationResult, ResultTab } from "./types";
import { formatPrice } from "./constants";
import { MiniStat, SimilarityBadge, PriceDistributionChart } from "./FormInputs";
import { generateValuationPdf } from "@/lib/generatePdf";
import { MethodologyButton, MethodologyModal } from "@/components/MethodologyModal";

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
  const [pdfError, setPdfError] = useState(false);
  const [showMethodology, setShowMethodology] = useState(false);

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
    } catch {
      setPdfError(true);
      setTimeout(() => setPdfError(false), 4000);
    } finally {
      setPdfLoading(false);
    }
  };

  return (
    <div className="space-y-6">

      {/* ── Hero: Estimated Value ── */}
      <div className="rounded-2xl bg-gradient-to-br from-blue-600 via-indigo-700 to-purple-800 p-5 sm:p-8 text-white relative overflow-hidden">
        <div className="absolute inset-0 opacity-10" style={{ backgroundImage: "radial-gradient(circle at 30% 20%, white 1px, transparent 1px), radial-gradient(circle at 70% 80%, white 1px, transparent 1px)", backgroundSize: "40px 40px" }} />
        <div className="relative z-10 text-center">
          <p className="text-xs font-semibold uppercase tracking-widest text-blue-200">Procijenjena tržišna vrijednost</p>
          <p className="text-3xl sm:text-5xl md:text-6xl font-extrabold mt-3 tracking-tight">{formatPrice(result.estimated_value_eur)} €</p>

          {/* Price range */}
          {result.price_range ? (
            <p className="mt-2 text-sm text-blue-200">
              Raspon: {formatPrice(result.price_range.conservative_total)} € — {formatPrice(result.price_range.optimistic_total)} €
            </p>
          ) : result.std_dev_per_m2 && result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0 && (() => {
            const rangeLow = Math.round((result.calculated_price_per_m2! - result.std_dev_per_m2!) * Math.max(areaM2, 1));
            const rangeHigh = Math.round((result.calculated_price_per_m2! + result.std_dev_per_m2!) * Math.max(areaM2, 1));
            return (
              <p className="mt-2 text-sm text-blue-200">
                Raspon: {formatPrice(Math.max(rangeLow, 0))} € — {formatPrice(rangeHigh)} €
              </p>
            );
          })()}

          <div className="flex flex-wrap items-center justify-center gap-3 mt-4">
            {result.valuation_method && (
              <span className="inline-flex items-center gap-1.5 bg-white/15 backdrop-blur-sm rounded-full px-4 py-1.5 text-sm font-medium">
                <Scale className="h-3.5 w-3.5" /> {result.valuation_method}
              </span>
            )}
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
            <MiniStat icon={<Check className="h-4 w-4 text-green-500" />} label="Korištenih" value={result.comparison_table?.length ?? result.usable_price_points ?? 0} />
            <MiniStat icon={<Hash className="h-4 w-4 text-red-500" />} label="Outlieri" value={result.outliers_removed ?? 0} />
            <MiniStat icon={<Percent className="h-4 w-4 text-orange-500" />} label="Diskont traž→real" value={`${result.realization_discount_percent?.toFixed(1) ?? "15"}%`} />
            <MiniStat icon={<Target className="h-4 w-4 text-purple-500" />} label="€/m² arit. sred." value={result.elaborat_stats?.arithmetic_mean_per_m2 ? formatPrice(Math.round(result.elaborat_stats.arithmetic_mean_per_m2)) : (result.market_average_asking ? formatPrice(Math.round(result.market_average_asking)) : "—")} />
            <MiniStat icon={<Activity className="h-4 w-4 text-indigo-500" />} label="Σ korekcija" value={result.total_adjustment_percent !== undefined ? `${result.total_adjustment_percent > 0 ? "+" : ""}${result.total_adjustment_percent.toFixed(1)}%` : "—"} />
          </div>

          {/* Comparison Table — Elaborat-style with K-coefficients */}
          {result.comparison_table && result.comparison_table.length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <Layers className="h-4 w-4" /> Tablica usporednih nekretnina
              </h3>
              {/* Check if any row has K-coefficients */}
              {(() => {
                const hasK = result.comparison_table!.some(r => r.k_total !== undefined && r.k_total !== 0);
                return (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-gray-200 text-left text-xs text-gray-500 uppercase tracking-wider">
                          <th className="pb-3 pr-2">#</th>
                          <th className="pb-3 pr-2">Oglas</th>
                          <th className="pb-3 pr-2 text-right">m²</th>
                          <th className="pb-3 pr-2 text-right">€/m²</th>
                          {hasK && <th className="pb-3 pr-1 text-right" title="Koeficijent stanja">Kc</th>}
                          {hasK && <th className="pb-3 pr-1 text-right" title="Koeficijent lokacije">Kl</th>}
                          {hasK && <th className="pb-3 pr-1 text-right" title="Koeficijent veličine">Ks</th>}
                          {hasK && <th className="pb-3 pr-1 text-right" title="Koeficijent orijentacije/pogleda">Ko</th>}
                          {hasK && <th className="pb-3 pr-2 text-right font-bold" title="Kc × Kl × Ks × Ko">K</th>}
                          <th className="pb-3 pr-2 text-right">Korig. €/m²</th>
                          <th className="pb-3 text-right">Sličnost</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {result.comparison_table!.map((row) => (
                          <tr key={row.index} className="hover:bg-gray-50 transition">
                            <td className="py-2.5 pr-2 text-gray-400 font-medium">{row.index}</td>
                            <td className="py-2.5 pr-2 max-w-[160px]">
                              {row.url ? (
                                <a href={row.url} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline truncate block text-xs">{row.title.slice(0, 35)}{row.title.length > 35 ? "…" : ""}</a>
                              ) : (
                                <span className="text-gray-800 truncate block text-xs">{row.title.slice(0, 35)}</span>
                              )}
                            </td>
                            <td className="py-2.5 pr-2 text-right text-gray-700 text-xs">{row.area_m2 > 0 ? row.area_m2 : "—"}</td>
                            <td className="py-2.5 pr-2 text-right text-gray-700">{formatPrice(Math.round(row.raw_price_per_m2))}</td>
                            {hasK && <td className="py-2.5 pr-1 text-right text-xs text-gray-600">{row.kc?.toFixed(2) ?? "—"}</td>}
                            {hasK && <td className="py-2.5 pr-1 text-right text-xs text-gray-600">{row.kl?.toFixed(2) ?? "—"}</td>}
                            {hasK && <td className="py-2.5 pr-1 text-right text-xs text-gray-600">{row.ks?.toFixed(2) ?? "—"}</td>}
                            {hasK && <td className="py-2.5 pr-1 text-right text-xs text-gray-600">{row.ko?.toFixed(2) ?? "—"}</td>}
                            {hasK && (
                              <td className={`py-2.5 pr-2 text-right text-xs font-bold ${
                                (row.k_total ?? 1) > 1.02 ? "text-green-700" : (row.k_total ?? 1) < 0.98 ? "text-red-700" : "text-gray-700"
                              }`}>{row.k_total?.toFixed(2) ?? "—"}</td>
                            )}
                            <td className="py-2.5 pr-2 text-right font-medium text-gray-900">{formatPrice(Math.round(row.adjusted_price_per_m2))}</td>
                            <td className="py-2.5 text-right">
                              <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold cursor-help ${
                                row.similarity_score >= 70 ? "bg-green-100 text-green-800" :
                                row.similarity_score >= 40 ? "bg-yellow-100 text-yellow-800" :
                                "bg-red-100 text-red-800"
                              }`} title={row.similarity_reason || undefined}>{row.similarity_score}%</span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        {/* Aritmetička sredina */}
                        <tr className="border-t-2 border-gray-300 bg-gray-50 font-semibold">
                          <td colSpan={hasK ? 9 : 3} className="py-3 pr-3 text-right text-gray-600">
                            {result.elaborat_stats ? "Aritmetička sredina:" : "Ponderirani prosjek:"}
                          </td>
                          <td className="py-3 pr-2 text-right text-blue-800 text-base">
                            {result.elaborat_stats?.arithmetic_mean_per_m2
                              ? `${formatPrice(Math.round(result.elaborat_stats.arithmetic_mean_per_m2))} €/m²`
                              : (result.market_average_asking ? `${formatPrice(Math.round(result.market_average_asking))} €/m²` : "—")}
                          </td>
                          <td></td>
                        </tr>
                        {/* Market factors (Fp + Ft) if available */}
                        {result.market_factors && result.market_factors.combined !== 1 && (
                          <tr className="bg-gray-50 text-sm">
                            <td colSpan={hasK ? 9 : 3} className="py-2 pr-3 text-right text-gray-500">
                              Tržišni faktor (1+Fp+Ft = {result.market_factors.combined.toFixed(2)}):
                            </td>
                            <td className="py-2 pr-2 text-right font-bold text-teal-700 text-base">
                              {result.elaborat_stats?.arithmetic_mean_per_m2
                                ? `${formatPrice(Math.round(result.elaborat_stats.arithmetic_mean_per_m2 * result.market_factors.combined))} €/m²`
                                : "—"}
                            </td>
                            <td></td>
                          </tr>
                        )}
                        {/* After realization gap */}
                        {result.realization_discount_percent && (
                          <tr className="bg-gray-50 text-sm">
                            <td colSpan={hasK ? 9 : 3} className="py-2 pr-3 text-right text-gray-500">
                              Diskont tražene → realizirane ({result.realization_discount_percent.toFixed(1)}%):
                            </td>
                            <td className="py-2 pr-2 text-right font-bold text-indigo-700 text-base">
                              {result.after_discount_per_m2 ? `${formatPrice(Math.round(result.after_discount_per_m2))} €/m²` : (result.calculated_price_per_m2 ? `${formatPrice(Math.round(result.calculated_price_per_m2))} €/m²` : "—")}
                            </td>
                            <td></td>
                          </tr>
                        )}
                        {/* After contextual adjustments */}
                        {result.total_adjustment_percent !== undefined && result.total_adjustment_percent !== 0 && (
                          <tr className="bg-gray-50 text-sm">
                            <td colSpan={hasK ? 9 : 3} className="py-2 pr-3 text-right text-gray-500">
                              Σ Korekcija agenta ({result.total_adjustment_percent > 0 ? "+" : ""}{result.total_adjustment_percent.toFixed(1)}%):
                            </td>
                            <td className="py-2 pr-2 text-right font-bold text-purple-700 text-base">
                              {result.calculated_price_per_m2 ? `${formatPrice(Math.round(result.calculated_price_per_m2))} €/m²` : "—"}
                            </td>
                            <td></td>
                          </tr>
                        )}
                        <tr className="bg-blue-50 text-base">
                          <td colSpan={hasK ? 9 : 3} className="py-3 pr-3 text-right font-bold text-blue-900">
                            Konačna procjena:
                          </td>
                          <td colSpan={2} className="py-3 text-right font-extrabold text-blue-900 text-lg">
                            {formatPrice(result.estimated_value_eur)} €
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                );
              })()}
            </div>
          )}

          {/* 2σ Test — Elaborat statistical validation */}
          {result.elaborat_stats && (
            <div className={`rounded-xl border p-4 sm:p-6 ${
              result.elaborat_stats.two_sigma_test_passed
                ? "bg-emerald-50 border-emerald-200"
                : "bg-amber-50 border-amber-200"
            }`}>
              <h3 className="text-xs font-semibold uppercase tracking-wider mb-3 flex items-center gap-2">
                <FlaskConical className="h-4 w-4" /> 2σ Test — Statistička validacija
                {result.elaborat_stats.two_sigma_test_passed
                  ? <span className="inline-flex items-center gap-1 text-emerald-700 text-xs font-bold"><CheckCircle2 className="h-3.5 w-3.5" /> Zadovoljava</span>
                  : <span className="inline-flex items-center gap-1 text-amber-700 text-xs font-bold"><XCircle className="h-3.5 w-3.5" /> Korigirano</span>
                }
              </h3>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-3">
                <div className="text-center">
                  <p className="text-lg font-bold text-gray-900">{formatPrice(Math.round(result.elaborat_stats.arithmetic_mean_per_m2))} €/m²</p>
                  <p className="text-[10px] text-gray-500 uppercase">Aritm. sredina</p>
                </div>
                <div className="text-center">
                  <p className="text-lg font-bold text-gray-900">{result.elaborat_stats.std_dev_per_m2.toFixed(0)} €/m²</p>
                  <p className="text-[10px] text-gray-500 uppercase">Std. devijacija</p>
                </div>
                <div className="text-center">
                  <p className={`text-lg font-bold ${
                    result.elaborat_stats.relative_std_dev_percent <= 15 ? "text-green-700" :
                    result.elaborat_stats.relative_std_dev_percent <= 30 ? "text-yellow-700" : "text-red-700"
                  }`}>{result.elaborat_stats.relative_std_dev_percent.toFixed(1)}%</p>
                  <p className="text-[10px] text-gray-500 uppercase">Koef. varijacije</p>
                </div>
                <div className="text-center">
                  <p className="text-lg font-bold text-gray-900">{result.elaborat_stats.two_sigma_excluded}</p>
                  <p className="text-[10px] text-gray-500 uppercase">Isključeno (2σ)</p>
                </div>
              </div>
              {result.elaborat_stats.explanation && (
                <p className="text-xs text-gray-600 leading-relaxed">
                  <Info className="h-3 w-3 inline mr-1" />{result.elaborat_stats.explanation}
                </p>
              )}
            </div>
          )}

          {/* Fp/Ft Market Adjustment Factors */}
          {result.market_factors && (result.market_factors.fp !== 0 || result.market_factors.ft !== 0) && (
            <div className="rounded-xl bg-teal-50 border border-teal-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-teal-600 uppercase tracking-wider mb-3 flex items-center gap-2">
                <Gauge className="h-4 w-4" /> Tržišni faktori korekcije
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div className="rounded-lg bg-white border border-teal-100 p-3 text-center">
                  <p className="text-xs text-gray-500 mb-1">Fp (veličina)</p>
                  <p className={`text-xl font-bold ${result.market_factors.fp > 0 ? "text-green-700" : result.market_factors.fp < 0 ? "text-red-700" : "text-gray-700"}`}>
                    {result.market_factors.fp > 0 ? "+" : ""}{(result.market_factors.fp * 100).toFixed(0)}%
                  </p>
                  <p className="text-[10px] text-gray-400 mt-1">{result.market_factors.fp_reason}</p>
                </div>
                <div className="rounded-lg bg-white border border-teal-100 p-3 text-center">
                  <p className="text-xs text-gray-500 mb-1">Ft (tržišni kontekst)</p>
                  <p className={`text-xl font-bold ${result.market_factors.ft > 0 ? "text-green-700" : result.market_factors.ft < 0 ? "text-red-700" : "text-gray-700"}`}>
                    {result.market_factors.ft > 0 ? "+" : ""}{(result.market_factors.ft * 100).toFixed(0)}%
                  </p>
                  <p className="text-[10px] text-gray-400 mt-1">{result.market_factors.ft_reason}</p>
                </div>
                <div className="rounded-lg bg-teal-100 border border-teal-200 p-3 text-center">
                  <p className="text-xs text-teal-700 font-semibold mb-1">Kombinirani (1+Fp+Ft)</p>
                  <p className="text-xl font-bold text-teal-900">{result.market_factors.combined.toFixed(2)}</p>
                  <p className="text-[10px] text-teal-600 mt-1">Multiplikator na baznu cijenu</p>
                </div>
              </div>
            </div>
          )}

          {/* Razloženi proračun — Pipeline breakdown */}
          {result.elaborat_stats && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <Calculator className="h-4 w-4" /> Razloženi proračun
              </h3>
              <div className="space-y-2">
                {/* Step 1: Arithmetic mean */}
                <div className="flex items-center justify-between p-3 rounded-lg bg-blue-50 border border-blue-100">
                  <div className="flex items-center gap-2">
                    <span className="h-6 w-6 rounded-full bg-blue-600 text-white text-xs font-bold flex items-center justify-center">1</span>
                    <span className="text-sm font-medium text-blue-900">
                      Aritmetička sredina korigiranih cijena
                    </span>
                  </div>
                  <span className="text-sm font-bold text-blue-800">
                    {formatPrice(Math.round(result.elaborat_stats.arithmetic_mean_per_m2))} €/m²
                  </span>
                </div>

                {/* Step 2: Market factors */}
                {result.market_factors && result.market_factors.combined !== 1 && (
                  <div className="flex items-center justify-between p-3 rounded-lg bg-teal-50 border border-teal-100">
                    <div className="flex items-center gap-2">
                      <span className="h-6 w-6 rounded-full bg-teal-600 text-white text-xs font-bold flex items-center justify-center">2</span>
                      <span className="text-sm font-medium text-teal-900">
                        × Tržišni faktor ({result.market_factors.combined.toFixed(2)})
                      </span>
                    </div>
                    <span className="text-sm font-bold text-teal-800">
                      {formatPrice(Math.round(result.elaborat_stats.arithmetic_mean_per_m2 * result.market_factors.combined))} €/m²
                    </span>
                  </div>
                )}

                {/* Step 3: Realization discount */}
                {result.realization_discount_percent !== undefined && result.realization_discount_percent > 0 && (
                  <div className="flex items-center justify-between p-3 rounded-lg bg-orange-50 border border-orange-100">
                    <div className="flex items-center gap-2">
                      <span className="h-6 w-6 rounded-full bg-orange-600 text-white text-xs font-bold flex items-center justify-center">
                        {result.market_factors && result.market_factors.combined !== 1 ? "3" : "2"}
                      </span>
                      <span className="text-sm font-medium text-orange-900">
                        − Diskont oglasna → realizirana ({result.realization_discount_percent.toFixed(1)}%)
                      </span>
                    </div>
                    <span className="text-sm font-bold text-orange-800">
                      {result.after_discount_per_m2 ? `${formatPrice(Math.round(result.after_discount_per_m2))} €/m²` : "—"}
                    </span>
                  </div>
                )}

                {/* Step 4: Agent adjustments */}
                {result.total_adjustment_percent !== undefined && result.total_adjustment_percent !== 0 && (
                  <div className="flex items-center justify-between p-3 rounded-lg bg-purple-50 border border-purple-100">
                    <div className="flex items-center gap-2">
                      <span className="h-6 w-6 rounded-full bg-purple-600 text-white text-xs font-bold flex items-center justify-center">
                        {(() => {
                          let n = 2;
                          if (result.market_factors && result.market_factors.combined !== 1) n++;
                          if (result.realization_discount_percent && result.realization_discount_percent > 0) n++;
                          return n;
                        })()}
                      </span>
                      <span className="text-sm font-medium text-purple-900">
                        {result.total_adjustment_percent > 0 ? "+" : ""}{result.total_adjustment_percent.toFixed(1)}% korekcije
                      </span>
                    </div>
                    <span className="text-sm font-bold text-purple-800">
                      {result.calculated_price_per_m2 ? `${formatPrice(Math.round(result.calculated_price_per_m2))} €/m²` : "—"}
                    </span>
                  </div>
                )}

                {/* Final: Total — show effective €/m² so multiplication is visually correct */}
                {(() => {
                  const effectivePerM2 = result.calculated_price_per_m2 && result.calculated_price_per_m2 > 0
                    ? result.calculated_price_per_m2
                    : result.estimated_value_eur / Math.max(areaM2, 1);
                  return (
                    <div className="flex items-center justify-between p-3 rounded-lg bg-blue-100 border-2 border-blue-300 mt-1">
                      <div className="flex items-center gap-2">
                        <ArrowRight className="h-5 w-5 text-blue-700" />
                        <span className="text-sm font-bold text-blue-900">
                          {formatPrice(Math.round(effectivePerM2))} €/m² × {Math.max(areaM2, 1)} m²
                        </span>
                      </div>
                      <span className="text-lg font-extrabold text-blue-900">
                        {formatPrice(result.estimated_value_eur)} €
                      </span>
                    </div>
                  );
                })()}
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

          {/* Benchmark Summary */}
          {result.benchmark_summary && (
            <div className="rounded-xl bg-indigo-50 border border-indigo-100 p-4">
              <h3 className="text-xs font-semibold text-indigo-400 uppercase tracking-wider mb-2 flex items-center gap-2">
                <Target className="h-3.5 w-3.5" /> Benchmark regije / Elaborat
              </h3>
              <p className="text-sm text-indigo-900 whitespace-pre-line">{result.benchmark_summary}</p>
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

          {/* V48: Unified price structure — merges Adjustments + Value Drivers */}
          {(() => {
            // Build unified list of all price factors (EXCLUDING realization discount —
            // discount is already shown in the table footer as a separate step)
            const factors: { name: string; desc: string; pct: number }[] = [];

            // 1. Engine adjustments (garage, renovation, condition, etc.)
            if (result.adjustments?.length) {
              for (const a of result.adjustments) {
                factors.push({ name: a.factor, desc: a.description, pct: a.percent });
              }
            }

            // 2. Algorithmic value drivers (deduplicated — skip if already covered by adjustments)
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

            // V48e: Use backend's authoritative total — matches the table footer's "Σ Korekcija"
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
                  <span className="text-sm font-bold text-gray-700">Σ Ukupni utjecaj</span>
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
                <Home className="h-4 w-4" /> Sažetak nekretnine
              </h3>
              <p className="text-sm text-blue-900 leading-relaxed">{result.target_property_summary}</p>
            </div>
          )}

          {/* Reasoning */}
          {result.valuation_reasoning && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
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

          {/* Engine comparables */}
          {result.used_comparables && result.used_comparables.length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
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
                          {c.is_outlier && c.outlier_reason && (
                            <span className="text-[10px] text-red-500 italic">{c.outlier_reason}</span>
                          )}
                        </div>
                        <div className="flex items-center gap-4 mt-1.5">
                          <span className="text-sm font-bold text-blue-700">{formatPrice(Math.round(c.price_per_m2))} €/m²</span>
                          {c.total_price && <span className="text-xs text-gray-500">({formatPrice(Math.round(c.total_price))} €)</span>}
                          <SimilarityBadge score={c.similarity_score} />
                        </div>
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
                    <div className="mt-3 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                      <div className="h-full rounded-full transition-all duration-500" style={{
                        width: `${Math.max((c.similarity_score <= 1 ? c.similarity_score * 100 : c.similarity_score), 5)}%`,
                        backgroundColor: (c.similarity_score <= 1 ? c.similarity_score : c.similarity_score / 100) >= 0.7 ? "#22c55e" : (c.similarity_score <= 1 ? c.similarity_score : c.similarity_score / 100) >= 0.4 ? "#eab308" : "#ef4444"
                      }} />
                    </div>
                    {/* V48: Similarity reason — algorithmic explanation of why this comp is similar/different */}
                    {c.similarity_reason && (
                      <div className="mt-1.5 text-[11px] text-gray-500 italic">💡 {c.similarity_reason}</div>
                    )}
                    {/* Per-comp correction */}
                    {!c.is_outlier && c.correction_percent !== undefined && c.correction_percent !== 0 && (
                      <div className={`mt-2 text-xs px-3 py-1.5 rounded-md ${c.correction_percent > 0 ? "bg-green-50 text-green-700" : "bg-red-50 text-red-700"}`}>
                        <span className="font-semibold">Korekcija: {c.correction_percent > 0 ? "+" : ""}{c.correction_percent.toFixed(1)}%</span>
                        {c.correction_reason && <span className="ml-1 text-gray-500">— {c.correction_reason}</span>}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Algorithmically-selected comparables */}
          {result.selected_comparables?.length > 0 && (
            <div className="rounded-xl bg-white border border-gray-200 p-4 sm:p-6">
              <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4 flex items-center gap-2">
                <ListChecks className="h-4 w-4" /> Odabrane usporedbe ({result.selected_comparables.length})
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
        <button onClick={onReset} className="rounded-xl bg-gray-100 px-6 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-200 transition flex items-center gap-2">
          <RefreshCw className="h-4 w-4" /> Nova procjena
        </button>
        <button onClick={handlePdfExport} disabled={pdfLoading}
          className="rounded-xl bg-indigo-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition flex items-center gap-2">
          <FileDown className="h-4 w-4" /> {pdfLoading ? "Generiram PDF..." : pdfError ? "PDF greška!" : "Preuzmi PDF"}
        </button>
        <button onClick={() => copyToClipboard(
          `Procjena: ${formatPrice(result.estimated_value_eur)} €\n` +
          `Cijena/m²: ${result.calculated_price_per_m2 ? formatPrice(Math.round(result.calculated_price_per_m2)) + " €" : "N/A"}\n` +
          `Pouzdanost: ${result.math_confidence_score ?? "N/A"}/100\n\n` +
          (result.valuation_reasoning ?? "")
        )} className="rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-blue-700 transition flex items-center gap-2">
          <ClipboardCopy className="h-4 w-4" /> Kopiraj izvještaj
        </button>
        <MethodologyButton onClick={() => setShowMethodology(true)} />
      </div>

      {/* Methodology Modal */}
      {showMethodology && <MethodologyModal onClose={() => setShowMethodology(false)} />}

    </div>
  );
}
