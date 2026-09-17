/**
 * Dokument koji ide sudu.
 *
 * Provjerava se ono što se na ispisu ne smije dogoditi: prazan odjeljak bez
 * stavaka, ime aplikacije na papiru, i iznos bez dvije decimale sa zarezom.
 * Prijelom stranica je stvar CSS-a i ovdje se ne da izmjeriti.
 */
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import TroskovnikIspis from "@/components/troskovnik/TroskovnikIspis";
import type { TroskovnikIzvozModel } from "@/lib/troskovnik-izvoz-model";
import { NAZIV_TARIFE } from "@/lib/tarifa";

function model(preinake: Partial<TroskovnikIzvozModel> = {}): TroskovnikIzvozModel {
  return {
    odvjetnik: { ime: "Ana Anić", adresa: "S. Radića 24", grad: "Daruvar", oib: "01234567890" },
    sud: "Općinski sud u Daruvaru",
    brojPredmeta: "P-123/24",
    zastupana: { polozaj: "Tužitelj", ime: "Ivan Horvat" },
    protustranka: { polozaj: "Tuženik", ime: "Marko Marić" },
    datumIzrade: new Date("2026-08-23T10:00:00Z"),
    radnje: [
      {
        opis: "Sastav tužbe",
        tarifniBroj: "Tbr. 7.1",
        datum: new Date("2026-04-11T12:00:00Z"),
        kolicina: 1,
        bodovaPoJedinici: 100,
        ukupnoBodova: 100,
        iznos: 200,
      },
    ],
    zbrojNagrade: { bodovi: 100, iznos: 200 },
    naknadaTroskova: 0,
    pdv: null,
    pristojbe: [],
    ukupnoPristojbe: 0,
    predujmovi: [],
    ukupnoPredujmovi: 0,
    sveukupno: 200,
    pravnaOsnova: {
      zabiljezena: true,
      // Puni naziv, s komorom u zagradi — na papiru se zagrada mora izgubiti.
      tarifaNaziv: NAZIV_TARIFE,
      tarifaVrijediOd: new Date(2023, 9, 14),
      vrijednostBoda: 2,
      uredbe: ["Uredba o Tarifi sudskih pristojbi (NN 50/2026)"],
      datum: new Date(2026, 7, 23, 10),
    },
    ...preinake,
  };
}

describe("TroskovnikIspis", () => {
  it("stavlja podatke stranaka u zaglavlje, po procesnoj ulozi", () => {
    render(<TroskovnikIspis model={model()} />);
    expect(screen.getByText("Sud:")).toBeInTheDocument();
    expect(screen.getByText("Općinski sud u Daruvaru")).toBeInTheDocument();
    expect(screen.getByText("Tužitelj:")).toBeInTheDocument();
    expect(screen.getByText("Ivan Horvat")).toBeInTheDocument();
    expect(screen.getByText("Tuženik:")).toBeInTheDocument();
    expect(screen.getByText("Posl. br.:")).toBeInTheDocument();
  });

  it("kad uloga nije odabrana, koristi neutralnu oznaku", () => {
    render(<TroskovnikIspis model={model({ zastupana: { ime: "Ivan Horvat" } })} />);
    expect(screen.getByText("Zastupana stranka:")).toBeInTheDocument();
  });

  it("iznos je desno poravnat, s dvije decimale i zarezom", () => {
    render(<TroskovnikIspis model={model()} />);
    const iznos = screen.getAllByText("200,00 €")[0];
    expect(iznos).toHaveClass("iznos");
    expect(iznos.tagName).toBe("TD");
  });

  it("datum je u obliku dd.MM.yyyy", () => {
    render(<TroskovnikIspis model={model()} />);
    expect(screen.getByText("11.04.2026")).toBeInTheDocument();
  });

  it("zaglavlje tablice postoji u <thead>, da se može ponavljati preko stranica", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    const thead = container.querySelector("thead");
    expect(thead).not.toBeNull();
    expect(within(thead as HTMLElement).getByText("Radnja")).toBeInTheDocument();
    expect(within(thead as HTMLElement).getByText("Tar. br.")).toBeInTheDocument();
    expect(within(thead as HTMLElement).getByText("Napomena")).toBeInTheDocument();
  });

  it("bez PDV-a nema redaka osnovice ni PDV-a", () => {
    render(<TroskovnikIspis model={model({ pdv: null })} />);
    expect(screen.queryByText(/Osnovica/)).not.toBeInTheDocument();
    expect(screen.queryByText(/PDV \(25%\)/)).not.toBeInTheDocument();
  });

  it("s PDV-om se osnovica, PDV i ukupno pojave", () => {
    render(
      <TroskovnikIspis model={model({ pdv: { osnovica: 200, iznos: 50, ukupno: 250 } })} />,
    );
    expect(screen.getByText("Osnovica (bez PDV):")).toBeInTheDocument();
    expect(screen.getByText("PDV (25%):")).toBeInTheDocument();
    expect(screen.getByText("250,00 €")).toBeInTheDocument();
  });

  it("bez pristojbi i predujmova nema ni razdjelnog praznog retka", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    expect(container.querySelector(".razmak")).toBeNull();
  });

  it("ponavlja naziv radnje samo kad se promijeni", () => {
    const r = model().radnje[0];
    const { container } = render(
      <TroskovnikIspis
        model={model({
          radnje: [r, { ...r, datum: new Date("2026-05-02T12:00:00Z") }, { ...r, opis: "Pristup ročištu" }],
        })}
      />,
    );
    const prviStupac = Array.from(container.querySelectorAll("tbody tr")).map(
      (tr) => tr.querySelector("td")?.textContent,
    );
    expect(prviStupac.slice(0, 3)).toEqual(["Sastav tužbe", "", "Pristup ročištu"]);
  });

  it("na dokumentu nema imena aplikacije ni ograde o izračunu", () => {
    const { container } = render(
      <TroskovnikIspis model={model({ napomena: "Traži se naknada troška." })} />,
    );
    const tekst = container.textContent ?? "";
    expect(tekst).not.toMatch(/PravoTek/i);
    expect(tekst).not.toMatch(/orijentacijski/i);
    expect(tekst).not.toMatch(/Hrvatska odvjetnička komora/i);
    expect(tekst).toContain("Traži se naknada troška.");
  });

  it("na dnu stoji neutralna bilješka o propisima i danu", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    const biljeska = container.querySelector(".pravna-osnova");
    expect(biljeska).not.toBeNull();
    expect(biljeska!.textContent).toContain("Obračun prema propisima:");
    expect(biljeska!.textContent).toContain("(NN 50/2026)");
    expect(biljeska!.textContent).toContain("Stanje propisa na dan 23.08.2026.");
    expect(biljeska!.textContent).not.toMatch(/odgovara/i);
  });

  it("kad propis nije zabilježen (stari zapis), bilješke nema", () => {
    const { container } = render(
      <TroskovnikIspis
        model={model({ pravnaOsnova: { zabiljezena: false, uredbe: [], datum: new Date() } })}
      />,
    );
    expect(container.querySelector(".pravna-osnova")).toBeNull();
  });

  it("bez napomene nema odjeljka napomene", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    expect(container.querySelector(".napomena")).toBeNull();
  });

  it("potpisuje se imenom odvjetnika, bez rodno određene oznake", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    const potpis = container.querySelector(".potpis");
    expect(potpis).not.toBeNull();
    expect(potpis!.textContent).toContain("Ana Anić");
    expect(potpis!.textContent).not.toMatch(/Odvjetnic?[ai]/);
  });
});
