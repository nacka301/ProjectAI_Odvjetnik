"use client";

/**
 * Spremljeni troškovnik, onakav kakav je spremljen.
 *
 * Namjerno je samo za čitanje. Zapis u `troskovnici` nosi obračun koji je već
 * mogao biti predan sudu, pa ga ova stranica prikazuje i ispisuje, ali ne
 * preračunava: ponovni izračun po današnjem katalogu mogao bi dati drukčiji
 * iznos od onoga na papiru, a razlika bi se primijetila tek na ročištu.
 *
 * Novi troškovnik se radi na `/troskovnik`, i to je jedina putanja koja računa.
 *
 * "Uredi kao novi" ne krši to pravilo: vodi na `/troskovnik?klon=<id>`, koji
 * praznu formu popuni podacima ovog zapisa kao polazištem, ali spremanje ondje
 * uvijek stvara nov redak u bazi. Ovaj zapis ostaje netaknut i dalje točno
 * odražava ono što je (možda) već predano sudu.
 */

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  AlertTriangle,
  ArrowLeft,
  Calculator,
  Copy,
  Loader2,
  Printer,
  User,
} from "lucide-react";
import AppHeader from "@/components/AppHeader";
import TroskovnikIspis from "@/components/troskovnik/TroskovnikIspis";
import { getTroskovnik, type TroskovnikRow } from "@/lib/supabase";
import { izvozModelIzZapisa } from "@/lib/troskovnik-zapis";
import type { TroskovnikIzvozModel } from "@/lib/troskovnik-izvoz-model";
import { formatBodovi, formatDatumIspis, formatEur } from "@/lib/format";
import { punaBiljeska } from "@/lib/pravna-osnova";

function Redak({
  oznaka,
  iznos,
  jak,
}: {
  oznaka: string;
  iznos: number;
  jak?: boolean;
}) {
  return (
    <div
      className={`flex items-baseline justify-between gap-4 ${
        jak ? "text-base font-bold text-slate-900" : "text-sm text-slate-600"
      }`}
    >
      <span>{oznaka}</span>
      <span className="tabular-nums">{formatEur(iznos)}</span>
    </div>
  );
}

function Podatak({ oznaka, vrijednost }: { oznaka: string; vrijednost?: string }) {
  if (!vrijednost) return null;
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-wider text-slate-400">{oznaka}</dt>
      <dd className="text-sm text-slate-800">{vrijednost}</dd>
    </div>
  );
}

export default function SpremljeniTroskovnikPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;

  const [zapis, setZapis] = useState<TroskovnikRow | null>(null);
  const [model, setModel] = useState<TroskovnikIzvozModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [greska, setGreska] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    (async () => {
      try {
        const red = await getTroskovnik(id);
        if (!red) {
          setGreska("Troškovnik nije pronađen. Možda je obrisan ili pripada drugom uredu.");
          return;
        }
        const m = izvozModelIzZapisa(red);
        if (!m) {
          setGreska("Ovaj je zapis spremljen bez razrade stavaka, pa se ne može prikazati.");
          return;
        }
        setZapis(red);
        setModel(m);
      } catch {
        setGreska("Greška pri dohvaćanju troškovnika.");
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const handlePrint = useCallback(() => window.print(), []);

  return (
    <div className="min-h-screen bg-gray-50 font-sans">
      <div className="print:hidden">
        <AppHeader />
      </div>

      <div className="py-10 px-4 print:hidden">
        <div className="mx-auto max-w-3xl">
          <Link
            href="/history"
            className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-800"
          >
            <ArrowLeft className="h-4 w-4" /> Povijest predmeta
          </Link>

          {loading && (
            <div className="mt-8 rounded-xl border border-slate-200 bg-white p-12 text-center">
              <Loader2 className="mx-auto h-6 w-6 animate-spin text-slate-400" />
            </div>
          )}

          {greska && (
            <div className="mt-8 rounded-xl border border-red-300 bg-red-50 p-6 text-center">
              <AlertTriangle className="mx-auto mb-2 h-6 w-6 text-red-500" />
              <p className="text-sm text-red-700">{greska}</p>
            </div>
          )}

          {model && zapis && (
            <>
              <div className="mt-4 flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h1 className="flex items-center gap-2 text-2xl font-bold text-slate-900">
                    <Calculator className="h-6 w-6 text-indigo-600" />
                    {model.zastupana?.ime ?? "Troškovnik"}
                  </h1>
                  <p className="mt-1 text-sm text-slate-500">
                    Spremljeno {formatDatumIspis(new Date(zapis.created_at))}
                  </p>
                </div>
                <div className="flex gap-2">
                  {zapis.client_id && (
                    <Link
                      href={`/klijenti/${zapis.client_id}`}
                      className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50"
                    >
                      <User className="h-3.5 w-3.5" /> Dosje stranke
                    </Link>
                  )}
                  <Link
                    href={`/troskovnik?klon=${zapis.id}`}
                    title="Otvara prazan novi troškovnik, popunjen ovim podacima. Ovaj zapis se ne mijenja."
                    className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50"
                  >
                    <Copy className="h-3.5 w-3.5" /> Uredi kao novi
                  </Link>
                  <button
                    onClick={handlePrint}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-2 text-xs font-medium text-white hover:bg-indigo-700"
                  >
                    <Printer className="h-3.5 w-3.5" /> Ispis
                  </button>
                </div>
              </div>

              {/* Zaglavlje */}
              <dl className="mt-6 grid grid-cols-2 gap-4 rounded-xl border border-slate-200 bg-white p-5 sm:grid-cols-3">
                <Podatak oznaka="Sud" vrijednost={model.sud} />
                <Podatak oznaka="Poslovni broj" vrijednost={model.brojPredmeta} />
                <Podatak oznaka="Odvjetnik" vrijednost={model.odvjetnik?.ime} />
                <Podatak
                  oznaka={model.zastupana?.polozaj || "Zastupana stranka"}
                  vrijednost={model.zastupana?.ime}
                />
                <Podatak
                  oznaka={model.protustranka?.polozaj || "Protustranka"}
                  vrijednost={model.protustranka?.ime}
                />
              </dl>

              {/* Radnje */}
              <div className="mt-4 overflow-hidden rounded-xl border border-slate-200 bg-white">
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 bg-slate-50 text-left text-[11px] uppercase tracking-wider text-slate-400">
                        <th className="px-4 py-2.5 font-medium">Radnja</th>
                        <th className="px-4 py-2.5 font-medium">Tar. br.</th>
                        <th className="px-4 py-2.5 text-right font-medium">Kol.</th>
                        <th className="px-4 py-2.5 text-right font-medium">Bodovi</th>
                        <th className="px-4 py-2.5 text-right font-medium">Iznos</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {model.radnje.map((r, i) => (
                        <tr key={i}>
                          <td className="px-4 py-2.5 text-slate-800">{r.opis}</td>
                          <td className="px-4 py-2.5 text-slate-500">{r.tarifniBroj}</td>
                          <td className="px-4 py-2.5 text-right tabular-nums text-slate-600">
                            {r.kolicina}
                          </td>
                          <td className="px-4 py-2.5 text-right tabular-nums text-slate-600">
                            {formatBodovi(r.ukupnoBodova)}
                          </td>
                          <td className="px-4 py-2.5 text-right tabular-nums text-slate-800">
                            {formatEur(r.iznos)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Zbrojevi */}
              <div className="mt-4 space-y-2 rounded-xl border border-slate-200 bg-white p-5">
                <Redak oznaka="Nagrada" iznos={model.zbrojNagrade.iznos} />
                {model.naknadaTroskova > 0 && (
                  <Redak oznaka="Naknada troškova (Tbr. 48.–51.)" iznos={model.naknadaTroskova} />
                )}
                {model.pdv && (
                  <>
                    <Redak oznaka="Osnovica (bez PDV-a)" iznos={model.pdv.osnovica} />
                    <Redak oznaka="PDV" iznos={model.pdv.iznos} />
                  </>
                )}
                {model.ukupnoPristojbe > 0 && (
                  <Redak oznaka="Sudske pristojbe" iznos={model.ukupnoPristojbe} />
                )}
                {model.ukupnoPredujmovi > 0 && (
                  <Redak oznaka="Predujmovi" iznos={model.ukupnoPredujmovi} />
                )}
                <div className="border-t border-slate-200 pt-2">
                  <Redak oznaka="Sveukupni trošak" iznos={model.sveukupno} jak />
                </div>
              </div>

              {model.napomena && (
                <p className="mt-4 rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-600">
                  {model.napomena}
                </p>
              )}

              {/* Po čemu je izračunat. Samo u aplikaciji — na ispis ide neutralna
                  inačica bez imena alata (TroskovnikIspis). */}
              <p className="mt-4 px-1 text-xs leading-relaxed text-slate-500">
                {punaBiljeska(model.pravnaOsnova)}
              </p>
            </>
          )}
        </div>
      </div>

      {/* Isti dokument koji ide sudu i s ekrana za izradu — vidljiv samo u ispisu. */}
      {model && <TroskovnikIspis model={model} />}
    </div>
  );
}
