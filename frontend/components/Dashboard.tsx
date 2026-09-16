"use client";

import { useEffect, useRef, useState } from "react";
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

  Sparkles,
  CircleHelp,
  Send,
  Landmark,
  Construction,
  Calculator,
  Users,
  Languages,
  Lock,
  Scale,
} from "lucide-react";
import { getRecentActivity, type ActivityItem, type ActivityType } from "@/lib/supabase";
import { loadOnboardingState } from "@/lib/onboarding";
import { useVodic } from "@/contexts/VodicContext";
import AppHeader from "@/components/AppHeader";
import { Obavijest } from "@/components/Obavijest";
import { SecurityFooter } from "@/components/TrustComponents";
import { useSubscription } from "@/lib/useSubscription";
import { useIsAdmin } from "@/lib/useIsAdmin";
import { isModuleLocked, moduleTitle } from "@/lib/modules";

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

/** A plain Croatian date, for "vrijedi do". */
function formatDatum(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString("hr-HR", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  } catch {
    return iso;
  }
}

const ACTIVITY_ICONS: Record<ActivityType, React.ReactNode> = {
  troskovnik: <Calculator className="h-4 w-4 text-indigo-500" />,
  valuation: <Building2 className="h-4 w-4 text-blue-500" />,
  contract: <ShieldAlert className="h-4 w-4 text-rose-500" />,
  chronology: <Building2 className="h-4 w-4 text-violet-500" />,
  legal: <Building2 className="h-4 w-4 text-amber-500" />,
  invoice: <Building2 className="h-4 w-4 text-teal-500" />,
};

const ACTIVITY_LABELS: Record<ActivityType, string> = {
  troskovnik: "Troškovnik",
  valuation: "Procjena",
  contract: "Ugovor",
  chronology: "Kronologija",
  legal: "Pravna analiza",
  invoice: "e-Račun",
};

/** Where a listed record opens. Only the troškovnik has a page of its own. */
function activityHref(item: ActivityItem): string {
  return item.type === "troskovnik" ? `/troskovnik/${item.id}` : "/history";
}

const BADGE_COLORS: Record<string, string> = {
  green: "text-green-700 bg-green-50",
  amber: "text-amber-700 bg-amber-50",
  red: "text-red-700 bg-red-50",
  indigo: "text-indigo-700 bg-indigo-50",
};

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

// ═══════════════════════════════════════════════════════════
//  FEATURE CARD DATA
// ═══════════════════════════════════════════════════════════

/**
 * A card is not what decides whether a module is open — `frontend/lib/modules.ts`
 * is, and the backend guard is what enforces it. This carries only the parts a
 * registry cannot: the icon, the copy, the route.
 */
interface FeatureCard {
  /** Key into the module registry. The card's DOM id is `card-${moduleId}`. */
  moduleId: string;
  href: string;
  icon: React.ReactNode;
  title: string;
  description: string;
  howItWorks: string;
}

const FEATURE_CARDS: FeatureCard[] = [
  {
    moduleId: "procjena",
    href: "/wizard",
    icon: <SearchIcon className="h-5 w-5" />,
    title: "Tržišna Procjena",
    description: "Izračun realne tržišne vrijednosti nekretnine temeljen na usporedivim transakcijama, mikrolokaciji i faktorima rizika.",
    howItWorks:
      "Unesite podatke ili zalijepite link oglasa. Sustav će analizirati tekst (npr. 'kaskadni teren') i primijeniti korektivne faktore na cijenu.",
  },
  {
    moduleId: "ugovor",
    href: "/ugovor",
    icon: <ShieldAlert className="h-5 w-5" />,
    title: "Analiza Ugovora",
    description: "Automatska detekcija pravnih zamki, nedostajućih klauzula i rizičnih odredbi u kupoprodajnim i zakupnim ugovorima.",
    howItWorks:
      "Učitajte nacrt ugovora. Sustav traži 'crvene zastavice' (npr. nedostatak tabularne izjave) i predlaže sigurnije klauzule prema ZOO-u.",
  },
  {
    moduleId: "zk",
    href: "/zk-procjena",
    icon: <Landmark className="h-5 w-5" />,
    title: "ZK Izvadak",
    description: "Dubinska analiza zemljišnoknjižnog stanja — identificira vlasnike, terete, hipoteke, plombe i pravne rizike za transakciju.",
    howItWorks:
      "Učitajte PDF ZK izvatka. Sustav identificira vlasnike, terete (hipoteke, služnosti, fiducije), plombe i ocjenjuje rizik za kupnju ili gradnju.",
  },
  {
    moduleId: "troskovnik",
    href: "/troskovnik",
    icon: <Calculator className="h-5 w-5" />,
    title: "Odvjetnički Troškovnik",
    description: "Izračun nagrade i troškova prema službenoj Tarifi o nagradama za rad odvjetnika s automatskim bodovanjem radnji.",
    howItWorks:
      "Unesite vrijednost predmeta spora, odaberite obavljene radnje — sustav automatski računa bodove i iznos u EUR prema službenoj tarifi (1 bod = 2,00 €).",
  },
  {
    moduleId: "stranke",
    href: "/klijenti",
    icon: <Users className="h-5 w-5" />,
    title: "Stranke",
    description: "Centralizirano upravljanje strankama, predmetima i troškovnicima — kompletni dosje svake stranke na jednom mjestu.",
    howItWorks:
      "Kreirajte stranke, povežite predmete i troškovnike — pregledajte kompletni dosje svake stranke.",
  },
  {
    moduleId: "prijevod",
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
  const subscription = useSubscription();
  const isAdmin = useIsAdmin();
  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [blockedModule, setBlockedModule] = useState<string | null>(null);
  /** Poruka za e-Komunikaciju (čeka FINA integraciju) — zamjena za window.alert(). */
  const [eKomunikacijaPoruka, setEKomunikacijaPoruka] = useState<string | null>(null);
  const { pokreni } = useVodic();
  /**
   * Čuva da se uvod pokrene TOČNO jednom po dolasku, ne dvaput.
   *
   * React Strict Mode u razvoju namjerno pozove efekt dvaput; bez ove zaštite
   * `pokreni` bi se pozvao dva puta zaredom (svejedno bezopasno, ali nepotrebno).
   */
  const uvodPokrenutRef = useRef(false);

  const cards = FEATURE_CARDS.map((card) => ({
    ...card,
    domId: `card-${card.moduleId}`,
    locked: isModuleLocked(card.moduleId, isAdmin),
  }));

  // "Start something" buttons must land somewhere the viewer can actually work.
  // Pointing an ordinary user at /wizard would bounce them straight back here.
  const startHere = isModuleLocked("procjena", isAdmin)
    ? { href: "/troskovnik", label: "Otvori Troškovnik", icon: Calculator }
    : { href: "/wizard", label: "Započni prvu analizu", icon: SearchIcon };

  // The middleware sends users here with ?modul=<id> when they type the route of
  // a module that is not finished. Read from location rather than
  // useSearchParams, which would demand a <Suspense> boundary around the page.
  useEffect(() => {
    const id = new URLSearchParams(window.location.search).get("modul");
    if (id && moduleTitle(id)) setBlockedModule(id);
  }, []);

  // Is this user new? The answer comes from user_profiles.onboarded_at for THIS
  // user, asked on every mount. An earlier version short-circuited on a browser-
  // global localStorage hint, so a second person signing in on the same browser
  // inherited the first person's "already onboarded" and never saw the tour — and
  // the same held for a profile that was deleted and recreated. There is no local
  // shortcut now: the guide is offered only after the database says
  // `isNew === true`, so nothing flashes while the query is in flight.
  //
  // Pokreće se sam, bez pitanja. Prije je ovdje stajala traka „Pokažimo vam
  // kako ovo radi" s gumbom „Kreni" — jedan dodatni klik između registracije i
  // uvoda koji ništa nije dodavao (odustajanje već postoji: „Preskoči" i Esc
  // unutar samog vodiča, oboje odmah bilježe da je uvod gotov).
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const { isNew } = await loadOnboardingState();
      if (!cancelled && isNew === true && !uvodPokrenutRef.current) {
        uvodPokrenutRef.current = true;
        pokreni("dobrodoslica");
      }
    })();
    return () => { cancelled = true; };
  }, [pokreni]);

  useEffect(() => {
    (async () => {
      try {
        const data = await getRecentActivity(10, isAdmin);
        setActivity(data);
      } catch {
        // Silently handle — dashboard still works without history
      } finally {
        setLoading(false);
      }
    })();
  }, [isAdmin]);

  return (
    <div className="min-h-screen bg-slate-50 font-sans">
      {/* ═══════════ Top Navigation ═══════════ */}
      {/* Gumb „Vodič" vise ne stoji ovdje nego u samom zaglavlju, kao stavka
          izbornika — dostupan je s Povijesti i iz Postavki jednako kao odavde. */}
      <AppHeader />

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-6 space-y-6">

        {/* ═══════════ 0. "MODULE STILL IN DEVELOPMENT" NOTICE ═══════════ */}
        {/* Shown after the middleware redirected a typed-in route back here.  */}
        {blockedModule && (
          <div
            role="status"
            className="flex items-start gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3"
          >
            <Construction className="h-4 w-4 text-amber-600 mt-0.5 shrink-0" />
            <p className="text-sm text-amber-900 flex-1">
              Modul <span className="font-semibold">{moduleTitle(blockedModule)}</span>{" "}
              još je u razvoju i zasad nije dostupan.
            </p>
            <button
              onClick={() => setBlockedModule(null)}
              className="text-amber-600 hover:text-amber-800 transition"
              aria-label="Zatvori obavijest"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        )}

        {/* ═══════════ 1. FEATURE GRID — Enterprise Module Cards ═══════════ */}
        <section>
          <div className="flex items-center justify-between gap-4 mb-5">
            <div>
              <h2 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-slate-900 tracking-tight">
                Moduli
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">Odaberite modul za početak rada</p>
            </div>
            {/* Brz ulaz u modul koji korisnik može odmah koristiti. Prije je ovo
                bio jedini koristan dio plavog bannera („Otvori Troškovnik"); banner
                je maknut jer je obećavao povlačenje dokumenta i prepoznavanje
                vrste — funkcije koje ne postoje i koje bi na zaključanim modulima
                zavarale korisnika. Gumb ostaje, na suvislom mjestu. */}
            <Link
              href={startHere.href}
              className="inline-flex shrink-0 items-center gap-2 bg-[#1B2A4A] hover:bg-[#243759] text-white text-sm font-medium px-4 py-2.5 rounded-lg transition shadow-sm whitespace-nowrap"
            >
              <startHere.icon className="h-4 w-4" />
              {startHere.label}
            </Link>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {cards.map((card) => {
              const inner = (
                <div
                  id={card.domId}
                  aria-disabled={card.locked || undefined}
                  className={`group relative bg-white rounded-lg border border-slate-200 shadow-sm p-6 transition-all duration-200 ${
                    card.locked
                      ? "cursor-not-allowed"
                      : "hover:border-[#1B2A4A]/40 hover:shadow-md hover:-translate-y-0.5"
                  }`}
                >
                  {/* "In development" badge */}
                  {card.locked && (
                    <div className="absolute top-3 right-3 z-10 flex items-center gap-1 bg-amber-50 text-amber-700 text-[10px] font-semibold uppercase tracking-wider px-2.5 py-1 rounded-md">
                      <Construction className="h-3 w-3" />
                      U razvoju
                    </div>
                  )}

                  {/* Top row: icon + info button */}
                  <div className="flex items-start justify-between mb-4">
                    <div className="w-10 h-10 rounded-lg bg-slate-100 group-hover:bg-slate-200/80 flex items-center justify-center transition-colors duration-200">
                      <span className="text-[#1B2A4A]">{card.icon}</span>
                    </div>
                    {!card.locked && <HoverInfoCard text={card.howItWorks} />}
                  </div>

                  {/* Title — Playfair serif. Stays sharp even when locked: the
                      point is that the feature is coming, not that it is broken. */}
                  <h3 className="font-[family-name:var(--font-playfair)] font-semibold text-[#1B2A4A] text-base mb-2 tracking-tight">
                    {card.title}
                  </h3>

                  {/* Description — blurred while the module is in development */}
                  <p
                    aria-hidden={card.locked || undefined}
                    className={`text-sm text-slate-500 leading-relaxed mb-4 ${
                      card.locked ? "blur-[3px] select-none" : ""
                    }`}
                  >
                    {card.description}
                  </p>

                  {/* CTA hint */}
                  {card.locked ? (
                    <div className="flex items-center gap-1.5 text-xs font-medium text-amber-700">
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

              // No <Link> wrapper: a locked card is not navigable at all, not
              // merely styled as if it were not.
              if (card.locked) {
                return <div key={card.domId}>{inner}</div>;
              }

              return (
                <Link key={card.domId} href={card.href}>
                  {inner}
                </Link>
              );
            })}
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
                  Otvorite modul da započnete
                </p>
                <Link
                  href={startHere.href}
                  className="inline-flex items-center gap-1 mt-4 text-xs font-medium text-blue-600 hover:underline"
                >
                  {startHere.label} <ArrowRight className="h-3 w-3" />
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
                      <Link href={activityHref(item)} className="flex items-center gap-3 min-w-0">
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
                            setEKomunikacijaPoruka(
                              `Predmet „${item.title}" pripremljen je za slanje na sud putem sustava e-Komunikacija.\n\nZa slanje je potrebna integracija s FINA sustavom e-Komunikacija i aktivna odvjetnička pametna kartica.`
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
                      <Link href={activityHref(item)} className="flex justify-center">
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
            {/* ── Subscription Card ── */}
            {/* One plan, so there is nothing to name here — what the user needs
                to know is which state they are in and until when. */}
            {!subscription.loading && (
              <Link href="/postavke" className="block">
                <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3 hover:border-slate-300 hover:shadow-sm transition">
                  <div className="flex items-center justify-between">
                    <h3 className="text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                      Pristup
                    </h3>
                    <span
                      className={`inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full border ${
                        subscription.state === "isteklo"
                          ? "bg-red-50 text-red-700 border-red-200"
                          : subscription.state === "proba"
                            ? "bg-amber-50 text-amber-700 border-amber-200"
                            : "bg-emerald-50 text-emerald-700 border-emerald-200"
                      }`}
                    >
                      {subscription.state === "pretplata" && <Sparkles className="h-2.5 w-2.5" />}
                      {subscription.state === "isteklo" ? "Zatvoreno" : "Aktivno"}
                    </span>
                  </div>

                  <div className="flex items-center gap-3">
                    <div
                      className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0"
                      style={{ background: "#1B2A4A" }}
                    >
                      {subscription.state === "isteklo" ? (
                        <Lock className="h-4 w-4" style={{ color: "#C9A84C" }} />
                      ) : (
                        <Scale className="h-4 w-4" style={{ color: "#C9A84C" }} />
                      )}
                    </div>
                    <div>
                      <p className="text-base font-bold text-slate-900">
                        {subscription.badgeLabel}
                      </p>
                      <p className="text-[11px] text-slate-400">
                        {subscription.state === "isteklo"
                          ? "Troškovnik i Stranke su zatvoreni"
                          : subscription.accessUntil
                            ? `Vrijedi do ${formatDatum(subscription.accessUntil)}`
                            : "Pristup je otvoren"}
                      </p>
                    </div>
                  </div>
                </div>
              </Link>
            )}
            {subscription.loading && (
              <div className="bg-white rounded-xl border border-slate-200 p-4">
                <div className="flex items-center gap-2">
                  <span className="h-2 w-2 rounded-full bg-slate-300 animate-pulse" />
                  <span className="text-xs text-slate-400">Učitavanje pristupa…</span>
                </div>
              </div>
            )}
            {/* Quick Stats */}
            {!loading && activity.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
                <h3 className="text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  Brzi pregled
                </h3>
                <div className="grid grid-cols-2 gap-3">
                  {/* Derived from what is on the list, not a fixed pair. The
                      hardcoded ["valuation","contract"] counted two locked
                      modules and never the one module that works. */}
                  {[...new Set(activity.map((a) => a.type))].map((t) => {
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
      {/* ConditionalSecurityFooter (app/layout.tsx) skips "/" so the public
          landing page's own Podnožje doesn't get a second footer stacked
          under it — Dashboard renders here directly instead. */}
      <SecurityFooter />
      {eKomunikacijaPoruka && (
        <Obavijest
          naslov="e-Komunikacija"
          poruka={eKomunikacijaPoruka}
          onClose={() => setEKomunikacijaPoruka(null)}
        />
      )}
    </div>
  );
}
