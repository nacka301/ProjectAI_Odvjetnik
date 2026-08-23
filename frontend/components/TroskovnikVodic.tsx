"use client";

import { useCallback, useEffect, useRef, useState, type CSSProperties } from "react";
import { createPortal } from "react-dom";
import { ArrowRight, Compass, X } from "lucide-react";
import { markOnboarded } from "@/lib/onboarding";

/**
 * The introduction, moved to where the work is.
 *
 * There was already a six-step tour, on the dashboard, pointing at the six
 * module cards. That teaches someone the menu. A lawyer opening this app for the
 * first time does not need to be told that a card labelled "Odvjetnički
 * Troškovnik" opens the troškovnik — they need to get one produced, once,
 * without guessing which of the forty fields on this screen are the ones that
 * matter.
 *
 * Hence four steps, on the real form, with the real data in it. The user types
 * into the actual fields as they go; nothing here is a mock-up, and whatever
 * they enter during the walkthrough is the troškovnik they end up with.
 *
 * The card is positioned relative to whichever field it is explaining — above
 * or below, whichever side has room — instead of a fixed spot on the screen,
 * because the four targets sit at very different heights on a long form. It is
 * portaled to `document.body` (same pattern as `MethodologyModal`) so it always
 * paints above the rest of the page regardless of stacking contexts introduced
 * elsewhere on this form (sticky header, sticky summary sidebar, dropdowns).
 */

export interface VodicKorak {
  /** DOM id of the element to ring. */
  target: string;
  title: string;
  text: string;
}

export const TROSKOVNIK_KORACI: VodicKorak[] = [
  {
    target: "vodic-vps",
    title: "1/4 · Vrijednost predmeta spora",
    text:
      "Upišite vrijednost spora u eurima. Iz nje se izvodi osnovica u bodovima — " +
      "sve nagrade u nastavku računaju se iz tog broja. U kaznenom predmetu ovo " +
      "polje ne postoji; tamo se polazi od zaprijećene kazne.",
  },
  {
    target: "vodic-radnje",
    title: "2/4 · Odaberite radnje",
    text:
      "Pretražite tarifu i dodajte radnje koje ste obavili — tužba, ročište, žalba. " +
      "Ista se stavka smije dodati više puta, svaka sa svojim datumom. Bodovi se " +
      "zbrajaju sami, ne morate ih tražiti u Tarifi.",
  },
  {
    target: "vodic-stranke",
    title: "3/4 · Stranke i PDV",
    text:
      "Otvorite ovaj odjeljak ako troškovnik ide na ispis — podaci suda, stranke i " +
      "protustranke idu u zaglavlje. Prekidač „U sustavu PDV-a\" desno određuje " +
      "obračunava li se PDV na nagradu.",
  },
  {
    target: "vodic-rezultat",
    title: "4/4 · Rezultat",
    text:
      "Iznos se osvježava dok unosite. Kad ste gotovi, troškovnik se sprema uz " +
      "stranku i predmet, i izvozi u PDF. Ovime je vodič gotov — otvorite ga opet " +
      "gumbom „Vodič\" u zaglavlju.",
  },
];

/**
 * Classes for the element the current step points at.
 *
 * z-[210] must stay above the overlay's own z-[200] (see below) so the ringed
 * element's opaque background shows through instead of the translucent
 * backdrop — that is how the "cut-out" over the highlighted field works.
 */
export function highlightClasses(active: boolean): string {
  return active ? "ring-2 ring-indigo-500 ring-offset-2 relative z-[210]" : "";
}

interface Props {
  /**
   * Controlled by the page: `?vodic=1` from the welcome screen, the header
   * "Vodič" button, or the answer to "has this user ever been through it".
   */
  open: boolean;
  /** Called when the user finishes or skips. */
  onClose: () => void;
  /** Called on every step change so the page can ring the right element. */
  onStepChange: (targetId: string | null) => void;
}

// ── Positioning ──
//
// The card sits above or below the ringed target, whichever side has more
// room, clamped so it always fits inside the viewport — it must never overlap
// the target and never clip its own content. Recomputed on every scroll and
// resize while a step is open (not just once on step change), because the
// target keeps moving while `scrollIntoView`'s smooth-scroll animation runs;
// it converges on the right spot once scrolling settles.

interface VodicPosition {
  /** Set when the card sits below the target; mutually exclusive with `bottom`. */
  top?: number;
  /** Set when the card sits above the target; mutually exclusive with `top`. */
  bottom?: number;
  left: number;
  width: number;
  maxHeight: number;
}

const MARGIN = 16; // gap from the viewport edge — matches the old w-[calc(100%-2rem)]
const GAP = 12; // gap between the card and the ringed target
const CARD_WIDTH = 420; // matches the old max-w-[420px]
const EPSILON = 0.5; // px — below this, treat two positions as unchanged

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function samePosition(a: VodicPosition | null, b: VodicPosition): boolean {
  if (!a) return false;
  return (
    Math.abs((a.top ?? -1) - (b.top ?? -1)) < EPSILON &&
    Math.abs((a.bottom ?? -1) - (b.bottom ?? -1)) < EPSILON &&
    Math.abs(a.left - b.left) < EPSILON &&
    Math.abs(a.width - b.width) < EPSILON &&
    Math.abs(a.maxHeight - b.maxHeight) < EPSILON
  );
}

export default function TroskovnikVodic({ open, onClose, onStepChange }: Props) {
  const [step, setStep] = useState(0);
  // Portals need a DOM to attach to — `false` during SSR, flipped after mount,
  // same guard as MethodologyModal.
  const [mounted, setMounted] = useState(false);
  const [position, setPosition] = useState<VodicPosition | null>(null);
  const cardRef = useRef<HTMLDivElement>(null);
  const rafRef = useRef<number | null>(null);

  useEffect(() => {
    setMounted(true);
  }, []);

  const korak = TROSKOVNIK_KORACI[step];

  const recomputePosition = useCallback(() => {
    if (!open) return;
    const target = document.getElementById(korak.target);
    const rect = target?.getBoundingClientRect();
    // Target not mounted (yet) or momentarily zero-sized — keep the last known
    // position instead of snapping the card to the origin.
    if (!rect || (rect.width === 0 && rect.height === 0)) return;

    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const width = Math.min(vw - 2 * MARGIN, CARD_WIDTH);
    const cardHeight = cardRef.current?.offsetHeight ?? 220;

    const spaceBelow = vh - rect.bottom - GAP - MARGIN;
    const spaceAbove = rect.top - GAP - MARGIN;

    let next: VodicPosition;
    if (spaceBelow >= cardHeight) {
      const top = rect.bottom + GAP;
      next = { top, left: 0, width, maxHeight: vh - top - MARGIN };
    } else if (spaceAbove >= cardHeight) {
      const bottom = vh - rect.top + GAP;
      next = { bottom, left: 0, width, maxHeight: spaceAbove };
    } else {
      // Neither side fits the card at its natural height — the target itself
      // is close to the full viewport height (e.g. the VPS card). Use
      // whichever side has more room and let the body text scroll internally
      // instead of clipping off-screen.
      const maxHeight = Math.max(120, Math.max(spaceBelow, spaceAbove));
      next =
        spaceBelow >= spaceAbove
          ? { top: rect.bottom + GAP, left: 0, width, maxHeight }
          : { bottom: vh - rect.top + GAP, left: 0, width, maxHeight };
    }

    const center = rect.left + rect.width / 2;
    next.left = clamp(center - width / 2, MARGIN, Math.max(MARGIN, vw - width - MARGIN));

    setPosition((prev) => (samePosition(prev, next) ? prev : next));
  }, [open, korak]);

  const scheduleRecompute = useCallback(() => {
    if (rafRef.current !== null) return;
    rafRef.current = requestAnimationFrame(() => {
      rafRef.current = null;
      recomputePosition();
    });
  }, [recomputePosition]);

  // Keep the position fresh for as long as a step is open: on the card's own
  // size changing, on the target's size changing (e.g. the user expands the
  // "Stranke" accordion mid-step), and on scroll/resize/orientation change.
  useEffect(() => {
    // No listeners to attach while closed. The stale `position` from the last
    // time this was open is left as-is rather than reset here — resetting
    // would mean calling setState synchronously in an effect body, and it's
    // harmless anyway: `!open` already short-circuits rendering below, and
    // reopening recomputes within a frame regardless of what it starts from.
    if (!open) return;
    scheduleRecompute();

    const targetEl = document.getElementById(korak.target);
    const cardObserver = new ResizeObserver(scheduleRecompute);
    if (cardRef.current) cardObserver.observe(cardRef.current);
    const targetObserver = targetEl ? new ResizeObserver(scheduleRecompute) : null;
    if (targetEl && targetObserver) targetObserver.observe(targetEl);

    window.addEventListener("scroll", scheduleRecompute, { passive: true });
    window.addEventListener("resize", scheduleRecompute);
    window.addEventListener("orientationchange", scheduleRecompute);

    return () => {
      window.removeEventListener("scroll", scheduleRecompute);
      window.removeEventListener("resize", scheduleRecompute);
      window.removeEventListener("orientationchange", scheduleRecompute);
      cardObserver.disconnect();
      targetObserver?.disconnect();
      if (rafRef.current !== null) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
    };
  }, [open, step, korak, scheduleRecompute]);

  // Tell the page which element to ring, and scroll it into view.
  useEffect(() => {
    const target = open ? korak.target : null;
    onStepChange(target);

    if (!target) return;
    // Small delay so the ring has painted before we measure the element —
    // also gives `recomputePosition` a second try once the target is settled.
    const timer = setTimeout(() => {
      const el = document.getElementById(target);
      if (!el) return;
      // A target taller than ~60% of the viewport (e.g. the VPS card) leaves
      // near-equal slivers of space above and below when centered — exactly
      // the pinch that used to clip the card. Scrolling it to the top instead
      // frees up room below, under the header, for the card to sit normally.
      const rect = el.getBoundingClientRect();
      const block: ScrollLogicalPosition = rect.height > window.innerHeight * 0.6 ? "start" : "center";
      el.scrollIntoView({ behavior: "smooth", block });
      scheduleRecompute();
    }, 80);
    return () => clearTimeout(timer);
  }, [open, step, korak, onStepChange, scheduleRecompute]);

  /** Finishing and skipping are the same instruction: do not ask me again. */
  const close = useCallback(() => {
    setStep(0);
    onStepChange(null);
    void markOnboarded();
    onClose();
  }, [onStepChange, onClose]);

  const next = useCallback(() => {
    if (step < TROSKOVNIK_KORACI.length - 1) setStep((s) => s + 1);
    else close();
  }, [step, close]);

  if (!open || !mounted) return null;

  const cardStyle: CSSProperties = position
    ? {
        position: "fixed",
        left: position.left,
        width: position.width,
        maxHeight: Math.min(position.maxHeight, window.innerHeight - 2 * MARGIN),
        ...(position.top !== undefined ? { top: position.top } : { bottom: position.bottom }),
      }
    : // First frame, before the target has been measured — render off-screen
      // rather than flashing at the wrong spot.
      { position: "fixed", left: -9999, top: -9999, width: CARD_WIDTH };

  const overlay = (
    <div className="fixed inset-0 z-[200] pointer-events-none print:hidden">
      {/* Backdrop. Clicking it skips, same as the text button — a user who wants
          out should not have to find the small link. Inline rgba instead of the
          Tailwind opacity-modifier class, which renders as a color-mix() in
          Tailwind v4 — sidesteps a paint issue that made the dimming invisible
          in production for reasons that couldn't be pinned down from the
          source alone. */}
      <div
        className="absolute inset-0 pointer-events-auto"
        style={{ backgroundColor: "rgba(15, 23, 42, 0.5)" }}
        onClick={close}
      />

      {/*
        `cardStyle.maxHeight` is a real pixel number (not a percentage), so it
        has to live on this element directly rather than a percentage
        `max-height` on a nested child — a percentage height only resolves
        against an ancestor with an explicit (non-auto) height, which this
        positioned-but-otherwise-auto-sized wrapper doesn't have.
      */}
      <div
        ref={cardRef}
        className="pointer-events-auto bg-white rounded-xl shadow-2xl border border-slate-200 p-4 sm:p-5 flex flex-col"
        style={cardStyle}
      >
        <div className="flex items-start justify-between gap-3 mb-2 shrink-0">
          <div className="flex items-center gap-2">
            <Compass className="h-4 w-4 text-indigo-600" />
            <span className="text-sm font-bold text-slate-900">{korak.title}</span>
          </div>
          <button
            onClick={close}
            aria-label="Zatvori vodič"
            className="text-slate-300 hover:text-slate-500 transition -mt-0.5"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Only this scrolls, if it ever has to — the title and the buttons
            below never do, so they can never be the part that's clipped. */}
        <div className="flex-1 min-h-0 overflow-y-auto mb-4">
          <p className="text-sm text-slate-600 leading-relaxed">{korak.text}</p>
        </div>

        <div className="flex items-center justify-between shrink-0">
          <button
            onClick={close}
            className="text-xs text-slate-400 hover:text-slate-600 transition"
          >
            Preskoči vodič
          </button>
          <div className="flex items-center gap-3">
            <div className="flex gap-1">
              {TROSKOVNIK_KORACI.map((k, i) => (
                <div
                  key={k.target}
                  className={`w-1.5 h-1.5 rounded-full transition ${
                    i === step ? "bg-indigo-600" : "bg-slate-200"
                  }`}
                />
              ))}
            </div>
            <button
              onClick={next}
              className="inline-flex items-center gap-1 bg-indigo-600 text-white text-xs font-medium px-4 py-2 rounded-lg hover:bg-indigo-700 transition"
            >
              {step < TROSKOVNIK_KORACI.length - 1 ? "Dalje" : "Završi"}
              <ArrowRight className="h-3 w-3" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );

  // Portal to document.body — the same escape hatch MethodologyModal uses —
  // so the overlay always paints above the rest of the page regardless of
  // stacking contexts introduced by ancestors in the normal page tree.
  return createPortal(overlay, document.body);
}
