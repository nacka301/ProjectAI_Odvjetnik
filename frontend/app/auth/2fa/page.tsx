"use client";

import { useState, useEffect, useCallback, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { QRCodeSVG } from "qrcode.react";
import { Loader2, Lock, Shield, AlertCircle, KeyRound, Check } from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import type { SecondFactorResult } from "@/lib/second-factor";

/**
 * The second-factor gate.
 *
 * It asks the backend what this account actually needs before rendering, because
 * the two cases look nothing alike:
 *
 *   • 2FA is on  → ask for a code
 *   • 2FA is off → offer to switch it on, right here
 *
 * The second case used to be a dead end. The gate demanded a code from an
 * authenticator the user had never paired, and the only page that could pair one
 * — /postavke — sat behind this very gate. The way out is enrolment, not a
 * bypass: the user leaves this screen with 2FA enabled, not skipped.
 */
export default function TwoFactorPage() {
  return (
    <Suspense fallback={null}>
      <TwoFactorContent />
    </Suspense>
  );
}

type Stage =
  | "loading"
  | "verify"    // account has 2FA — ask for the code
  | "offer"     // account has none — offer to enrol
  | "pair"      // showing the QR, waiting for the first code
  | "recovery"; // enrolled; show the recovery codes once

type SetupData = { otpauth_url: string; manual_entry_key: string };

function TwoFactorContent() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [stage, setStage] = useState<Stage>("loading");
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [setupData, setSetupData] = useState<SetupData | null>(null);
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);

  // Where the middleware wanted to send the user before it stopped them.
  const next = searchParams.get("next");
  const destination = next && next.startsWith("/") ? next : "/";

  /** Hard navigation so the middleware re-evaluates the gate with the new cookie. */
  const enterApp = useCallback(() => {
    window.location.href = destination;
  }, [destination]);

  // ── What does this account need? ──
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const res = await fetch("/api/2fa/gate-status", { cache: "no-store" });

        if (!res.ok) {
          if (!cancelled) redirectToUnavailable(destination, "backend");
          return;
        }

        const result: SecondFactorResult = await res.json();
        if (cancelled) return;

        if (result.outcome === "unknown") {
          // Cannot tell — that is its own screen, not a code prompt.
          redirectToUnavailable(destination, result.reason);
        } else if (result.outcome === "required") {
          setStage("verify");
        } else {
          setStage("offer");
        }
      } catch {
        if (!cancelled) redirectToUnavailable(destination, "unreachable");
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [destination]);

  // ── Existing 2FA: verify a code ──
  const handleVerify = async () => {
    setError(null);
    if (!token) {
      setError("Unesite kod.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/2fa/verify", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token }),
      });

      const data = await res.json().catch(() => null);

      if (!res.ok || data?.success !== true) {
        setError(data?.error ?? "Neispravan kod.");
        setToken("");
        return;
      }

      enterApp();
    } catch {
      setError("Greška pri provjeri koda. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  // ── No 2FA yet: start pairing ──
  const handleStartSetup = async () => {
    setError(null);
    setLoading(true);
    try {
      const res = await fetch("/api/2fa/setup", { method: "POST" });
      const data = await res.json().catch(() => null);

      if (!res.ok || !data?.otpauth_url) {
        setError(data?.error ?? "Priprema nije uspjela. Pokušajte ponovo.");
        return;
      }

      setSetupData(data);
      setToken("");
      setStage("pair");
    } catch {
      setError("Poslužitelj nije dostupan. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  // ── Confirm the first code and switch 2FA on ──
  const handleEnroll = async () => {
    setError(null);
    if (token.length !== 6) {
      setError("Unesite točno 6 znamenki iz aplikacije.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/2fa/enroll", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          secret: setupData?.manual_entry_key.replace(/\s/g, "") ?? "",
          token,
        }),
      });

      const data = await res.json().catch(() => null);

      if (!res.ok || data?.success !== true) {
        setError(data?.error ?? "Neispravan kod. Provjerite vrijeme na uređaju.");
        setToken("");
        return;
      }

      setRecoveryCodes(data.recovery_codes ?? []);
      setStage("recovery");
    } catch {
      setError("Greška pri aktivaciji. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  const handleSignOut = async () => {
    const supabase = createSupabaseBrowser();
    await supabase.auth.signOut();
    router.push("/auth/login");
    router.refresh();
  };

  // ── Render ──

  if (stage === "loading") {
    return (
      <Shell subtitle="Provjera">
        <div className="flex flex-col items-center gap-3 py-10">
          <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
          <p className="text-sm text-slate-500">Provjeravamo vaš račun…</p>
        </div>
      </Shell>
    );
  }

  if (stage === "recovery") {
    return (
      <Shell
        subtitle="Kodovi Za Oporavak"
        title="2FA je uključen"
        lead="Spremite ove kodove — svaki se može iskoristiti jednom ako izgubite pristup aplikaciji."
      >
        <div className="grid grid-cols-2 gap-2">
          {recoveryCodes.map((code) => (
            <code
              key={code}
              className="text-center font-mono text-sm tracking-wider text-slate-900 bg-slate-50 border border-slate-200 rounded-lg py-2"
            >
              {code}
            </code>
          ))}
        </div>

        <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-xl p-3 text-xs text-amber-800">
          <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-amber-500" />
          <span>Ovo je jedini put da vidite ove kodove. Zapišite ih prije nastavka.</span>
        </div>

        <PrimaryButton onClick={enterApp} icon={<Check className="h-4 w-4" />}>
          Spremio sam kodove — nastavi
        </PrimaryButton>
      </Shell>
    );
  }

  if (stage === "offer" || stage === "pair") {
    return (
      <Shell
        subtitle="Aktivacija Zaštite"
        title={stage === "offer" ? "Uključite dvofaktorsku zaštitu" : "Povežite aplikaciju"}
        lead={
          stage === "offer"
            ? "Vaš račun još nema drugi faktor. Aktivirajte ga sada — traje manje od minute."
            : "Skenirajte kod u Google Authenticatoru ili sličnoj aplikaciji, pa upišite prvi kod."
        }
      >
        {error && <ErrorBanner message={error} />}

        {stage === "offer" ? (
          <>
            <div className="flex flex-col items-center gap-4 py-2">
              <IconBadge>
                <KeyRound className="h-8 w-8" style={{ color: "var(--auth-gold)" }} />
              </IconBadge>
              <p className="text-xs text-slate-500 text-center leading-relaxed max-w-xs">
                Trebat će vam aplikacija za autentifikaciju na telefonu
                (Google Authenticator, Authy, 1Password i slične).
              </p>
            </div>

            <PrimaryButton
              onClick={handleStartSetup}
              disabled={loading}
              icon={loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Shield className="h-4 w-4" />}
            >
              {loading ? "Pripremam…" : "Uključi dvofaktorsku zaštitu"}
            </PrimaryButton>
          </>
        ) : (
          <>
            <div className="flex flex-col items-center gap-4 py-2">
              {setupData && (
                <div className="bg-white p-3 rounded-xl border border-slate-200">
                  <QRCodeSVG value={setupData.otpauth_url} size={168} />
                </div>
              )}

              <p className="text-[11px] text-slate-400 text-center">
                Ne možete skenirati? Unesite ručno:
                <br />
                <code className="font-mono text-slate-600 tracking-wider">
                  {setupData?.manual_entry_key}
                </code>
              </p>

              <CodeInput
                value={token}
                onChange={setToken}
                maxLength={6}
                onEnter={handleEnroll}
              />
            </div>

            <PrimaryButton
              onClick={handleEnroll}
              disabled={loading || token.length !== 6}
              icon={loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Shield className="h-4 w-4" />}
            >
              {loading ? "Aktiviram…" : "Potvrdi i uključi"}
            </PrimaryButton>
          </>
        )}

        <SignOutButton onClick={handleSignOut} />
      </Shell>
    );
  }

  // stage === "verify"
  return (
    <Shell
      subtitle="Dvofaktorska Provjera"
      title="Dvofaktorska autentifikacija"
      lead="Unesite kod iz vaše authenticator aplikacije"
    >
      {error && <ErrorBanner message={error} />}

      <div className="flex flex-col items-center gap-4 py-2">
        <IconBadge>
          <Lock className="h-8 w-8" style={{ color: "var(--auth-gold)" }} />
        </IconBadge>

        <CodeInput value={token} onChange={setToken} maxLength={8} onEnter={handleVerify} />

        <p className="text-xs text-slate-400 text-center leading-relaxed">
          Unesite 6-znamenkasti kod iz aplikacije
          <br />
          ili 8-znakovni kod za oporavak
        </p>
      </div>

      <PrimaryButton
        onClick={handleVerify}
        disabled={loading || !token}
        icon={loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Shield className="h-4 w-4" />}
      >
        {loading ? "Provjera..." : "Potvrdi prijavu"}
      </PrimaryButton>

      <SignOutButton onClick={handleSignOut} />
    </Shell>
  );
}

// ── Helpers ──

function redirectToUnavailable(destination: string, reason: string) {
  const params = new URLSearchParams({ next: destination, razlog: reason });
  window.location.href = `/auth/nedostupno?${params.toString()}`;
}

function Shell({
  subtitle,
  title,
  lead,
  children,
}: {
  subtitle: string;
  title?: string;
  lead?: string;
  children: React.ReactNode;
}) {
  return (
    <>
      <div className="flex items-center justify-center gap-3 mb-8">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center shadow-lg"
          style={{ background: "var(--auth-navy)", boxShadow: "0 4px 20px rgba(201,168,76,0.15)" }}
        >
          <Shield className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
        </div>
        <div>
          <h1 className="font-serif text-xl font-bold text-white tracking-tight">PravoTek</h1>
          <p className="text-[10px] uppercase tracking-[0.2em]" style={{ color: "var(--auth-gold)" }}>
            {subtitle}
          </p>
        </div>
      </div>

      <div
        className="rounded-2xl shadow-2xl overflow-hidden"
        style={{
          background: "var(--auth-card-bg)",
          border: "1px solid var(--auth-card-border)",
          boxShadow: "0 25px 50px -12px rgba(0,0,0,0.4), 0 0 0 1px rgba(201,168,76,0.1)",
        }}
      >
        {title && (
          <div className="px-6 pt-6 pb-2 sm:px-8 sm:pt-8">
            <h2 className="font-serif text-lg font-semibold text-slate-900 mb-1">{title}</h2>
            {lead && <p className="text-sm text-slate-500">{lead}</p>}
          </div>
        )}

        <div className="px-6 pb-6 sm:px-8 sm:pb-8 pt-6 space-y-5">{children}</div>
      </div>

      <div className="flex items-center justify-center gap-2 mt-6 text-xs text-slate-500">
        <Shield className="h-3.5 w-3.5" style={{ color: "var(--auth-gold-muted, #B8A060)" }} />
        <span>Zaštićeno dvofaktorskom autentifikacijom</span>
      </div>
    </>
  );
}

function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700">
      <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
      <span>{message}</span>
    </div>
  );
}

function IconBadge({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="w-16 h-16 rounded-2xl flex items-center justify-center"
      style={{ background: "var(--auth-navy)" }}
    >
      {children}
    </div>
  );
}

function CodeInput({
  value,
  onChange,
  maxLength,
  onEnter,
}: {
  value: string;
  onChange: (next: string) => void;
  maxLength: number;
  onEnter: () => void;
}) {
  return (
    <input
      type="text"
      inputMode="numeric"
      maxLength={maxLength}
      value={value}
      onChange={(e) => onChange(e.target.value.replace(/[^A-Za-z0-9]/g, "").slice(0, maxLength))}
      placeholder="000000"
      className="w-48 text-center border border-slate-200 rounded-xl px-4 py-3 text-xl font-mono tracking-[0.3em] text-slate-900 placeholder:text-slate-300 focus:ring-2 outline-none transition"
      style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
      autoFocus
      onKeyDown={(e) => {
        if (e.key === "Enter") onEnter();
      }}
    />
  );
}

function PrimaryButton({
  onClick,
  disabled,
  icon,
  children,
}: {
  onClick: () => void;
  disabled?: boolean;
  icon?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed hover:opacity-90"
      style={{ background: "var(--auth-navy)", boxShadow: "0 4px 14px rgba(27,42,74,0.4)" }}
    >
      {icon}
      {children}
    </button>
  );
}

function SignOutButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className="w-full flex items-center justify-center gap-1.5 text-xs font-medium transition py-2 rounded-lg hover:bg-slate-50"
      style={{ color: "var(--auth-gold)" }}
    >
      ← Odjavi se
    </button>
  );
}
