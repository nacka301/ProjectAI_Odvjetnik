"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";

import {
  Search as SearchIcon,
  Scale,
  BookOpen,
  ScrollText,
  Clock,
  Building2,
  ChevronRight,
  Loader2,
  ShieldAlert,
  ArrowRight,
  Gavel,
  X,
  Info,
  Upload,
  FileText,
  Sparkles,
  CircleHelp,
  Eye,
  Send,
  Receipt,
} from "lucide-react";
import LegalFeed from "@/components/LegalFeed";
import { getRecentActivity, type ActivityItem, type ActivityType } from "@/lib/supabase";

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
  chronology: <ScrollText className="h-4 w-4 text-violet-500" />,
  legal: <Gavel className="h-4 w-4 text-amber-500" />,
  invoice: <Receipt className="h-4 w-4 text-teal-500" />,
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
    title: "1/5 · Tržišna Procjena",
    text: "Ovdje izračunavate realnu vrijednost nekretnine. Unesite podatke ili zalijepite link oglasa.",
  },
  {
    target: "card-ugovor",
    title: "2/5 · Analiza Ugovora",
    text: "Učitajte nacrt ugovora — AI pronalazi pravne zamke i predlaže sigurnije klauzule.",
  },
  {
    target: "card-kronologija",
    title: "3/5 · Kronologija Spisa",
    text: "Ubacite sve dokumente odjednom — AI slaže interaktivnu kronologiju za pripremu tužbe.",
  },
  {
    target: "card-zk",
    title: "4/5 · ZK & Sudska Praksa",
    text: "AI tumači ZK izvadak i traži relevantnu sudsku praksu za Vaš slučaj.",
  },
  {
    target: "card-fiskalizacija",
    title: "5/5 · Fiskalizacija 2.0",
    text: "Generirajte e-račun (UBL 2.1) izravno povezan s predmetom — spremno za e-Račun sustav.",
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
      <div className="absolute bottom-8 left-1/2 -translate-x-1/2 pointer-events-auto">
        <div className="bg-white rounded-xl shadow-2xl border border-slate-200 p-5 w-[380px]">
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
  iconBg: string;
  iconColor: string;
  accentBorder: string;
  accentBg: string;
  title: string;
  description: string;
  howItWorks: string;
}

const FEATURE_CARDS: FeatureCard[] = [
  {
    id: "card-procjena",
    href: "/wizard",
    icon: <SearchIcon className="h-6 w-6" />,
    iconBg: "bg-blue-100 group-hover:bg-blue-200",
    iconColor: "text-blue-600",
    accentBorder: "hover:border-blue-400",
    accentBg: "bg-blue-50 group-hover:bg-blue-100",
    title: "Tržišna Procjena",
    description: "Izračun realne vrijednosti nekretnine uz analizu mikrolokacije i rizika.",
    howItWorks:
      "Unesite podatke ili zalijepite link oglasa. AI će analizirati tekst (npr. 'kaskadni teren') i primijeniti korektivne faktore na cijenu.",
  },
  {
    id: "card-ugovor",
    href: "/ugovor",
    icon: <ShieldAlert className="h-6 w-6" />,
    iconBg: "bg-emerald-100 group-hover:bg-emerald-200",
    iconColor: "text-emerald-600",
    accentBorder: "hover:border-emerald-400",
    accentBg: "bg-emerald-50 group-hover:bg-emerald-100",
    title: "Analiza Ugovora",
    description: "Detekcija pravnih zamki i nedostataka u kupoprodajnim ugovorima.",
    howItWorks:
      "Učitajte nacrt ugovora. AI traži 'crvene zastavice' (npr. nedostatak tabularne izjave) i predlaže sigurnije klauzule prema ZOO-u.",
  },
  {
    id: "card-kronologija",
    href: "/kronologija",
    icon: <ScrollText className="h-6 w-6" />,
    iconBg: "bg-violet-100 group-hover:bg-violet-200",
    iconColor: "text-violet-600",
    accentBorder: "hover:border-violet-400",
    accentBg: "bg-violet-50 group-hover:bg-violet-100",
    title: "Kronologija Spisa",
    description: "Automatsko slaganje događaja iz gomile dokumenata u vremensku crtu.",
    howItWorks:
      "Ubacite sve mailove, račune i dopise odjednom. AI će izvući datume i složiti interaktivnu kronologiju za pripremu tužbe.",
  },
  {
    id: "card-zk",
    href: "/sudska-praksa",
    icon: <Gavel className="h-6 w-6" />,
    iconBg: "bg-amber-100 group-hover:bg-amber-200",
    iconColor: "text-amber-600",
    accentBorder: "hover:border-amber-400",
    accentBg: "bg-amber-50 group-hover:bg-amber-100",
    title: "ZK & Sudska Praksa",
    description: "Analiza tereta i povezivanje činjenica s presudama Vrhovnog suda.",
    howItWorks:
      "AI tumači ZK izvadak (terete, plombe) i traži relevantnu sudsku praksu za Vaš slučaj.",
  },
  {
    id: "card-fiskalizacija",
    href: "/fiskalizacija",
    icon: <Receipt className="h-6 w-6" />,
    iconBg: "bg-teal-100 group-hover:bg-teal-200",
    iconColor: "text-teal-600",
    accentBorder: "hover:border-teal-400",
    accentBg: "bg-teal-50 group-hover:bg-teal-100",
    title: "Fiskalizacija 2.0",
    description: "Automatska izrada e-računa (UBL 2.1) iz analiziranih predmeta.",
    howItWorks:
      "Unesite podatke stranaka i iznos — PravoTek generira UBL 2.1 XML e-račun povezan s predmetom, spreman za slanje putem e-Račun sustava.",
  },
];

// ═══════════════════════════════════════════════════════════
//  Dashboard Component — "PravoTek — Pravna Inteligencija"
// ═══════════════════════════════════════════════════════════

export default function Dashboard() {
  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [bannerDismissed, setBannerDismissed] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [showTour, setShowTour] = useState(false);
  const [tourStep, setTourStep] = useState(0);

  // Check if first visit (tour hasn't been seen)
  useEffect(() => {
    const seen = localStorage.getItem("pravotek_tour_seen");
    if (!seen) setShowTour(true);
  }, []);

  // Check if banner was dismissed
  useEffect(() => {
    const dismissed = localStorage.getItem("pravotek_banner_dismissed");
    if (dismissed) setBannerDismissed(true);
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
    localStorage.setItem("pravotek_banner_dismissed", "1");
  };

  const endTour = useCallback(() => {
    setShowTour(false);
    setTourStep(0);
    localStorage.setItem("pravotek_tour_seen", "1");
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

  // Drag & drop handlers (visual only — routing to the right module)
  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(true);
  }, []);
  const handleDragLeave = useCallback(() => setDragOver(false), []);
  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    // If files are dropped, route to chronology (the multi-doc tool)
    if (e.dataTransfer.files.length > 0) {
      window.location.href = "/kronologija";
    }
  }, []);

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
      <header className="bg-slate-900 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
              <Scale className="h-5 w-5 text-white" />
            </div>
            <div>
              <h1 className="text-lg font-bold tracking-tight">PravoTek</h1>
              <p className="text-[10px] text-slate-400 uppercase tracking-widest">
                Pravna Inteligencija
              </p>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <button
              onClick={() => { setShowTour(true); setTourStep(0); }}
              className="text-xs text-slate-500 hover:text-white transition flex items-center gap-1"
              title="Pokreni vodič"
            >
              <CircleHelp className="h-3.5 w-3.5" />
              Vodič
            </button>
            <Link
              href="/history"
              className="text-xs text-slate-400 hover:text-white transition flex items-center gap-1"
            >
              <Clock className="h-3.5 w-3.5" />
              Puna povijest
            </Link>
          </div>
        </div>
      </header>

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
                  Dobrodošli u Vaš AI Pravni Ured
                </h2>
                <p className="text-sm sm:text-base text-slate-300 leading-relaxed max-w-2xl">
                  Ovo je Vaš centar za analizu spisa. Odaberite modul u nastavku ili jednostavno{" "}
                  <span className="text-blue-400 font-medium">povucite (Drag &amp; Drop)</span>{" "}
                  bilo koji dokument da AI sam prepozna što treba učiniti.
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

        {/* ═══════════ 2. FEATURE GRID — 2×2 Educational Cards ═══════════ */}
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-slate-700 uppercase tracking-wide">
              Moduli
            </h2>
            <span className="text-[11px] text-slate-400">Odaberite modul za početak rada</span>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {FEATURE_CARDS.map((card) => (
              <Link
                key={card.id}
                href={card.href}
                id={card.id}
                className={`group relative bg-white rounded-xl border border-slate-200 p-6 transition overflow-hidden ${card.accentBorder} hover:shadow-lg ${
                  tourTargetId === card.id
                    ? "relative z-[110] ring-2 ring-blue-500 ring-offset-2 shadow-xl"
                    : ""
                }`}
              >
                {/* Accent corner blob */}
                <div className={`absolute top-0 right-0 w-24 h-24 ${card.accentBg} rounded-bl-[48px] -mr-3 -mt-3 transition`} />

                <div className="relative">
                  {/* Top row: icon + info button */}
                  <div className="flex items-start justify-between mb-4">
                    <div className={`w-12 h-12 rounded-xl ${card.iconBg} flex items-center justify-center transition`}>
                      <span className={card.iconColor}>{card.icon}</span>
                    </div>
                    {/* How it works hover card */}
                    <HoverInfoCard text={card.howItWorks} />
                  </div>

                  {/* Title */}
                  <h3 className="font-bold text-slate-900 text-base mb-1.5">
                    {card.title}
                  </h3>

                  {/* Description */}
                  <p className="text-sm text-slate-500 leading-relaxed mb-4">
                    {card.description}
                  </p>

                  {/* CTA hint */}
                  <div className="flex items-center gap-1.5 text-xs font-medium text-blue-600 opacity-0 group-hover:opacity-100 transition-opacity">
                    Otvori modul
                    <ArrowRight className="h-3.5 w-3.5" />
                  </div>
                </div>
              </Link>
            ))}
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
                <div className="grid grid-cols-[1fr_auto_auto_44px] gap-2 px-4 py-2.5 bg-slate-50 border-b border-slate-200 text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  <span>Predmet</span>
                  <span className="w-28 text-center">Status</span>
                  <span className="w-24 text-center">e-Sud</span>
                  <span />
                </div>

                {/* Rows */}
                <div className="divide-y divide-slate-100">
                  {activity.slice(0, 12).map((item) => (
                    <div
                      key={item.id}
                      className="grid grid-cols-[1fr_auto_auto_44px] gap-2 items-center px-4 py-3 hover:bg-slate-50 transition group"
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
                      <div className="w-28 flex justify-center">
                        {item.badge && (
                          <span className={`inline-flex items-center text-xs font-medium px-2.5 py-1 rounded-full ${
                            item.badgeColor ? BADGE_COLORS[item.badgeColor] || "text-slate-500 bg-slate-100" : "text-slate-500 bg-slate-100"
                          }`}>
                            {item.badge}
                          </span>
                        )}
                      </div>

                      {/* e-Komunikacija button */}
                      <div className="w-24 flex justify-center">
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

          {/* ── RIGHT: Legal Feed + Stats ── */}
          <div className="space-y-4">
            {/* Legal Intelligence Feed */}
            <LegalFeed />

            {/* Quick Stats */}
            {!loading && activity.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
                <h3 className="text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  Brzi pregled
                </h3>
                <div className="grid grid-cols-2 gap-3">
                  {(["valuation", "contract", "chronology", "legal"] as ActivityType[]).map((t) => {
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

      {/* Footer */}
      <footer className="max-w-7xl mx-auto px-4 sm:px-6 pb-6">
        <div className="border-t border-slate-200 pt-4 text-center space-y-1 text-[11px] text-slate-400">
          <div className="flex items-center justify-between">
            <span>PravoTek v2.0</span>
            <span>
              AI analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11)
            </span>
          </div>
          <p>Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu.</p>
        </div>
      </footer>
    </div>
  );
}
