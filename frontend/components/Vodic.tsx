"use client";

import { useCallback, useEffect, useState } from "react";
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
 * The card is a single centred modal — the same spot on the screen for every
 * step of both legs — over a dimmed backdrop. An earlier version positioned the
 * card against whichever field it explained; on a long form that left it landing
 * at a different height each step, occasionally clipped or on top of the very
 * field it described, and made the Troškovnik → Stranke handover jump. A fixed
 * centre removes all of that: nothing to measure, nothing to recompute, nothing
 * that can be cut off. The step's field is still ringed on the page underneath,
 * scrolled into view, so the spatial cue survives. Portaled to `document.body`
 * (same pattern as `MethodologyModal`) so it always paints above the rest of the
 * page regardless of stacking contexts on this form.
 */

export interface VodicKorak {
  /** DOM id of the element to ring. */
  target: string;
  title: string;
  text: string;
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

  // Tell the page which element to ring, and scroll it into view so the field
  // the step is about is visible on the page under the centred modal.
  useEffect(() => {
    const target = open ? korak.target : null;
    onStepChange(target);

    if (!target) return;
    // Small delay so the ring has painted before we scroll to the element.
    const timer = setTimeout(() => {
      document
        .getElementById(korak.target)
        ?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 80);
    return () => clearTimeout(timer);
  }, [open, korak, onStepChange]);

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

        A single centred modal: same position for every step, never clipped.
        The body scrolls if it ever has to (a very short viewport); the title
        and the buttons never do, so they can never be the part that's cut off.
        `vodic-pop` gives it a small fade-up on mount to match the backdrop.
      */}
      <div className="fixed inset-0 z-[220] flex items-center justify-center p-4 pointer-events-none print:hidden">
        <div className="relative pointer-events-auto w-[calc(100%-2rem)] max-w-[440px] max-h-[calc(100vh-2rem)] bg-white rounded-xl shadow-2xl border border-slate-200 p-4 sm:p-5 flex flex-col vodic-pop">
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
