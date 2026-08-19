"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Script from "next/script";
import { useRouter } from "next/navigation";
import { Info, Loader2 } from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { generateNonce, type NoncePair } from "@/lib/google-nonce";

/**
 * Google sign-in, without the trip through supabase.co.
 *
 * `signInWithOAuth` hands the browser to Supabase, which hands it to Google —
 * so Google names the origin it will return to, and that origin is
 * `<project-ref>.supabase.co`. A lawyer deciding whether to trust this app with
 * client files reads a random string at exactly the wrong moment.
 *
 * Google Identity Services inverts that: Google renders here, on pravotek.com,
 * hands back a signed ID token, and the token goes to Supabase over the
 * existing session machinery. Supabase never appears in the address bar.
 *
 * Two things keep this from being a downgrade in reliability:
 *
 *   • The visible button is not optional. One Tap is suppressed after a few
 *     dismissals, blocked in some browsers, and absent in private windows —
 *     shipping only One Tap means some users find no way in at all.
 *
 *   • If Google's script does not load (blocked, offline, corporate proxy), the
 *     button falls back to `signInWithOAuth`. That flow does show supabase.co,
 *     which is the whole thing we set out to remove — but a rare bad first
 *     impression beats a button that does nothing.
 *
 * With no `NEXT_PUBLIC_GOOGLE_CLIENT_ID` set, this renders exactly the old
 * button and the old flow. Deploying without the variable cannot break sign-in.
 */

const GSI_SRC = "https://accounts.google.com/gsi/client";

/** How long to wait for Google's script before giving up on it. */
const SCRIPT_TIMEOUT_MS = 4000;

/** Google refuses widths above this, and looks cramped below the lower bound. */
const BUTTON_MIN_WIDTH = 200;
const BUTTON_MAX_WIDTH = 400;

// ── Minimal shape of the global Google exposes. Only what is called here. ──
interface GoogleIdConfig {
  client_id: string;
  callback: (response: { credential: string }) => void;
  nonce: string;
  auto_select: boolean;
  cancel_on_tap_outside: boolean;
  use_fedcm_for_prompt: boolean;
}

interface GoogleButtonOptions {
  type: "standard";
  theme: "outline";
  size: "large";
  shape: "pill";
  text: "signin_with" | "signup_with";
  logo_alignment: "center";
  locale: string;
  width: number;
}

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: GoogleIdConfig) => void;
          renderButton: (parent: HTMLElement, options: GoogleButtonOptions) => void;
          prompt: () => void;
          cancel: () => void;
        };
      };
    };
  }
}

/** `loading` is the brief window before we know which button we can offer. */
type Phase = "loading" | "ready" | "failed";

interface GoogleSignInProps {
  /** Only changes the wording on the button. Both modes sign in the same way. */
  mode: "login" | "register";
  /** Where to land afterwards. Already passed through `safeNextPath`. */
  nextPath: string;
  /** Errors surface in the parent's card, so all failures look the same. */
  onError: (message: string) => void;
  /** Lets the parent lock its own form while a Google sign-in is in flight. */
  onBusyChange?: (busy: boolean) => void;
  /** Parent is busy with something else. */
  disabled?: boolean;
}

// ── The hand-built button, used for the fallback flow only ──
function GoogleLogo() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24">
      <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/>
      <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
      <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
      <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
    </svg>
  );
}

export default function GoogleSignIn({
  mode,
  nextPath,
  onError,
  onBusyChange,
  disabled = false,
}: GoogleSignInProps) {
  const router = useRouter();
  const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

  // No client id configured — there is nothing to initialise, so skip straight
  // to the flow that has always worked.
  const [phase, setPhase] = useState<Phase>(clientId ? "loading" : "failed");
  const [busy, setBusy] = useState(false);

  const buttonHostRef = useRef<HTMLDivElement>(null);
  const nonceRef = useRef<NoncePair | null>(null);
  const [nonceReady, setNonceReady] = useState(false);
  const [scriptReady, setScriptReady] = useState(false);
  /** Guards against a second `initialize()` if React re-runs the effect. */
  const initialisedRef = useRef(false);

  const label = mode === "register" ? "Registracija putem Googlea" : "Prijava putem Googlea";

  const setBusyState = useCallback(
    (value: boolean) => {
      setBusy(value);
      onBusyChange?.(value);
    },
    [onBusyChange],
  );

  // ── Mint the nonce once, up front ──
  // Regenerating it between `initialize()` and the credential callback would
  // leave Google's token carrying a hash of a nonce nobody holds any more.
  useEffect(() => {
    if (!clientId) return;

    let cancelled = false;
    generateNonce()
      .then((pair) => {
        if (cancelled) return;
        nonceRef.current = pair;
        setNonceReady(true);
      })
      .catch(() => {
        // No nonce means no replay protection, and signing in without it is not
        // a trade worth making silently. Fall back instead.
        if (!cancelled) setPhase("failed");
      });

    return () => {
      cancelled = true;
    };
  }, [clientId]);

  // ── Give up on Google's script after a bounded wait ──
  // `onError` from <Script> covers an outright network failure, but a proxy
  // that stalls the request forever produces no event at all, and the user is
  // left looking at a placeholder.
  useEffect(() => {
    if (!clientId) return;

    const timer = setTimeout(() => {
      if (!initialisedRef.current) setPhase("failed");
    }, SCRIPT_TIMEOUT_MS);

    return () => clearTimeout(timer);
  }, [clientId]);

  // ── The credential callback, kept in a ref ──
  // GIS captures whatever function was passed to `initialize()`. Holding the
  // live one in a ref means `nextPath` and `onError` stay current without
  // re-initialising Google on every parent render.
  const handleCredentialRef = useRef<(credential: string) => void>(() => {});

  handleCredentialRef.current = async (credential: string) => {
    const nonce = nonceRef.current;
    if (!nonce) {
      onError("Prijava putem Googlea trenutno nije dostupna. Pokušajte ponovo.");
      return;
    }

    setBusyState(true);
    try {
      const supabase = createSupabaseBrowser();
      // RAW nonce here — Supabase hashes it itself and compares the result
      // against the claim inside the token, which carries the HASHED one.
      const { error } = await supabase.auth.signInWithIdToken({
        provider: "google",
        token: credential,
        nonce: nonce.raw,
      });

      if (error) {
        onError("Prijava putem Googlea nije uspjela. Pokušajte ponovo.");
        setBusyState(false);
        return;
      }

      // Same two steps the password sign-in takes: navigate, then let the
      // server re-read the freshly written session cookies. Without the
      // refresh the first render still comes from the signed-out cache.
      router.push(nextPath);
      router.refresh();
    } catch {
      onError("Greška pri povezivanju sa serverom. Pokušajte ponovo.");
      setBusyState(false);
    }
  };

  // ── Initialise Google, draw its button, then offer One Tap ──
  useEffect(() => {
    if (!clientId || !scriptReady || !nonceReady || initialisedRef.current) return;

    const host = buttonHostRef.current;
    const google = window.google;
    if (!host || !google?.accounts?.id) {
      setPhase("failed");
      return;
    }

    try {
      google.accounts.id.initialize({
        client_id: clientId,
        // HASHED nonce here — it is what Google signs into the token.
        nonce: nonceRef.current!.hashed,
        callback: (response) => handleCredentialRef.current(response.credential),
        // Signing a lawyer in on page load, with no deliberate act, is not a
        // decision this app gets to make for them.
        auto_select: false,
        cancel_on_tap_outside: true,
        // Chrome is removing third-party cookies; without FedCM One Tap simply
        // stops appearing there.
        use_fedcm_for_prompt: true,
      });

      // Google caps the width at 400 and measures in pixels, so the button is
      // sized from its container rather than by CSS.
      const measured = host.clientWidth || BUTTON_MAX_WIDTH;
      google.accounts.id.renderButton(host, {
        type: "standard",
        theme: "outline",
        size: "large",
        shape: "pill",
        text: mode === "register" ? "signup_with" : "signin_with",
        logo_alignment: "center",
        locale: "hr",
        width: Math.min(BUTTON_MAX_WIDTH, Math.max(BUTTON_MIN_WIDTH, measured)),
      });

      initialisedRef.current = true;
      setPhase("ready");

      // One Tap comes after the button exists, never instead of it.
      google.accounts.id.prompt();
    } catch {
      setPhase("failed");
    }

    return () => {
      window.google?.accounts?.id?.cancel();
    };
  }, [clientId, scriptReady, nonceReady, mode]);

  // ── Fallback: the original redirect flow ──
  const handleFallback = async () => {
    setBusyState(true);
    try {
      const supabase = createSupabaseBrowser();
      const { error } = await supabase.auth.signInWithOAuth({
        provider: "google",
        options: {
          redirectTo: `${process.env.NEXT_PUBLIC_SITE_URL || window.location.origin}/auth/callback?next=${encodeURIComponent(nextPath)}`,
        },
      });
      if (error) throw error;
      // On success the browser leaves this page; busy stays true deliberately.
    } catch {
      onError(
        mode === "register"
          ? "Greška pri registraciji putem Googlea."
          : "Greška pri prijavi putem Googlea.",
      );
      setBusyState(false);
    }
  };

  const isBlocked = disabled || busy;

  return (
    <div>
      {clientId && (
        <Script
          src={GSI_SRC}
          strategy="afterInteractive"
          onReady={() => setScriptReady(true)}
          onError={() => setPhase("failed")}
        />
      )}

      {/* Google's own button. Kept mounted once initialised — GIS draws into
          this node and does not survive being unmounted and recreated. */}
      <div
        className={
          phase === "ready"
            ? `flex justify-center transition-opacity ${isBlocked ? "opacity-50 pointer-events-none" : ""}`
            : "hidden"
        }
      >
        <div ref={buttonHostRef} className="w-full" />
      </div>

      {/* Placeholder while Google may still answer. 40px is what `size: "large"`
          actually measures, so the card does not jump when the real button
          replaces this. */}
      {phase === "loading" && (
        <div className="w-full h-10 rounded-full bg-slate-100 animate-pulse" />
      )}

      {phase === "failed" && (
        <>
          <div
            className="flex items-start gap-2 rounded-xl p-3 text-sm mb-3"
            style={{ background: "var(--auth-gold-muted)" }}
          >
            <Info className="h-4 w-4 shrink-0 mt-0.5" style={{ color: "var(--auth-gold)" }} />
            <span className="text-slate-600">
              Googleova prijava učitat će se preko posrednika.
            </span>
          </div>

          <button
            onClick={handleFallback}
            disabled={isBlocked}
            className="w-full flex items-center justify-center gap-3 bg-white border border-slate-200 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed text-sm font-medium text-slate-700 py-2.5 rounded-xl transition shadow-sm"
          >
            {busy ? <Loader2 className="h-5 w-5 animate-spin text-slate-400" /> : <GoogleLogo />}
            {label}
          </button>
        </>
      )}

      {/* The Google-drawn button carries no busy state of its own. */}
      {phase === "ready" && busy && (
        <div className="flex items-center justify-center gap-2 mt-2 text-xs text-slate-400">
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
          Prijavljivanje...
        </div>
      )}
    </div>
  );
}
