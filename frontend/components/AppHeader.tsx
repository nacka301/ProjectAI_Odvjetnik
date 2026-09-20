"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { Scale, Clock, ShieldCheck, Shield, LogOut, Compass } from "lucide-react";
import { signOutCompletely } from "@/lib/sign-out";
import { useSubscription } from "@/lib/useSubscription";
import { useIsAdmin } from "@/lib/useIsAdmin";
import { isPathLocked } from "@/lib/modules";
import { VODIC_NAZIVI, useVodic, type VodicId } from "@/contexts/VodicContext";
import React from "react";

// ── Subscription badge styling ──
// Three states, not three plans. Amber while the trial runs down, green once
// it is paid for, red when access has closed — the colour carries the urgency
// so the wording does not have to shout.
const STATE_BADGE_STYLES: Record<string, { bg: string; text: string; border: string }> = {
  proba:     { bg: "bg-amber-500/10",   text: "text-amber-400",   border: "border-amber-500/30" },
  pretplata: { bg: "bg-emerald-500/10", text: "text-emerald-400", border: "border-emerald-500/30" },
  isteklo:   { bg: "bg-red-500/10",     text: "text-red-400",     border: "border-red-500/30" },
};

interface NavItem {
  href: string;
  label: string;
  icon: typeof Clock;
}

const navItems: NavItem[] = [
  { href: "/history", label: "Povijest", icon: Clock },
  { href: "/postavke", label: "Postavke", icon: Shield },
];

/**
 * Stavka „Vodič" — uvijek dostupna, iz svakog ekrana.
 *
 * Vodiča ima tri, pa jedan klik ne može značiti jednu stvar: gumb otvara mali
 * popis. Prije je „Vodič" bio gumb na troškovniku i na strankama zasebno, pa
 * odvjetnik koji se vratio za mjesec dana nije imao odakle ga pokrenuti ako se
 * zatekao na Povijesti ili u Postavkama.
 */
function VodicIzbornik() {
  const { pokreni } = useVodic();
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    if (!open) return;
    const vani = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const esc = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", vani);
    document.addEventListener("keydown", esc);
    return () => {
      document.removeEventListener("mousedown", vani);
      document.removeEventListener("keydown", esc);
    };
  }, [open]);

  const odaberi = (id: VodicId) => {
    setOpen(false);
    pokreni(id);
  };

  return (
    <div className="relative" ref={ref}>
      {/* max-sm:min-h-12/min-w-12 + max-sm:-m-2.5: dodirno područje je puni
          48px, ali negativna margina ga vraća na izvornu ~28px širinu u
          retku — ikona ostaje ista, prošireno je samo područje na koje se
          može kliknuti (paddingom, ne ikonom), točno kako traži prompt.
          Bez ovoga pet ikona (admin prikaz) ne bi stalo na 360px uz puni
          logotip. */}
      <button
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="menu"
        aria-expanded={open}
        className={`text-xs transition flex items-center justify-center gap-1 p-1.5 sm:p-0 max-sm:min-h-12 max-sm:min-w-12 max-sm:-m-2.5 rounded ${
          open ? "text-white font-semibold" : "text-slate-400 hover:text-white"
        }`}
        title="Pokreni vodič"
      >
        <Compass className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
        <span className="hidden sm:inline">Vodič</span>
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 top-full z-50 mt-2 w-56 overflow-hidden rounded-lg border border-slate-200 bg-white py-1 shadow-xl"
        >
          <p className="px-3 pb-1 pt-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-400">
            Pokreni vodič
          </p>
          {(Object.keys(VODIC_NAZIVI) as VodicId[]).map((id) => (
            <button
              key={id}
              role="menuitem"
              onClick={() => odaberi(id)}
              className="block w-full px-3 py-2 text-left text-xs text-slate-700 transition hover:bg-slate-50"
            >
              {VODIC_NAZIVI[id]}
              {id === "dobrodoslica" && (
                <span className="block text-[10px] text-slate-400">
                  na podacima za primjer, ništa se ne sprema
                </span>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

interface AppHeaderProps {
  /** Extra elements rendered before the nav links (e.g. tour button) */
  extraStart?: React.ReactNode;
}

function AppHeaderInner({ extraStart }: AppHeaderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const subscription = useSubscription();

  // ── Admin detection (cookie set by middleware) ──
  const isAdmin = useIsAdmin();

  // A link to a module still in development is a link to a redirect. Neither
  // /history nor /postavke belongs to a module, so both always stay; this
  // guards future nav items that do.
  const visibleNavItems = navItems.filter(({ href }) => !isPathLocked(href, isAdmin));

  return (
    <header className="bg-slate-900 text-white">
      {/* Jedan red, ≤56px na mobitelu: retka visinu ovdje ne diktira logo
          (32px) nego dodirne mete desno (48px, max-sm:min-h-12) — zato je
          py samo 4px (py-1): 48 + 4+4 = 56px točno. Podnaslov ide dolje tek
          na sm+, gdje zaglavlje ionako naraste na py-4. */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 py-1 sm:py-4 flex items-center justify-between">
        {/* ── Logo ── */}
        {/* shrink-0: logotip se nikad ne smije stisnuti/skratiti — čak i
            kad peti (admin) gumb stigne u desni klaster, popušta on, ne
            marka. */}
        <Link href="/" className="flex items-center gap-2 sm:gap-3 group shrink-0">
          <div className="w-8 h-8 shrink-0 bg-blue-600 rounded-lg flex items-center justify-center">
            <Scale className="h-5 w-5 text-white" />
          </div>
          <div className="min-w-0">
            <h1 className="text-base sm:text-lg font-bold tracking-tight group-hover:text-blue-300 transition truncate">
              PravoTek
            </h1>
            <p className="hidden sm:block text-[10px] text-slate-400 uppercase tracking-widest">
              Pravna Inteligencija
            </p>
          </div>
        </Link>

        {/* ── Right side: optional extras + nav links + logout ── */}
        <div className="flex items-center gap-2 sm:gap-4">
          {extraStart}

          <VodicIzbornik />

          {visibleNavItems.map(({ href, label, icon: Icon }) => {
            const isActive = pathname === href;
            return (
              <Link
                key={href}
                href={href}
                className={`relative text-xs transition flex items-center justify-center gap-1 p-1.5 sm:p-0 max-sm:min-h-12 max-sm:min-w-12 max-sm:-m-2.5 rounded ${
                  isActive
                    ? "text-white font-semibold"
                    : "text-slate-400 hover:text-white"
                }`}
              >
                <Icon className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
                <span className="hidden sm:inline">{label}</span>
                {isActive && (
                  <span className="hidden sm:block absolute -bottom-3 left-1/2 -translate-x-1/2 w-5 h-0.5 bg-blue-400 rounded-full" />
                )}
              </Link>
            );
          })}

          {/* ── Admin link (visible only to admin email) ── */}
          {isAdmin && (
            <Link
              href="/admin"
              className={`relative text-xs transition flex items-center justify-center gap-1 p-1.5 sm:p-0 max-sm:min-h-12 max-sm:min-w-12 max-sm:-m-2.5 rounded ${
                pathname === "/admin"
                  ? "text-amber-400 font-semibold"
                  : "text-amber-500/70 hover:text-amber-400"
              }`}
              title="Admin Dashboard"
            >
              <ShieldCheck className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
              <span className="hidden sm:inline">Admin</span>
              {pathname === "/admin" && (
                <span className="hidden sm:block absolute -bottom-3 left-1/2 -translate-x-1/2 w-5 h-0.5 bg-amber-400 rounded-full" />
              )}
            </Link>
          )}

          <div className="h-4 w-px bg-slate-700 hidden sm:block" />

          {/* Subscription badge */}
          {!subscription.loading && (
            <Link
              href="/postavke"
              className={`hidden sm:inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs font-semibold uppercase tracking-wider transition hover:opacity-90 ${
                STATE_BADGE_STYLES[subscription.state]?.bg ?? STATE_BADGE_STYLES.pretplata.bg
              } ${
                STATE_BADGE_STYLES[subscription.state]?.text ?? STATE_BADGE_STYLES.pretplata.text
              } ${
                STATE_BADGE_STYLES[subscription.state]?.border ?? STATE_BADGE_STYLES.pretplata.border
              }`}
              title="Pristup — otvori postavke"
            >
              {subscription.badgeLabel}
            </Link>
          )}
          {subscription.loading && (
            <span className="hidden sm:inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-xs text-slate-500">
              <span className="h-1.5 w-1.5 rounded-full bg-slate-500 animate-pulse" />
              Pristup…
            </span>
          )}

          <div className="h-4 w-px bg-slate-700 hidden sm:block" />

          <button
            onClick={async () => {
              await signOutCompletely();
              router.push("/auth/login");
              router.refresh();
            }}
            className="text-xs text-slate-500 hover:text-red-400 transition flex items-center justify-center gap-1 p-1.5 sm:p-0 max-sm:min-h-12 max-sm:min-w-12 max-sm:-m-2.5"
            title="Odjavi se"
          >
            <LogOut className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
            <span className="hidden sm:inline">Odjava</span>
          </button>
        </div>
      </div>
    </header>
  );
}

export default React.memo(AppHeaderInner);
