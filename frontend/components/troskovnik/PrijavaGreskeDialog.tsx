"use client";

import { useCallback, useEffect, useState } from "react";
import { AlertCircle, CheckCircle, Loader2, X } from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

/**
 * „Iznos mi ne izgleda točno" — kratka prijava greške u izračunu (plan pravne
 * zaštite, dio D).
 *
 * Svrha je da prijava stigne, a ne da korisnik zatvori alat i ode. Zato samo
 * dva polja, od kojih je jedno neobavezno, a ulaz i rezultat izračuna šalju se
 * sami — bez njih se greška ne može ponoviti.
 *
 * Podaci stranaka i odvjetnika u prijavu NE ulaze: pozivatelj šalje ulaz bez
 * njih, a backend ih iz svega što stigne još jednom uklanja
 * (PrijavaGreskeCiscenje).
 */
interface Props {
  open: boolean;
  onClose: () => void;
  /** Spremljeni troškovnik na koji se prijava odnosi; izostaje na obrascu. */
  troskovnikId?: string;
  /** Pozvano tek pri slanju, da se ne računa dok je dijalog zatvoren. */
  ulazniPodaci?: () => unknown;
  rezultat?: () => unknown;
}

export default function PrijavaGreskeDialog({ open, onClose, troskovnikId, ulazniPodaci, rezultat }: Props) {
  const { authHeaders } = useAuth();
  const [opis, setOpis] = useState("");
  const [ocekivani, setOcekivani] = useState("");
  const [salje, setSalje] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);
  const [poslano, setPoslano] = useState(false);

  // Svako otvaranje počinje ispočetka.
  useEffect(() => {
    if (open) {
      setOpis("");
      setOcekivani("");
      setGreska(null);
      setPoslano(false);
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const naEsc = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !salje) onClose();
    };
    window.addEventListener("keydown", naEsc);
    return () => window.removeEventListener("keydown", naEsc);
  }, [open, salje, onClose]);

  const posalji = useCallback(async () => {
    if (opis.trim().length < 10) {
      setGreska("Opišite ukratko što ne izgleda točno (barem 10 znakova).");
      return;
    }
    setSalje(true);
    setGreska(null);
    try {
      const res = await fetch(`${API_BASE}/api/prijave-gresaka`, {
        method: "POST",
        headers: { ...authHeaders, "Content-Type": "application/json" },
        body: JSON.stringify({
          opis: opis.trim(),
          ocekivani_iznos: ocekivani.trim() || undefined,
          troskovnik_id: troskovnikId,
          ulazni_podaci: ulazniPodaci?.(),
          rezultat: rezultat?.(),
        }),
      });
      if (!res.ok) {
        const tijelo = await res.json().catch(() => ({}));
        setGreska(
          res.status === 429
            ? "Poslali ste više prijava u kratkom vremenu. Pokušajte za sat vremena ili pišite na vackadavid@gmail.com."
            : tijelo.error || "Prijava nije poslana. Pišite na vackadavid@gmail.com.",
        );
        return;
      }
      setPoslano(true);
    } catch {
      setGreska("Prijava nije poslana — provjerite vezu ili pišite na vackadavid@gmail.com.");
    } finally {
      setSalje(false);
    }
  }, [opis, ocekivani, troskovnikId, ulazniPodaci, rezultat, authHeaders]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 print:hidden"
      onClick={() => !salje && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="prijava-greske-naslov"
        className="w-full max-w-md rounded-xl bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <h2 id="prijava-greske-naslov" className="text-sm font-bold text-slate-900">
            Iznos mi ne izgleda točno
          </h2>
          <button onClick={onClose} disabled={salje} className="text-slate-400 hover:text-slate-700" aria-label="Zatvori">
            <X className="h-4 w-4" />
          </button>
        </div>

        {poslano ? (
          <div className="space-y-4 px-5 py-6 text-center">
            <CheckCircle className="mx-auto h-8 w-8 text-emerald-500" />
            <p className="text-sm text-slate-700">
              Hvala. Prijava je stigla. Pregledat ću izračun i javiti vam se e-poštom.
            </p>
            <button
              onClick={onClose}
              className="rounded-lg px-4 py-2 text-sm font-medium text-white hover:opacity-90"
              style={{ background: "#1B2A4A" }}
            >
              Zatvori
            </button>
          </div>
        ) : (
          <div className="space-y-4 px-5 py-4">
            <label className="block">
              <span className="text-xs font-semibold text-slate-700">Što ne izgleda točno?</span>
              <textarea
                value={opis}
                onChange={(e) => setOpis(e.target.value)}
                rows={4}
                maxLength={2000}
                autoFocus
                placeholder="Npr. pristojba za tužbu trebala bi biti 310,00 €, a alat je izračunao 273,23 €."
                className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none"
              />
            </label>
            <label className="block">
              <span className="text-xs font-semibold text-slate-700">
                Koliko bi po vama trebalo biti? <span className="font-normal text-slate-400">(neobavezno)</span>
              </span>
              <input
                value={ocekivani}
                onChange={(e) => setOcekivani(e.target.value)}
                maxLength={200}
                className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none"
              />
            </label>
            <p className="text-[11px] leading-snug text-slate-500">
              Uz prijavu se šalju unesene stavke i izračun, bez imena i podataka stranaka.
            </p>
            {greska && (
              <p role="alert" className="flex items-start gap-2 text-xs text-red-700">
                <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                {greska}
              </p>
            )}
            <div className="flex justify-end gap-2">
              <button onClick={onClose} disabled={salje} className="rounded-lg px-3 py-2 text-sm text-slate-600 hover:bg-slate-100">
                Odustani
              </button>
              <button
                onClick={posalji}
                disabled={salje}
                className="inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-60"
                style={{ background: "#1B2A4A" }}
              >
                {salje && <Loader2 className="h-4 w-4 animate-spin" />}
                Pošalji
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
