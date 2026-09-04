"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { usePathname, useRouter } from "next/navigation";
import { ArrowRight, Compass, X } from "lucide-react";
import type { VodicKorak } from "@/lib/vodic-koraci";

/**
 * Motor vodiča. Zna prikazati korak, ne zna što u njemu piše — tekst svih
 * vodiča živi u `lib/vodic-koraci.ts`, a koji je vodič pokrenut odlučuje
 * `contexts/VodicContext.tsx`.
 *
 * ── Kartica se nikad ne preklapa s poljem koje objašnjava ──
 *
 * Kartica je prikovana uz RUB viewporta — gore ili dolje — nikad preko polja.
 * Dvije ranije verzije svaka je pala na svoj način: prva je kartica bila
 * zalijepljena uz polje, pa je na dugoj formi svaki korak završavala na drugoj
 * visini i ponekad se rezala; druga ju je centrirala, pa je sjela točno na polje
 * na koje pokazuje (i kartica i polje bili su skrolani na sredinu). Ova mjeri
 * prstenovano polje, bira rub s više prostora (polje u gornjoj polovici →
 * kartica dolje, i obrnuto), skrola polje u pojas koji kartica NE pokriva i
 * prikuje karticu uz taj rub. `max-height` drži karticu u ekranu; skrola samo
 * njezino tijelo, pa naslov i gumbi nikad nisu ono što se odreže. Portalana je
 * u `document.body` (isti obrazac kao `MethodologyModal`) da uvijek slika iznad
 * stranice bez obzira na stacking contexte iznad nje.
 *
 * ── Što korak može ──
 *
 * Korak bez `target` ništa ne ističe i kartica stoji na sredini ekrana — uvodna
 * i završna kartica vodiča dobrodošlice. Korak s `ruta` živi na drugoj stranici;
 * motor ondje navigira i pričeka da se cilj pojavi. Korak čiji se cilj ni nakon
 * čekanja ne nađe PRESKAČE se uz upozorenje u konzoli: prazan okvir ili srušen
 * vodič bili bi gori od koraka manje.
 */

/** Top band kept clear so the ringed field never lands under a sticky header. */
const VODIC_HEADER_GAP = 72;
/** Breathing room between the card and the viewport edge it is pinned to. */
const VODIC_EDGE_GAP = 16;
/** Gap between the card's band and the ringed field's band. */
const VODIC_CARD_GAP = 12;

/** Koliko se čeka da se cilj pojavi u DOM-u prije nego se korak preskoči. */
const CILJ_TIMEOUT_MS = 2500;
const CILJ_INTERVAL_MS = 100;

/** The card's pinned viewport edge and horizontal alignment for a step. */
interface Spot {
  /** `center` = kartica na sredini ekrana; korak bez cilja. */
  edge: "top" | "bottom" | "center";
  align: "center" | "start" | "end";
}

/**
 * Nađi element koraka.
 *
 * Prvo `data-vodic`, pa DOM `id` iste vrijednosti. Atribut, ne CSS klasa i ne
 * položaj u DOM-u: klase se mijenjaju pri svakom redizajnu i vodič bi tiho
 * pukao. `id` ostaje kao rezerva jer stara sidra (`id="vodic-vps"`) još stoje.
 */
export function nadjiCilj(target: string | undefined): HTMLElement | null {
  if (!target) return null;
  const poAtributu = document.querySelector<HTMLElement>(`[data-vodic="${target}"]`);
  if (poAtributu) return poAtributu;
  return document.getElementById(target);
}

/**
 * Classes for the element the current step points at.
 *
 * z-[210] must sit BETWEEN the two overlay layers: above the backdrop's z-[200]
 * so the ringed element's opaque background shows through instead of the dim —
 * that is the "cut-out" — and below the card's z-[220] so the step text is never
 * hidden behind the very field it points at.
 */
export function highlightClasses(active: boolean): string {
  return active ? "ring-2 ring-indigo-500 ring-offset-2 relative z-[210]" : "";
}

interface Props {
  open: boolean;
  /** Koraci ovog vodiča — jedan od popisa iz `lib/vodic-koraci.ts`. */
  koraci: VodicKorak[];
  /**
   * Zatvaranje — i „Završi" i „Preskoči" i Esc. `zavrseno` je true samo kad je
   * korisnik došao do kraja, pa pozivatelj zna treba li još i otpratiti ga
   * nekamo (vodič dobrodošlice otvara prazan troškovnik).
   */
  onClose: (zavrseno: boolean) => void;
  /** Called on every step change so the page can ring the right element. */
  onStepChange: (targetId: string | null) => void;
}

export default function Vodic({ open, koraci, onClose, onStepChange }: Props) {
  const [step, setStep] = useState(0);
  // Portals need a DOM to attach to — `false` during SSR, flipped after mount,
  // same guard as MethodologyModal.
  const [mounted, setMounted] = useState(false);
  /** Je li cilj trenutnog koraka pronađen. Dok nije, kartica stoji na sredini. */
  const [ciljSpreman, setCiljSpreman] = useState(false);
  const router = useRouter();
  const pathname = usePathname();

  /**
   * Smjer kretanja, da se korak bez cilja preskoči na PRAVU stranu: naprijed
   * kad je korisnik stisnuo „Dalje", natrag kad se vraća strelicom.
   */
  const smjer = useRef<1 | -1>(1);

  useEffect(() => {
    // Whether a DOM exists to portal into is not knowable during render — the
    // same mount guard MethodologyModal uses.
    setMounted(true);
  }, []);

  // Vodič uvijek kreće od početka kad se otvori; bez ovoga bi drugo pokretanje
  // iz izbornika nastavilo ondje gdje je prošlo stalo.
  useEffect(() => {
    if (open) {
      setStep(0);
      smjer.current = 1;
    }
  }, [open]);

  const korak = koraci[step];
  const zadnji = step === koraci.length - 1;

  const [pozicija, setPozicija] = useState<Spot>({ edge: "bottom", align: "center" });
  const cardRef = useRef<HTMLDivElement>(null);

  // Tell the page which element to ring. Korak bez cilja ne ističe ništa.
  useEffect(() => {
    onStepChange(open && ciljSpreman ? (korak?.target ?? null) : null);
  }, [open, korak?.target, ciljSpreman, onStepChange]);

  /** Zatvaranje — „Završi", „Preskoči", Esc i klik na zatamnjenje. */
  const close = useCallback(
    (zavrseno: boolean) => {
      setStep(0);
      setCiljSpreman(false);
      onStepChange(null);
      onClose(zavrseno);
    },
    [onStepChange, onClose],
  );

  const next = useCallback(() => {
    smjer.current = 1;
    if (step < koraci.length - 1) setStep((s) => s + 1);
    else close(true);
  }, [step, koraci.length, close]);

  const prev = useCallback(() => {
    smjer.current = -1;
    setStep((s) => Math.max(0, s - 1));
  }, []);

  /**
   * Place the card and scroll the ringed field clear of it.
   *
   * Candidate spots are tried in order and the first that leaves NO overlap —
   * accounting for how far the page can actually scroll — wins:
   *   1. card bottom, field centred in the strip above it   (the common case)
   *   2. card top, field centred in the strip below it      (field sits low and
   *      the page can't scroll far enough to lift it clear of a bottom card)
   *   3/4. card beside the field, on the roomier side        (tall side-column
   *      field — the `lg:sticky` summary at a short/zoomed viewport)
   *   fallback: card top, tall field pinned just below it, overflowing off-screen
   */
  const namjesti = useCallback(
    (smooth: boolean) => {
      const el = nadjiCilj(korak?.target);
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
      const maxScroll = Math.max(0, document.documentElement.scrollHeight - vh);
      // A `position: sticky` field (the summary column on `lg`) does not move
      // out of its stuck band however far you scroll — so treat it as fixed
      // where it is and let the candidate search route around it instead.
      //
      // Gleda se i PRETKE, ne samo sam element: sidra „popis radnji", „bodovi",
      // „pristojbe" i „spremi" leže UNUTAR tog sticky stupca, pa su sama
      // `static`. Dok se provjeravao samo element, vodič bi ih pokušavao
      // doskrolati, stupac se ne bi pomaknuo, i kartica bi svejedno sjela preko
      // njih — točno kvar zbog kojeg je ova provjera i nastala.
      const nepomican = (cvor: HTMLElement | null): boolean => {
        for (let n = cvor; n; n = n.parentElement) {
          const poz = getComputedStyle(n).position;
          if (poz === "sticky" || poz === "fixed") return true;
        }
        return false;
      };
      const sticky = nepomican(el);
      // Field top we can actually reach by scrolling, given the page's limits.
      const reachableTop = (wantTop: number) => {
        if (sticky) return rect.top;
        const want = rect.top - wantTop; // >0 scroll down, <0 scroll up
        const applied = Math.max(-scrollY, Math.min(maxScroll - scrollY, want));
        return rect.top - applied;
      };

      const cardRectFor = (s: Spot) => {
        const top = s.edge === "top" ? VODIC_EDGE_GAP : vh - VODIC_EDGE_GAP - cardH;
        let left = (vw - cardW) / 2;
        if (s.align === "start") left = VODIC_EDGE_GAP;
        if (s.align === "end") left = vw - VODIC_EDGE_GAP - cardW;
        return { top, bottom: top + cardH, left, right: left + cardW };
      };

      const wantedTop = (s: Spot) => {
        if (s.align !== "center") return VODIC_HEADER_GAP;
        const stripTop =
          s.edge === "top" ? VODIC_EDGE_GAP + cardH + VODIC_CARD_GAP : VODIC_HEADER_GAP;
        const stripBottom =
          s.edge === "bottom"
            ? vh - VODIC_EDGE_GAP - cardH - VODIC_CARD_GAP
            : vh - VODIC_EDGE_GAP;
        const stripH = Math.max(0, stripBottom - stripTop);
        return rect.height >= stripH ? stripTop : stripTop + (stripH - rect.height) / 2;
      };

      const clears = (s: Spot) => {
        const c = cardRectFor(s);
        const t0 = reachableTop(wantedTop(s));
        const t = { top: t0, bottom: t0 + rect.height, left: rect.left, right: rect.right };
        return (
          t.bottom <= c.top || t.top >= c.bottom || t.right <= c.left || t.left >= c.right
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
      const pos = candidates.find(clears) ?? { edge: "top" as const, align: "center" as const };

      setPozicija(pos);

      const delta = rect.top - reachableTop(wantedTop(pos));
      if (Math.abs(delta) > 2) {
        window.scrollBy({ top: delta, behavior: smooth ? "smooth" : "auto" });
      }
    },
    [korak?.target],
  );

  // ── Navigacija na rutu koraka ──
  // Zaseban efekt, prije traženja cilja: korak 8/8 vodiča dobrodošlice živi na
  // /klijenti, a dotad smo na /troskovniku.
  useEffect(() => {
    if (!open || !korak?.ruta) return;
    if (pathname !== korak.ruta) router.push(korak.ruta);
  }, [open, korak?.ruta, pathname, router]);

  // ── Traženje cilja, pa pozicioniranje ──
  //
  // Cilj se ne traži jednom nego se čeka: nakon navigacije nova stranica treba
  // vremena da se montira, a i na istoj stranici sklopivi odjeljak može tek
  // trebati narasti. Ne pojavi li se u CILJ_TIMEOUT_MS, korak se preskoči.
  useEffect(() => {
    if (!open || !korak) return;

    // Korak bez cilja ništa ne traži — kartica ide na sredinu.
    if (!korak.target) {
      setCiljSpreman(true);
      setPozicija({ edge: "center", align: "center" });
      return;
    }

    setCiljSpreman(false);

    let odustani = false;
    const timeouts: ReturnType<typeof setTimeout>[] = [];
    const pocetak = Date.now();

    const trazi = () => {
      if (odustani) return;

      if (nadjiCilj(korak.target)) {
        setCiljSpreman(true);
        // Tri prolaza: jedan gladak za oko, pa dva trenutna ispravka nakon što
        // se kartica premjeri i stranica smiri poslije skrola.
        timeouts.push(setTimeout(() => namjesti(true), 90));
        timeouts.push(setTimeout(() => namjesti(false), 480));
        timeouts.push(setTimeout(() => namjesti(false), 900));
        return;
      }

      if (Date.now() - pocetak >= CILJ_TIMEOUT_MS) {
        console.warn(
          `[vodic] Korak „${korak.title}" nema svoj cilj u DOM-u ` +
            `(data-vodic="${korak.target}") — korak je preskočen. ` +
            "Sidro je vjerojatno preimenovano ili uklonjeno.",
        );
        // Preskačemo u smjeru kretanja. Nema li kamo (prvi ili zadnji korak),
        // vodič se zatvara — bolje nego zaglaviti na koraku bez sadržaja.
        const sljedeci = step + smjer.current;
        if (sljedeci >= 0 && sljedeci < koraci.length) setStep(sljedeci);
        else close(false);
        return;
      }

      timeouts.push(setTimeout(trazi, CILJ_INTERVAL_MS));
    };

    trazi();

    return () => {
      odustani = true;
      timeouts.forEach(clearTimeout);
    };
    // `namjesti` i `close` ovise o istom koraku; `koraci.length` se ne mijenja
    // unutar jednog vodiča.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, step, korak?.target]);

  // Re-place the card and field when the viewport changes size (rotation, zoom,
  // devtools), throttled to one pass per frame.
  useEffect(() => {
    if (!open || !ciljSpreman) return;
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
  }, [open, ciljSpreman, namjesti]);

  // ── Tipkovnica ──
  // Vodič pokriva cijeli ekran i nema što drugo primati tipke, pa listener stoji
  // na prozoru. Esc zatvara, strelice kreću naprijed i natrag.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        close(false);
      } else if (e.key === "ArrowRight") {
        e.preventDefault();
        next();
      } else if (e.key === "ArrowLeft") {
        e.preventDefault();
        prev();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, close, next, prev]);

  if (!open || !mounted || !korak) return null;

  const naSredini = pozicija.edge === "center";

  const overlay = (
    <>
      {/* Backdrop. Clicking it skips, same as the text button — a user who wants
          out should not have to find the small link. Inline rgba instead of the
          Tailwind opacity-modifier class, which renders as a color-mix() in
          Tailwind v4 — sidesteps a paint issue that made the dimming invisible
          in production for reasons that couldn't be pinned down from the
          source alone.

          z-[200]: the step's field is lifted to z-[210] and pokes through this
          dim (the "cut-out"). */}
      <div
        className="fixed inset-0 z-[200] pointer-events-auto vodic-fade print:hidden"
        style={{ backgroundColor: "rgba(15, 23, 42, 0.5)" }}
        onClick={() => close(false)}
      />

      {/*
        The card lives in its OWN layer at z-[220] — above the step's ringed
        field (z-[210]). An earlier version nested it in the backdrop's own
        z-[200] layer, so a field scrolled to the centre of the screen painted
        its white card straight over the instruction text.
      */}
      <div
        className={`fixed inset-x-0 z-[220] flex px-4 pointer-events-none print:hidden ${
          naSredini
            ? "inset-y-0 items-center"
            : pozicija.edge === "top"
              ? "top-0 pt-4"
              : "bottom-0 pb-4"
        } ${
          pozicija.align === "start"
            ? "justify-start"
            : pozicija.align === "end"
              ? "justify-end"
              : "justify-center"
        }`}
      >
        <div
          key={`${step}-${korak.target ?? "sredina"}`}
          ref={cardRef}
          className="relative pointer-events-auto w-full max-w-[440px] max-h-[calc(100vh-2rem)] bg-white rounded-xl shadow-2xl border border-slate-200 p-4 sm:p-5 flex flex-col vodic-pop"
        >
          <div className="flex items-start justify-between gap-3 mb-2 shrink-0">
            <div className="flex items-center gap-2">
              <Compass className="h-4 w-4 text-indigo-600" />
              <span className="text-sm font-bold text-slate-900">{korak.title}</span>
            </div>
            <button
              onClick={() => close(false)}
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

          <div className="flex items-center justify-between gap-3">
            <button
              onClick={() => close(false)}
              className="shrink-0 text-xs text-slate-400 hover:text-slate-600 transition"
            >
              Preskoči
            </button>
            <div className="flex items-center gap-3">
              <div className="flex gap-1">
                {koraci.map((k, i) => (
                  <div
                    key={`${i}-${k.target ?? ""}`}
                    className={`w-1.5 h-1.5 rounded-full transition ${
                      i === step ? "bg-indigo-600" : "bg-slate-200"
                    }`}
                  />
                ))}
              </div>
              {step > 0 && (
                <button
                  onClick={prev}
                  className="text-xs text-slate-500 hover:text-slate-800 transition"
                >
                  Natrag
                </button>
              )}
              <button
                onClick={next}
                className="inline-flex items-center gap-1 bg-indigo-600 text-white text-xs font-medium px-4 py-2 rounded-lg hover:bg-indigo-700 transition"
              >
                {zadnji ? "Završi" : "Dalje"}
                <ArrowRight className="h-3 w-3" />
              </button>
            </div>
          </div>

          <p className="mt-2 text-center text-[10px] text-slate-300 shrink-0">
            Strelice ← → za kretanje, Esc zatvara
          </p>
        </div>
      </div>
    </>
  );

  // Portal to document.body — the same escape hatch MethodologyModal uses —
  // so the overlay always paints above the rest of the page regardless of
  // stacking contexts introduced by ancestors in the normal page tree.
  return createPortal(overlay, document.body);
}
