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
      // Extract base32 secret from manual_entry_key (remove spaces)
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
    if (verifyToken.length !== 6) {
      setError("Unesite 6-znamenkasti kod iz aplikacije.");
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
        setError(data.message || "Nevažeći kod.");
        return;
      }
      setRecoveryCodes(data.recovery_codes ?? []);
      setTwoFAEnabled(true);
      setStep("recovery");
    } catch {
      setError("Greška pri provjeri koda.");
    } finally {
      setActionLoading(false);
    }
  };

  // ── Disable 2FA ──
  const handleDisable = async () => {
    setError(null);
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
        setError(data.message || "Nevažeći kod.");
        return;
      }
      setTwoFAEnabled(false);
      setStep("idle");
      setDisableToken("");
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
    setTimeout(() => setCopiedState(false), 2000);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-slate-500" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950 text-white">
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Link href="/" className="flex items-center gap-2 text-slate-400 hover:text-white transition">
              <ArrowLeft className="h-4 w-4" />
              <span className="text-sm hidden sm:inline">Početna</span>
            </Link>
            <div className="h-5 w-px bg-slate-700" />
            <div className="flex items-center gap-2">
              <Scale className="h-4 w-4 text-blue-400" />
              <h1 className="text-lg font-bold tracking-tight">Postavke</h1>
            </div>
          </div>
          <div className="text-right">
            <p className="text-xs text-slate-500">{userEmail}</p>
          </div>
        </div>
      </header>

      <main className="max-w-4xl mx-auto px-4 sm:px-6 py-8 space-y-8">
        {/* ═══════════ 2FA SECTION ═══════════ */}
        <section className="bg-slate-900/50 border border-slate-800 rounded-2xl overflow-hidden">
          {/* Section header */}
          <div className="px-6 py-5 border-b border-slate-800 flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-blue-600/20 border border-blue-500/30 flex items-center justify-center">
              <Shield className="h-5 w-5 text-blue-400" />
            </div>
            <div>
              <h2 className="text-base font-semibold">Dvofaktorska Autentifikacija (2FA)</h2>
              <p className="text-sm text-slate-400">Zaštitite svoj račun TOTP kodom iz authenticator aplikacije</p>
            </div>
            <div className="ml-auto">
              {statusLoading ? (
                <Loader2 className="h-5 w-5 animate-spin text-slate-500" />
              ) : twoFAEnabled ? (
                <span className="flex items-center gap-1.5 text-xs font-semibold text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 px-3 py-1.5 rounded-full">
                  <ShieldCheck className="h-3.5 w-3.5" /> Aktivno
                </span>
              ) : (
                <span className="flex items-center gap-1.5 text-xs font-semibold text-slate-400 bg-slate-700/30 border border-slate-700 px-3 py-1.5 rounded-full">
                  <ShieldOff className="h-3.5 w-3.5" /> Neaktivno
                </span>
              )}
            </div>
          </div>

          <div className="px-6 py-5 space-y-5">
            {/* Error box */}
            {error && (
              <div className="flex items-start gap-2 bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-sm text-red-400 animate-in fade-in duration-200">
                <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5 text-red-400" />
                <span>{error}</span>
              </div>
            )}

            {/* ── IDLE: Not setup or already enabled ── */}
            {step === "idle" && !twoFAEnabled && (
              <div className="space-y-4">
                <div className="flex items-start gap-3 p-4 bg-blue-500/5 border border-blue-500/10 rounded-xl">
                  <Smartphone className="h-5 w-5 text-blue-400 shrink-0 mt-0.5" />
                  <div className="text-sm text-slate-300 space-y-1">
                    <p className="font-semibold text-white">Kako funkcionira 2FA?</p>
                    <p>Nakon aktivacije, pri svakoj prijavi trebat ćete unijeti 6-znamenkasti kod iz aplikacije kao što je
                      <strong> Google Authenticator</strong>, <strong>Authy</strong> ili <strong>Microsoft Authenticator</strong>.
                    </p>
                  </div>
                </div>
                <button
                  onClick={handleStartSetup}
                  disabled={actionLoading}
                  className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold px-5 py-2.5 rounded-xl transition shadow-lg"
                >
                  {actionLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Key className="h-4 w-4" />}
                  Aktiviraj 2FA
                </button>
              </div>
            )}

            {step === "idle" && twoFAEnabled && (
              <div className="space-y-4">
                <div className="flex items-start gap-3 p-4 bg-emerald-500/5 border border-emerald-500/10 rounded-xl">
                  <Lock className="h-5 w-5 text-emerald-400 shrink-0 mt-0.5" />
                  <div className="text-sm text-slate-300">
                    <p>Dvofaktorska autentifikacija je <strong className="text-emerald-400">aktivna</strong>. Vaš račun je zaštićen dodatnim slojem sigurnosti.</p>
                  </div>
                </div>
                <button
                  onClick={() => { setStep("disable"); setError(null); }}
                  className="flex items-center gap-2 bg-red-600/20 hover:bg-red-600/30 border border-red-500/30 text-red-400 text-sm font-semibold px-5 py-2.5 rounded-xl transition"
                >
                  <ShieldOff className="h-4 w-4" />
                  Deaktiviraj 2FA
                </button>
              </div>
            )}

            {/* ── QR CODE STEP ── */}
            {step === "qr" && setupData && (
              <div className="space-y-5">
                <div className="space-y-3">
                  <p className="text-sm text-slate-300">
                    <strong className="text-white">Korak 1:</strong> Skenirajte QR kod u vašoj authenticator aplikaciji.
                  </p>

                  {/* QR Code — using Google Charts API for simplicity */}
                  <div className="flex justify-center">
                    <div className="bg-white p-4 rounded-2xl shadow-lg">
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img
                        src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(setupData.otpauth_url)}`}
                        alt="2FA QR Code"
                        width={200}
                        height={200}
                        className="block"
                      />
                    </div>
                  </div>

                  {/* Manual key */}
                  <div className="space-y-2">
                    <p className="text-xs text-slate-500">Ili unesite ručno:</p>
                    <div className="flex items-center gap-2">
                      <code className="flex-1 bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm font-mono text-slate-200 tracking-widest text-center">
                        {setupData.manual_entry_key}
                      </code>
                      <button
                        onClick={() => copyToClipboard(setupData.manual_entry_key, setCopied)}
                        className="p-2 rounded-lg bg-slate-800 border border-slate-700 hover:bg-slate-700 transition"
                        title="Kopiraj"
                      >
                        {copied ? <Check className="h-4 w-4 text-emerald-400" /> : <Copy className="h-4 w-4 text-slate-400" />}
                      </button>
                    </div>
                  </div>
                </div>

                <div className="space-y-3">
                  <p className="text-sm text-slate-300">
                    <strong className="text-white">Korak 2:</strong> Unesite 6-znamenkasti kod iz aplikacije za potvrdu.
                  </p>
                  <div className="flex items-center gap-3">
                    <input
                      type="text"
                      inputMode="numeric"
                      maxLength={6}
                      value={verifyToken}
                      onChange={(e) => setVerifyToken(e.target.value.replace(/\D/g, "").slice(0, 6))}
                      placeholder="000000"
                      className="w-40 text-center bg-slate-800 border border-slate-700 rounded-xl px-4 py-2.5 text-lg font-mono tracking-[0.3em] text-white placeholder:text-slate-600 focus:ring-2 focus:ring-blue-500 outline-none transition"
                      autoFocus
                    />
                    <button
                      onClick={handleVerify}
                      disabled={actionLoading || verifyToken.length !== 6}
                      className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold px-5 py-2.5 rounded-xl transition shadow"
                    >
                      {actionLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
                      Potvrdi
                    </button>
                  </div>
                </div>

                <button
                  onClick={() => { setStep("idle"); setError(null); setVerifyToken(""); }}
                  className="text-xs text-slate-500 hover:text-slate-300 transition"
                >
                  ← Odustani
                </button>
              </div>
            )}

            {/* ── RECOVERY CODES STEP ── */}
            {step === "recovery" && (
              <div className="space-y-5">
                <div className="flex items-start gap-3 p-4 bg-amber-500/10 border border-amber-500/20 rounded-xl">
                  <AlertTriangle className="h-5 w-5 text-amber-400 shrink-0 mt-0.5" />
                  <div className="text-sm text-slate-300 space-y-1">
                    <p className="font-semibold text-amber-400">Spremite kodove za oporavak!</p>
                    <p>Ovi kodovi omogućuju pristup računu ako izgubite pristup authenticator aplikaciji. Svaki kod može se koristiti samo jednom.</p>
                  </div>
                </div>

                <div className="bg-slate-800 border border-slate-700 rounded-xl p-4">
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                    {recoveryCodes.map((code, i) => (
                      <div
                        key={i}
                        className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-center font-mono text-sm text-slate-200 tracking-wider"
                      >
                        {code}
                      </div>
                    ))}
                  </div>
                  <div className="mt-3 flex justify-center">
                    <button
                      onClick={() => copyToClipboard(recoveryCodes.join("\n"), setRecoveryCodesCopied)}
                      className="flex items-center gap-2 text-xs text-slate-400 hover:text-white transition px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-700"
                    >
                      {recoveryCodesCopied ? <Check className="h-3.5 w-3.5 text-emerald-400" /> : <Copy className="h-3.5 w-3.5" />}
                      {recoveryCodesCopied ? "Kopirano!" : "Kopiraj sve kodove"}
                    </button>
                  </div>
                </div>

                <button
                  onClick={() => { setStep("idle"); setRecoveryCodes([]); setVerifyToken(""); setSetupData(null); }}
                  className="flex items-center gap-2 bg-slate-700 hover:bg-slate-600 text-white text-sm font-semibold px-5 py-2.5 rounded-xl transition"
                >
                  <Check className="h-4 w-4" />
                  Gotovo — kodovi su spremljeni
                </button>
              </div>
            )}

            {/* ── DISABLE STEP ── */}
            {step === "disable" && (
              <div className="space-y-4">
                <div className="flex items-start gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-xl">
                  <AlertTriangle className="h-5 w-5 text-red-400 shrink-0 mt-0.5" />
                  <div className="text-sm text-slate-300 space-y-1">
                    <p className="font-semibold text-red-400">Deaktivacija 2FA</p>
                    <p>Unesite trenutni 6-znamenkasti kod iz authenticator aplikacije za potvrdu deaktivacije.</p>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <input
                    type="text"
                    inputMode="numeric"
                    maxLength={6}
                    value={disableToken}
                    onChange={(e) => setDisableToken(e.target.value.replace(/\D/g, "").slice(0, 6))}
                    placeholder="000000"
                    className="w-40 text-center bg-slate-800 border border-slate-700 rounded-xl px-4 py-2.5 text-lg font-mono tracking-[0.3em] text-white placeholder:text-slate-600 focus:ring-2 focus:ring-red-500 outline-none transition"
                    autoFocus
                  />
                  <button
                    onClick={handleDisable}
                    disabled={actionLoading || !disableToken}
                    className="flex items-center gap-2 bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold px-5 py-2.5 rounded-xl transition"
                  >
                    {actionLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ShieldOff className="h-4 w-4" />}
                    Deaktiviraj
                  </button>
                </div>

                <button
                  onClick={() => { setStep("idle"); setError(null); setDisableToken(""); }}
                  className="text-xs text-slate-500 hover:text-slate-300 transition"
                >
                  ← Odustani
                </button>
              </div>
            )}
          </div>
        </section>
      </main>
    </div>
  );
}
