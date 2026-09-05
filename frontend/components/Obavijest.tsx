"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { AlertTriangle, Info, X } from "lucide-react";

/**
 * Zamjena za preglednikov `window.alert()`.
 *
 * `alert()` prikazuje generički okvir preglednika ("Na web-lokaciji ...
 * navodi se sljedeće") — bez boja i fonta aplikacije, izgleda kao greška
 * preglednika, ne kao dio proizvoda. Ovo je ista svrha (jedna poruka, jedan
 * gumb za potvrdu), ali kao obična komponenta u vizualnom jeziku ostatka
 * aplikacije (isti `#1B2A4A`, `rounded-xl`, `shadow-2xl` kao
 * `MethodologyModal`).
 *
 * Poziva se lokalno, po stranici: stranica drži `useState` s porukom (ili
 * `null`) i renderira `<Obavijest .../>` samo dok poruka postoji — isti
 * obrazac kao `MethodologyModal`, bez potrebe za globalnim provideром.
 */

interface ObavijestProps {
  naslov: string;
  /** Prelomi u tekstu (\n) se poštuju — nije potrebno ručno graditi paragrafe. */
  poruka: string;
  onClose: () => void;
  /** `info` (zadano) = navy, za obavijesti; `greska` = crveno, za stvarne greške. */
  vrsta?: "info" | "greska";
}

export function Obavijest({ naslov, poruka, onClose, vrsta = "info" }: ObavijestProps) {
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  if (!mounted) return null;

  const jeGreska = vrsta === "greska";

  const modal = (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/60 px-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-sm rounded-xl border border-slate-200 bg-white shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start gap-3 px-5 pb-4 pt-5">
          <div
            className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${
              jeGreska ? "bg-red-50" : "bg-[#1B2A4A]/10"
            }`}
          >
            {jeGreska ? (
              <AlertTriangle className="h-4.5 w-4.5 text-red-600" />
            ) : (
              <Info className="h-4.5 w-4.5 text-[#1B2A4A]" />
            )}
          </div>
          <div className="min-w-0 flex-1 pt-0.5">
            <h2 className="text-sm font-bold text-slate-900">{naslov}</h2>
            <p className="mt-1.5 whitespace-pre-line text-sm leading-relaxed text-slate-600">
              {poruka}
            </p>
          </div>
          <button
            onClick={onClose}
            aria-label="Zatvori"
            className="-mt-1 text-slate-300 transition hover:text-slate-500"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="flex justify-end border-t border-slate-100 px-5 py-3">
          <button
            onClick={onClose}
            className="rounded-lg bg-[#1B2A4A] px-4 py-2 text-sm font-medium text-white transition hover:bg-[#1B2A4A]/90"
          >
            U redu
          </button>
        </div>
      </div>
    </div>
  );

  // Portal na document.body — isti izlaz kao MethodologyModal, da obavijest
  // uvijek slika iznad ostatka stranice bez obzira na stacking contexte.
  return createPortal(modal, document.body);
}
