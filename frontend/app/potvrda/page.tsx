"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { AlertCircle, Loader2, Scale } from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { safeNextPath } from "@/lib/auth-redirect";
import { signOutCompletely } from "@/lib/sign-out";
import {
  PRAVNI_TEKSTOVI_IZMJENA,
  PRAVNI_TEKSTOVI_VERZIJA,
  PRAVNI_TEKSTOVI_VERZIJA_TEKST,
} from "@/lib/pravno";

/**
 * Potvrda Izjave o odgovornosti i Uvjeta korištenja (plan pravne zaštite, dio C).
 *
 * Middleware ovamo šalje prijavljenog korisnika koji tekuću verziju još nije
 * potvrdio. Jedan ekran, četiri rečenice i poveznice na cijeli tekst — ne zid od
 * teksta. Potvrda ide kroz RPC `potvrdi_pravne_tekstove`, koji sam uzima
 * identitet i vrijeme, pa se ne može podmetnuti tuđa ni antedatirana.
 *
 * Kad korisnik stigne ovamo, a potvrda već postoji, stranica ga vraća dalje —
 * osim ako middleware javi `razlog=nepoznato` (nije mogao pitati bazu). Tada bi
 * automatski povratak bio petlja, pa se nudi gumb „Pokušaj ponovno".
 */
export default function PotvrdaPage() {
  return (
    <Suspense fallback={null}>
      <PotvrdaSadrzaj />
    </Suspense>
  );
}

type Faza = "ucitava" | "potvrda" | "greska-ucitavanja" | "vec-potvrdeno";

function PotvrdaSadrzaj() {
  const searchParams = useSearchParams();
  const odrediste = safeNextPath(searchParams.get("next"));
  // Middleware nije mogao pitati bazu. Ako je potvrda ipak tu, ne vraćamo
  // korisnika automatski — to bi bila petlja — nego nudimo da pokuša sam.
  const middlewareNijeProvjerio = searchParams.get("razlog") === "nepoznato";

  const [faza, setFaza] = useState<Faza>("ucitava");
  const [izmijenjena, setIzmijenjena] = useState(false);
  const [sprema, setSprema] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);

  /** Puna navigacija, da middleware ponovno provjeri i izda kolačić potvrde. */
  const udi = useCallback(() => {
    window.location.href = odrediste;
  }, [odrediste]);

  const ucitaj = useCallback(async () => {
    setFaza("ucitava");
    try {
      const supabase = createSupabaseBrowser();
      // RLS vraća samo vlastite retke; `verzija` je dovoljna za oba pitanja.
      const { data, error } = await supabase
        .from("potvrde_pravnih_tekstova")
        .select("verzija")
        .order("potvrdeno_at", { ascending: false })
        .limit(20);
      if (error) throw error;

      const verzije = (data ?? []).map((r: { verzija: string }) => r.verzija);
      if (verzije.includes(PRAVNI_TEKSTOVI_VERZIJA)) {
        if (middlewareNijeProvjerio) setFaza("vec-potvrdeno");
        else udi();
        return;
      }
      setIzmijenjena(verzije.length > 0);
      setFaza("potvrda");
    } catch {
      setFaza("greska-ucitavanja");
    }
  }, [udi, middlewareNijeProvjerio]);

  useEffect(() => {
    ucitaj();
  }, [ucitaj]);

  const potvrdi = useCallback(async () => {
    setSprema(true);
    setGreska(null);
    try {
      const supabase = createSupabaseBrowser();
      const { error } = await supabase.rpc("potvrdi_pravne_tekstove", {
        p_verzija: PRAVNI_TEKSTOVI_VERZIJA,
        p_user_agent: navigator.userAgent.slice(0, 300),
      });
      if (error) throw error;
      udi();
    } catch {
      setGreska("Potvrda nije spremljena. Provjerite vezu i pokušajte ponovno.");
      setSprema(false);
    }
  }, [udi]);

  const odjava = useCallback(async () => {
    await signOutCompletely();
    window.location.href = "/auth/login";
  }, []);

  return (
    <div className="min-h-[80vh] bg-slate-50 px-4 py-12 sm:py-20">
      <div className="mx-auto max-w-xl">
        {faza === "ucitava" && (
          <div className="flex justify-center py-24">
            <Loader2 className="h-6 w-6 animate-spin text-slate-400" aria-label="Učitavanje" />
          </div>
        )}

        {faza === "greska-ucitavanja" && (
          <div className="rounded-xl border border-slate-200 bg-white p-6 text-center sm:p-8">
            <AlertCircle className="mx-auto h-6 w-6 text-red-500" />
            <p className="mt-3 text-sm text-slate-700">
              Ne mogu provjeriti jeste li već potvrdili uvjete. Provjerite vezu.
            </p>
            <button
              onClick={ucitaj}
              className="mt-5 rounded-lg px-4 py-2.5 text-sm font-medium text-white transition hover:opacity-90"
              style={{ background: "#1B2A4A" }}
            >
              Pokušaj ponovno
            </button>
          </div>
        )}

        {faza === "vec-potvrdeno" && (
          <div className="rounded-xl border border-slate-200 bg-white p-6 text-center sm:p-8">
            <p className="text-sm text-slate-700">
              Uvjete ste već potvrdili, ali ih trenutno ne mogu provjeriti. Pokušajte ponovno za
              trenutak.
            </p>
            <button
              onClick={udi}
              className="mt-5 rounded-lg px-4 py-2.5 text-sm font-medium text-white transition hover:opacity-90"
              style={{ background: "#1B2A4A" }}
            >
              Pokušaj ponovno
            </button>
          </div>
        )}

        {faza === "potvrda" && (
          <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
            <div className="flex items-center gap-4 border-b border-slate-200 px-6 py-5 sm:px-8">
              <div
                className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg"
                style={{ background: "#1B2A4A" }}
              >
                <Scale className="h-5 w-5" style={{ color: "#C9A84C" }} />
              </div>
              <div>
                <h1 className="font-serif text-xl font-semibold text-slate-900">
                  {izmijenjena ? "Izjava o odgovornosti je izmijenjena" : "Prije nego počnete"}
                </h1>
                <p className="mt-0.5 text-xs text-slate-400">Verzija od {PRAVNI_TEKSTOVI_VERZIJA_TEKST}</p>
              </div>
            </div>

            <div className="space-y-4 px-6 py-6 text-[15px] leading-relaxed text-slate-700 sm:px-8">
              {izmijenjena && PRAVNI_TEKSTOVI_IZMJENA && (
                <p className="rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-900">
                  {PRAVNI_TEKSTOVI_IZMJENA}
                </p>
              )}
              <p>
                PravoTek je pomoćni alat za izračun troškovnika — nije pravni savjet i ne zamjenjuje
                vašu prosudbu.
              </p>
              <p>
                Svaki izračun provjerite prije nego ga upotrijebite u postupku;{" "}
                <strong className="text-slate-900">za sadržaj podneska odgovara odvjetnik.</strong>
              </p>
              <p>
                Alat ne mora u svakom trenutku pratiti posljednju izmjenu propisa, a izračuni nisu
                neovisno stručno revidirani. Uz svaki novi spremljeni troškovnik piše po kojem je
                propisu izračunat.
              </p>

              <p className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
                <a
                  href="/odgovornost"
                  target="_blank"
                  rel="noopener"
                  className="font-medium underline underline-offset-2"
                  style={{ color: "#1B2A4A" }}
                >
                  Pročitajte cijelu Izjavu o odgovornosti
                </a>
                <a
                  href="/uvjeti"
                  target="_blank"
                  rel="noopener"
                  className="font-medium underline underline-offset-2"
                  style={{ color: "#1B2A4A" }}
                >
                  Uvjeti korištenja
                </a>
              </p>

              {greska && (
                <p role="alert" className="flex items-start gap-2 text-sm text-red-700">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                  {greska}
                </p>
              )}

              <div className="flex flex-col-reverse items-stretch gap-3 pt-2 sm:flex-row sm:items-center sm:justify-between">
                <button
                  onClick={odjava}
                  className="text-sm text-slate-500 underline-offset-2 hover:text-slate-800 hover:underline"
                >
                  Odjava
                </button>
                <button
                  onClick={potvrdi}
                  disabled={sprema}
                  className="inline-flex items-center justify-center gap-2 rounded-lg px-5 py-3 text-sm font-semibold text-white transition hover:opacity-90 disabled:opacity-60"
                  style={{ background: "#1B2A4A" }}
                >
                  {sprema && <Loader2 className="h-4 w-4 animate-spin" />}
                  Razumijem i nastavljam
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
