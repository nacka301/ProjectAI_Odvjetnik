"use client";

import { Eye } from "lucide-react";
import { useDemo } from "@/contexts/DemoContext";

/**
 * Traka koja stoji na vrhu ekrana cijelo vrijeme dok traje prikaz primjera.
 *
 * Namjerno jantarna i namjerno velika. Siva napomena sitnim slovima ovdje bi
 * bila gora od ničega: cijela poanta trake je da odvjetnik ni u jednom trenutku
 * ne pomisli da gleda svoje podatke. Zato i „Izađi" stoji odmah uz nju — izlaz
 * ne smije biti nešto što se traži.
 *
 * `z-[190]` je ispod zatamnjenja vodiča (`z-[200]`), pa je traka za vrijeme
 * koraka pod istim zatamnjenjem kao i ostatak stranice — inače bi svijetlila
 * jače od polja koje korak objašnjava.
 */
export default function DemoTraka() {
  const { demo, izadjiIzDemo } = useDemo();
  if (!demo) return null;

  return (
    <div
      role="status"
      className="sticky top-0 z-[190] flex items-center justify-center gap-3 bg-amber-400 px-4 py-2 text-center text-slate-900 print:hidden"
    >
      <Eye className="h-4 w-4 shrink-0" aria-hidden />
      <p className="text-xs font-bold uppercase tracking-wide sm:text-sm">
        Prikaz primjera — podaci nisu vaši i ne spremaju se
      </p>
      <button
        onClick={izadjiIzDemo}
        className="shrink-0 rounded-md border border-slate-900/30 px-2 py-0.5 text-xs font-semibold transition hover:bg-amber-300"
      >
        Izađi
      </button>
    </div>
  );
}
