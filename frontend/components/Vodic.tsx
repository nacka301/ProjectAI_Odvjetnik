"use client";

import { useCallback, useEffect, useRef, useState } from "react";
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
 * Hence steps on the real form, with the real data in it. The user types into
 * the actual fields as they go; nothing here is a mock-up, and whatever they
 * enter during the walkthrough is the troškovnik they end up with.
 *
 * The walkthrough spans BOTH modules a September user can open: four steps in
 * the troškovnik, then three in Stranke. The step list is a prop rather than a
 * constant inside this file, so the two legs share every bit of the highlighting
 * logic and differ only in their text.
 *
 * The card is pinned to an EDGE of the viewport — top or bottom — never over
 * the field it explains. Two earlier versions each failed one way: one glued
 * the card to the field, and on a long form it landed at a different height
 * each step and sometimes clipped; the next centred the card, which then sat
 * squarely on top of the very field it pointed at (both card and field were
 * scrolled to the middle). This version measures the ringed field, picks the
 * edge with more room (field in the top half of the viewport → card at the
 * bottom, and vice versa), scrolls the field into the band the card does NOT
 * cover, and anchors the card to that edge. The card keeps a fixed max-height
 * so nothing clips; only its text body scrolls. Portaled to `document.body`
 * (same pattern as `MethodologyModal`) so it always paints above the rest of
 * the page regardless of stacking contexts on this form.
 */

/** Top band kept clear so the ringed field never lands under a sticky header. */
const VODIC_HEADER_GAP = 72;
/** Breathing room between the card and the viewport edge it is pinned to. */
const VODIC_EDGE_GAP = 16;
/** Gap between the card's band and the ringed field's band. */
const VODIC_CARD_GAP = 12;

export interface VodicKorak {
  /** DOM id of the element to ring. */
  target: string;
  title: string;
  text: string;
}

/** The card's pinned viewport edge and horizontal alignment for a step. */
interface Spot {
  edge: "top" | "bottom";
  align: "center" | "start" | "end";
}

/**
 * Brojevi u naslovima teku kroz oba modula (1/7 … 7/7), ne od početka u svakome.
 * Korisnik tako zna koliko je ostalo, a ne dobiva dvaput „1/…". Ako se dvije
 * dionice ikad razdvoje, brojeve treba vratiti na 1/4 odnosno 1/3.
 */
export const TROSKOVNIK_KORACI: VodicKorak[] = [
  {
    target: "vodic-vps",
    title: "1/7 · Vrijednost predmeta spora",
    text:
      "Upišite vrijednost spora u eurima. Iz nje se izvodi osnovica u bodovima — " +
      "sve nagrade u nastavku računaju se iz tog broja. U kaznenom predmetu ovo " +
      "polje ne postoji; tamo se polazi od zaprijećene kazne.",
  },
  {
    target: "vodic-radnje",
    title: "2/7 · Odaberite radnje",
    text:
      "Pretražite tarifu i dodajte radnje koje ste obavili — tužba, ročište, žalba. " +
      "Ista se stavka smije dodati više puta, svaka sa svojim datumom. Bodovi se " +
      "zbrajaju sami, ne morate ih tražiti u Tarifi.",
  },
  {
    target: "vodic-stranke",
    title: "3/7 · Stranke i PDV",
    text:
      "Odjeljak je neobavezan i služi samo zaglavlju ispisa. Vaši podaci — naziv " +
      "ureda, OIB, adresa, IBAN — povlače se iz Postavki → Odvjetnički ured; ondje " +
      "ih upišete jednom, a ovdje ih mijenjate samo za ovaj troškovnik. Sud i " +
      "stranke dodajte ako troškovnik ide sudu. Prekidač „U sustavu PDV-a\" desno " +
      "određuje obračunava li se PDV na nagradu.",
  },
  {
    target: "vodic-rezultat",
    title: "4/7 · Rezultat",
    text:
      "Iznos se osvježava dok unosite. Kad ste gotovi, troškovnik se sprema uz " +
      "stranku i predmet; „Ispis\" daje dokument za sud, „Excel\" tablicu za daljnji " +
      "rad. Slijede Stranke — ondje taj isti troškovnik nađete u dosjeu.",
  },
];

/**
 * Druga dionica — modul Stranke.
 *
 * Tri koraka, istim redom kojim se posao odvija: otvori stranku, pronađi je,
 * uđi u njezin dosje. Zadnji korak zatvara krug s troškovnikom: troškovnik se
 * sprema uz stranku, pa je dosje mjesto na kojem se sve o njoj vidi zajedno.
 */
export const STRANKE_KORACI: VodicKorak[] = [
  {
    target: "vodic-nova-stranka",
    title: "5/7 · Nova stranka",
    text:
      "Ovim gumbom otvarate obrazac za novu stranku. Obavezno je samo ime " +
      "odnosno naziv; OIB, adresa i kontakt idu u zaglavlje troškovnika i na " +
      "dopise, pa ih je vrijedno upisati odmah. Stranku ne morate otvarati " +
      "ručno — ako je upišete u troškovnik, otvorit će se sama pri spremanju.",
  },
  {
    target: "vodic-pretraga",
    title: "6/7 · Pretraga",
    text:
      "Pretražuje po imenu, OIB-u i adresi. Kad se popis stranaka nakupi, ovo " +
      "je brži put do predmeta nego listanje.",
  },
  {
    target: "vodic-dosje",
    title: "7/7 · Dosje stranke",
    text:
      "Klik na stranku otvara njezin dosje — predmeti i svi troškovnici " +
      "spremljeni uz nju, sa zbrojem. Ovime je vodič gotov; otvorite ga opet " +
      "gumbom „Vodič\" u zaglavlju bilo kojeg od dva modula.",
  },
];

/**
 * Classes for the element the current step points at.
 *
 * z-[210] must sit BETWEEN the two overlay layers (see below): above the
 * backdrop's z-[200] so the ringed element's opaque background shows through
 * instead of the dim — that is the "cut-out" — and below the card's z-[220] so
 * the step text is never hidden behind the very field it points at.
 */
export function highlightClasses(active: boolean): string {
  return active ? "ring-2 ring-indigo-500 ring-offset-2 relative z-[210]" : "";
}

interface Props {
  /**
   * Controlled by the page: `?vodic=1` from the welcome screen or from the
   * previous leg, the header "Vodič" button, or the answer to "has this user
   * ever been through it".
   */
  open: boolean;
  /** The steps of this leg — {@link TROSKOVNIK_KORACI} or {@link STRANKE_KORACI}. */
  koraci: VodicKorak[];
  /** Called when the user finishes or skips. */
  onClose: () => void;
  /** Called on every step change so the page can ring the right element. */
  onStepChange: (targetId: string | null) => void;
  /**
   * Where the walkthrough goes after the last step of THIS leg.
   *
   * When given, the last step's button hands over instead of ending: it does
   * NOT mark the user as onboarded, because the introduction is not over — the
   * next leg will do that when it finishes. Skipping still ends it everywhere,
   * from any step of any leg.
   *
   * Omit on the final leg; the button then reads "Završi" and closes for good.
   */
  onNastavak?: () => void;
  /** Label of the last step's button when {@link onNastavak} is given. */
  nastavakOznaka?: string;
}

export default function Vodic({
  open,
  koraci,
  onClose,
  onStepChange,
  onNastavak,
  nastavakOznaka = "Dalje",
}: Props) {
  const [step, setStep] = useState(0);
  // Portals need a DOM to attach to — `false` during SSR, flipped after mount,
  // same guard as MethodologyModal.
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    // Whether a DOM exists to portal into is not knowable during render — the
    // same mount guard MethodologyModal uses.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
  }, []);

  const korak = koraci[step];
  const zadnji = step === koraci.length - 1;

  /**
   * Where the card sits relative to the viewport for the current step.
   * `edge` is the pinned edge; `align` is horizontal — `center` normally, or
   * `start`/`end` when the field is too tall to clear vertically but sits in a
   * side column (the `lg:sticky` summary), so the card tucks in beside it.
   */
  const [pozicija, setPozicija] = useState<Spot>({
    edge: "bottom",
    align: "center",
  });
  const cardRef = useRef<HTMLDivElement>(null);

  // Tell the page which element to ring.
  useEffect(() => {
    onStepChange(open ? korak.target : null);
  }, [open, korak.target, onStepChange]);

  /**
   * Place the card and scroll the ringed field clear of it.
   *
   * The card is pinned to a viewport edge; the field is scrolled into the strip
   * the card leaves free. Candidate spots are tried in order and the first that
   * leaves NO overlap — accounting for how far the page can actually scroll —
   * wins:
   *   1. card bottom, field centred in the strip above it   (the common case)
   *   2. card top, field centred in the strip below it      (field sits low and
   *      the page can't scroll far enough to lift it clear of a bottom card)
   *   3/4. card beside the field, on the roomier side        (tall side-column
   *      field — the `lg:sticky` summary at a short/zoomed viewport)
   *   fallback: card top, tall field pinned just below it, overflowing off-screen
   * Runs three times per step — once smooth for the eye, then two instant
   * corrections for the card re-measuring and the page settling after scroll —
   * and again on resize.
   */
  const namjesti = useCallback(
    (smooth: boolean) => {
      const el = document.getElementById(korak.target);
      const cardEl = cardRef.current;
      if (!el || !cardEl) return;

      const vh = window.innerHeight;
      const vw = window.innerWidth;
      // getBoundingClientRect (not offset*) so units match `rect` under browser
      // zoom, where the two disagree.
      const cardBox = cardEl.getBoundingClientRect();
      const cardH = Math.min(cardBox.height, vh - VODIC_EDGE_GAP * 2);
      const cardW = Math.min(cardBox.width, vw - VODIC_EDGE_GAP * 2);
      const rect = el.getBoundingClientRect();

      const scrollY = window.scrollY;
      const maxScroll = Math.max(
        0,
        document.documentElement.scrollHeight - vh,
      );
      // A `position: sticky` field (the summary column on `lg`) does not move
      // out of its stuck band however far you scroll — so treat it as fixed
      // where it is and let the candidate search route around it instead.
      const sticky = getComputedStyle(el).position === "sticky";
      // Field top we can actually reach by scrolling, given the page's limits.
      const reachableTop = (wantTop: number) => {
        if (sticky) return rect.top;
        const want = rect.top - wantTop; // >0 scroll down, <0 scroll up
        const applied = Math.max(-scrollY, Math.min(maxScroll - scrollY, want));
        return rect.top - applied;
      };

      const cardRectFor = (s: Spot) => {
        const top =
          s.edge === "top"
            ? VODIC_EDGE_GAP
            : vh - VODIC_EDGE_GAP - cardH;
        let left = (vw - cardW) / 2;
        if (s.align === "start") left = VODIC_EDGE_GAP;
        if (s.align === "end") left = vw - VODIC_EDGE_GAP - cardW;
        return { top, bottom: top + cardH, left, right: left + cardW };
      };

      const wantedTop = (s: Spot) => {
        if (s.align !== "center") return VODIC_HEADER_GAP;
        const stripTop =
          s.edge === "top"
            ? VODIC_EDGE_GAP + cardH + VODIC_CARD_GAP
            : VODIC_HEADER_GAP;
        const stripBottom =
          s.edge === "bottom"
            ? vh - VODIC_EDGE_GAP - cardH - VODIC_CARD_GAP
            : vh - VODIC_EDGE_GAP;
        const stripH = Math.max(0, stripBottom - stripTop);
        return rect.height >= stripH
          ? stripTop
          : stripTop + (stripH - rect.height) / 2;
      };

      const clears = (s: Spot) => {
        const c = cardRectFor(s);
        const t0 = reachableTop(wantedTop(s));
        const t = { top: t0, bottom: t0 + rect.height, left: rect.left, right: rect.right };
        return (
          t.bottom <= c.top ||
          t.top >= c.bottom ||
          t.right <= c.left ||
          t.left >= c.right
        );
      };

      const candidates: Spot[] = [
        { edge: "bottom", align: "center" },
        { edge: "top", align: "center" },
      ];
      if (vw - rect.right >= cardW + VODIC_CARD_GAP)
        candidates.push({ edge: "bottom", align: "end" });
      if (rect.left >= cardW + VODIC_CARD_GAP)
        candidates.push({ edge: "bottom", align: "start" });

      // Nothing clears only on a viewport too small for card + field together;
      // fall back to card at the top with the field pinned just below it,
      // overflowing off the bottom — its top and ring corner still show.
      const pos = candidates.find(clears) ?? { edge: "top", align: "center" };

      setPozicija(pos);

      const delta = rect.top - reachableTop(wantedTop(pos));
      if (Math.abs(delta) > 2) {
        window.scrollBy({ top: delta, behavior: smooth ? "smooth" : "auto" });
      }
    },
    [korak.target],
  );

  useEffect(() => {
    if (!open) return;
    const t1 = setTimeout(() => namjesti(true), 90);
    const t2 = setTimeout(() => namjesti(false), 480);
    const t3 = setTimeout(() => namjesti(false), 900);
    return () => {
      clearTimeout(t1);
      clearTimeout(t2);
      clearTimeout(t3);
    };
  }, [open, korak.target, namjesti]);

  // Re-place the card and field when the viewport changes size (rotation, zoom,
  // devtools), throttled to one pass per frame.
  useEffect(() => {
    if (!open) return;
    let raf = 0;
    const onResize = () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => namjesti(false));
    };
    window.addEventListener("resize", onResize);
    window.addEventListener("orientationchange", onResize);
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("resize", onResize);
      window.removeEventListener("orientationchange", onResize);
    };
  }, [open, namjesti]);

  /** Finishing the LAST leg and skipping are the same instruction: do not ask me again. */
  const close = useCallback(() => {
    setStep(0);
    onStepChange(null);
    void markOnboarded();
    onClose();
  }, [onStepChange, onClose]);

  /**
   * Hands over to the next leg without marking anything: the walkthrough
   * continues on another page, and stamping "onboarded" here would end it
   * for anyone who then reloads.
   *
   * Deliberately does NOT call `onClose`. The next leg lives on another
   * route, so `onNastavak` navigates and this page unmounts on its own —
   * and a page whose close handler also rewrites the address (both do, to
   * drop `?vodic=1`) would otherwise fire a second navigation racing the
   * first. Leaving the card up until the new page paints also spares the
   * user a flash of the bare form on the way out.
   */
  const nastavi = useCallback(() => {
    setStep(0);
    onStepChange(null);
    onNastavak?.();
  }, [onStepChange, onNastavak]);

  const next = useCallback(() => {
    if (step < koraci.length - 1) setStep((s) => s + 1);
    else if (onNastavak) nastavi();
    else close();
  }, [step, koraci.length, onNastavak, nastavi, close]);

  if (!open || !mounted) return null;

  const overlay = (
    <>
      {/* Backdrop. Clicking it skips, same as the text button — a user who wants
          out should not have to find the small link. Inline rgba instead of the
          Tailwind opacity-modifier class, which renders as a color-mix() in
          Tailwind v4 — sidesteps a paint issue that made the dimming invisible
          in production for reasons that couldn't be pinned down from the
          source alone. `vodic-fade` fades the backdrop in on mount, so a new
          leg (Troškovnik → Stranke) appears deliberately rather than blinking.

          z-[200]: the step's field is lifted to z-[210] and pokes through this
          dim (the "cut-out"). */}
      <div
        className="fixed inset-0 z-[200] pointer-events-auto vodic-fade print:hidden"
        style={{ backgroundColor: "rgba(15, 23, 42, 0.5)" }}
        onClick={close}
      />

      {/*
        The card lives in its OWN layer at z-[220] — above the step's ringed
        field (z-[210]). An earlier version nested it in the backdrop's own
        z-[200] layer, so a field scrolled to the centre of the screen painted
        its white card straight over the instruction text.

        Pinned to the top OR bottom edge (whichever `pozicija.edge` the effect
        chose), full width up to 440px. Normally centred; `pozicija.align`
        shifts it to a side when it has to tuck in next to a tall side-column
        field. `max-h` plus the edge padding keep it inside the viewport; only
        the text body scrolls, so the title and the buttons can never be the
        part that's cut off. The `key` restarts `vodic-pop` on every step so a
        move to a new spot reads as a deliberate fade rather than a jump.
      */}
      <div
        className={`fixed inset-x-0 z-[220] flex px-4 pointer-events-none print:hidden ${
          pozicija.edge === "top" ? "top-0 pt-4" : "bottom-0 pb-4"
        } ${
          pozicija.align === "start"
            ? "justify-start"
            : pozicija.align === "end"
              ? "justify-end"
              : "justify-center"
        }`}
      >
        <div
          key={korak.target}
          ref={cardRef}
          className="relative pointer-events-auto w-full max-w-[440px] max-h-[calc(100vh-2rem)] bg-white rounded-xl shadow-2xl border border-slate-200 p-4 sm:p-5 flex flex-col vodic-pop"
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
                {koraci.map((k, i) => (
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
                {!zadnji ? "Dalje" : onNastavak ? nastavakOznaka : "Završi"}
                <ArrowRight className="h-3 w-3" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );

  // Portal to document.body — the same escape hatch MethodologyModal uses —
  // so the overlay always paints above the rest of the page regardless of
  // stacking contexts introduced by ancestors in the normal page tree.
  return createPortal(overlay, document.body);
}
