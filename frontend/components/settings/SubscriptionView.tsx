"use client";

import { Loader2, CreditCard, Lock, CheckCircle2, Calendar, Mail, Clock } from "lucide-react";
import {
  useSubscription,
  CIJENA_MJESECNO,
  CIJENA_GODISNJE,
  KONTAKT_EMAIL,
  type SubscriptionState,
} from "@/lib/useSubscription";

interface Props {
  userId: string;
}

/**
 * Where the user reads their standing: which state they are in, until when, and
 * what happens next.
 *
 * This screen used to print "PravoTek" followed by a tier name, taken from its
 * own private copy of a three-tier label map. There is one plan now, so there is
 * nothing to name; what matters is the state, and that comes from the backend
 * rather than from date arithmetic done here.
 */

const STATE_LABELS: Record<
  SubscriptionState,
  { label: string; color: string; bg: string; border: string }
> = {
  proba: {
    label: "Probno razdoblje",
    color: "text-amber-700",
    bg: "bg-amber-50",
    border: "border-amber-200",
  },
  pretplata: {
    label: "Pretplata aktivna",
    color: "text-emerald-700",
    bg: "bg-emerald-50",
    border: "border-emerald-200",
  },
  isteklo: {
    label: "Pristup zatvoren",
    color: "text-red-700",
    bg: "bg-red-50",
    border: "border-red-200",
  },
};

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString("hr-HR", {
      day: "2-digit",
      month: "long",
      year: "numeric",
    });
  } catch {
    return iso;
  }
}

export default function SubscriptionView({ userId: _propUserId }: Props) {
  const { state, badgeLabel, accessUntil, loading } = useSubscription();

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  const stateInfo = STATE_LABELS[state];
  const expired = state === "isteklo";

  return (
    <>
      {/* Header */}
      <div className="px-6 sm:px-8 py-5 border-b border-slate-200 flex items-center gap-4">
        <div
          className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0"
          style={{ background: "#1B2A4A" }}
        >
          <CreditCard className="h-5 w-5 text-white" />
        </div>
        <div>
          <h2 className="font-serif text-lg font-semibold text-slate-900">Pretplata</h2>
          <p className="text-sm text-slate-400 mt-0.5">
            Stanje vašeg pristupa Troškovniku i Strankama
          </p>
        </div>
      </div>

      {/* Content */}
      <div className="px-6 sm:px-8 py-6 space-y-6">
        {/* State */}
        <div className="rounded-xl p-5 bg-slate-50 border border-slate-200">
          <div className="flex items-center gap-3">
            <div
              className="w-12 h-12 rounded-xl flex items-center justify-center shrink-0"
              style={{ background: "#1B2A4A" }}
            >
              {expired ? (
                <Lock className="h-6 w-6" style={{ color: "#C9A84C" }} />
              ) : state === "proba" ? (
                <Clock className="h-6 w-6" style={{ color: "#C9A84C" }} />
              ) : (
                <CheckCircle2 className="h-6 w-6" style={{ color: "#C9A84C" }} />
              )}
            </div>
            <div>
              <p className="text-lg font-serif font-bold text-slate-900">{badgeLabel}</p>
              <div className="flex items-center gap-2 mt-0.5">
                <span
                  className={`inline-flex items-center text-xs font-bold uppercase tracking-wider px-2.5 py-0.5 rounded-full border ${stateInfo.bg} ${stateInfo.color} ${stateInfo.border}`}
                >
                  {stateInfo.label}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Until when */}
        <div className="flex items-center gap-3 text-sm">
          <Calendar className="h-4 w-4 text-slate-400" />
          <span className="text-slate-500">
            {expired ? "Pristup je zatvoren od:" : state === "proba" ? "Proba traje do:" : "Pretplata vrijedi do:"}
          </span>
          <span className="font-semibold text-slate-800">{formatDate(accessUntil)}</span>
        </div>

        <div className="h-px bg-slate-100" />

        {/* What happens next */}
        {expired ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 space-y-3">
            <p className="text-sm font-semibold text-red-800">
              Troškovnik i Stranke zatvoreni su do uplate pretplate.
            </p>
            <p className="text-sm text-red-700">
              Vaši podaci nisu izbrisani — predmeti, stranke i troškovnici stoje
              gdje ste ih ostavili i vraćaju se čim pretplata proradi.
            </p>
          </div>
        ) : (
          <p className="text-sm text-slate-600">
            {state === "proba"
              ? "Kad proba istekne, Troškovnik i Stranke zatvaraju se do uplate pretplate. Podaci ostaju sačuvani."
              : "Pristup Troškovniku i Strankama je otvoren."}
          </p>
        )}

        {/* Price and how to pay */}
        <div className="rounded-lg bg-slate-50 border border-slate-200 p-4">
          <p className="text-[11px] uppercase tracking-wider text-slate-400 font-medium">
            Cijena pretplate
          </p>
          <p className="mt-1 text-lg font-semibold text-slate-900">
            {CIJENA_MJESECNO} mjesečno
            <span className="text-slate-400 font-normal"> ili </span>
            {CIJENA_GODISNJE} godišnje
          </p>
        </div>

        <div>
          <p className="text-sm font-medium text-slate-800">
            {expired ? "Kako otvoriti pristup" : "Kako platiti"}
          </p>
          <p className="text-sm text-slate-600 mt-1">
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
      </div>
    </>
  );
}
