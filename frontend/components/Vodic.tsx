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
 * ── Kako cilj probija zatamnjenje ──
 *
 * Ranija verzija je to radila CSS-om na stvarnom elementu stranice: cilj bi
 * dobio `relative z-[210]`, iznad zatamnjenja (`z-[200]`). To je krhko na
 * stranici ovog opsega — svaki `sticky`, `backdrop-blur` ili bilo koji drugi
 * roditelj koji stvara vlastiti stacking context ZAROBI taj z-index unutar
 * sebe, pa cilj ostane vidljivo zatamnjen unatoč vlastitom z-210 (to se
 * dogodilo dvaput: zaglavlju Stranaka i sažetku troškovnika). Popravak je
 * uvijek bio točkast — jedno mjesto po jednom otkrivenom kvaru — jer sljedeći
 * ugniježđeni roditelj koji nekome padne na pamet ponovno probija isto.
 *
 * Ova verzija ne dodiruje stvarnu stranicu uopće. Mjeri cilj preko
 * `getBoundingClientRect()` (koordinate su uvijek u odnosu na viewport, bez
 * obzira gdje element živi u DOM-u ili je li mu roditelj `sticky`) i iz tih
 * koordinata crta „rupu" u zatamnjenju: jedan `fixed` element čiji
 * `box-shadow` prekriva cijeli ekran OSIM pravokutnika cilja (spread sjene
 * 9999px, bez radijusa — ekvivalent beskonačnom prekrivanju), plus prsten
 * (drugi sloj sjene). Sam cilj se NIKAD ne dira — ne dobiva ni klasu ni
 * z-index — pa ga nijedan roditeljski stacking context više ne može zarobiti.
 * Klik na zatamnjeno područje (izvan rupe) i dalje zatvara vodič, preko četiri
 * pravokutnika koji popločuju sve OSIM rupe.
 *
 * Rupa prati cilj i preko skrola — ne samo na tri ustaljena mjerenja koja
 * pozicioniraju karticu, nego na svaki `scroll` događaj dok je korak otvoren,
 * pa se pomiče glatko i tijekom automatskog i tijekom ručnog skrolanja.
 *
 * ── Kartica se nikad ne preklapa s poljem koje objašnjava ──
 *
 * Kartica je prikovana uz RUB viewporta — gore ili dolje — nikad preko polja.
 * Mjeri prstenovano polje, bira rub s više prostora (polje u gornjoj polovici
 * → kartica dolje, i obrnuto), skrola polje u pojas koji kartica NE pokriva i
 * prikuje karticu uz taj rub. Polje se skrola UZ SAM RUB tog pojasa, do
 * kartice — ne na sredinu cijelog slobodnog prostora — jer polje koje ostane
 * lebdjeti daleko od kartice izgleda kao da kartica objašnjava nešto drugo.
 * `max-height` drži karticu u ekranu; skrola samo njezino tijelo, pa naslov i
 * gumbi nikad nisu ono što se odreže. Portalana je u `document.body` (isti
 * obrazac kao `MethodologyModal`) da uvijek slika iznad stranice.
 *
 * ── Što korak može ──
 *
 * Korak bez `target` ništa ne ističe (nema rupe, samo ravnomjerno zatamnjenje)
 * i kartica stoji na sredini ekrana — uvodna i završna kartica vodiča
 * dobrodošlice. Korak s `ruta` živi na drugoj stranici; motor ondje navigira
 * i pričeka da se cilj pojavi. Korak čiji se cilj ni nakon čekanja ne nađe
 * PRESKAČE se uz upozorenje u konzoli: prazan okvir ili srušen vodič bili bi
 * gori od koraka manje.
 */

/** Top band kept clear so the ringed field never lands under a sticky header. */
const VODIC_HEADER_GAP = 72;
/** Breathing room between the card and the viewport edge it is pinned to. */
const VODIC_EDGE_GAP = 16;
/** Gap between the card's band and the ringed field's band. */
const VODIC_CARD_GAP = 12;
/** Razmak rupe od stvarnih rubova cilja — isti vizualni dojam kao stari `ring-offset-2`. */
const SPOT_PADDING = 6;

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
  /** Izmjereni pravokutnik cilja (viewport-relativan) — crta rupu u zatamnjenju. `null` = nema rupe. */
  const [spotRect, setSpotRect] = useState<DOMRect | null>(null);
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
      setSpotRect(null);
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

  /** Mjeri trenutni cilj i osvježava rupu — na skrolu, resizeu i kad se cilj nađe. */
  const osvjeziRupu = useCallback(() => {
    const el = nadjiCilj(korak?.target);
    setSpotRect(el ? el.getBoundingClientRect() : null);
  }, [korak?.target]);

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
      setSpotRect(rect);

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
        // Prilijepi polje uz RUB pojasa najbliži kartici — ne na sredinu
        // cijelog slobodnog prostora. Kratko polje centrirano u visokom
        // pojasu (npr. mali odjeljak pristojbi u visokom sažetku) zna
        // završiti daleko od kartice prikovane uz suprotni rub, pa izgleda
        // kao da kartica objašnjava nešto drugo. `Math.max` sam pokriva i
        // slučaj kad je polje previsoko za pojas — tad ostaje uz `stripTop`,
        // kao i prije.
        if (s.edge === "bottom") return Math.max(stripTop, stripBottom - rect.height);
        return stripTop;
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

    // Korak bez cilja ništa ne traži — kartica ide na sredinu, bez rupe.
    if (!korak.target) {
      setCiljSpreman(true);
      setSpotRect(null);
      setPozicija({ edge: "center", align: "center" });
      return;
    }

    setCiljSpreman(false);
    // Stara rupa se gasi dok se traži nova — inače bi kratko ostala vidljiva
    // na položaju PROŠLOG koraka, na pogrešnom mjestu na ekranu.
    setSpotRect(null);

    let odustani = false;
    const timeouts: ReturnType<typeof setTimeout>[] = [];
    const pocetak = Date.now();

    const trazi = () => {
      if (odustani) return;

      const nadjen = nadjiCilj(korak.target);
      if (nadjen) {
        setCiljSpreman(true);
        setSpotRect(nadjen.getBoundingClientRect());
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

  // Rupa prati cilj i IZMEĐU namjesti()-jevih tri mjerenja — na svaki skrol,
  // ne samo na 90/480/900ms. Bez ovoga rupa bi tijekom glatkog automatskog
  // skrola (ili ručnog skrola korisnika dok je korak otvoren) kaskadno
  // „skakala" umjesto da glatko prati cilj.
  useEffect(() => {
    if (!open || !ciljSpreman) return;
    let raf = 0;
    const onScroll = () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(osvjeziRupu);
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("scroll", onScroll);
    };
  }, [open, ciljSpreman, osvjeziRupu]);

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

  // ── Rupa u zatamnjenju (spotlight) ──
  //
  // `spotRect` je jedini ulaz — rečeno je RECT, ne element, pa nema veze gdje
  // cilj živi u DOM-u ni je li mu ikoji roditelj sticky/transform/opacity.
  // Rupa je JEDAN `fixed` element s dva sloja `box-shadow`: uski prsten oko
  // ruba i golem raspon (9999px) koji prekriva sve OSTALO. Klik na zatamnjeno
  // i dalje zatvara vodič — pokrivaju ga četiri pravokutnika koja popločuju
  // ekran oko rupe (rupa sama ima `pointer-events: none`, pa klik/upis u
  // stvarno polje ispod nje i dalje prolazi do njega).
  const rupa = spotRect
    ? {
        top: spotRect.top - SPOT_PADDING,
        left: spotRect.left - SPOT_PADDING,
        right: spotRect.right + SPOT_PADDING,
        bottom: spotRect.bottom + SPOT_PADDING,
      }
    : null;

  const overlay = (
    <>
      {rupa ? (
        <>
          {/* Četiri pravokutnika oko rupe — hvataju klik izvan cilja (zatvara
              vodič), bez ijednog piksela preko samog cilja. Sva geometrija ide
              kroz `style`, namjerno bez ijedne Tailwind pozicijske klase
              (`top-0` i sl.) — dvije definicije istog svojstva na istom
              elementu, makar inline uvijek pobjeđuje, čita se kao proturječje
              i vodi u istu vrstu tihe greške koju je ovaj redizajn trebao
              ukloniti. */}
          <div
            data-testid="vodic-hvatac"
            className="fixed z-[200] pointer-events-auto vodic-fade print:hidden"
            style={{ top: 0, left: 0, right: 0, height: Math.max(0, rupa.top) }}
            onClick={() => close(false)}
          />
          <div
            data-testid="vodic-hvatac"
            className="fixed z-[200] pointer-events-auto vodic-fade print:hidden"
            style={{ top: rupa.bottom, left: 0, right: 0, bottom: 0 }}
            onClick={() => close(false)}
          />
          <div
            data-testid="vodic-hvatac"
            className="fixed z-[200] pointer-events-auto vodic-fade print:hidden"
            style={{
              top: rupa.top,
              left: 0,
              width: Math.max(0, rupa.left),
              height: Math.max(0, rupa.bottom - rupa.top),
            }}
            onClick={() => close(false)}
          />
          <div
            data-testid="vodic-hvatac"
            className="fixed z-[200] pointer-events-auto vodic-fade print:hidden"
            style={{
              top: rupa.top,
              left: rupa.right,
              right: 0,
              height: Math.max(0, rupa.bottom - rupa.top),
            }}
            onClick={() => close(false)}
          />
          {/* Sama rupa: prsten + zatamnjenje svega izvan sebe, oboje preko
              box-shadowa. `pointer-events: none` pušta klik/fokus do
              stvarnog cilja ispod. */}
          <div
            data-testid="vodic-rupa"
            className="fixed z-[205] pointer-events-none rounded-lg vodic-fade print:hidden"
            style={{
              top: rupa.top,
              left: rupa.left,
              width: Math.max(0, rupa.right - rupa.left),
              height: Math.max(0, rupa.bottom - rupa.top),
              boxShadow: "0 0 0 2px #6366f1, 0 0 0 9999px rgba(15, 23, 42, 0.5)",
            }}
          />
        </>
      ) : (
        /* Korak bez cilja — ravnomjerno zatamnjenje, klik zatvara. Inline rgba
           umjesto Tailwind opacity-modifier klase, koja se u Tailwind v4
           renderira kao color-mix() — zaobilazi problem s iscrtavanjem koji je
           zatamnjenje činio nevidljivim u produkciji iz razloga koji se nisu
           dali utvrditi samo iz izvornog koda. */
        <div
          data-testid="vodic-zatamnjenje-puno"
          className="fixed inset-0 z-[200] pointer-events-auto vodic-fade print:hidden"
          style={{ backgroundColor: "rgba(15, 23, 42, 0.5)" }}
          onClick={() => close(false)}
        />
      )}

      {/*
        The card lives in its OWN layer at z-[220] — above the spotlight
        (z-[205]) and the click-catching bands (z-[200]).
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
