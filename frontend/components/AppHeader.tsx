"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { Scale, Clock, ShieldAlert, Shield, LogOut, Search, Zap } from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { useUserId } from "@/lib/useUserId";
import { useCredits } from "@/lib/useCredits";
import React from "react";

interface NavItem {
  href: string;
  label: string;
  icon: typeof Clock;
}

const navItems: NavItem[] = [
  { href: "/history", label: "Povijest", icon: Clock },
  { href: "/audit", label: "Revizija", icon: ShieldAlert },
  { href: "/postavke", label: "Postavke", icon: Shield },
];

interface AppHeaderProps {
  /** Extra elements rendered before the nav links (e.g. tour button) */
  extraStart?: React.ReactNode;
}

export default function AppHeader({ extraStart }: AppHeaderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const userId = useUserId();
  const credits = useCredits(userId);

  return (
    <header className="bg-slate-900 text-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
        {/* ── Logo ── */}
        <Link href="/" className="flex items-center gap-3 group">
          <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
            <Scale className="h-5 w-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold tracking-tight group-hover:text-blue-300 transition">
              PravoTek
            </h1>
            <p className="text-[10px] text-slate-400 uppercase tracking-widest">
              Pravna Inteligencija
            </p>
          </div>
        </Link>

        {/* ── Right side: search trigger + optional extras + nav links + logout ── */}
        <div className="flex items-center gap-2 sm:gap-4">
          {/* Search trigger */}
          <button
            onClick={() => window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", ctrlKey: true }))}
            className="hidden sm:inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-xs text-slate-400 hover:text-white hover:border-slate-500 transition"
            title="Pretraži (Ctrl+K)"
          >
            <Search className="h-3.5 w-3.5" />
            <span>Pretraži</span>
            <kbd className="ml-1 rounded border border-slate-600 bg-slate-700/50 px-1 py-0.5 text-[9px] font-mono">⌘K</kbd>
          </button>

          {extraStart}

          {navItems.map(({ href, label, icon: Icon }) => {
            const isActive = pathname === href;
            return (
              <Link
                key={href}
                href={href}
                className={`relative text-xs transition flex items-center gap-1 p-1.5 sm:p-0 rounded ${
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

          <div className="h-4 w-px bg-slate-700 hidden sm:block" />

          {/* Credit badge */}
          {!credits.loading && credits.total > 0 && (
            <Link
              href="/postavke"
              className="hidden sm:inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-xs transition hover:border-slate-500"
              title={`${credits.remaining} / ${credits.total} kredita preostalo`}
            >
              <Zap className="h-3 w-3 text-amber-400" />
              <span className="tabular-nums text-white font-medium">{credits.remaining}</span>
              <span className="text-slate-500">/ {credits.total}</span>
            </Link>
          )}
          {!credits.loading && credits.total === 0 && !credits.error && userId && (
            <Link
              href="/postavke"
              className="hidden sm:inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-xs text-slate-400 transition hover:border-slate-500 hover:text-white"
              title="Krediti"
            >
              <Zap className="h-3 w-3" />
              <span>0 kredita</span>
            </Link>
          )}

          <div className="h-4 w-px bg-slate-700 hidden sm:block" />

          <button
            onClick={async () => {
              const supabase = createSupabaseBrowser();
              await supabase.auth.signOut();
              router.push("/auth/login");
              router.refresh();
            }}
            className="text-xs text-slate-500 hover:text-red-400 transition flex items-center gap-1 p-1.5 sm:p-0"
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
