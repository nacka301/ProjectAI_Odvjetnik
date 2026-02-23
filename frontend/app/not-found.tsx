import Link from "next/link";
import { Scale, ArrowLeft, Search } from "lucide-react";

export default function NotFound() {
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* ── Mini header ── */}
      <header className="bg-slate-900 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex items-center gap-3">
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
      </header>

      {/* ── Content ── */}
      <div className="flex-1 flex items-center justify-center px-4">
        <div className="max-w-lg w-full text-center space-y-8">
          {/* Large 404 number */}
          <div className="relative">
            <p className="text-[120px] sm:text-[160px] font-bold text-slate-100 leading-none select-none">
              404
            </p>
            <div className="absolute inset-0 flex items-center justify-center">
              <Search className="h-16 w-16 text-[#1B2A4A] opacity-40" />
            </div>
          </div>

          {/* Heading + description */}
          <div className="space-y-3 -mt-4">
            <h2
              className="text-2xl sm:text-3xl font-bold text-[#1B2A4A]"
              style={{ fontFamily: "var(--font-playfair), serif" }}
            >
              Stranica nije pronađena
            </h2>
            <p className="text-sm text-gray-500 max-w-sm mx-auto">
              Tražena stranica ne postoji, premještena je ili je poveznica
              zastarjela. Provjerite adresu ili se vratite na početnu.
            </p>
          </div>

          {/* Actions */}
          <div className="flex flex-col sm:flex-row items-center justify-center gap-3">
            <Link
              href="/"
              className="inline-flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-6 py-3 text-sm font-semibold text-white hover:bg-[#243660] transition shadow-sm"
            >
              <ArrowLeft className="h-4 w-4" />
              Povratak na početnu
            </Link>
          </div>

          {/* Subtle footer */}
          <p className="text-xs text-gray-400">
            Ako mislite da je ovo greška, obratite se podršci.
          </p>
        </div>
      </div>
    </div>
  );
}
