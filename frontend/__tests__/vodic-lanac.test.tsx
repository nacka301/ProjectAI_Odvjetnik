/**
 * Vodič kroz dva modula — predaja štafete i kraj.
 *
 * Bitno je samo jedno pravilo, ali ono se lako slomi: „onboarded" se smije
 * upisati tek kad je uvod stvarno gotov. Ako se upiše na kraju prve dionice,
 * korisnik koji osvježi stranicu usred druge izgubi ostatak zauvijek i nema
 * načina da ga vrati. Zato se ovdje mjeri TKO zove `markOnboarded`, a ne kako
 * kartica izgleda.
 */
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const markOnboarded = vi.fn();
vi.mock("@/lib/onboarding", () => ({
  markOnboarded: () => markOnboarded(),
}));

// jsdom nema ResizeObserver, a kartica ga koristi da se premjesti kad joj se
// promijeni visina. Ovdje se ništa ne mjeri — u jsdom-u su sve dimenzije nula —
// pa je prazan nadomjestak dovoljan da se komponenta uopće montira.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
vi.stubGlobal("ResizeObserver", ResizeObserverStub);

import Vodic, {
  STRANKE_KORACI,
  TROSKOVNIK_KORACI,
  type VodicKorak,
} from "@/components/Vodic";

const DVA_KORAKA: VodicKorak[] = [
  { target: "a", title: "1/2 · Prvi", text: "Prvi korak." },
  { target: "b", title: "2/2 · Drugi", text: "Drugi korak." },
];

beforeEach(() => {
  markOnboarded.mockClear();
});

describe("predaja štafete", () => {
  it("zadnji korak dionice s nastavkom predaje dalje i NE upisuje onboarded", async () => {
    const onNastavak = vi.fn();
    const onClose = vi.fn();

    render(
      <Vodic
        open
        koraci={DVA_KORAKA}
        onClose={onClose}
        onStepChange={() => {}}
        onNastavak={onNastavak}
        nastavakOznaka="Dalje: Stranke"
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: /Dalje$/ }));
    await userEvent.click(screen.getByRole("button", { name: /Dalje: Stranke/ }));

    expect(onNastavak).toHaveBeenCalledOnce();
    expect(markOnboarded).not.toHaveBeenCalled();
    // Predaja NE zatvara karticu sama: `onNastavak` odvodi na drugu rutu, pa se
    // stranica odjavi sama. Da se ovdje zvao i `onClose`, stranica bi uz njega
    // ispalila i drugu promjenu adrese koja se utrkuje s odlaskom.
    expect(onClose).not.toHaveBeenCalled();
  });

  it("zadnji korak zadnje dionice završava i upisuje onboarded", async () => {
    const onClose = vi.fn();

    render(
      <Vodic open koraci={DVA_KORAKA} onClose={onClose} onStepChange={() => {}} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /Dalje$/ }));
    await userEvent.click(screen.getByRole("button", { name: /Završi/ }));

    expect(onClose).toHaveBeenCalledOnce();
    expect(markOnboarded).toHaveBeenCalledOnce();
  });

  it("Preskoči vodič prekida cijeli uvod i kad dionica ima nastavak", async () => {
    const onNastavak = vi.fn();

    render(
      <Vodic
        open
        koraci={DVA_KORAKA}
        onClose={() => {}}
        onStepChange={() => {}}
        onNastavak={onNastavak}
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: "Preskoči vodič" }));

    // Preskakanje znači „ne pitaj me više" — ne vodi u sljedeći modul.
    expect(markOnboarded).toHaveBeenCalledOnce();
    expect(onNastavak).not.toHaveBeenCalled();
  });
});

describe("numeracija kroz obje dionice", () => {
  it("teče 1/7 … 7/7 bez preskakanja i bez ponavljanja", () => {
    const sviKoraci = [...TROSKOVNIK_KORACI, ...STRANKE_KORACI];
    const ukupno = sviKoraci.length;

    expect(sviKoraci.map((k) => k.title.split(" ")[0])).toEqual(
      Array.from({ length: ukupno }, (_, i) => `${i + 1}/${ukupno}`),
    );
  });

  it("nijedna meta se ne ponavlja — dvije bi se ringale istovremeno", () => {
    const mete = [...TROSKOVNIK_KORACI, ...STRANKE_KORACI].map((k) => k.target);
    expect(new Set(mete).size).toBe(mete.length);
  });
});
