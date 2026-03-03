"use client";

import { Shield, Lock, ServerCrash } from "lucide-react";

// ═══════════════════════════════════════════════════════════
//  TRUST UI COMPONENTS — Psychological Security for Lawyers
//
//  These components are designed to build confidence that
//  the platform handles data with utmost confidentiality.
//  Odvjetnička tajna (attorney-client privilege) is sacred.
// ═══════════════════════════════════════════════════════════

// ── 1. PRIVACY BANNER — Above document input areas ──
// Use on: /prijevod, /ugovor, /zk-procjena
// Shows a prominent but elegant shield banner reassuring
// users that their data is private and never stored.

export function PrivacyBanner() {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-emerald-200 bg-gradient-to-r from-emerald-50 to-teal-50 px-4 py-3 mb-4">
      <Shield className="h-5 w-5 text-emerald-600 shrink-0 mt-0.5" />
      <div className="text-sm leading-relaxed">
        <span className="font-semibold text-emerald-800">Vaši dokumenti su zaštićeni.</span>{" "}
        <span className="text-emerald-700">
          Podaci se kriptiraju tijekom prijenosa (TLS 1.3) i <strong>ne koriste se za treniranje
          modela strojnog učenja</strong>. Tekstovi se ne pohranjuju trajno na vanjskim servisima.
          Obrada je sukladna odvjetničkoj tajni (čl. 13. Zakona o odvjetništvu) i Uredbi (EU) 2016/679 (GDPR).
        </span>
      </div>
    </div>
  );
}

// ── 2. LOGIN TRUST TEXT — Below login button ──
// A subtle, reassuring message with a lock icon.

export function LoginTrustText() {
  return (
    <div className="flex items-center justify-center gap-2 mt-4 text-xs text-slate-400">
      <Lock className="h-3.5 w-3.5 text-slate-400" />
      <span>
        Kriptirani prijenos podataka (TLS 1.3). Obrada sukladna GDPR-u i odvjetničkoj tajni.
      </span>
    </div>
  );
}

// ── 3. FOOTER TRUST BADGES — Bottom of every page ──
// Professional SVG-style badges: AES-256, GDPR, Zero-Data Retention

export function TrustBadges() {
  return (
    <div className="flex flex-wrap items-center justify-center gap-4 sm:gap-6 py-3">
      {/* AES-256 Encryption */}
      <div className="flex items-center gap-1.5 text-[11px] text-slate-400">
        <svg
          className="h-4 w-4 text-slate-400"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
          <path d="M7 11V7a5 5 0 0 1 10 0v4" />
        </svg>
        <span>256-bit AES</span>
      </div>

      {/* Divider */}
      <div className="h-3 w-px bg-slate-200" />

      {/* GDPR */}
      <div className="flex items-center gap-1.5 text-[11px] text-slate-400">
        <svg
          className="h-4 w-4 text-slate-400"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          <path d="m9 12 2 2 4-4" />
        </svg>
        <span>GDPR sukladno</span>
      </div>

      {/* Divider */}
      <div className="h-3 w-px bg-slate-200" />

      {/* Zero-Data Retention */}
      <div className="flex items-center gap-1.5 text-[11px] text-slate-400">
        <svg
          className="h-4 w-4 text-slate-400"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <circle cx="12" cy="12" r="10" />
          <path d="M12 6v6l4 2" />
          <line x1="4" y1="4" x2="20" y2="20" />
        </svg>
        <span>Zero-Data Retention</span>
      </div>
    </div>
  );
}

// ── 4. SECURITY FOOTER — Complete footer with legal text + badges ──
// Designed to replace the existing dashboard-only footer and be used globally.

export function SecurityFooter() {
  return (
    <footer className="w-full border-t border-slate-200 bg-white mt-auto">
      <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 space-y-4">
        {/* Trust Badges — centered */}
        <div className="flex flex-wrap items-center justify-center gap-5 sm:gap-8">
          {/* AES-256 */}
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <svg className="h-4 w-4 text-slate-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
            <span>256-bit AES</span>
          </div>
          <div className="h-3.5 w-px bg-slate-200" />
          {/* GDPR */}
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <svg className="h-4 w-4 text-slate-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              <path d="m9 12 2 2 4-4" />
            </svg>
            <span>GDPR sukladno</span>
          </div>
          <div className="h-3.5 w-px bg-slate-200" />
          {/* Zero-Data Retention */}
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <svg className="h-4 w-4 text-slate-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <path d="M12 6v6l4 2" />
              <line x1="4" y1="4" x2="20" y2="20" />
            </svg>
            <span>Zero-Data Retention</span>
          </div>
        </div>

        {/* Disclaimer */}
        <div className="text-center space-y-2 max-w-4xl mx-auto">
          <p className="text-xs text-slate-400 leading-relaxed">
            Analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11).
            Obrada osobnih podataka sukladno Uredbi (EU) 2016/679 (GDPR). Svi pozivi prema analitičkim servisima koriste Zero-Data Retention politiku — vaši tekstovi se ne pohranjuju niti koriste za treniranje modela.
          </p>
          {/* Legal links */}
          <div className="flex items-center justify-center gap-1 text-xs text-slate-400">
            <a href="/uvjeti" className="hover:text-slate-600 transition underline underline-offset-2">
              Uvjeti korištenja
            </a>
            <span>·</span>
            <a href="/privatnost" className="hover:text-slate-600 transition underline underline-offset-2">
              Politika privatnosti
            </a>
            <span>·</span>
            <a href="/dpa" className="hover:text-slate-600 transition underline underline-offset-2">
              DPA
            </a>
          </div>
          <p className="text-xs text-slate-400">
            © {new Date().getFullYear()} PravoTek. Sva prava pridržana.
          </p>
        </div>
      </div>
    </footer>
  );
}
