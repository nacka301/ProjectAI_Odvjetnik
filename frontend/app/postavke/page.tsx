"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Shield,
  ShieldCheck,
  ShieldOff,
  Loader2,
  Copy,
  Check,
  AlertTriangle,
  Key,
  Smartphone,
  Lock,
  CheckCircle2,
  QrCode,
  Info,
  User,
  Building2,
  Users,
  KeyRound,
  Clock,
  Database,
} from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { API_BASE } from "@/lib/api";
import PersonalProfileForm from "@/components/settings/PersonalProfileForm";
import LawFirmForm from "@/components/settings/LawFirmForm";
import SubscriptionView from "@/components/settings/SubscriptionView";
import AppHeader from "@/components/AppHeader";
import ActivityLog from "@/components/settings/ActivityLog";
import PrivacyPanel from "@/components/settings/PrivacyPanel";

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

// ── Sidebar Tabs ──
type SettingsTab = "profil" | "sigurnost" | "ured" | "tim" | "pretplata" | "dnevnik" | "privatnost";

const SIDEBAR_ITEMS: { id: SettingsTab; label: string; icon: React.ElementType }[] = [
  { id: "profil",    label: "Osobni profil",      icon: User },
  { id: "sigurnost", label: "Sigurnost",           icon: Shield },
  { id: "ured",      label: "Odvjetnički ured",    icon: Building2 },
  { id: "tim",       label: "Upravljanje timom",   icon: Users },
  { id: "pretplata", label: "Pristup",              icon: KeyRound },
  { id: "dnevnik",   label: "Dnevnik aktivnosti",  icon: Clock },
  { id: "privatnost",label: "Privatnost i podaci", icon: Database },
];

// ── Helpers ──
/**
 * Auth headers for backend calls. Reads the token straight from the session
 * on every call rather than from React state — the 2FA fetches fire right
 * after mount, before a state-based hook would have settled.
 */
async function apiHeaders(
  supabase: ReturnType<typeof createSupabaseBrowser>,
): Promise<Record<string, string>> {
  const { data: { session } } = await supabase.auth.getSession();
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (session) headers["Authorization"] = `Bearer ${session.access_token}`;
  return headers;
}

export default function PostavkePage() {
  const router = useRouter();
  const supabase = useMemo(() => createSupabaseBrowser(), []);

  // ── Tab state ──
  const [activeTab, setActiveTab] = useState<SettingsTab>("sigurnost");

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
      try {
        const { data: { user } } = await supabase.auth.getUser();
        if (!user) {
          router.push("/auth/login");
          return;
        }
        setUserId(user.id);
        setUserEmail(user.email ?? null);
      } catch {
        router.push("/auth/login");
      } finally {
        setLoading(false);
      }
    })();
  }, [supabase, router]);

  // ── Check 2FA status ──
  const checkStatus = useCallback(async () => {
    if (!userId) return;
    setStatusLoading(true);
    try {
      // No userId in the query — the backend answers for the token holder.
      const res = await fetch(`${API_BASE}/api/2fa/status`, {
        headers: await apiHeaders(supabase),
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
  }, [userId, supabase]);

  useEffect(() => {
    if (userId) checkStatus();
  }, [userId, checkStatus]);

  // ── Start setup ──
  const handleStartSetup = async () => {
    setError(null);
    setSuccess(null);
    setActionLoading(true);
    try {
      // Email comes from the token on the backend — nothing to send.
      const res = await fetch(`${API_BASE}/api/2fa/setup`, {
        method: "POST",
        headers: await apiHeaders(supabase),
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
        headers: await apiHeaders(supabase),
        body: JSON.stringify({
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
      setSetupData(null);
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
        headers: await apiHeaders(supabase),
        body: JSON.stringify({ token: disableToken }),
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
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <div className="flex flex-col items-center gap-3">
          <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
          <p className="text-sm text-slate-500">Učitavanje postavki...</p>
        </div>
      </div>
    );
  }

  // ── Placeholder content for non-implemented tabs ──
  const renderPlaceholder = (title: string, description: string) => (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="w-14 h-14 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
        <Lock className="h-6 w-6 text-slate-300" />
      </div>
      <h3 className="font-serif text-lg font-semibold text-slate-800 mb-1">{title}</h3>
      <p className="text-sm text-slate-400 max-w-sm">{description}</p>
      <span className="mt-4 inline-flex items-center text-xs font-medium text-slate-400 bg-slate-100 px-3 py-1 rounded-full">
        Uskoro dostupno
      </span>
    </div>
  );

  return (
    <div className="min-h-screen bg-slate-50">
      {/* ════════════════════ HEADER ════════════════════ */}
      <AppHeader />

      {/* ════════════════════ BODY: SIDEBAR + CONTENT ════════════════════ */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 lg:py-10">
        <div className="flex flex-col lg:flex-row gap-8">

          {/* ── SIDEBAR ── */}
          <aside className="lg:w-56 shrink-0">
            <nav className="flex lg:flex-col gap-1 overflow-x-auto lg:overflow-visible pb-2 lg:pb-0">
              {SIDEBAR_ITEMS.map(({ id, label, icon: Icon }) => {
                const isActive = activeTab === id;
                return (
                  <button
                    key={id}
                    onClick={() => { setActiveTab(id); setError(null); setSuccess(null); }}
                    className={`
                      flex items-center gap-2.5 whitespace-nowrap rounded-lg px-3.5 py-2.5 text-sm font-medium transition-all duration-150
                      ${isActive
                        ? "bg-[#1B2A4A] text-white shadow-sm"
                        : "text-slate-500 hover:text-slate-800 hover:bg-slate-100"
                      }
                    `}
                  >
                    <Icon className={`h-4 w-4 shrink-0 ${isActive ? "text-white/80" : "text-slate-400"}`} />
                    {label}
                  </button>
                );
              })}
            </nav>
          </aside>

          {/* ── MAIN CONTENT PANEL ── */}
          <main className="flex-1 min-w-0">
            <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">

              {/* ═══ TAB: Sigurnost ═══ */}
              {activeTab === "sigurnost" && (
                <>
                  {/* Panel Header */}
                  <div className="px-6 sm:px-8 py-5 border-b border-slate-200 flex items-center gap-4">
                    <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0" style={{ background: "#1B2A4A" }}>
                      <Shield className="h-5 w-5 text-white" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <h2 className="font-serif text-lg font-semibold text-slate-900">Dvofaktorska Autentifikacija</h2>
                      <p className="text-sm text-slate-400 mt-0.5">Zaštitite račun TOTP kodom iz authenticator aplikacije</p>
                    </div>
                    <div className="shrink-0">
                      {statusLoading ? (
                        <Loader2 className="h-5 w-5 animate-spin text-slate-400" />
                      ) : twoFAEnabled ? (
                        <span className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider px-3.5 py-1.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                          <ShieldCheck className="h-3.5 w-3.5" /> Aktivno
                        </span>
                      ) : (
                        <span className="flex items-center gap-1.5 text-xs font-semibold text-slate-400 bg-slate-100 border border-slate-200 px-3.5 py-1.5 rounded-full">
                          <ShieldOff className="h-3.5 w-3.5" /> Neaktivno
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Panel Body */}
                  <div className="px-6 sm:px-8 py-6 space-y-5">

                    {/* Global alerts */}
                    {error && (
                      <div className="flex items-start gap-3 rounded-lg p-4 bg-red-50 border border-red-200">
                        <AlertTriangle className="h-5 w-5 shrink-0 mt-0.5 text-red-500" />
                        <div>
                          <p className="text-sm font-semibold text-red-700">Greška</p>
                          <p className="text-sm text-red-600 mt-0.5">{error}</p>
                        </div>
                      </div>
                    )}

                    {success && (
                      <div className="flex items-start gap-3 rounded-lg p-4 bg-emerald-50 border border-emerald-200">
                        <CheckCircle2 className="h-5 w-5 shrink-0 mt-0.5 text-emerald-600" />
                        <p className="text-sm text-emerald-700 font-medium">{success}</p>
                      </div>
                    )}

                    {/* ══════════ IDLE: 2FA NOT ENABLED ══════════ */}
                    {step === "idle" && !twoFAEnabled && (
                      <div className="space-y-6">
                        {/* Info panel */}
                        <div className="rounded-lg p-5 bg-slate-50 border border-slate-200">
                          <div className="flex items-start gap-4">
                            <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0 mt-0.5 bg-slate-200">
                              <Smartphone className="h-5 w-5 text-slate-600" />
                            </div>
                            <div className="space-y-2">
                              <p className="text-sm font-semibold text-slate-800">Kako funkcionira 2FA?</p>
                              <p className="text-sm text-slate-500 leading-relaxed">
                                Dvofaktorska autentifikacija dodaje dodatni sloj sigurnosti vašem računu. 
                                Nakon aktivacije, uz lozinku trebat ćete unijeti i 6-znamenkasti kod iz aplikacije:
                              </p>
                              <div className="flex flex-wrap gap-2 pt-1">
                                {["Google Authenticator", "Authy", "Microsoft Authenticator"].map(app => (
                                  <span
                                    key={app}
                                    className="inline-flex items-center text-xs font-medium px-2.5 py-1 rounded-md bg-white text-slate-600 border border-slate-200"
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
                              className="flex items-center gap-2.5 rounded-lg p-3 text-xs text-slate-500 bg-slate-50 border border-slate-100"
                            >
                              <Icon className="h-4 w-4 shrink-0 text-slate-400" />
                              <span>{label}</span>
                            </div>
                          ))}
                        </div>

                        {/* CTA Button */}
                        <button
                          onClick={handleStartSetup}
                          disabled={actionLoading}
                          className="w-full sm:w-auto flex items-center justify-center gap-2.5 text-sm font-semibold py-2.5 px-6 rounded-lg transition-colors duration-150 disabled:opacity-50 disabled:cursor-not-allowed text-white hover:opacity-90"
                          style={{ background: "#1B2A4A" }}
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
                        <div className="rounded-lg p-5 bg-emerald-50 border border-emerald-200">
                          <div className="flex items-start gap-4">
                            <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0 bg-emerald-100">
                              <ShieldCheck className="h-5 w-5 text-emerald-600" />
                            </div>
                            <div>
                              <p className="text-sm font-semibold text-emerald-800">
                                Dvofaktorska autentifikacija je aktivna
                              </p>
                              <p className="text-sm text-emerald-600/80 mt-1 leading-relaxed">
                                Vaš račun je zaštićen dodatnim slojem sigurnosti. Pri svakoj prijavi potreban je kod iz vaše authenticator aplikacije.
                              </p>
                            </div>
                          </div>
                        </div>

                        <div className="h-px w-full bg-slate-100" />

                        <button
                          onClick={() => { setStep("disable"); setError(null); setSuccess(null); }}
                          className="flex items-center gap-2 text-sm font-medium px-4 py-2 rounded-lg transition-all text-slate-500 hover:text-red-600 bg-slate-50 border border-slate-200 hover:border-red-200 hover:bg-red-50"
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
                            <div className="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold text-white" style={{ background: "#1B2A4A" }}>1</div>
                            <span className="text-sm font-semibold text-slate-800">Skenirajte QR kod</span>
                          </div>
                          <div className="flex-1 h-px bg-slate-200" />
                          <div className="flex items-center gap-2">
                            <div className="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold bg-slate-100 text-slate-400">2</div>
                            <span className="text-sm text-slate-400">Potvrdite kod</span>
                          </div>
                        </div>

                        {/* QR Card */}
                        <div className="rounded-lg overflow-hidden border border-slate-200">
                          <div className="px-5 py-3 flex items-center gap-2 bg-slate-50 border-b border-slate-200">
                            <QrCode className="h-4 w-4 text-slate-500" />
                            <p className="text-sm font-medium text-slate-700">Skenirajte u authenticator aplikaciji</p>
                          </div>
                          <div className="p-6 flex flex-col items-center gap-5 bg-white">
                            <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
                              <QRCodeSVG
                                value={setupData.otpauth_url}
                                size={200}
                                level="M"
                                includeMargin={false}
                                bgColor="#FFFFFF"
                                fgColor="#1B2A4A"
                              />
                            </div>
                            <p className="text-xs text-slate-400 text-center">
                              Otvorite <strong className="text-slate-600">Google Authenticator</strong> ili <strong className="text-slate-600">Authy</strong> i skenirajte kod
                            </p>
                          </div>
                        </div>

                        {/* Manual key fallback */}
                        <div className="space-y-2">
                          <div className="flex items-center gap-2">
                            <Info className="h-3.5 w-3.5 text-slate-400" />
                            <p className="text-xs text-slate-500">Ne možete skenirati? Unesite ključ ručno:</p>
                          </div>
                          <div className="flex items-center gap-2">
                            <code className="flex-1 rounded-lg px-4 py-2.5 text-sm font-mono tracking-[0.15em] text-center bg-slate-50 border border-slate-200 text-slate-700">
                              {setupData.manual_entry_key}
                            </code>
                            <button
                              onClick={() => copyToClipboard(setupData.manual_entry_key, setCopied)}
                              className="p-2.5 rounded-lg transition-all bg-slate-50 border border-slate-200 hover:bg-slate-100"
                              title="Kopiraj ključ"
                            >
                              {copied
                                ? <Check className="h-4 w-4 text-emerald-600" />
                                : <Copy className="h-4 w-4 text-slate-400" />
                              }
                            </button>
                          </div>
                        </div>

                        {/* Divider */}
                        <div className="flex items-center gap-4">
                          <div className="flex-1 h-px bg-slate-200" />
                          <span className="text-xs uppercase tracking-wider text-slate-300">Potvrda</span>
                          <div className="flex-1 h-px bg-slate-200" />
                        </div>

                        {/* Token input */}
                        <div className="space-y-3">
                          <label className="block text-sm font-medium text-slate-700">
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
                              className="w-44 text-center rounded-lg px-4 py-2.5 text-xl font-mono tracking-[0.4em] placeholder:tracking-[0.4em] outline-none transition-all duration-200 bg-white text-slate-900 border border-slate-300 focus:border-slate-500 focus:ring-1 focus:ring-slate-500"
                              autoFocus
                              onKeyDown={(e) => { if (e.key === "Enter" && verifyToken.length === 6) handleVerify(); }}
                            />
                            <button
                              onClick={handleVerify}
                              disabled={actionLoading || verifyToken.length !== 6}
                              className="flex items-center gap-2 text-sm font-semibold py-2.5 px-6 rounded-lg transition-colors duration-150 disabled:opacity-40 disabled:cursor-not-allowed text-white hover:opacity-90"
                              style={{ background: verifyToken.length === 6 ? "#1B2A4A" : "#94a3b8" }}
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
                          className="text-xs text-slate-400 hover:text-slate-600 transition mt-2"
                        >
                          ← Odustani od aktivacije
                        </button>
                      </div>
                    )}

                    {/* ══════════ RECOVERY CODES ══════════ */}
                    {step === "recovery" && (
                      <div className="space-y-6">
                        {/* Warning */}
                        <div className="flex items-start gap-4 rounded-lg p-5 bg-amber-50 border border-amber-200">
                          <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0 bg-amber-100">
                            <AlertTriangle className="h-5 w-5 text-amber-600" />
                          </div>
                          <div className="space-y-1">
                            <p className="text-sm font-bold text-amber-800">Spremite kodove za oporavak</p>
                            <p className="text-sm text-amber-700/80 leading-relaxed">
                              Ovi kodovi omogućuju pristup računu ako izgubite pristup authenticator aplikaciji. 
                              Svaki kod može se koristiti <strong className="text-amber-800">samo jednom</strong>. 
                              Čuvajte ih na sigurnom mjestu.
                            </p>
                          </div>
                        </div>

                        {/* Recovery codes grid */}
                        <div className="rounded-lg p-5 bg-slate-50 border border-slate-200">
                          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
                            {recoveryCodes.map((code, i) => (
                              <div
                                key={i}
                                className="rounded-md px-3 py-2.5 text-center font-mono text-sm tracking-wider bg-white border border-slate-200 text-slate-700"
                              >
                                {code}
                              </div>
                            ))}
                          </div>
                          <div className="mt-4 flex justify-center">
                            <button
                              onClick={() => copyToClipboard(recoveryCodes.join("\n"), setRecoveryCodesCopied)}
                              className="flex items-center gap-2 text-xs font-medium px-4 py-2 rounded-lg transition-all bg-white border border-slate-200 text-slate-600 hover:bg-slate-100"
                            >
                              {recoveryCodesCopied ? <Check className="h-3.5 w-3.5 text-emerald-600" /> : <Copy className="h-3.5 w-3.5" />}
                              {recoveryCodesCopied ? "Kopirano u međuspremnik!" : "Kopiraj sve kodove"}
                            </button>
                          </div>
                        </div>

                        {/* Done button */}
                        <button
                          onClick={() => { setStep("idle"); setRecoveryCodes([]); setVerifyToken(""); setSetupData(null); }}
                          className="flex items-center gap-2 text-sm font-semibold py-2.5 px-6 rounded-lg transition-colors duration-150 text-white hover:opacity-90"
                          style={{ background: "#1B2A4A" }}
                        >
                          <Check className="h-4 w-4" />
                          Gotovo — kodovi su spremljeni
                        </button>
                      </div>
                    )}

                    {/* ══════════ DISABLE 2FA ══════════ */}
                    {step === "disable" && (
                      <div className="space-y-5">
                        <div className="flex items-start gap-4 rounded-lg p-5 bg-red-50 border border-red-200">
                          <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0 bg-red-100">
                            <AlertTriangle className="h-5 w-5 text-red-500" />
                          </div>
                          <div className="space-y-1">
                            <p className="text-sm font-bold text-red-700">Deaktivacija 2FA</p>
                            <p className="text-sm text-red-600/80 leading-relaxed">
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
                            className="w-44 text-center rounded-lg px-4 py-2.5 text-xl font-mono tracking-[0.4em] placeholder:tracking-[0.4em] text-slate-900 outline-none transition-all duration-200 bg-white border border-slate-300 focus:border-red-400 focus:ring-1 focus:ring-red-400"
                            autoFocus
                            onKeyDown={(e) => { if (e.key === "Enter" && disableToken.length === 6) handleDisable(); }}
                          />
                          <button
                            onClick={handleDisable}
                            disabled={actionLoading || !disableToken}
                            className="flex items-center gap-2 bg-red-600 hover:bg-red-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-semibold px-6 py-2.5 rounded-lg transition-colors"
                          >
                            {actionLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ShieldOff className="h-4 w-4" />}
                            {actionLoading ? "Deaktiviranje..." : "Deaktiviraj"}
                          </button>
                        </div>

                        <button
                          onClick={() => { setStep("idle"); setError(null); setSuccess(null); setDisableToken(""); }}
                          className="text-xs text-slate-400 hover:text-slate-600 transition"
                        >
                          ← Odustani
                        </button>
                      </div>
                    )}
                  </div>
                </>
              )}

              {/* ═══ TAB: Osobni profil ═══ */}
              {activeTab === "profil" && userId && (
                <PersonalProfileForm userId={userId} userEmail={userEmail} />
              )}

              {/* ═══ TAB: Odvjetnički ured ═══ */}
              {activeTab === "ured" && userId && (
                <LawFirmForm userId={userId} />
              )}

              {/* ═══ TAB: Upravljanje timom ═══ */}
              {activeTab === "tim" && renderPlaceholder("Upravljanje timom", "Pozovite kolege, dodijelite uloge i upravljajte pristupom.")}

              {/* ═══ TAB: Pretplata ═══ */}
              {activeTab === "pretplata" && userId && (
                <SubscriptionView userId={userId} />
              )}

              {/* ═══ TAB: Dnevnik aktivnosti ═══ */}
              {activeTab === "dnevnik" && userId && (
                <ActivityLog userId={userId} />
              )}

              {/* ═══ TAB: Privatnost i podaci ═══ */}
              {activeTab === "privatnost" && userId && (
                <PrivacyPanel userEmail={userEmail ?? ""} />
              )}
            </div>

            {/* Footer compliance */}
            <div className="flex flex-col items-center gap-2 pt-6 pb-4">
              <div className="flex items-center gap-4 text-[10px] tracking-wider uppercase text-slate-300">
                <span>GDPR</span>
                <span className="text-slate-200">·</span>
                <span>AES-256</span>
                <span className="text-slate-200">·</span>
                <span>TLS 1.3</span>
                <span className="text-slate-200">·</span>
                <span>TOTP RFC 6238</span>
              </div>
              {/* Pravni dokumenti — da se do njih dođe i iz Postavki, ne samo iz podnožja. */}
              <nav
                aria-label="Pravni dokumenti"
                className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-[11px] text-slate-500"
              >
                <Link href="/uvjeti" className="underline underline-offset-2 hover:text-slate-800">Uvjeti korištenja</Link>
                <Link href="/odgovornost" className="underline underline-offset-2 hover:text-slate-800">Izjava o odgovornosti</Link>
                <Link href="/privatnost" className="underline underline-offset-2 hover:text-slate-800">Politika privatnosti</Link>
                <Link href="/dpa" className="underline underline-offset-2 hover:text-slate-800">Ugovor o obradi podataka</Link>
              </nav>
              <p className="text-[11px] text-slate-400 text-center">
                © {new Date().getFullYear()} PravoTek · Sva prava pridržana.
              </p>
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}
