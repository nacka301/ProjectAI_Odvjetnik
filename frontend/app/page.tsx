"use client";

import Link from "next/link";
import { Scale, FileSearch, ArrowRight } from "lucide-react";

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

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Valuation Card */}
          <Link
            href="/valuation"
            className="group flex flex-col items-center gap-4 rounded-xl border border-gray-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-blue-300"
          >
            <Scale className="h-10 w-10 text-blue-600" />
            <h2 className="text-lg font-semibold text-gray-900">
              Procjena vrijednosti
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Generirajte mišljenje o tržišnoj vrijednosti nekretnine za sudski
              postupak (razvod, dioba).
            </p>
            <span className="flex items-center gap-1 text-sm font-medium text-blue-600 group-hover:underline">
              Otvori <ArrowRight className="h-4 w-4" />
            </span>
          </Link>

          {/* ZK Analysis Card */}
          <Link
            href="/analyze"
            className="group flex flex-col items-center gap-4 rounded-xl border border-gray-200 bg-white p-8 shadow-sm transition hover:shadow-md hover:border-blue-300"
          >
            <FileSearch className="h-10 w-10 text-emerald-600" />
            <h2 className="text-lg font-semibold text-gray-900">
              ZK Izvadak — Analiza rizika
            </h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Uploadajte zemljišnoknjižni izvadak i dobijte analizu hipoteka,
              sporova i pravnog rizika.
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
