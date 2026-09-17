"use client";

import { useCallback, useEffect, useState } from "react";
import { Flag, Loader2, RefreshCw } from "lucide-react";
import { API_BASE } from "@/lib/api";

/**
 * Prijave „Iznos mi ne izgleda točno" (plan pravne zaštite, dio D).
 *
 * Popis ne ovisi o tome je li mail otišao: baza je izvor istine, a stupac
 * „Mail" pokazuje kojima obavijest nije stigla — to su prijave koje bi se inače
 * izgubile.
 */
interface Prijava {
  id: string;
  created_at: string;
  user_id: string | null;
  troskovnik_id: string | null;
  opis: string;
  ocekivani_iznos: string | null;
  ulazni_podaci: unknown;
  rezultat: unknown;
  verzija_izracuna: string | null;
  status: "nova" | "potvrdena" | "nije_greska" | "ispravljena";
  email_poslan: boolean;
}

const STATUSI: { value: Prijava["status"]; label: string }[] = [
  { value: "nova", label: "Nova" },
  { value: "potvrdena", label: "Potvrđena greška" },
  { value: "nije_greska", label: "Nije greška" },
  { value: "ispravljena", label: "Ispravljena" },
];

interface Props {
  authHeaders: Record<string, string>;
  /** user_id → e-mail, iz popisa korisnika koji admin već ima. */
  emailPoKorisniku: Record<string, string>;
}

export default function PrijaveGresakaPanel({ authHeaders, emailPoKorisniku }: Props) {
  const [prijave, setPrijave] = useState<Prijava[] | null>(null);
  const [greska, setGreska] = useState<string | null>(null);
  const [otvorena, setOtvorena] = useState<string | null>(null);

  const ucitaj = useCallback(async () => {
    if (!authHeaders.Authorization) return;
    setGreska(null);
    try {
      const res = await fetch(`${API_BASE}/api/admin/prijave-gresaka`, { headers: authHeaders });
      if (!res.ok) throw new Error();
      setPrijave(await res.json());
    } catch {
      setGreska("Prijave nisu dohvaćene.");
    }
  }, [authHeaders]);

  useEffect(() => {
    ucitaj();
  }, [ucitaj]);

  const promijeni = useCallback(
    async (id: string, status: Prijava["status"]) => {
      const res = await fetch(`${API_BASE}/api/admin/prijave-gresaka/${id}`, {
        method: "PATCH",
        headers: { ...authHeaders, "Content-Type": "application/json" },
        body: JSON.stringify({ status }),
      });
      if (res.ok) setPrijave((p) => p?.map((x) => (x.id === id ? { ...x, status } : x)) ?? null);
      else setGreska("Status nije promijenjen.");
    },
    [authHeaders],
  );

  const novih = prijave?.filter((p) => p.status === "nova").length ?? 0;

  return (
    <section className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-slate-200 flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Flag className="h-4 w-4 text-slate-600" />
          <h2 className="text-sm font-bold text-slate-900">Prijave grešaka u izračunu</h2>
          {novih > 0 && (
            <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-semibold text-amber-800">
              {novih} nov{novih === 1 ? "a" : "ih"}
            </span>
          )}
        </div>
        <button onClick={ucitaj} className="text-slate-400 hover:text-slate-700" aria-label="Osvježi">
          <RefreshCw className="h-4 w-4" />
        </button>
      </div>

      {greska && <p className="px-5 py-3 text-xs text-red-700">{greska}</p>}
      {prijave === null && !greska && (
        <div className="px-5 py-6 text-center">
          <Loader2 className="mx-auto h-5 w-5 animate-spin text-slate-400" />
        </div>
      )}
      {prijave?.length === 0 && <p className="px-5 py-6 text-sm text-slate-500">Nema prijava.</p>}

      <ul className="divide-y divide-slate-100">
        {prijave?.map((p) => (
          <li key={p.id} className="px-5 py-3 text-sm">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <p className="text-xs text-slate-500">
                  {new Date(p.created_at).toLocaleString("hr-HR")} ·{" "}
                  {(p.user_id && emailPoKorisniku[p.user_id]) || "korisnik obrisan"}
                  {p.troskovnik_id && (
                    <>
                      {" · "}
                      <a href={`/troskovnik/${p.troskovnik_id}`} className="underline">troškovnik</a>
                    </>
                  )}
                  {!p.email_poslan && <span className="ml-2 text-amber-700">mail nije poslan</span>}
                </p>
                <p className="mt-1 whitespace-pre-line text-slate-800">{p.opis}</p>
                {p.ocekivani_iznos && (
                  <p className="mt-1 text-xs text-slate-600">Očekivano: {p.ocekivani_iznos}</p>
                )}
                <button
                  onClick={() => setOtvorena(otvorena === p.id ? null : p.id)}
                  className="mt-1 text-xs text-slate-500 underline"
                >
                  {otvorena === p.id ? "Sakrij podatke" : "Ulaz i rezultat"}
                </button>
                {otvorena === p.id && (
                  <pre className="mt-2 max-h-80 overflow-auto rounded bg-slate-50 p-2 text-[11px] text-slate-700">
                    {JSON.stringify({ verzija_izracuna: p.verzija_izracuna, ulaz: p.ulazni_podaci, rezultat: p.rezultat }, null, 2)}
                  </pre>
                )}
              </div>
              <select
                value={p.status}
                onChange={(e) => promijeni(p.id, e.target.value as Prijava["status"])}
                className="rounded-lg border border-slate-300 px-2 py-1 text-xs"
                aria-label="Status prijave"
              >
                {STATUSI.map((s) => (
                  <option key={s.value} value={s.value}>{s.label}</option>
                ))}
              </select>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
