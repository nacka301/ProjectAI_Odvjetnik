"use client";

import Link from "next/link";
import { Scale, FileSearch, ArrowRight, BookOpen, ClipboardList } from "lucide-react";

export default function Home() {
  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4 font-sans">
      <div className="max-w-3xl w-full text-center">
        <h1 className="text-4xl font-bold text-gray-900 mb-3">
          ⚖️ Legal AI Assistant
        </h1>
        <p className="text-gray-500 mb-12 text-lg">
          AI pomoćnik za odvjetnike — odaberite alat
        </p>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Wizard – structured input */}
          <Link
            href="/wizard"
            className="group flex flex-col items-center gap-4 rounded-xl border-2 border-blue-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-blue-400 md:col-span-3"
          >
            <ClipboardList className="h-12 w-12 text-blue-600" />
            <h2 className="text-xl font-semibold text-gray-900">
              Wizard — Unos nekretnine
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed max-w-lg">
              Korak-po-korak unesite tip, lokaciju i detalje nekretnine.
              Sustav validira podatke, generira opis i pokreće tržišnu procjenu.
            </p>
            <span className="flex items-center gap-1 text-sm font-medium text-blue-600 group-hover:underline">
              Pokreni <ArrowRight className="h-4 w-4" />
            </span>
          </Link>

          {/* NEW: Unified ZK → Valuation Card */}
          <Link
            href="/zk-procjena"
            className="group flex flex-col items-center gap-4 rounded-xl border-2 border-indigo-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-indigo-400 md:col-span-3"
          >
            <BookOpen className="h-12 w-12 text-indigo-600" />
            <h2 className="text-xl font-semibold text-gray-900">
              ZK Izvadak → Analiza + Procjena
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed max-w-lg">
              Uploadajte ZK izvadak — sustav automatski parsira sva 3 lista (A/B/C),
              analizira pravni rizik i procjenjuje tržišnu vrijednost nekretnine. Sve u jednom koraku.
            </p>
            <span className="flex items-center gap-1 text-sm font-medium text-indigo-600 group-hover:underline">
              Pokreni <ArrowRight className="h-4 w-4" />
            </span>
          </Link>

          {/* Valuation Card (legacy) */}
          <Link
            href="/valuation"
            className="group flex flex-col items-center gap-4 rounded-xl border border-gray-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-blue-300"
          >
            <Scale className="h-10 w-10 text-blue-600" />
            <h2 className="text-lg font-semibold text-gray-900">
              Procjena vrijednosti
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Procjena iz PDF-a ili teksta bez ZK analize — direktni unos.
            </p>
            <span className="flex items-center gap-1 text-sm font-medium text-blue-600 group-hover:underline">
              Otvori <ArrowRight className="h-4 w-4" />
            </span>
          </Link>

          {/* ZK Analysis Card (legacy) */}
          <Link
            href="/analyze"
            className="group flex flex-col items-center gap-4 rounded-xl border border-gray-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-blue-300"
          >
            <FileSearch className="h-10 w-10 text-emerald-600" />
            <h2 className="text-lg font-semibold text-gray-900">
              ZK Analiza rizika
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Samo pravna analiza ZK izvadka — bez procjene vrijednosti.
            </p>
            <span className="flex items-center gap-1 text-sm font-medium text-emerald-600 group-hover:underline">
              Otvori <ArrowRight className="h-4 w-4" />
            </span>
          </Link>
        </div>
      </div>
    </div>
  );
}
