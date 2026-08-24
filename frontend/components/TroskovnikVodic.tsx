"use client";

import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
} from "react";
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
// The card is placed against the ringed target on whichever side actually has
// room for it at its natural height: below, above, right, or left. The
// horizontal sides matter more than they look — the tall targets on this form
// (the VPS card, the result sidebar) sit in one column of a three-column grid,
// so on a 700px-high window there is no vertical room for the card but plenty
// beside them. Trying to force a vertical placement there is what used to
// squeeze the card until its own text was cut off.
//
// The card is never shrunk below its natural height to make a placement
// "work"; the only height bound is the viewport itself.

interface VodicPosition {
  top: number;
  left: number;
  width: number;
  /** Only ever binds if the viewport itself is shorter than the card. */
  maxHeight: number;
}

const MARGIN = 16; // gap from the viewport edge — matches the old w-[calc(100%-2rem)]
const GAP = 12; // gap between the card and the ringed target (clears ring-offset-2)
const CARD_WIDTH = 420; // matches the old max-w-[420px]
const MIN_SIDE_WIDTH = 280; // narrowest the card may shrink to fit beside a target
const HEADER_SAFE = 76; // the page's sticky header, so the target parks below it
const ALIGN_TOLERANCE = 24; // px of scroll drift accepted before correcting
const FALLBACK_HEIGHT = 220; // only until the card has been measured once
const EPSILON = 0.5; // px — below this, treat two positions as unchanged

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function samePosition(a: VodicPosition | null, b: VodicPosition): boolean {
  if (!a) return false;
  return (
    Math.abs(a.top - b.top) < EPSILON &&
    Math.abs(a.left - b.left) < EPSILON &&
    Math.abs(a.width - b.width) < EPSILON &&
    Math.abs(a.maxHeight - b.maxHeight) < EPSILON
  );
}

interface Viewport {
  width: number;
  height: number;
}

/**
 * Where the card goes, given the ringed element and the card's natural size.
 *
 * Tries below → above → right → left and takes the first side the card fits on
 * whole, narrowing itself (never shortening) to use a tight side strip. Only a
 * target that cannot share the screen with the card at all — measured on a
 * 390×640 phone against the VPS card — falls through to the last resort below.
 */
export function computeVodicPosition(
  target: { top: number; bottom: number; left: number; right: number },
  card: { width: number; height: number },
  viewport: Viewport,
): VodicPosition {
  const { width: vw, height: vh } = viewport;
  const { width: wFull, height: h } = card;

  const roomBelow = vh - target.bottom - GAP - MARGIN;
  const roomAbove = target.top - GAP - MARGIN;
  const roomRight = vw - target.right - GAP - MARGIN;
  const roomLeft = target.left - GAP - MARGIN;

  const finish = (p: { top: number; left: number; width: number }): VodicPosition => ({
    top: clamp(p.top, MARGIN, Math.max(MARGIN, vh - h - MARGIN)),
    left: clamp(p.left, MARGIN, Math.max(MARGIN, vw - p.width - MARGIN)),
    width: p.width,
    maxHeight: vh - 2 * MARGIN,
  });

  // Below and above keep the full width, centred on the target.
  const centredLeft = clamp(
    (target.left + target.right) / 2 - wFull / 2,
    MARGIN,
    Math.max(MARGIN, vw - wFull - MARGIN),
  );
  if (roomBelow >= h) return finish({ top: target.bottom + GAP, left: centredLeft, width: wFull });
  if (roomAbove >= h) return finish({ top: target.top - GAP - h, left: centredLeft, width: wFull });

  // Beside it. A side strip is often narrower than the card's preferred width
  // (a 1024px-wide window leaves ~340px next to the form column), so the card
  // narrows to fit rather than being pushed on top of the target — it grows
  // taller instead, and there is always vertical room when we get here.
  const centredTop = clamp(
    (target.top + target.bottom) / 2 - h / 2,
    MARGIN,
    Math.max(MARGIN, vh - h - MARGIN),
  );
  if (roomRight >= MIN_SIDE_WIDTH) {
    const width = Math.min(wFull, roomRight);
    return finish({ top: centredTop, left: target.right + GAP, width });
  }
  if (roomLeft >= MIN_SIDE_WIDTH) {
    const width = Math.min(wFull, roomLeft);
    return finish({ top: centredTop, left: target.left - GAP - width, width });
  }

  // Nothing clears the target on any side: target + card exceed the viewport
  // however they are arranged, so something has to give. The card stays whole
  // and readable, pinned to the bottom edge — and because `targetOffset` parks
  // the target against the header rather than centring it, what the card covers
  // is the target's lower end, while the labelled field the step is actually
  // about stays visible above it.
  return finish({ top: vh - MARGIN - h, left: centredLeft, width: wFull });
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
    // Whether a DOM exists to portal into is not knowable during render — the
    // same mount guard MethodologyModal uses.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
  }, []);

  const korak = TROSKOVNIK_KORACI[step];

  const recomputePosition = useCallback(() => {
    if (!open) return;
    const rect = document.getElementById(korak.target)?.getBoundingClientRect();
    // Target not mounted (yet) or momentarily zero-sized — keep the last known
    // position instead of snapping the card to the origin.
    if (!rect || (rect.width === 0 && rect.height === 0)) return;

    // clientWidth/Height, not innerWidth/Height: the latter count the
    // scrollbar, which would let the card sit underneath it.
    const viewport = {
      width: document.documentElement.clientWidth,
      height: document.documentElement.clientHeight,
    };
    const next = computeVodicPosition(
      rect,
      {
        width: Math.min(viewport.width - 2 * MARGIN, CARD_WIDTH),
        // The card is never height-clamped below its natural size, so this
        // measurement stays honest — it can't feed back on itself.
        height: cardRef.current?.offsetHeight ?? FALLBACK_HEIGHT,
      },
      viewport,
    );

    setPosition((prev) => (samePosition(prev, next) ? prev : next));
  }, [open, korak]);

  // rAF only coalesces the high-frequency sources (scroll, resize). Nothing
  // depends on a frame ever being served: every source that must be correct
  // even in a background tab calls `recomputePosition` directly.
  const scheduleRecompute = useCallback(() => {
    if (rafRef.current !== null) return;
    rafRef.current = requestAnimationFrame(() => {
      rafRef.current = null;
      recomputePosition();
    });
  }, [recomputePosition]);

  // Position before the browser paints, so the card never shows up at a stale
  // spot. `mounted` is in the deps because the card only exists in the DOM
  // once the portal is live — that is the first render where it can be
  // measured rather than estimated.
  useLayoutEffect(() => {
    if (!open || !mounted) return;
    // Measuring laid-out boxes is the "external system" the rule carves out:
    // the position is a fact about the rendered DOM, and nothing earlier in the
    // render can know it.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    recomputePosition();
  }, [open, mounted, step, recomputePosition]);

  // Keep the position fresh for as long as a step is open: on the card's own
  // size changing, on the target's size changing (e.g. the user expands the
  // "Stranke" accordion mid-step), and on scroll/resize/orientation change.
  useEffect(() => {
    if (!open || !mounted) return;

    const targetEl = document.getElementById(korak.target);
    // ResizeObserver already batches per frame, and `samePosition` stops a
    // measurement from bouncing, so these recompute directly.
    const cardObserver = new ResizeObserver(recomputePosition);
    if (cardRef.current) cardObserver.observe(cardRef.current);
    const targetObserver = targetEl ? new ResizeObserver(recomputePosition) : null;
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
  }, [open, mounted, step, korak, recomputePosition, scheduleRecompute]);

  /**
   * How far the page is from having the target parked just under the sticky
   * header, which is where a step wants it.
   *
   * The old call was `scrollIntoView({ block: "center" })`, and centring is the
   * worst thing to do with a tall target: it splits the leftover space into two
   * slivers, neither big enough for the card, which is how the card ended up
   * squeezed on top of the very field it was describing. Parking the target
   * against the header instead keeps the leftover space in one usable piece —
   * and when even that is not enough, it is the target's lower end rather than
   * its labelled field that the card has to cover.
   */
  const targetOffset = useCallback(() => {
    const rect = document.getElementById(korak.target)?.getBoundingClientRect();
    if (!rect) return null;
    return rect.top - HEADER_SAFE;
  }, [korak]);

  const alignTarget = useCallback(
    (behavior: ScrollBehavior) => {
      const offset = targetOffset();
      if (offset !== null && Math.abs(offset) > 1) window.scrollBy({ top: offset, behavior });
    },
    [targetOffset],
  );

  const targetIsAligned = useCallback(() => {
    const offset = targetOffset();
    return offset === null || Math.abs(offset) <= ALIGN_TOLERANCE;
  }, [targetOffset]);

  // Tell the page which element to ring, and scroll it into view.
  useEffect(() => {
    const target = open ? korak.target : null;
    onStepChange(target);

    if (!target) return;
    // Small delay so the ring has painted before we measure the element.
    // The follow-up recomputes land after the smooth scroll has settled:
    // `scrollend` is not reliable across browsers, and the scroll listener
    // alone would leave the card stale if frames are throttled.
    const timers = [
      setTimeout(() => alignTarget("smooth"), 80),
      setTimeout(recomputePosition, 200),
      setTimeout(() => {
        // If the smooth scroll never landed — frames throttled, the animation
        // interrupted, reduced-motion settings — put the target where it
        // belongs outright. By now a smooth scroll has long finished, so this
        // is a no-op in the normal case rather than a second jump.
        if (!targetIsAligned()) alignTarget("auto");
        recomputePosition();
      }, 600),
    ];
    return () => timers.forEach(clearTimeout);
  }, [open, step, korak, onStepChange, recomputePosition, alignTarget, targetIsAligned]);

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
        top: position.top,
        left: position.left,
        width: position.width,
        maxHeight: position.maxHeight,
      }
    : // Only the very first render, before the layout effect has measured the
      // target. Kept off-screen but laid out, so it can be measured at its
      // natural height instead of flashing at the wrong spot.
      { position: "fixed", top: -9999, left: -9999, width: CARD_WIDTH };

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
