"use client";

import { useEffect, useState } from "react";
import { Loader2, CreditCard, Download, CheckCircle2, Calendar } from "lucide-react";
import { API_BASE } from "@/lib/api";

function apiHeaders(): Record<string, string> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const apiKey = process.env.NEXT_PUBLIC_API_KEY;
  if (apiKey) headers["X-API-Key"] = apiKey;
  return headers;
}

interface SubscriptionData {
  plan_name: string;
  status: string;
  valid_until: string | null;
}

interface Props {
  userId: string;
}

const STATUS_LABELS: Record<string, { label: string; color: string; bg: string; border: string }> = {
  active:   { label: "Aktivno",   color: "text-emerald-700", bg: "bg-emerald-50", border: "border-emerald-200" },
  trial:    { label: "Probno",    color: "text-amber-700",   bg: "bg-amber-50",   border: "border-amber-200" },
  canceled: { label: "Otkazano",  color: "text-red-600",     bg: "bg-red-50",     border: "border-red-200" },
  expired:  { label: "Isteklo",   color: "text-slate-500",   bg: "bg-slate-50",   border: "border-slate-200" },
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

export default function SubscriptionView({ userId }: Props) {
  const [sub, setSub] = useState<SubscriptionData | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!userId) return;
    (async () => {
      try {
        const res = await fetch(
          `${API_BASE}/api/settings/subscription?userId=${encodeURIComponent(userId)}`,
          { headers: apiHeaders() }
        );
        if (res.ok) {
          setSub(await res.json());
        }
      } catch {
        // silent
      } finally {
        setLoading(false);
      }
    })();
  }, [userId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  const planName = sub?.plan_name || "Starter";
  const status = sub?.status || "trial";
  const statusInfo = STATUS_LABELS[status] || STATUS_LABELS.trial;

  return (
    <>
      {/* Header */}
      <div className="px-6 sm:px-8 py-5 border-b border-slate-200 flex items-center gap-4">
        <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0" style={{ background: "#1B2A4A" }}>
          <CreditCard className="h-5 w-5 text-white" />
        </div>
        <div>
          <h2 className="font-serif text-lg font-semibold text-slate-900">Pretplata i računi</h2>
          <p className="text-sm text-slate-400 mt-0.5">Vaš trenutni plan, fakture i povijest plaćanja</p>
        </div>
      </div>

      {/* Content */}
      <div className="px-6 sm:px-8 py-6 space-y-6">
        {/* Plan Badge */}
        <div className="rounded-xl p-5 bg-slate-50 border border-slate-200">
          <div className="flex items-center justify-between flex-wrap gap-3">
            <div className="flex items-center gap-3">
              <div className="w-12 h-12 rounded-xl flex items-center justify-center" style={{ background: "#1B2A4A" }}>
                <CheckCircle2 className="h-6 w-6" style={{ color: "#C9A84C" }} />
              </div>
              <div>
                <p className="text-lg font-serif font-bold text-slate-900">
                  PravoTek {planName}
                </p>
                <div className="flex items-center gap-2 mt-0.5">
                  <span className={`inline-flex items-center text-xs font-bold uppercase tracking-wider px-2.5 py-0.5 rounded-full border ${statusInfo.bg} ${statusInfo.color} ${statusInfo.border}`}>
                    {statusInfo.label}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Valid Until */}
        <div className="flex items-center gap-3 text-sm">
          <Calendar className="h-4 w-4 text-slate-400" />
          <span className="text-slate-500">Vrijedi do:</span>
          <span className="font-semibold text-slate-800">{formatDate(sub?.valid_until ?? null)}</span>
        </div>

        {/* Divider */}
        <div className="h-px bg-slate-100" />

        {/* Invoice download */}
        <div>
          <button
            className="flex items-center gap-2 text-sm font-medium px-4 py-2.5 rounded-lg transition-all text-slate-600 bg-white border border-slate-200 hover:bg-slate-50 hover:border-slate-300"
          >
            <Download className="h-4 w-4" />
            Preuzmi R1 račun za tekući mjesec
          </button>
          <p className="text-[11px] text-slate-400 mt-2">
            Fakture se automatski generiraju svakog 1. u mjesecu.
          </p>
        </div>
      </div>
    </>
  );
}
