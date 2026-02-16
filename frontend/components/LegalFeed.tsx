"use client";

import { useEffect, useState } from "react";
import {
  Scale,
  Gavel,
  TrendingUp,
  TrendingDown,
  Minus,
  AlertTriangle,
  Loader2,
  RefreshCw,
  BookOpen,
  Info,
} from "lucide-react";

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

interface LegalFeedItem {
  case_number: string;
  court: string;
  date: string;
  topic: string;
  headline: string;
  takeaway: string;
  impact: string;
  area_of_law: string;
}

function ImpactBadge({ impact }: { impact: string }) {
  const upper = impact?.toUpperCase() ?? "";
  if (upper === "VISOK")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-red-700 bg-red-50 px-2 py-0.5 rounded-full">
        <TrendingUp className="h-3 w-3" /> Visok
      </span>
    );
  if (upper === "NIZAK")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-full">
        <TrendingDown className="h-3 w-3" /> Nizak
      </span>
    );
  return (
    <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full">
      <Minus className="h-3 w-3" /> Srednji
    </span>
  );
}

export default function LegalFeed() {
  const [items, setItems] = useState<LegalFeedItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<number | null>(null);

  const fetchFeed = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${API}/api/legal/feed?count=5`);
      if (!res.ok) throw new Error("Greška u dohvatu");
      const data: LegalFeedItem[] = await res.json();
      setItems(data);
    } catch {
      setError("Nije moguće učitati pravne novosti");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchFeed();
  }, []);

  return (
    <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
      {/* Header */}
      <div className="px-4 py-3 bg-gradient-to-r from-amber-50 to-orange-50 border-b border-slate-200 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <div className="w-7 h-7 rounded-lg bg-amber-100 flex items-center justify-center">
            <Gavel className="h-4 w-4 text-amber-700" />
          </div>
          <div>
            <h3 className="text-xs font-bold text-slate-800 tracking-wide">
              PravoTek — Sudska Praksa
            </h3>
            <p className="text-[10px] text-slate-500">
              Najnovija sudska praksa za nekretnine
            </p>
          </div>
        </div>
        <button
          onClick={fetchFeed}
          disabled={loading}
          className="p-1.5 rounded-lg hover:bg-amber-100 transition disabled:opacity-50"
          title="Osvježi"
        >
          <RefreshCw
            className={`h-3.5 w-3.5 text-amber-600 ${loading ? "animate-spin" : ""}`}
          />
        </button>
      </div>

      {/* Content */}
      <div className="divide-y divide-slate-100">
        {loading && items.length === 0 && (
          <div className="p-8 text-center">
            <Loader2 className="h-5 w-5 animate-spin text-amber-500 mx-auto" />
            <p className="text-xs text-slate-400 mt-2">
              Dohvaćam sudsku praksu...
            </p>
          </div>
        )}

        {error && (
          <div className="p-4 text-center">
            <AlertTriangle className="h-5 w-5 text-amber-500 mx-auto" />
            <p className="text-xs text-slate-500 mt-1">{error}</p>
            <button
              onClick={fetchFeed}
              className="text-xs text-blue-600 hover:underline mt-2"
            >
              Pokušaj ponovno
            </button>
          </div>
        )}

        {!loading &&
          !error &&
          items.map((item, idx) => (
            <button
              key={idx}
              onClick={() => setExpanded(expanded === idx ? null : idx)}
              className="w-full text-left px-4 py-3 hover:bg-slate-50 transition"
            >
              <div className="flex items-start justify-between gap-2">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-[10px] font-mono text-slate-400">
                      {item.case_number}
                    </span>
                    <ImpactBadge impact={item.impact} />
                  </div>
                  <p className="text-xs font-semibold text-slate-800 leading-snug">
                    {item.headline}
                  </p>
                  <div className="flex items-center gap-2 mt-1">
                    <span className="text-[10px] text-slate-400">
                      {item.court}
                    </span>
                    <span className="text-[10px] text-slate-300">•</span>
                    <span className="text-[10px] text-slate-400">
                      {item.date}
                    </span>
                    <span className="text-[10px] text-slate-300">•</span>
                    <span className="text-[10px] text-blue-500 font-medium">
                      {item.topic}
                    </span>
                  </div>

                  {/* Expanded content */}
                  {expanded === idx && (
                    <div className="mt-3 p-3 bg-amber-50 rounded-lg border border-amber-100">
                      <div className="flex items-start gap-2">
                        <BookOpen className="h-3.5 w-3.5 text-amber-600 mt-0.5 shrink-0" />
                        <div>
                          <p className="text-xs font-medium text-amber-800 mb-1">
                            Ključni zaključak:
                          </p>
                          <p className="text-xs text-amber-700 leading-relaxed">
                            {item.takeaway}
                          </p>
                          <p className="text-[10px] text-amber-500 mt-2">
                            Područje: {item.area_of_law}
                          </p>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
                <Scale className="h-3.5 w-3.5 text-slate-300 shrink-0 mt-1" />
              </div>
            </button>
          ))}
      </div>

      {/* V34: AI Provenance Disclaimer */}
      <div className="px-4 py-3 bg-blue-50 border-t border-blue-100">
        <div className="flex items-start gap-2">
          <Info className="h-3.5 w-3.5 text-blue-500 mt-0.5 shrink-0" />
          <p className="text-[10px] text-blue-700 leading-relaxed">
            <strong>Napomena:</strong> Presude su ilustrativne i generirane AI modelom na temelju
            ustaljenih pravnih stajališta. Za autentičnu sudsku praksu koristite{" "}
            <a
              href="https://sudskapraksa.csp.vsrh.hr"
              target="_blank"
              rel="noopener noreferrer"
              className="underline font-medium"
            >
              sudsku bazu podataka VSRH
            </a>.
            {" "}AI analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11).
            Obrada podataka sukladno Uredbi (EU) 2016/679 (GDPR).
          </p>
        </div>
      </div>
    </div>
  );
}
