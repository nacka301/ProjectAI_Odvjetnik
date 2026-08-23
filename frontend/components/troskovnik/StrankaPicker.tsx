"use client";

/**
 * Odabir stranke iz već spremljenih, umjesto pretipkavanja.
 *
 * Troškovnik je takav birač imao samo za zastupanu stranku; protustranka se
 * upisivala rukom, iako je često već u bazi — protivnik iz jednog predmeta
 * redovito je stranka u drugom.
 *
 * Namjerno je odvojen od onog za zastupanu stranku i ne pokušava ga zamijeniti.
 * Ondje odabir znači i vezu: troškovnik se sprema uz tu stranku, polja se
 * odvezuju čim ih korisnik dirne, i o tome se javlja porukom. Ovdje odabir ne
 * znači ništa osim prepisivanja četiriju polja. Spajanje toga dvoga u jednu
 * komponentu značilo bi zastavice koje bi jednoj strani uvijek bile suvišne.
 */

import { useEffect, useMemo, useRef, useState } from "react";
import { Building2, ChevronDown, Loader2, Search, User } from "lucide-react";

/** Onoliko stranke koliko je ovom biraču potrebno — širi oblik uredno prolazi. */
export interface StrankaOpcija {
  id: string;
  ime_naziv: string;
  oib?: string;
  adresa?: string;
  grad?: string;
  tip: string;
}

interface Props {
  stranke: StrankaOpcija[];
  loading: boolean;
  onSelect: (stranka: StrankaOpcija) => void;
  /**
   * Stranka koja je već odabrana kao zastupana — izostavlja se s popisa.
   * Nitko nije sam sebi protustranka, a ponuditi to znači ponuditi pogrešku.
   */
  izostaviId?: string;
  placeholder?: string;
}

export default function StrankaPicker({
  stranke,
  loading,
  onSelect,
  izostaviId,
  placeholder = "Pretraži spremljene stranke…",
}: Props) {
  const [upit, setUpit] = useState("");
  const [otvoren, setOtvoren] = useState(false);
  const okvir = useRef<HTMLDivElement>(null);

  // Klik izvan zatvara popis — inače ostaje visjeti nad ostatkom obrasca.
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (okvir.current && !okvir.current.contains(e.target as Node)) setOtvoren(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const ponuda = useMemo(() => {
    const dostupne = izostaviId ? stranke.filter((s) => s.id !== izostaviId) : stranke;
    const q = upit.trim().toLowerCase();
    if (!q) return dostupne;
    return dostupne.filter(
      (s) =>
        s.ime_naziv.toLowerCase().includes(q) ||
        (s.oib ?? "").toLowerCase().includes(q) ||
        (s.adresa ?? "").toLowerCase().includes(q) ||
        (s.grad ?? "").toLowerCase().includes(q),
    );
  }, [stranke, izostaviId, upit]);

  return (
    <div ref={okvir} className="relative">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 pointer-events-none" />
        <input
          type="text"
          value={upit}
          onChange={(e) => {
            setUpit(e.target.value);
            if (!otvoren) setOtvoren(true);
          }}
          onFocus={() => setOtvoren(true)}
          placeholder={placeholder}
          className="w-full pl-10 pr-10 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
        />
        <button
          type="button"
          onClick={() => setOtvoren(!otvoren)}
          aria-label={otvoren ? "Zatvori popis stranaka" : "Otvori popis stranaka"}
          className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-600"
        >
          <ChevronDown className={`h-4 w-4 transition ${otvoren ? "rotate-180" : ""}`} />
        </button>
      </div>

      {otvoren && (
        <div className="absolute z-50 mt-1 w-full max-h-64 overflow-y-auto bg-white border border-slate-200 rounded-xl shadow-lg">
          {loading ? (
            <div className="flex items-center justify-center py-6 text-slate-400">
              <Loader2 className="h-4 w-4 animate-spin mr-2" />
              <span className="text-xs">Učitavanje stranaka…</span>
            </div>
          ) : ponuda.length === 0 ? (
            <div className="py-6 text-center">
              <p className="text-xs text-slate-400">
                {upit.trim() ? "Nema rezultata pretrage" : "Nema spremljenih stranaka"}
              </p>
              <p className="text-[10px] text-slate-400 mt-1">Podatke možete upisati i ručno.</p>
            </div>
          ) : (
            ponuda.map((s) => (
              <button
                key={s.id}
                type="button"
                onClick={() => {
                  onSelect(s);
                  setUpit("");
                  setOtvoren(false);
                }}
                className="w-full text-left px-4 py-2.5 hover:bg-indigo-50 transition flex items-center gap-3 border-b border-slate-100 last:border-0"
              >
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100">
                  {s.tip === "pravna" ? (
                    <Building2 className="h-3.5 w-3.5 text-slate-500" />
                  ) : (
                    <User className="h-3.5 w-3.5 text-slate-500" />
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-slate-800 truncate">{s.ime_naziv}</p>
                  <div className="flex items-center gap-2 text-[10px] text-slate-400">
                    {s.oib && <span>OIB: {s.oib}</span>}
                    {s.grad && <span>· {s.grad}</span>}
                    {s.tip === "pravna" && <span className="text-indigo-500">Pravna</span>}
                  </div>
                </div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
