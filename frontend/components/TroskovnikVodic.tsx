"use client";

import { useCallback, useEffect, useState } from "react";
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

/** Classes for the element the current step points at. */
export function highlightClasses(active: boolean): string {
  return active ? "ring-2 ring-indigo-500 ring-offset-2 relative z-[110]" : "";
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

export default function TroskovnikVodic({ open, onClose, onStepChange }: Props) {
  const [step, setStep] = useState(0);

  // Tell the page which element to ring, and scroll it into view.
  useEffect(() => {
    const target = open ? TROSKOVNIK_KORACI[step]?.target ?? null : null;
    onStepChange(target);

    if (!target) return;
    // Small delay so the ring has painted before we measure the element.
    const timer = setTimeout(() => {
      document.getElementById(target)?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 80);
    return () => clearTimeout(timer);
  }, [open, step, onStepChange]);

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

  if (!open) return null;

  const korak = TROSKOVNIK_KORACI[step];

  return (
    <div className="fixed inset-0 z-[100] pointer-events-none print:hidden">
      {/* Backdrop. Clicking it skips, same as the text button — a user who wants
          out should not have to find the small link. */}
      <div className="absolute inset-0 bg-slate-900/50 pointer-events-auto" onClick={close} />

      <div className="absolute bottom-6 left-1/2 -translate-x-1/2 pointer-events-auto w-[calc(100%-2rem)] max-w-[420px]">
        <div className="bg-white rounded-xl shadow-2xl border border-slate-200 p-4 sm:p-5">
          <div className="flex items-start justify-between gap-3 mb-2">
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

          <p className="text-sm text-slate-600 leading-relaxed mb-4">{korak.text}</p>

          <div className="flex items-center justify-between">
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
    </div>
  );
}
