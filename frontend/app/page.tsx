"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import {
  Search as SearchIcon,
  Clock,
  Building2,
  ChevronRight,
  Loader2,
  ShieldAlert,
  ArrowRight,
  X,
  Info,
  Upload,
  FileText,
  Sparkles,
  CircleHelp,
  Eye,
  Send,
  Landmark,
  Construction,
  Calculator,
  Users,
  Languages,
} from "lucide-react";
import { getRecentActivity, type ActivityItem, type ActivityType } from "@/lib/supabase";
import AppHeader from "@/components/AppHeader";

// ── Helpers ──

function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "upravo";
  if (mins < 60) return `prije ${mins} min`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `prije ${hrs}h`;
  const days = Math.floor(hrs / 24);
  if (days < 7) return `prije ${days}d`;
  return new Date(iso).toLocaleDateString("hr-HR");
}

const ACTIVITY_ICONS: Record<ActivityType, React.ReactNode> = {
  valuation: <Building2 className="h-4 w-4 text-blue-500" />,
  contract: <ShieldAlert className="h-4 w-4 text-rose-500" />,
  chronology: <Building2 className="h-4 w-4 text-violet-500" />,
  legal: <Building2 className="h-4 w-4 text-amber-500" />,
  invoice: <Building2 className="h-4 w-4 text-teal-500" />,
};

const ACTIVITY_LABELS: Record<ActivityType, string> = {
  valuation: "Procjena",
  contract: "Ugovor",
  chronology: "Kronologija",
  legal: "Pravna analiza",
  invoice: "e-Račun",
};

const BADGE_COLORS: Record<string, string> = {
  green: "text-green-700 bg-green-50",
  amber: "text-amber-700 bg-amber-50",
  red: "text-red-700 bg-red-50",
};

// ── Tour Guide Steps ──

const TOUR_STEPS = [
  {
    target: "card-procjena",
    title: "1/6 · Tržišna Procjena",
    text: "Ovdje izračunavate realnu vrijednost nekretnine. Unesite podatke ili zalijepite link oglasa.",
  },
  {
    target: "card-ugovor",
    title: "2/6 · Analiza Ugovora",
    text: "Učitajte nacrt ugovora — sustav pronalazi pravne zamke i predlaže sigurnije klauzule.",
  },
  {
    target: "card-zk",
    title: "3/6 · ZK Izvadak",
    text: "Učitajte ZK izvadak — sustav identificira terete, hipoteke, plombe i pravne rizike.",
  },
  {
    target: "card-troskovnik",
    title: "4/6 · Odvjetnički Troškovnik",
    text: "Izračunajte nagradu i troškove prema službenoj tarifi — odaberite radnje, a bodovi se računaju automatski.",
  },
  {
    target: "card-stranke",
    title: "5/6 · Stranke",
    text: "Upravljajte strankama, predmetima i troškovnicima — pregledajte kompletan dosje na jednom mjestu.",
  },
  {
    target: "card-prijevod",
    title: "6/6 · Sudski Tumač",
    text: "Zalijepite pravni tekst na ćirilici ili stranom jeziku — modul za pravnu lingvistiku prevodi uz strogu terminologiju.",
  },
];

// ── HoverCard Component ──

function HoverInfoCard({ text }: { text: string }) {
  const [open, setOpen] = useState(false);
  const timeout = useRef<ReturnType<typeof setTimeout>>(undefined);

  const show = () => {
    clearTimeout(timeout.current);
    setOpen(true);
  };
  const hide = () => {
    timeout.current = setTimeout(() => setOpen(false), 150);
  };

  return (
    <div className="relative inline-block" onMouseEnter={show} onMouseLeave={hide}>
      <button
        type="button"
        className="w-5 h-5 rounded-full bg-slate-100 hover:bg-blue-100 flex items-center justify-center transition"
        aria-label="Više informacija"
        onClick={(e) => { e.preventDefault(); e.stopPropagation(); setOpen(!open); }}
      >
        <Info className="h-3 w-3 text-slate-400 hover:text-blue-600" />
      </button>
      {open && (
        <div
          className="absolute z-50 bottom-full mb-2 left-1/2 -translate-x-1/2 w-72 bg-slate-900 text-white text-xs leading-relaxed rounded-lg shadow-xl p-3.5 pointer-events-auto animate-in"
          onMouseEnter={show}
          onMouseLeave={hide}
        >
          <div className="flex items-start gap-2">
            <CircleHelp className="h-3.5 w-3.5 text-blue-400 mt-0.5 shrink-0" />
            <div>
              <span className="text-[10px] text-blue-400 uppercase tracking-wider font-semibold block mb-1">Kako funkcionira</span>
              {text}
            </div>
          </div>
          <div className="absolute top-full left-1/2 -translate-x-1/2 -mt-px">
            <div className="w-2.5 h-2.5 bg-slate-900 rotate-45 -mt-1.5" />
          </div>
        </div>
      )}
    </div>
  );
}

// ── Tour Overlay ──

function TourOverlay({
  step,
  total,
  title,
  text,
  onNext,
  onSkip,
}: {
  step: number;
  total: number;
  title: string;
  text: string;
  onNext: () => void;
  onSkip: () => void;
}) {
  return (
    <div className="fixed inset-0 z-[100] pointer-events-none">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-slate-900/60 pointer-events-auto" onClick={onSkip} />
      {/* Tooltip (centered at bottom) */}
      <div className="absolute bottom-8 left-1/2 -translate-x-1/2 pointer-events-auto w-[calc(100%-2rem)] max-w-[380px]">
        <div className="bg-white rounded-xl shadow-2xl border border-slate-200 p-4 sm:p-5">
          <div className="flex items-center gap-2 mb-2">
            <Eye className="h-4 w-4 text-blue-600" />
            <span className="text-sm font-bold text-slate-900">{title}</span>
          </div>
          <p className="text-sm text-slate-600 leading-relaxed mb-4">{text}</p>
          <div className="flex items-center justify-between">
            <button
              onClick={onSkip}
              className="text-xs text-slate-400 hover:text-slate-600 transition"
            >
              Preskoči vodič
            </button>
            <div className="flex items-center gap-3">
              {/* Step dots */}
              <div className="flex gap-1">
                {Array.from({ length: total }).map((_, i) => (
                  <div
                    key={i}
                    className={`w-1.5 h-1.5 rounded-full transition ${
                      i === step ? "bg-blue-600" : "bg-slate-200"
                    }`}
                  />
                ))}
              </div>
              <button
                onClick={onNext}
                className="inline-flex items-center gap-1 bg-blue-600 text-white text-xs font-medium px-4 py-2 rounded-lg hover:bg-blue-700 transition"
              >
                {step < total - 1 ? "Dalje" : "Završi"}
                <ArrowRight className="h-3 w-3" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
//  FEATURE CARD DATA
// ═══════════════════════════════════════════════════════════

interface FeatureCard {
  id: string;
  href: string;
  icon: React.ReactNode;
  title: string;
  description: string;
  howItWorks: string;
  disabled?: boolean;
  disabledLabel?: string;
}

const FEATURE_CARDS: FeatureCard[] = [
  {
    id: "card-procjena",
    href: "/wizard",
    icon: <SearchIcon className="h-5 w-5" />,
    title: "Tržišna Procjena",
    description: "Izračun realne tržišne vrijednosti nekretnine temeljen na usporedivim transakcijama, mikrolokaciji i faktorima rizika.",
    howItWorks:
      "Unesite podatke ili zalijepite link oglasa. Sustav će analizirati tekst (npr. 'kaskadni teren') i primijeniti korektivne faktore na cijenu.",
  },
  {
    id: "card-ugovor",
    href: "/ugovor",
    icon: <ShieldAlert className="h-5 w-5" />,
    title: "Analiza Ugovora",
    description: "Automatska detekcija pravnih zamki, nedostajućih klauzula i rizičnih odredbi u kupoprodajnim i zakupnim ugovorima.",
    howItWorks:
      "Učitajte nacrt ugovora. Sustav traži 'crvene zastavice' (npr. nedostatak tabularne izjave) i predlaže sigurnije klauzule prema ZOO-u.",
  },
  {
    id: "card-zk",
    href: "/zk-procjena",
    icon: <Landmark className="h-5 w-5" />,
    title: "ZK Izvadak",
    description: "Dubinska analiza zemljišnoknjižnog stanja — identificira vlasnike, terete, hipoteke, plombe i pravne rizike za transakciju.",
    howItWorks:
      "Učitajte PDF ZK izvatka. Sustav identificira vlasnike, terete (hipoteke, služnosti, fiducije), plombe i ocjenjuje rizik za kupnju ili gradnju.",
  },
  {
    id: "card-troskovnik",
    href: "/troskovnik",
    icon: <Calculator className="h-5 w-5" />,
    title: "Odvjetnički Troškovnik",
    description: "Izračun nagrade i troškova prema službenoj Tarifi o nagradama za rad odvjetnika s automatskim bodovanjem radnji.",
    howItWorks:
      "Unesite vrijednost predmeta spora, odaberite obavljene radnje — sustav automatski računa bodove i iznos u EUR prema službenoj tarifi (1 bod = 2,00 €).",
  },
  {
    id: "card-stranke",
    href: "/klijenti",
    icon: <Users className="h-5 w-5" />,
    title: "Stranke",
    description: "Centralizirano upravljanje strankama, predmetima i troškovnicima — kompletni dosje svake stranke na jednom mjestu.",
    howItWorks:
      "Kreirajte stranke, povežite predmete i troškovnike — pregledajte kompletni dosje svake stranke.",
  },
  {
    id: "card-prijevod",
    href: "/prijevod",
    icon: <Languages className="h-5 w-5" />,
    title: "Sudski Tumač",
    description: "Pravni prijevod i transliteracija uz strogu terminologiju — ćirilica↔latinica, engleski i njemački↔hrvatski.",
    howItWorks:
      "Zalijepite tekst presude, ugovora ili rješenja. Modul za pravnu lingvistiku prevodi uz strogu pravnu terminologiju ili preslovi pismo.",
  },
];

// ═══════════════════════════════════════════════════════════
//  Dashboard Component — "PravoTek — Pravna Inteligencija"
// ═══════════════════════════════════════════════════════════

export default function Dashboard() {
  const router = useRouter();
  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [bannerDismissed, setBannerDismissed] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [showTour, setShowTour] = useState(false);
  const [tourStep, setTourStep] = useState(0);

  // Check if first visit (tour hasn't been seen)
  useEffect(() => {
    try {
      const seen = localStorage.getItem("pravotek_tour_seen");
      if (!seen) setShowTour(true);
    } catch { /* localStorage unavailable */ }
  }, []);

  // Check if banner was dismissed
  useEffect(() => {
    try {
      const dismissed = localStorage.getItem("pravotek_banner_dismissed");
      if (dismissed) setBannerDismissed(true);
    } catch { /* localStorage unavailable */ }
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const data = await getRecentActivity(10);
        setActivity(data);
      } catch {
        // Silently handle — dashboard still works without history
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const dismissBanner = () => {
    setBannerDismissed(true);
    try { localStorage.setItem("pravotek_banner_dismissed", "1"); } catch { /* storage unavailable */ }
  };

  const endTour = useCallback(() => {
    setShowTour(false);
    setTourStep(0);
    try { localStorage.setItem("pravotek_tour_seen", "1"); } catch { /* storage unavailable */ }
  }, []);

  const nextTourStep = useCallback(() => {
    if (tourStep < TOUR_STEPS.length - 1) {
      setTourStep((s) => s + 1);
    } else {
      endTour();
    }
  }, [tourStep, endTour]);

  // Highlight active tour card
  const tourTargetId = showTour ? TOUR_STEPS[tourStep]?.target : null;

  // Auto-scroll to the active tour target element
  useEffect(() => {
    if (!tourTargetId) return;
    // Small delay so the DOM has painted the highlight ring before we measure
    const timer = setTimeout(() => {
      const el = document.getElementById(tourTargetId);
      if (el) {
        el.scrollIntoView({ behavior: "smooth", block: "center" });
      }
    }, 80);
    return () => clearTimeout(timer);
  }, [tourTargetId]);

  // Drag & drop handlers (visual only — routing to the right module)
  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(true);
  }, []);
  const handleDragLeave = useCallback(() => setDragOver(false), []);
  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    // If files are dropped, route to ZK analysis (the doc analysis tool)
    if (e.dataTransfer.files.length > 0) {
      router.push("/zk-procjena");
    }
  }, [router]);

  return (
    <div className="min-h-screen bg-slate-50 font-sans">
      {/* ═══════════ Tour Overlay ═══════════ */}
      {showTour && (
        <TourOverlay
          step={tourStep}
          total={TOUR_STEPS.length}
          title={TOUR_STEPS[tourStep].title}
          text={TOUR_STEPS[tourStep].text}
          onNext={nextTourStep}
          onSkip={endTour}
        />
      )}

      {/* ═══════════ Top Navigation ═══════════ */}
      <AppHeader
        extraStart={
          <button
            onClick={() => { setShowTour(true); setTourStep(0); }}
            className="text-xs text-slate-500 hover:text-white transition flex items-center gap-1 p-1.5 sm:p-0"
            title="Pokreni vodič"
          >
            <CircleHelp className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
            <span className="hidden sm:inline">Vodič</span>
          </button>
        }
      />

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-6 space-y-6">

        {/* ═══════════ 1. ONBOARDING WELCOME BANNER ═══════════ */}
        {!bannerDismissed && (
          <section className="relative overflow-hidden rounded-xl bg-gradient-to-br from-slate-900 via-slate-800 to-blue-900 text-white p-6 sm:p-8 shadow-lg">
            {/* Decorative elements */}
            <div className="absolute top-0 right-0 w-64 h-64 bg-blue-600/10 rounded-full -mr-20 -mt-20 blur-3xl" />
            <div className="absolute bottom-0 left-0 w-48 h-48 bg-indigo-500/10 rounded-full -ml-16 -mb-16 blur-2xl" />

            <button
              onClick={dismissBanner}
              className="absolute top-4 right-4 w-7 h-7 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center transition"
              aria-label="Zatvori"
            >
              <X className="h-4 w-4 text-white/70" />
            </button>

            <div className="relative flex items-start gap-5">
              <div className="hidden sm:flex w-14 h-14 rounded-2xl bg-blue-600/20 border border-blue-500/30 items-center justify-center shrink-0">
                <Sparkles className="h-7 w-7 text-blue-400" />
              </div>
              <div>
                <h2 className="text-xl sm:text-2xl font-bold tracking-tight mb-2">
                  Dobrodošli u Vaš Pravni Ured
                </h2>
                <p className="text-sm sm:text-base text-slate-300 leading-relaxed max-w-2xl">
                  Ovo je Vaš centar za analizu spisa. Odaberite modul u nastavku ili jednostavno{" "}
                  <span className="text-blue-400 font-medium">povucite (Drag &amp; Drop)</span>{" "}
                  bilo koji dokument da sustav sam prepozna što treba učiniti.
                </p>
                <div className="flex items-center gap-3 mt-4">
                  <Link
                    href="/wizard"
                    className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium px-5 py-2.5 rounded-lg transition shadow-sm"
                  >
                    <SearchIcon className="h-4 w-4" />
                    Započni prvu analizu
                  </Link>
                  <button
                    onClick={dismissBanner}
                    className="text-xs text-slate-400 hover:text-white transition px-3 py-2"
                  >
                    Ne prikazuj opet
                  </button>
                </div>
              </div>
            </div>
          </section>
        )}

        {/* ═══════════ 2. FEATURE GRID — Enterprise Module Cards ═══════════ */}
        <section>
          <div className="flex items-center justify-between mb-5">
            <div>
              <h2 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-slate-900 tracking-tight">
                Moduli
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">Odaberite modul za početak rada</p>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {FEATURE_CARDS.map((card) => {
              const inner = (
                <div
                  id={card.id}
                  className={`group relative bg-white rounded-lg border border-slate-200 shadow-sm p-6 transition-all duration-200 ${
                    card.disabled
                      ? "opacity-60 cursor-not-allowed"
                      : "hover:border-[#1B2A4A]/40 hover:shadow-md hover:-translate-y-0.5"
                  } ${
                    tourTargetId === card.id
                      ? "z-[110] ring-2 ring-[#1B2A4A] ring-offset-2 shadow-lg"
                      : ""
                  }`}
                >
                  {/* Disabled badge */}
                  {card.disabled && card.disabledLabel && (
                    <div className="absolute top-3 right-3 z-10 flex items-center gap-1 bg-slate-100 text-slate-500 text-[10px] font-semibold uppercase tracking-wider px-2.5 py-1 rounded-md">
                      <Construction className="h-3 w-3" />
                      {card.disabledLabel}
                    </div>
                  )}

                  {/* Top row: icon + info button */}
                  <div className="flex items-start justify-between mb-4">
                    <div className="w-10 h-10 rounded-lg bg-slate-100 group-hover:bg-slate-200/80 flex items-center justify-center transition-colors duration-200">
                      <span className="text-[#1B2A4A]">{card.icon}</span>
                    </div>
                    {!card.disabled && <HoverInfoCard text={card.howItWorks} />}
                  </div>

                  {/* Title — Playfair serif */}
                  <h3 className="font-[family-name:var(--font-playfair)] font-semibold text-[#1B2A4A] text-base mb-2 tracking-tight">
                    {card.title}
                  </h3>

                  {/* Description */}
                  <p className="text-sm text-slate-500 leading-relaxed mb-4">
                    {card.description}
                  </p>

                  {/* CTA hint */}
                  {card.disabled ? (
                    <div className="flex items-center gap-1.5 text-xs font-medium text-slate-400">
                      <Construction className="h-3.5 w-3.5" />
                      Uskoro dostupno
                    </div>
                  ) : (
                    <div className="flex items-center gap-1.5 text-xs font-medium text-[#1B2A4A] opacity-0 group-hover:opacity-100 transition-opacity duration-200">
                      Otvori modul
                      <ArrowRight className="h-3.5 w-3.5" />
                    </div>
                  )}
                </div>
              );

              if (card.disabled) {
                return <div key={card.id}>{inner}</div>;
              }

              return (
                <Link key={card.id} href={card.href}>
                  {inner}
                </Link>
              );
            })}
          </div>
        </section>

        {/* ═══════════ 3. QUICK DROP ZONE ═══════════ */}
        <section>
          <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={`relative rounded-xl border-2 border-dashed transition-all duration-200 px-6 py-10 text-center ${
              dragOver
                ? "border-blue-400 bg-blue-50/50 shadow-inner"
                : "border-slate-300 bg-white hover:border-slate-400 hover:bg-slate-50/50"
            }`}
          >
            <div className="flex flex-col items-center gap-3">
              <div className={`w-14 h-14 rounded-2xl flex items-center justify-center transition ${
                dragOver ? "bg-blue-100" : "bg-slate-100"
              }`}>
                <Upload className={`h-6 w-6 transition ${dragOver ? "text-blue-600" : "text-slate-400"}`} />
              </div>
              <div>
                <p className={`text-sm font-medium transition ${dragOver ? "text-blue-700" : "text-slate-600"}`}>
                  Povucite bilo koji dokument ovdje za brzu analizu...
                </p>
                <p className="text-xs text-slate-400 mt-1 flex items-center justify-center gap-1.5">
                  <FileText className="h-3 w-3" />
                  Podržano: PDF, Word, JPG (OCR uključen)
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* ═══════════ 4. MAIN CONTENT: Case Feed + Legal Feed ═══════════ */}
        <section className="grid grid-cols-1 lg:grid-cols-[1fr_340px] gap-6">
          {/* ── LEFT: Smart Case Feed ── */}
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-700 uppercase tracking-wide">
                Nedavni predmeti
              </h2>
              <Link
                href="/history"
                className="text-xs text-blue-600 hover:underline flex items-center gap-1"
              >
                Vidi sve <ArrowRight className="h-3 w-3" />
              </Link>
            </div>

            {/* Loading state */}
            {loading && (
              <div className="bg-white rounded-xl border border-slate-200 p-12 text-center">
                <Loader2 className="h-6 w-6 animate-spin text-slate-400 mx-auto" />
                <p className="text-xs text-slate-400 mt-2">
                  Učitavam povijest...
                </p>
              </div>
            )}

            {/* Empty state */}
            {!loading && activity.length === 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-12 text-center">
                <div className="w-12 h-12 bg-slate-100 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <Clock className="h-6 w-6 text-slate-300" />
                </div>
                <p className="text-sm text-slate-500 font-medium">
                  Još nema predmeta
                </p>
                <p className="text-xs text-slate-400 mt-1">
                  Pokrenite prvu procjenu ili analizu da započnete
                </p>
                <Link
                  href="/wizard"
                  className="inline-flex items-center gap-1 mt-4 text-xs font-medium text-blue-600 hover:underline"
                >
                  Nova procjena <ArrowRight className="h-3 w-3" />
                </Link>
              </div>
            )}

            {/* Case table */}
            {!loading && activity.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
                {/* Table header */}
                <div className="grid grid-cols-[1fr_44px] sm:grid-cols-[1fr_auto_auto_44px] gap-2 px-4 py-2.5 bg-slate-50 border-b border-slate-200 text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  <span>Predmet</span>
                  <span className="w-28 text-center hidden sm:block">Status</span>
                  <span className="w-24 text-center hidden sm:block">e-Sud</span>
                  <span />
                </div>

                {/* Rows */}
                <div className="divide-y divide-slate-100">
                  {activity.slice(0, 12).map((item) => (
                    <div
                      key={item.id}
                      className="grid grid-cols-[1fr_44px] sm:grid-cols-[1fr_auto_auto_44px] gap-2 items-center px-3 sm:px-4 py-3 hover:bg-slate-50 transition group"
                    >
                      {/* Left: type icon + title + subtitle */}
                      <Link href="/history" className="flex items-center gap-3 min-w-0">
                        <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center shrink-0">
                          {ACTIVITY_ICONS[item.type]}
                        </div>
                        <div className="min-w-0">
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                              {ACTIVITY_LABELS[item.type]}
                            </span>
                            <span className="text-[10px] text-slate-300">•</span>
                            <span className="text-sm font-medium text-slate-900 truncate">
                              {item.title}
                            </span>
                          </div>
                          <span className="text-[11px] text-slate-400">
                            {timeAgo(item.created_at)}
                            {item.subtitle ? ` · ${item.subtitle}` : ""}
                          </span>
                        </div>
                      </Link>

                      {/* Badge */}
                      <div className="w-28 justify-center hidden sm:flex">
                        {item.badge && (
                          <span className={`inline-flex items-center text-xs font-medium px-2.5 py-1 rounded-full ${
                            item.badgeColor ? BADGE_COLORS[item.badgeColor] || "text-slate-500 bg-slate-100" : "text-slate-500 bg-slate-100"
                          }`}>
                            {item.badge}
                          </span>
                        )}
                      </div>

                      {/* e-Komunikacija button */}
                      <div className="w-24 justify-center hidden sm:flex">
                        <button
                          onClick={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            alert(
                              `📤 e-Komunikacija\n\nPredmet "${item.title}" pripremljen za slanje na sud putem sustava e-Komunikacija.\n\nOva funkcionalnost zahtijeva integraciju s FINA e-Komunikacija sustavom i aktivnu odvjetničku pametnu karticu.`
                            );
                          }}
                          className="inline-flex items-center gap-1 text-[10px] font-medium text-blue-600 hover:text-blue-800 bg-blue-50 hover:bg-blue-100 px-2 py-1.5 rounded-lg transition whitespace-nowrap"
                          title="Pošalji u e-Komunikaciju (e-Sud)"
                        >
                          <Send className="h-3 w-3" />
                          e-Sud
                        </button>
                      </div>

                      {/* Arrow */}
                      <Link href="/history" className="flex justify-center">
                        <ChevronRight className="h-4 w-4 text-slate-300 group-hover:text-slate-500 transition" />
                      </Link>
                    </div>
                  ))}
                </div>

                {/* Footer */}
                {activity.length > 12 && (
                  <div className="px-4 py-2.5 bg-slate-50 border-t border-slate-200 text-center">
                    <Link
                      href="/history"
                      className="text-xs text-blue-600 hover:underline"
                    >
                      Prikaži svih {activity.length} predmeta →
                    </Link>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* ── RIGHT: Stats ── */}
          <div className="space-y-4">
            {/* Quick Stats */}
            {!loading && activity.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
                <h3 className="text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  Brzi pregled
                </h3>
                <div className="grid grid-cols-2 gap-3">
                  {(["valuation", "contract"] as ActivityType[]).map((t) => {
                    const count = activity.filter((a) => a.type === t).length;
                    if (count === 0) return null;
                    return (
                      <div key={t} className="bg-slate-50 rounded-lg p-3 flex items-center gap-2">
                        {ACTIVITY_ICONS[t]}
                        <div>
                          <div className="text-lg font-bold text-slate-900">{count}</div>
                          <div className="text-[10px] text-slate-400">{ACTIVITY_LABELS[t]}</div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </section>
      </main>
    </div>
  );
}
