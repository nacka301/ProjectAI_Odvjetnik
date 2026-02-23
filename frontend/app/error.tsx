"use client";

import { useEffect, useMemo } from "react";
import Link from "next/link";
import {
  Scale,
  RefreshCw,
  ArrowLeft,
  WifiOff,
  ShieldAlert,
  ServerCrash,
  AlertTriangle,
} from "lucide-react";

/* ── Error classification ── */
type ErrorKind = "network" | "auth" | "server" | "generic";

function classifyError(error: Error): ErrorKind {
  const msg = (error.message || "").toLowerCase();
  if (
    msg.includes("fetch") ||
    msg.includes("network") ||
    msg.includes("econnrefused") ||
    msg.includes("failed to fetch") ||
    msg.includes("load failed")
  )
    return "network";
  if (
    msg.includes("unauthorized") ||
    msg.includes("401") ||
    msg.includes("auth") ||
    msg.includes("session")
  )
    return "auth";
  if (msg.includes("500") || msg.includes("server") || msg.includes("internal"))
    return "server";
  return "generic";
}

const errorMeta: Record<
  ErrorKind,
  { icon: typeof AlertTriangle; title: string; description: string; color: string }
> = {
  network: {
    icon: WifiOff,
    title: "Problem s mrežom",
    description:
      "Nismo se mogli povezati s poslužiteljem. Provjerite internetsku vezu i pokušajte ponovno.",
    color: "text-amber-600",
  },
  auth: {
    icon: ShieldAlert,
    title: "Sesija je istekla",
    description:
      "Vaša sesija je istekla ili nemate ovlasti za ovu radnju. Prijavite se ponovno.",
    color: "text-red-600",
  },
  server: {
    icon: ServerCrash,
    title: "Greška na poslužitelju",
    description:
      "Došlo je do problema na našem poslužitelju. Pokušajte ponovno za trenutak.",
    color: "text-orange-600",
  },
  generic: {
    icon: AlertTriangle,
    title: "Nešto je pošlo po krivu",
    description:
      "Došlo je do neočekivane greške. Pokušajte ponovno ili se vratite na početnu stranicu.",
    color: "text-slate-600",
  },
};

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Unhandled error:", error);
  }, [error]);

  const kind = useMemo(() => classifyError(error), [error]);
  const meta = errorMeta[kind];
  const Icon = meta.icon;

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* ── Mini header ── */}
      <header className="bg-slate-900 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex items-center gap-3">
          <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
            <Scale className="h-5 w-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold tracking-tight">PravoTek</h1>
            <p className="text-[10px] text-slate-400 uppercase tracking-widest">
              Pravna Inteligencija
            </p>
          </div>
        </div>
      </header>

      {/* ── Content ── */}
      <div className="flex-1 flex items-center justify-center px-4">
        <div className="max-w-md w-full text-center space-y-6">
          {/* Icon */}
          <div
            className={`mx-auto flex items-center justify-center w-16 h-16 rounded-full bg-white shadow-sm border border-slate-100 ${meta.color}`}
          >
            <Icon className="h-8 w-8" />
          </div>

          {/* Heading + description */}
          <div className="space-y-2">
            <h2
              className="text-2xl font-bold text-[#1B2A4A]"
              style={{ fontFamily: "var(--font-playfair), serif" }}
            >
              {meta.title}
            </h2>
            <p className="text-sm text-gray-500 max-w-sm mx-auto">
              {meta.description}
            </p>
          </div>

          {/* Actions */}
          <div className="flex flex-col sm:flex-row items-center justify-center gap-3">
            <button
              onClick={reset}
              className="inline-flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-6 py-3 text-sm font-semibold text-white hover:bg-[#243660] transition shadow-sm"
            >
              <RefreshCw className="h-4 w-4" />
              Pokušaj ponovno
            </button>
            {kind === "auth" ? (
              <Link
                href="/auth/login"
                className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-6 py-3 text-sm font-medium text-gray-700 hover:bg-gray-50 transition"
              >
                <ShieldAlert className="h-4 w-4" />
                Prijava
              </Link>
            ) : (
              <Link
                href="/"
                className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-6 py-3 text-sm font-medium text-gray-700 hover:bg-gray-50 transition"
              >
                <ArrowLeft className="h-4 w-4" />
                Početna stranica
              </Link>
            )}
          </div>

          {/* Error digest (dev aid) */}
          {error.digest && (
            <p className="text-[11px] text-gray-400 font-mono">
              Referenca greške: {error.digest}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
