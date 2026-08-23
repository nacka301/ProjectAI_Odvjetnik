"use client";

import Link from "next/link";
import { Lock, Loader2, Mail } from "lucide-react";
import {
  useSubscription,
  CIJENA_MJESECNO,
  CIJENA_GODISNJE,
  KONTAKT_EMAIL,
} from "@/lib/useSubscription";
import { useIsAdmin } from "@/lib/useIsAdmin";
import AppHeader from "@/components/AppHeader";

/**
 * Stands in front of Troškovnik and Stranke once the trial or subscription has
 * run out, and explains what happened.
 *
 * This is courtesy, not the lock — exactly as `frontend/lib/modules.ts` says of
 * the in-development modules. Everything here runs on hardware the user
 * controls, and the route can be reached by typing it. The module is actually
 * closed because `Backend/Middleware/SubscriptionGateMiddleware.cs` answers 402
 * for its API prefixes, on a verdict the browser has no say in.
 *
 * What this adds is an explanation. Without it an expired office would open
 * Troškovnik, watch every request fail, and have to guess why.
 *
 * /postavke is deliberately NOT wrapped: that is where the user reads their
 * status and finds the address to write to.
 */
export default function PretplataGate({ children }: { children: React.ReactNode }) {
  const { state, loading } = useSubscription();
  const isAdmin = useIsAdmin();

  // Nothing is decided until the answer is in. Rendering the page first and
  // swapping in a paywall a moment later would flash the user's own work at
  // them before taking it away; rendering the paywall first would accuse a
  // paying user of not paying.
  if (loading) {
    return (
      <div className="min-h-screen bg-slate-50">
        <AppHeader />
        <div className="flex items-center justify-center py-32">
          <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
        </div>
      </div>
    );
  }

  if (isAdmin || state !== "isteklo") {
    return <>{children}</>;
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />

      <main className="max-w-2xl mx-auto px-4 sm:px-6 py-16">
        <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
          <div className="px-6 sm:px-8 py-6 border-b border-slate-200 flex items-center gap-4">
            <div
              className="w-11 h-11 rounded-lg flex items-center justify-center shrink-0"
              style={{ background: "#1B2A4A" }}
            >
              <Lock className="h-5 w-5" style={{ color: "#C9A84C" }} />
            </div>
            <div>
              <h1 className="font-serif text-xl font-semibold text-slate-900">
                Probno razdoblje je isteklo
              </h1>
              <p className="text-sm text-slate-400 mt-0.5">
                Troškovnik i Stranke zatvoreni su do uplate pretplate.
              </p>
            </div>
          </div>

          <div className="px-6 sm:px-8 py-6 space-y-5 text-sm text-slate-600">
            <p>
              Vaši podaci nisu nigdje otišli. Predmeti, stranke i troškovnici
              stoje gdje ste ih ostavili i vraćaju se čim pretplata proradi.
            </p>

            <div className="rounded-lg bg-slate-50 border border-slate-200 p-4">
              <p className="text-[11px] uppercase tracking-wider text-slate-400 font-medium">
                Pretplata
              </p>
              <p className="mt-1 text-lg font-semibold text-slate-900">
                {CIJENA_MJESECNO} mjesečno
                <span className="text-slate-400 font-normal"> ili </span>
                {CIJENA_GODISNJE} godišnje
              </p>
            </div>

            <div>
              <p className="font-medium text-slate-800">Kako platiti</p>
              <p className="mt-1">
                Plaćanje se za sada dogovara e-poštom — javite se i pretplata se
                otvara ručno, obično isti dan.
              </p>
              <a
                href={`mailto:${KONTAKT_EMAIL}?subject=${encodeURIComponent("PravoTek — pretplata")}`}
                className="mt-3 inline-flex items-center gap-2 text-sm font-medium px-4 py-2.5 rounded-lg text-white transition hover:opacity-90"
                style={{ background: "#1B2A4A" }}
              >
                <Mail className="h-4 w-4" />
                {KONTAKT_EMAIL}
              </a>
            </div>

            <div className="h-px bg-slate-100" />

            <p className="text-[13px] text-slate-500">
              Stanje pretplate možete pogledati u{" "}
              <Link href="/postavke" className="underline hover:text-slate-800">
                Postavkama
              </Link>
              .
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
