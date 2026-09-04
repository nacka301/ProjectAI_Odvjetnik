/**
 * Motor vodiča — tipkovnica i korak čiji cilj ne postoji.
 *
 * Dvije stvari koje se lako slome i obje tiho: sidro preimenovano pri redizajnu
 * (vodič bi ostao stajati na praznom okviru dok korisnik ne odustane) i
 * tipkovnica (nikad je nije ni bilo, pa je nitko nije ni testirao).
 *
 * Ne mjeri se GDJE kartica stoji — u jsdom-u su sve dimenzije nula, pa bi takav
 * test tvrdio nešto što ne može znati.
 */
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  usePathname: () => "/troskovnik",
}));

import Vodic from "@/components/Vodic";
import type { VodicKorak } from "@/lib/vodic-koraci";

const KORACI: VodicKorak[] = [
  { target: "prvi", title: "1/3 · Prvi", text: "Prvi korak." },
  { target: "drugi", title: "2/3 · Drugi", text: "Drugi korak." },
  { target: "treci", title: "3/3 · Treći", text: "Treći korak." },
];

const dodana: HTMLElement[] = [];

/** Postavi sidra u DOM; izostavljeno sidro simulira preimenovan element. */
function postaviSidra(...imena: string[]) {
  for (const ime of imena) {
    const el = document.createElement("div");
    el.setAttribute("data-vodic", ime);
    document.body.appendChild(el);
    dodana.push(el);
  }
}

beforeEach(() => {
  push.mockClear();
});

afterEach(() => {
  // Samo sidra koja je test dodao. Brisanje cijelog <body> odnijelo bi i
  // spremnik u koji je vodič portalan, pa bi React pri raspremanju pao na
  // "node to be removed is not a child of this node" — kvar testa, ne koda.
  while (dodana.length) dodana.pop()?.remove();
  vi.restoreAllMocks();
});

describe("tipkovnica", () => {
  it("→ vodi naprijed, ← natrag, Esc zatvara", async () => {
    postaviSidra("prvi", "drugi", "treci");
    const onClose = vi.fn();
    render(<Vodic open koraci={KORACI} onClose={onClose} onStepChange={() => {}} />);

    await userEvent.keyboard("{ArrowRight}");
    expect(await screen.findByText(/2\/3 · Drugi/)).toBeTruthy();

    await userEvent.keyboard("{ArrowLeft}");
    expect(await screen.findByText(/1\/3 · Prvi/)).toBeTruthy();

    await userEvent.keyboard("{Escape}");
    // Esc je preskakanje, ne završetak: nitko ne pritisne Esc misleći „gotov sam".
    expect(onClose).toHaveBeenCalledWith(false);
  });
});

describe("cilj koji ne postoji", () => {
  it("preskače korak i upozorava, umjesto da zaglavi na praznom okviru", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    // Sidro „drugi" nedostaje — kao da ga je redizajn preimenovao.
    postaviSidra("prvi", "treci");

    render(<Vodic open koraci={KORACI} onClose={() => {}} onStepChange={() => {}} />);

    await userEvent.keyboard("{ArrowRight}");

    await waitFor(
      () => expect(screen.getByText(/3\/3 · Treći/)).toBeTruthy(),
      { timeout: 4000 },
    );
    expect(warn).toHaveBeenCalled();
    expect(String(warn.mock.calls[0][0])).toContain("drugi");
  }, 10000);
});

describe("korak bez sidra", () => {
  it("prikazuje se i bez ijednog elementa u DOM-u", async () => {
    const bezSidra: VodicKorak[] = [
      { title: "Ovo je primjer", text: "Uvodna kartica ne pokazuje ni na što." },
    ];
    const onClose = vi.fn();
    render(<Vodic open koraci={bezSidra} onClose={onClose} onStepChange={() => {}} />);

    expect(screen.getByText("Ovo je primjer")).toBeTruthy();

    await userEvent.click(screen.getByRole("button", { name: /Završi/ }));
    // Dolazak do kraja je nešto drugo od preskakanja: samo on otvara prazan
    // troškovnik na izlasku iz prikaza primjera.
    expect(onClose).toHaveBeenCalledWith(true);
  });
});

describe("korak na drugoj ruti", () => {
  it("navigira prije nego potraži svoj cilj", async () => {
    postaviSidra("prvi");
    const koraci: VodicKorak[] = [
      { target: "prvi", title: "1/2 · Ovdje", text: "Na troškovniku." },
      { target: "drugi", ruta: "/klijenti", title: "2/2 · Ondje", text: "Na strankama." },
    ];

    render(<Vodic open koraci={koraci} onClose={() => {}} onStepChange={() => {}} />);
    await userEvent.keyboard("{ArrowRight}");

    await waitFor(() => expect(push).toHaveBeenCalledWith("/klijenti"));
  });
});

describe("rupa u zatamnjenju (spotlight)", () => {
  /**
   * Ovo je jedini razlog zašto se cijeli mehanizam isticanja i mijenjao: cilj
   * se nikad ne dira (ni klasa ni z-index), pa mu roditelj (sticky, blur,
   * transform — bilo koji stacking context) ne može ništa. Rupa se crta IZ
   * IZMJERENOG PRAVOKUTNIKA cilja, potpuno neovisno o tome gdje on živi u
   * DOM-u. Test to i provjerava: cilj ugniježđen unutar `position: sticky`
   * roditelja (koji je do sada svaki put zarobio z-index pristup) i dalje
   * dobiva ispravno pozicioniranu rupu.
   */
  it("cilj UNUTAR sticky roditelja svejedno dobiva rupu na svojim koordinatama", async () => {
    const sticky = document.createElement("div");
    sticky.style.position = "sticky";
    document.body.appendChild(sticky);
    dodana.push(sticky);

    const el = document.createElement("div");
    el.setAttribute("data-vodic", "prvi");
    el.getBoundingClientRect = () =>
      ({ top: 100, left: 50, right: 250, bottom: 180, width: 200, height: 80 }) as DOMRect;
    sticky.appendChild(el);

    render(<Vodic open koraci={KORACI} onClose={() => {}} onStepChange={() => {}} />);

    const rupa = await screen.findByTestId("vodic-rupa");
    expect(rupa.style.top).toBe("94px"); // 100 − SPOT_PADDING(6)
    expect(rupa.style.left).toBe("44px"); // 50 − 6
    expect(rupa.style.width).toBe("212px"); // (250−50) + 2×6
    expect(rupa.style.height).toBe("92px"); // (180−100) + 2×6

    // Puno zatamnjenje se NE koristi kad postoji cilj — samo rupa + četiri
    // hvatača klika oko nje.
    expect(screen.queryByTestId("vodic-zatamnjenje-puno")).toBeNull();
    expect(screen.getAllByTestId("vodic-hvatac")).toHaveLength(4);
  });

  it("korak bez cilja crta puno zatamnjenje, ne rupu", () => {
    const bezSidra: VodicKorak[] = [{ title: "Uvod", text: "Bez cilja." }];
    render(<Vodic open koraci={bezSidra} onClose={() => {}} onStepChange={() => {}} />);

    expect(screen.getByTestId("vodic-zatamnjenje-puno")).toBeTruthy();
    expect(screen.queryByTestId("vodic-rupa")).toBeNull();
    expect(screen.queryAllByTestId("vodic-hvatac")).toHaveLength(0);
  });

  it("klik na hvatač zatvara vodič kao preskakanje", async () => {
    const el = document.createElement("div");
    el.setAttribute("data-vodic", "prvi");
    el.getBoundingClientRect = () =>
      ({ top: 100, left: 50, right: 250, bottom: 180, width: 200, height: 80 }) as DOMRect;
    document.body.appendChild(el);
    dodana.push(el);

    const onClose = vi.fn();
    render(<Vodic open koraci={KORACI} onClose={onClose} onStepChange={() => {}} />);

    const [hvatac] = await screen.findAllByTestId("vodic-hvatac");
    await userEvent.click(hvatac);
    expect(onClose).toHaveBeenCalledWith(false);
  });
});
