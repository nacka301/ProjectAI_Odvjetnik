"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  Shield,
  ShieldCheck,
  ShieldOff,
  Loader2,
  Copy,
  Check,
  AlertTriangle,
  ArrowLeft,
  Key,
  Smartphone,
  Lock,
  Scale,
  CheckCircle2,
  QrCode,
  Info,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { API_BASE } from "@/lib/api";

// ── Types ──
interface SetupResponse {
  otpauth_url: string;
  manual_entry_key: string;
}

interface VerifyResponse {
  success: boolean;
  message: string;
  recovery_codes?: string[];
}

interface StatusResponse {
  enabled: boolean;
}

// ── Helpers ──
function apiHeaders(): Record<string, string> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const apiKey = process.env.NEXT_PUBLIC_API_KEY;
  if (apiKey) headers["X-API-Key"] = apiKey;
  return headers;
}

export default function PostavkePage() {
  const router = useRouter();
  const supabase = createSupabaseBrowser();

  // ── Auth state ──
  const [userId, setUserId] = useState<string | null>(null);
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  // ── 2FA state ──
  const [twoFAEnabled, setTwoFAEnabled] = useState(false);
  const [statusLoading, setStatusLoading] = useState(true);

  // ── Setup flow ──
  const [step, setStep] = useState<"idle" | "qr" | "verify" | "recovery" | "disable">("idle");
  const [setupData, setSetupData] = useState<SetupResponse | null>(null);
  const [verifyToken, setVerifyToken] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [secretForVerify, setSecretForVerify] = useState("");

  // ── UI state ──
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [copied, setCopied] = useState(false);
  const [recoveryCodesCopied, setRecoveryCodesCopied] = useState(false);
  const [disableToken, setDisableToken] = useState("");

  // ── Load user ──
  useEffect(() => {
    (async () => {
      const { data: { user } } = await supabase.auth.getUser();
      if (!user) {
        router.push("/auth/login");
        return;
      }
      setUserId(user.id);
      setUserEmail(user.email ?? null);
      setLoading(false);
    })();
  }, [supabase, router]);

  // ── Check 2FA status ──
  const checkStatus = useCallback(async () => {
    if (!userId) return;
    setStatusLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/2fa/status?userId=${encodeURIComponent(userId)}`, {
        headers: apiHeaders(),
      });
      if (res.ok) {
        const data: StatusResponse = await res.json();
        setTwoFAEnabled(data.enabled);
      }
    } catch {
      // Silently fail — will show as disabled
    } finally {
      setStatusLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    if (userId) checkStatus();
  }, [userId, checkStatus]);

  // ── Start setup ──
  const handleStartSetup = async () => {
    setError(null);
    setSuccess(null);
    setActionLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/2fa/setup`, {
        method: "POST",
        headers: apiHeaders(),
        body: JSON.stringify({ email: userEmail }),
      });
      if (!res.ok) throw new Error("Greška pri generiranju 2FA.");
      const data: SetupResponse = await res.json();
      setSetupData(data);
      setSecretForVerify(data.manual_entry_key.replace(/\s/g, ""));
      setStep("qr");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Nepoznata greška.");
    } finally {
      setActionLoading(false);
    }
  };

  // ── Verify token ──
  const handleVerify = async () => {
    setError(null);
    setSuccess(null);
    if (verifyToken.length !== 6) {
      setError("Neispravan kod — unesite točno 6 znamenki iz aplikacije.");
      return;
    }
    setActionLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/2fa/verify-setup`, {
        method: "POST",
        headers: apiHeaders(),
        body: JSON.stringify({
          user_id: userId,
          email: userEmail,
          secret: secretForVerify,
          token: verifyToken,
        }),
      });
      const data: VerifyResponse = await res.json();
      if (!data.success) {
        setError(data.message || "Neispravan kod. Provjerite vrijeme na uređaju i pokušajte ponovo.");
        setVerifyToken("");
        return;
      }
      setRecoveryCodes(data.recovery_codes ?? []);
      setTwoFAEnabled(true);
      setSuccess("Dvofaktorska autentifikacija je uspješno aktivirana!");
      setStep("recovery");
    } catch {
      setError("Greška pri provjeri koda. Pokušajte ponovo.");
    } finally {
      setActionLoading(false);
    }
  };

  // ── Disable 2FA ──
  const handleDisable = async () => {
    setError(null);
    setSuccess(null);
    if (!disableToken) {
      setError("Unesite trenutni 2FA kod za potvrdu.");
      return;
    }
    setActionLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/2fa/disable`, {
        method: "POST",
        headers: apiHeaders(),
        body: JSON.stringify({
          user_id: userId,
          token: disableToken,
        }),
      });
      const data: VerifyResponse = await res.json();
      if (!data.success) {
        setError(data.message || "Neispravan kod.");
        return;
      }
      setTwoFAEnabled(false);
      setStep("idle");
      setDisableToken("");
      setSuccess("2FA je uspješno deaktiviran.");
    } catch {
      setError("Greška pri deaktivaciji 2FA.");
    } finally {
      setActionLoading(false);
    }
  };

  // ── Copy helpers ──
  const copyToClipboard = (text: string, setCopiedState: (v: boolean) => void) => {
    navigator.clipboard.writeText(text);
    setCopiedState(true);
    setTimeout(() => setCopiedState(false), 2500);
  };

  // ── Loading screen ──
  if (loading) {
    return (
      <div
        className="min-h-screen flex items-center justify-center"
        style={{ background: "linear-gradient(160deg, #0F1A2E 0%, #1B2A4A 40%, #162240 100%)" }}
      >
        <div className="flex flex-col items-center gap-3">
          <Loader2 className="h-8 w-8 animate-spin" style={{ color: "var(--auth-gold)" }} />
          <p className="text-sm text-slate-400">Učitavanje postavki...</p>
        </div>
      </div>
    );
  }

  return (
    <div
      className="min-h-screen"
      style={{ background: "linear-gradient(160deg, #0F1A2E 0%, #1B2A4A 40%, #162240 100%)" }}
    >
      {/* Decorative background elements */}
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div
          className="absolute -top-40 -right-40 w-[600px] h-[600px] rounded-full blur-[140px]"
          style={{ background: "rgba(201, 168, 76, 0.05)" }}
        />
        <div
          className="absolute -bottom-40 -left-40 w-[500px] h-[500px] rounded-full blur-[120px]"
          style={{ background: "rgba(201, 168, 76, 0.03)" }}
        />
      </div>

      {/* ════════════════════ HEADER ════════════════════ */}
      <header
        className="sticky top-0 z-50 backdrop-blur-md border-b"
        style={{ background: "rgba(15, 26, 46, 0.85)", borderColor: "rgba(201, 168, 76, 0.12)" }}
      >
        <div className="max-w-4xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Link
              href="/"
              className="flex items-center gap-2 text-slate-400 hover:text-white transition group"
            >
              <ArrowLeft className="h-4 w-4 group-hover:-translate-x-0.5 transition-transform" />
              <span className="text-sm hidden sm:inline">Početna</span>
            </Link>
            <div className="h-5 w-px" style={{ background: "rgba(201, 168, 76, 0.2)" }} />
            <div className="flex items-center gap-2.5">
              <div
                className="w-8 h-8 rounded-lg flex items-center justify-center"
                style={{ background: "var(--auth-navy)", border: "1px solid rgba(201, 168, 76, 0.25)" }}
              >
                <Scale className="h-4 w-4" style={{ color: "var(--auth-gold)" }} />
              </div>
              <div>
                <h1 className="font-serif text-lg font-bold text-white tracking-tight">Postavke</h1>
              </div>
            </div>
          </div>
          <div className="text-right">
            <p className="text-[11px] text-slate-500 truncate max-w-[200px]">{userEmail}</p>
          </div>
        </div>
      </header>

      {/* ════════════════════ MAIN ════════════════════ */}
      <main className="relative z-10 max-w-4xl mx-auto px-4 sm:px-6 py-8 sm:py-10 space-y-8">

        {/* ═══════════ SECURITY SECTION ═══════════ */}
        <section
          className="rounded-2xl overflow-hidden shadow-2xl"
          style={{
            background: "rgba(15, 26, 46, 0.6)",
            border: "1px solid rgba(201, 168, 76, 0.15)",
            boxShadow: "0 25px 50px -12px rgba(0,0,0,0.4)",
          }}
        >
          {/* ── Section Header ── */}
          <div
            className="px-6 sm:px-8 py-5 flex items-center gap-4"
            style={{ borderBottom: "1px solid rgba(201, 168, 76, 0.1)" }}
          >
            <div
              className="w-12 h-12 rounded-xl flex items-center justify-center shrink-0"
              style={{ background: "rgba(201, 168, 76, 0.1)", border: "1px solid rgba(201, 168, 76, 0.2)" }}
            >
              <Shield className="h-6 w-6" style={{ color: "var(--auth-gold)" }} />
            </div>
            <div className="flex-1 min-w-0">
              <h2 className="font-serif text-lg font-semibold text-white">Dvofaktorska Autentifikacija</h2>
              <p className="text-sm text-slate-400 mt-0.5">Zaštitite račun TOTP kodom iz authenticator aplikacije</p>
            </div>
            <div className="shrink-0">
              {statusLoading ? (
                <Loader2 className="h-5 w-5 animate-spin text-slate-500" />
              ) : twoFAEnabled ? (
                <span
                  className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider px-3.5 py-1.5 rounded-full"
                  style={{ color: "var(--auth-gold)", background: "rgba(201, 168, 76, 0.1)", border: "1px solid rgba(201, 168, 76, 0.25)" }}
                >
                  <ShieldCheck className="h-3.5 w-3.5" /> Aktivno
                </span>
              ) : (
                <span className="flex items-center gap-1.5 text-xs font-semibold text-slate-500 bg-slate-800/50 border border-slate-700/50 px-3.5 py-1.5 rounded-full">
                  <ShieldOff className="h-3.5 w-3.5" /> Neaktivno
                </span>
              )}
            </div>
          </div>

          {/* ── Section Body ── */}
          <div className="px-6 sm:px-8 py-6 space-y-5">

            {/* Global alerts */}
            {error && (
              <div
                className="flex items-start gap-3 rounded-xl p-4 animate-in fade-in slide-in-from-top-1 duration-300"
                style={{ background: "rgba(220, 38, 38, 0.08)", border: "1px solid rgba(220, 38, 38, 0.2)" }}
              >
                <AlertTriangle className="h-5 w-5 shrink-0 mt-0.5 text-red-400" />
                <div>
                  <p className="text-sm font-semibold text-red-400">Greška</p>
                  <p className="text-sm text-red-300/80 mt-0.5">{error}</p>
                </div>
              </div>
            )}

            {success && (
              <div
                className="flex items-start gap-3 rounded-xl p-4 animate-in fade-in slide-in-from-top-1 duration-300"
                style={{ background: "rgba(201, 168, 76, 0.08)", border: "1px solid rgba(201, 168, 76, 0.2)" }}
              >
                <CheckCircle2 className="h-5 w-5 shrink-0 mt-0.5" style={{ color: "var(--auth-gold)" }} />
                <p className="text-sm" style={{ color: "var(--auth-gold)" }}>{success}</p>
              </div>
            )}

            {/* ══════════ IDLE: 2FA NOT ENABLED ══════════ */}
            {step === "idle" && !twoFAEnabled && (
              <div className="space-y-6">
                {/* Info panel */}
                <div
                  className="rounded-xl p-5"
                  style={{ background: "rgba(201, 168, 76, 0.04)", border: "1px solid rgba(201, 168, 76, 0.1)" }}
                >
                  <div className="flex items-start gap-4">
                    <div
                      className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0 mt-0.5"
                      style={{ background: "rgba(201, 168, 76, 0.1)" }}
                    >
                      <Smartphone className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
                    </div>
                    <div className="space-y-2">
                      <p className="text-sm font-semibold text-white">Kako funkcionira 2FA?</p>
                      <p className="text-sm text-slate-400 leading-relaxed">
                        Dvofaktorska autentifikacija dodaje dodatni sloj sigurnosti vašem računu. 
                        Nakon aktivacije, uz lozinku trebat ćete unijeti i 6-znamenkasti kod iz aplikacije:
                      </p>
                      <div className="flex flex-wrap gap-2 pt-1">
                        {["Google Authenticator", "Authy", "Microsoft Authenticator"].map(app => (
                          <span
                            key={app}
                            className="inline-flex items-center text-xs font-medium px-2.5 py-1 rounded-md"
                            style={{ background: "rgba(201, 168, 76, 0.08)", color: "var(--auth-gold-light)", border: "1px solid rgba(201, 168, 76, 0.15)" }}
                          >
                            {app}
                          </span>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>

                {/* Security benefits */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  {[
                    { icon: Shield, label: "Zaštita od neovlaštenog pristupa" },
                    { icon: Lock, label: "Odvjetnička tajna pod zaštitom" },
                    { icon: Key, label: "TOTP standard (RFC 6238)" },
                  ].map(({ icon: Icon, label }) => (
                    <div
                      key={label}
                      className="flex items-center gap-2.5 rounded-lg p-3 text-xs text-slate-400"
                      style={{ background: "rgba(255,255,255,0.02)", border: "1px solid rgba(255,255,255,0.04)" }}
                    >
                      <Icon className="h-4 w-4 shrink-0" style={{ color: "var(--auth-gold-light)" }} />
                      <span>{label}</span>
                    </div>
                  ))}
                </div>

                {/* CTA Button */}
                <button
                  onClick={handleStartSetup}
                  disabled={actionLoading}
                  className="w-full sm:w-auto flex items-center justify-center gap-2.5 text-sm font-bold py-3 px-8 rounded-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                  style={{
                    background: "linear-gradient(135deg, var(--auth-gold) 0%, #B8963F 100%)",
                    color: "var(--auth-navy-dark)",
                    boxShadow: "0 4px 20px rgba(201, 168, 76, 0.3), inset 0 1px 0 rgba(255,255,255,0.2)",
                  }}
                >
                  {actionLoading ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Shield className="h-4 w-4" />
                  )}
                  {actionLoading ? "Generiranje..." : "Aktiviraj 2FA (Preporučeno)"}
                </button>
              </div>
            )}

            {/* ══════════ IDLE: 2FA ALREADY ENABLED ══════════ */}
            {step === "idle" && twoFAEnabled && (
              <div className="space-y-5">
                <div
                  className="rounded-xl p-5"
                  style={{ background: "rgba(201, 168, 76, 0.06)", border: "1px solid rgba(201, 168, 76, 0.15)" }}
                >
                  <div className="flex items-start gap-4">
                    <div
                      className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0"
                      style={{ background: "rgba(201, 168, 76, 0.15)" }}
                    >
                      <ShieldCheck className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
                    </div>
                    <div>
                      <p className="text-sm font-semibold" style={{ color: "var(--auth-gold)" }}>
                        Dvofaktorska autentifikacija je aktivna
                      </p>
                      <p className="text-sm text-slate-400 mt-1 leading-relaxed">
                        Vaš račun je zaštićen dodatnim slojem sigurnosti. Pri svakoj prijavi potreban je kod iz vaše authenticator aplikacije.
                      </p>
                    </div>
                  </div>
                </div>

                <div
                  className="h-px w-full"
                  style={{ background: "rgba(201, 168, 76, 0.08)" }}
                />

                <button
                  onClick={() => { setStep("disable"); setError(null); setSuccess(null); }}
                  className="flex items-center gap-2 text-sm font-medium px-4 py-2 rounded-lg transition-all text-slate-500 hover:text-red-400"
                  style={{ background: "rgba(255,255,255,0.03)", border: "1px solid rgba(255,255,255,0.06)" }}
                >
                  <ShieldOff className="h-4 w-4" />
                  Deaktiviraj 2FA
                </button>
              </div>
            )}

            {/* ══════════ QR CODE STEP ══════════ */}
            {step === "qr" && setupData && (
              <div className="space-y-6">
                {/* Step indicator */}
                <div className="flex items-center gap-3">
                  <div className="flex items-center gap-2">
                    <div
                      className="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold"
                      style={{ background: "var(--auth-gold)", color: "var(--auth-navy-dark)" }}
                    >1</div>
                    <span className="text-sm font-semibold text-white">Skenirajte QR kod</span>
                  </div>
                  <div className="flex-1 h-px" style={{ background: "rgba(201, 168, 76, 0.2)" }} />
                  <div className="flex items-center gap-2">
                    <div className="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold bg-slate-700/50 text-slate-500">2</div>
                    <span className="text-sm text-slate-500">Potvrdite kod</span>
                  </div>
                </div>

                {/* QR Card */}
                <div
                  className="rounded-xl overflow-hidden"
                  style={{ border: "1px solid rgba(201, 168, 76, 0.12)" }}
                >
                  <div
                    className="px-5 py-3 flex items-center gap-2"
                    style={{ background: "rgba(201, 168, 76, 0.06)", borderBottom: "1px solid rgba(201, 168, 76, 0.1)" }}
                  >
                    <QrCode className="h-4 w-4" style={{ color: "var(--auth-gold)" }} />
                    <p className="text-sm font-medium text-white">Skenirajte u authenticator aplikaciji</p>
                  </div>
                  <div className="p-6 flex flex-col items-center gap-5" style={{ background: "rgba(0,0,0,0.15)" }}>
                    {/* QR Code */}
                    <div
                      className="bg-white p-4 rounded-2xl shadow-xl"
                      style={{ boxShadow: "0 8px 32px rgba(0,0,0,0.3), 0 0 0 1px rgba(201,168,76,0.1)" }}
                    >
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img
                        src={`https://api.qrserver.com/v1/create-qr-code/?size=220x220&data=${encodeURIComponent(setupData.otpauth_url)}&margin=0`}
                        alt="2FA QR Code"
                        width={220}
                        height={220}
                        className="block"
                      />
                    </div>
                    <p className="text-[11px] text-slate-500 text-center">
                      Otvorite <strong className="text-slate-400">Google Authenticator</strong> ili <strong className="text-slate-400">Authy</strong> i skenirajte kod
                    </p>
                  </div>
                </div>

                {/* Manual key fallback */}
                <div className="space-y-2">
                  <div className="flex items-center gap-2">
                    <Info className="h-3.5 w-3.5 text-slate-500" />
                    <p className="text-xs text-slate-500">Ne možete skenirati? Unesite ključ ručno:</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <code
                      className="flex-1 rounded-lg px-4 py-2.5 text-sm font-mono tracking-[0.2em] text-center"
                      style={{ background: "rgba(201, 168, 76, 0.05)", border: "1px solid rgba(201, 168, 76, 0.12)", color: "var(--auth-gold-light)" }}
                    >
                      {setupData.manual_entry_key}
                    </code>
                    <button
                      onClick={() => copyToClipboard(setupData.manual_entry_key, setCopied)}
                      className="p-2.5 rounded-lg transition-all"
                      style={{
                        background: copied ? "rgba(201, 168, 76, 0.15)" : "rgba(201, 168, 76, 0.05)",
                        border: "1px solid rgba(201, 168, 76, 0.15)",
                      }}
                      title="Kopiraj ključ"
                    >
                      {copied
                        ? <Check className="h-4 w-4" style={{ color: "var(--auth-gold)" }} />
                        : <Copy className="h-4 w-4 text-slate-400" />
                      }
                    </button>
                  </div>
                </div>

                {/* Divider */}
                <div className="flex items-center gap-4">
                  <div className="flex-1 h-px" style={{ background: "rgba(201, 168, 76, 0.1)" }} />
                  <span className="text-xs uppercase tracking-wider" style={{ color: "rgba(201, 168, 76, 0.4)" }}>Potvrda</span>
                  <div className="flex-1 h-px" style={{ background: "rgba(201, 168, 76, 0.1)" }} />
                </div>

                {/* Token input */}
                <div className="space-y-3">
                  <label className="block text-sm font-medium text-slate-300">
                    Unesite 6-znamenkasti kod iz aplikacije:
                  </label>
                  <div className="flex items-center gap-3">
                    <input
                      type="text"
                      inputMode="numeric"
                      maxLength={6}
                      value={verifyToken}
                      onChange={(e) => { setVerifyToken(e.target.value.replace(/\D/g, "").slice(0, 6)); setError(null); }}
                      placeholder="000000"
                      className="w-44 text-center rounded-xl px-4 py-3 text-xl font-mono tracking-[0.4em] placeholder:tracking-[0.4em] outline-none transition-all duration-200"
                      style={{
                        background: "rgba(201, 168, 76, 0.04)",
                        border: verifyToken.length === 6
                          ? "2px solid var(--auth-gold)"
                          : "2px solid rgba(201, 168, 76, 0.12)",
                        color: "var(--auth-gold-light)",
                        boxShadow: verifyToken.length === 6 ? "0 0 20px rgba(201, 168, 76, 0.15)" : "none",
                      }}
                      autoFocus
                      onKeyDown={(e) => { if (e.key === "Enter" && verifyToken.length === 6) handleVerify(); }}
                    />
                    <button
                      onClick={handleVerify}
                      disabled={actionLoading || verifyToken.length !== 6}
                      className="flex items-center gap-2 text-sm font-bold py-3 px-6 rounded-xl transition-all duration-200 disabled:opacity-40 disabled:cursor-not-allowed"
                      style={{
                        background: verifyToken.length === 6
                          ? "linear-gradient(135deg, var(--auth-gold) 0%, #B8963F 100%)"
                          : "rgba(201, 168, 76, 0.1)",
                        color: verifyToken.length === 6 ? "var(--auth-navy-dark)" : "var(--auth-gold)",
                        boxShadow: verifyToken.length === 6 ? "0 4px 20px rgba(201, 168, 76, 0.25)" : "none",
                      }}
                    >
                      {actionLoading ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Check className="h-4 w-4" />
                      )}
                      {actionLoading ? "Provjera..." : "Potvrdi i aktiviraj"}
                    </button>
                  </div>
                </div>

                {/* Cancel */}
                <button
                  onClick={() => { setStep("idle"); setError(null); setSuccess(null); setVerifyToken(""); }}
                  className="text-xs text-slate-500 hover:text-slate-300 transition mt-2"
                >
                  ← Odustani od aktivacije
                </button>
              </div>
            )}

            {/* ══════════ RECOVERY CODES ══════════ */}
            {step === "recovery" && (
              <div className="space-y-6">
                {/* Warning */}
                <div
                  className="flex items-start gap-4 rounded-xl p-5"
                  style={{ background: "rgba(201, 168, 76, 0.06)", border: "1px solid rgba(201, 168, 76, 0.15)" }}
                >
                  <div
                    className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0"
                    style={{ background: "rgba(201, 168, 76, 0.15)" }}
                  >
                    <AlertTriangle className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
                  </div>
                  <div className="space-y-1">
                    <p className="text-sm font-bold" style={{ color: "var(--auth-gold)" }}>Spremite kodove za oporavak</p>
                    <p className="text-sm text-slate-400 leading-relaxed">
                      Ovi kodovi omogućuju pristup računu ako izgubite pristup authenticator aplikaciji. 
                      Svaki kod može se koristiti <strong className="text-slate-300">samo jednom</strong>. 
                      Čuvajte ih na sigurnom mjestu.
                    </p>
                  </div>
                </div>

                {/* Recovery codes grid */}
                <div
                  className="rounded-xl p-5"
                  style={{ background: "rgba(0,0,0,0.2)", border: "1px solid rgba(201, 168, 76, 0.1)" }}
                >
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
                    {recoveryCodes.map((code, i) => (
                      <div
                        key={i}
                        className="rounded-lg px-3 py-2.5 text-center font-mono text-sm tracking-wider"
                        style={{ background: "rgba(201, 168, 76, 0.04)", border: "1px solid rgba(201, 168, 76, 0.1)", color: "var(--auth-gold-light)" }}
                      >
                        {code}
                      </div>
                    ))}
                  </div>
                  <div className="mt-4 flex justify-center">
                    <button
                      onClick={() => copyToClipboard(recoveryCodes.join("\n"), setRecoveryCodesCopied)}
                      className="flex items-center gap-2 text-xs font-medium px-4 py-2 rounded-lg transition-all"
                      style={{
                        background: recoveryCodesCopied ? "rgba(201, 168, 76, 0.12)" : "rgba(201, 168, 76, 0.05)",
                        border: "1px solid rgba(201, 168, 76, 0.15)",
                        color: "var(--auth-gold)",
                      }}
                    >
                      {recoveryCodesCopied ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
                      {recoveryCodesCopied ? "Kopirano u međuspremnik!" : "Kopiraj sve kodove"}
                    </button>
                  </div>
                </div>

                {/* Done button */}
                <button
                  onClick={() => { setStep("idle"); setRecoveryCodes([]); setVerifyToken(""); setSetupData(null); }}
                  className="flex items-center gap-2 text-sm font-bold py-3 px-8 rounded-xl transition-all duration-200"
                  style={{
                    background: "linear-gradient(135deg, var(--auth-gold) 0%, #B8963F 100%)",
                    color: "var(--auth-navy-dark)",
                    boxShadow: "0 4px 20px rgba(201, 168, 76, 0.3)",
                  }}
                >
                  <Check className="h-4 w-4" />
                  Gotovo — kodovi su spremljeni
                </button>
              </div>
            )}

            {/* ══════════ DISABLE 2FA ══════════ */}
            {step === "disable" && (
              <div className="space-y-5">
                <div
                  className="flex items-start gap-4 rounded-xl p-5"
                  style={{ background: "rgba(220, 38, 38, 0.06)", border: "1px solid rgba(220, 38, 38, 0.15)" }}
                >
                  <div
                    className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0"
                    style={{ background: "rgba(220, 38, 38, 0.1)" }}
                  >
                    <AlertTriangle className="h-5 w-5 text-red-400" />
                  </div>
                  <div className="space-y-1">
                    <p className="text-sm font-bold text-red-400">Deaktivacija 2FA</p>
                    <p className="text-sm text-slate-400 leading-relaxed">
                      Nakon deaktivacije, vaš račun neće biti zaštićen dvofaktorskom autentifikacijom. 
                      Unesite trenutni kod iz aplikacije za potvrdu.
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <input
                    type="text"
                    inputMode="numeric"
                    maxLength={6}
                    value={disableToken}
                    onChange={(e) => { setDisableToken(e.target.value.replace(/\D/g, "").slice(0, 6)); setError(null); }}
                    placeholder="000000"
                    className="w-44 text-center rounded-xl px-4 py-3 text-xl font-mono tracking-[0.4em] placeholder:tracking-[0.4em] text-white outline-none transition-all duration-200"
                    style={{
                      background: "rgba(220, 38, 38, 0.04)",
                      border: "2px solid rgba(220, 38, 38, 0.2)",
                    }}
                    autoFocus
                    onKeyDown={(e) => { if (e.key === "Enter" && disableToken.length === 6) handleDisable(); }}
                  />
                  <button
                    onClick={handleDisable}
                    disabled={actionLoading || !disableToken}
                    className="flex items-center gap-2 bg-red-600 hover:bg-red-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-semibold px-6 py-3 rounded-xl transition-all"
                  >
                    {actionLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ShieldOff className="h-4 w-4" />}
                    {actionLoading ? "Deaktiviranje..." : "Deaktiviraj"}
                  </button>
                </div>

                <button
                  onClick={() => { setStep("idle"); setError(null); setSuccess(null); setDisableToken(""); }}
                  className="text-xs text-slate-500 hover:text-slate-300 transition"
                >
                  ← Odustani
                </button>
              </div>
            )}
          </div>
        </section>

        {/* ═══════════ FOOTER COMPLIANCE ═══════════ */}
        <div className="flex flex-col items-center gap-3 pt-4 pb-8">
          <div className="flex items-center gap-4 text-[10px] tracking-wider uppercase" style={{ color: "rgba(201,168,76,0.35)" }}>
            <span>GDPR</span>
            <span style={{ color: "rgba(201,168,76,0.15)" }}>·</span>
            <span>AES-256</span>
            <span style={{ color: "rgba(201,168,76,0.15)" }}>·</span>
            <span>TLS 1.3</span>
            <span style={{ color: "rgba(201,168,76,0.15)" }}>·</span>
            <span>TOTP RFC 6238</span>
          </div>
          <p className="text-[11px] text-slate-600 text-center">
            © {new Date().getFullYear()} PravoTek · Pravna inteligencija za odvjetnike
          </p>
        </div>
      </main>
    </div>
  );
}
