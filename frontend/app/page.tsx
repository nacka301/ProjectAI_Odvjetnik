"use client";

import Link from "next/link";
import { ArrowRight, BookOpen, ClipboardList, Clock } from "lucide-react";

export default function Home() {
  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4 font-sans">
      <div className="max-w-2xl w-full text-center">
        <h1 className="text-4xl font-bold text-gray-900 mb-3">
          ⚖️ Legal AI Assistant
        </h1>
        <p className="text-gray-500 mb-12 text-lg">
          AI pomoćnik za odvjetnike — odaberite alat
        </p>

        <div className="grid grid-cols-1 gap-6">
          {/* Wizard – structured input */}
          <Link
            href="/wizard"
            className="group flex flex-col items-center gap-4 rounded-xl border-2 border-blue-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-blue-400"
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

          {/* Unified ZK → Valuation Card */}
          <Link
            href="/zk-procjena"
            className="group flex flex-col items-center gap-4 rounded-xl border-2 border-indigo-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-indigo-400"
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

          {/* History — saved evaluations */}
          <Link
            href="/history"
            className="group flex flex-col items-center gap-4 rounded-xl border-2 border-gray-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-gray-400"
          >
            <Clock className="h-12 w-12 text-gray-500" />
            <h2 className="text-xl font-semibold text-gray-900">
              Povijest procjena
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed max-w-lg">
              Pregledajte sve dosadašnje procjene, preuzmite PDF izvještaje
              i usporedite rezultate za različite nekretnine.
            </p>
            <span className="flex items-center gap-1 text-sm font-medium text-gray-600 group-hover:underline">
              Pregledaj <ArrowRight className="h-4 w-4" />
            </span>
          </Link>
        </div>
      </div>
    </div>
  );
}
