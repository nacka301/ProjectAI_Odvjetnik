"use client";

import { useState } from "react";
import {
  X,
  Search,
  Calculator,
  BarChart3,
  Scale,
  FileText,
  Layers,
  Info,
  ChevronDown,
  ChevronUp,
  Shield,
} from "lucide-react";

// ═══════════════════════════════════════════════════════════
//  METHODOLOGY MODAL — "Kako radi procjena?"
//
//  Professional explanation of the valuation methodology
//  for attorneys. Zero "AI" terminology — uses standard
//  Croatian real estate appraisal vocabulary (poredbena
//  metoda, troškovna metoda, Fp/Ft korekcije, etc.)
// ═══════════════════════════════════════════════════════════

interface MethodologyStep {
  icon: React.ReactNode;
  title: string;
  subtitle: string;
  details: string[];
  legal?: string;
}

const STEPS: MethodologyStep[] = [
  {
    icon: <Search className="h-5 w-5" />,
    title: "1. Prikupljanje tržišnih podataka",
    subtitle: "Automatsko pretraživanje oglašenih nekretnina",
    details: [
      "Sustav pretražuje aktivne oglase na Njuškalo.hr i ostalim hrvatskim portalima za nekretnine.",
      "Filtrira oglase prema lokaciji, vrsti nekretnine, površini i drugim zadanim parametrima.",
      "Svaki oglas se analizira: izvlači se cijena, površina, stanje, katnost, orijentacija i ostale značajke.",
      "Rezultat: baza usporedivih nekretnina (komparabilnih transakcija) za vašu mikrolokaciju.",
    ],
    legal: "Sukladno čl. 22. Zakona o procjeni vrijednosti nekretnina (NN 78/15)",
  },
  {
    icon: <BarChart3 className="h-5 w-5" />,
    title: "2. Poredbena metoda (komparativna analiza)",
    subtitle: "Usporedba s realiziranim cijenama na tržištu",
    details: [
      "Odabiru se najsličnije nekretnine prema postavljenim kriterijima (lokacija, površina, stanje, katnost).",
      "Za svaku usporedivu nekretninu izračunava se koeficijent sličnosti (similarity score) na temelju odstupanja parametara.",
      "Primjenjuju se korekcijski koeficijenti: Kc (stanje), Kl (lokacija), Ks (površina), Ko (ostalo).",
      "Merodavna je cijena po m² korigirana za razlike između vaše i usporedive nekretnine.",
    ],
    legal: "Čl. 23.–25. ZoPVN — Poredbena metoda",
  },
  {
    icon: <Calculator className="h-5 w-5" />,
    title: "3. Korekcijski faktori (Fp i Ft)",
    subtitle: "Prilagodba za specifičnosti nekretnine i tržišta",
    details: [
      "Faktor položaja (Fp): Korigira vrijednost ovisno o mikrolokaciji — blizina centra, javni prijevoz, infrastruktura, buka, zelenilo.",
      "Faktor tržišta (Ft): Kompenzira razliku između oglašenih i realiziranih cijena, uzimajući u obzir pregovaračku maržu i tržišne trendove.",
      "Kombinirani korekcijski faktor: Fk = (1 + Fp) × (1 + Ft).",
      "Primjer: Fp = +5% (dobra lokacija), Ft = -20% (oglašene > realizirane) → Fk = 0.84.",
    ],
    legal: "Čl. 12. Pravilnika o metodama procjene vrijednosti nekretnina (NN 105/15)",
  },
  {
    icon: <Scale className="h-5 w-5" />,
    title: "4. Izračun tržišne vrijednosti",
    subtitle: "Statistički prosjek korigiranih cijena",
    details: [
      "Izračunava se medijan i srednja vrijednost cijena po m² iz korigiranih usporedivih nekretnina.",
      "Automatski se odbacuju ekstremne vrijednosti (outlieri) koje značajno odstupaju od medijana.",
      "Raspon procijenjene vrijednosti (min–max) reflektira interval pouzdanosti izračuna.",
      "Konačna procijenjena vrijednost = korigirana cijena/m² × ukupna površina.",
    ],
    legal: "Čl. 4. ZoPVN — Tržišna vrijednost",
  },
  {
    icon: <FileText className="h-5 w-5" />,
    title: "5. Stručno mišljenje i elaborat",
    subtitle: "Obrazloženje procjene u formi stručnog izvješća",
    details: [
      "Generira se tekstualno obrazloženje koje objašnjava logiku procjene, odabrane usporedive nekretnine i primijenjene korekcije.",
      "Elaborat sadrži: rekapitulaciju, tablicu usporedbi, korekcijske faktore i konačnu vrijednost.",
      "Neovisni revizijski sloj automatski provjerava procjenu prema tržišnim podacima (cross-validation).",
      "Rezultat se može preuzeti kao PDF dokument spreman za sudsku ili odvjetničku uporabu.",
    ],
    legal: "Čl. 26. ZoPVN — Sadržaj procjembenog elaborata",
  },
];

// ── Info button trigger ──

export function MethodologyButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className="inline-flex items-center gap-1.5 rounded-lg border border-blue-200 bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700 hover:bg-blue-100 hover:border-blue-300 transition-colors"
      title="Kako radi procjena?"
    >
      <Info className="h-3.5 w-3.5" />
      Kako radi procjena?
    </button>
  );
}

// ── Full-screen modal ──

export function MethodologyModal({ onClose }: { onClose: () => void }) {
  const [expandedStep, setExpandedStep] = useState<number | null>(0);

  const toggleStep = (idx: number) => {
    setExpandedStep(expandedStep === idx ? null : idx);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl border border-gray-200 animate-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between rounded-t-2xl border-b border-gray-100 bg-white/95 backdrop-blur-sm px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 shadow-lg shadow-blue-500/25">
              <Layers className="h-5 w-5 text-white" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-gray-900">Metodologija procjene</h2>
              <p className="text-xs text-gray-500">Sukladno Zakonu o procjeni vrijednosti nekretnina</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Intro */}
        <div className="px-6 py-4 border-b border-gray-100 bg-gradient-to-r from-blue-50 to-indigo-50">
          <p className="text-sm text-gray-700 leading-relaxed">
            Sustav koristi <strong>poredbenu (komparativnu) metodu</strong> procjene sukladno
            Zakonu o procjeni vrijednosti nekretnina (NN 78/15) i pripadajućem Pravilniku
            o metodama procjene (NN 105/15). Postupak se odvija u 5 koraka:
          </p>
        </div>

        {/* Steps accordion */}
        <div className="divide-y divide-gray-100">
          {STEPS.map((step, idx) => (
            <div key={idx} className="group">
              <button
                onClick={() => toggleStep(idx)}
                className="flex w-full items-center gap-4 px-6 py-4 text-left hover:bg-gray-50 transition-colors"
              >
                <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl transition-colors ${
                  expandedStep === idx
                    ? "bg-blue-600 text-white shadow-md shadow-blue-600/25"
                    : "bg-gray-100 text-gray-500 group-hover:bg-blue-50 group-hover:text-blue-600"
                }`}>
                  {step.icon}
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="text-sm font-semibold text-gray-900">{step.title}</h3>
                  <p className="text-xs text-gray-500 mt-0.5">{step.subtitle}</p>
                </div>
                {expandedStep === idx ? (
                  <ChevronUp className="h-4 w-4 text-gray-400 shrink-0" />
                ) : (
                  <ChevronDown className="h-4 w-4 text-gray-400 shrink-0" />
                )}
              </button>

              {expandedStep === idx && (
                <div className="px-6 pb-4 animate-in slide-in-from-top-2 duration-200">
                  <div className="ml-14 space-y-2">
                    {step.details.map((detail, i) => (
                      <div key={i} className="flex items-start gap-2">
                        <div className="mt-1.5 h-1.5 w-1.5 rounded-full bg-blue-400 shrink-0" />
                        <p className="text-sm text-gray-700 leading-relaxed">{detail}</p>
                      </div>
                    ))}
                    {step.legal && (
                      <div className="mt-3 flex items-start gap-2 rounded-lg bg-slate-50 border border-slate-200 px-3 py-2">
                        <Scale className="h-3.5 w-3.5 text-slate-500 mt-0.5 shrink-0" />
                        <p className="text-[11px] text-slate-500 italic">{step.legal}</p>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Footer disclaimer */}
        <div className="rounded-b-2xl border-t border-gray-100 bg-gray-50 px-6 py-4">
          <div className="flex items-start gap-3">
            <Shield className="h-4 w-4 text-gray-400 mt-0.5 shrink-0" />
            <div className="text-[11px] text-gray-500 leading-relaxed space-y-1">
              <p>
                Procjena ne predstavlja pravni savjet niti ovlašteni procjembeni elaborat.
                Služi kao <strong>informativni alat</strong> za odvjetnike, vještake i stručne suradnike
                pri orijentacijskoj procjeni tržišne vrijednosti.
              </p>
              <p>
                Svi podaci se kriptiraju tijekom prijenosa i ne pohranjuju se trajno
                na vanjskim servisima. Obrada je sukladna GDPR-u.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
