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
    expect(within(thead as HTMLElement).getByText("Datum")).toBeInTheDocument();
  });

  it("stupac Datum se ne prikazuje kad nijedan redak nema datum", () => {
    const { container } = render(
      <TroskovnikIspis
        model={model({
          radnje: [
            { opis: "Sastav tužbe", tarifniBroj: "Tbr. 7.1", datum: null, kolicina: 1, bodovaPoJedinici: 100, ukupnoBodova: 100, iznos: 200 },
          ],
        })}
      />,
    );
    expect(screen.queryByText("Datum")).not.toBeInTheDocument();
    expect(container.querySelector("table")).toHaveClass("bez-datuma");
  });

  it("stupac Napomena se ne ispisuje — nijedan izvor podataka ga danas ne puni", () => {
    render(<TroskovnikIspis model={model()} />);
    expect(screen.queryByText("Napomena")).not.toBeInTheDocument();
  });

  it("bez PDV-a obračun nema PDV redaka, ni redak Osnovica ne postoji nigdje", () => {
    render(<TroskovnikIspis model={model({ pdv: null })} />);
    expect(screen.queryByText(/Osnovica/)).not.toBeInTheDocument();
    expect(screen.queryByText(/^PDV/)).not.toBeInTheDocument();
    expect(screen.queryByText("Nagrada s PDV-om")).not.toBeInTheDocument();
  });

  it("s PDV-om se prikazuju PDV i Nagrada s PDV-om, s pravim postotkom", () => {
    render(
      <TroskovnikIspis
        model={model({ pdv: { osnovica: 200, iznos: 44, ukupno: 244, stopa: 22 } })}
      />,
    );
    expect(screen.getByText("PDV (22 %)")).toBeInTheDocument();
    expect(screen.getByText("Nagrada s PDV-om")).toBeInTheDocument();
    expect(screen.getByText("244,00 €")).toBeInTheDocument();
    // "Osnovica" više ne postoji nigdje — bio je isti broj kao "Nagrada (zbroj radnji)".
    expect(screen.queryByText(/Osnovica/)).not.toBeInTheDocument();
  });

  it("obračun: Nagrada (zbroj radnji) je čisti zbroj radnji, bez naknade troškova", () => {
    render(
      <TroskovnikIspis
        model={model({ naknadaTroskova: 30, sveukupno: 230 })}
      />,
    );
    // Radnja u fixtureu iznosi 200,00 € — naknada (30) se NE smije uračunati ovdje,
    // inače bi se udvostručila uz zaseban redak "Naknada troškova".
    expect(screen.getByText("Nagrada (zbroj radnji)")).toBeInTheDocument();
    expect(screen.getAllByText("200,00 €").length).toBeGreaterThan(0);
    expect(screen.getByText("Naknada troškova (Tbr. 48.-51.)")).toBeInTheDocument();
    expect(screen.getByText("30,00 €")).toBeInTheDocument();
  });

  it("naknada troškova se ne ispisuje kad je nula", () => {
    render(<TroskovnikIspis model={model({ naknadaTroskova: 0 })} />);
    expect(screen.queryByText(/Naknada troškova/)).not.toBeInTheDocument();
  });

  it("sudske pristojbe i izdaci se ne ispisuju u obračunu kad su nula, čak i s praznim popisom", () => {
    render(
      <TroskovnikIspis
        model={model({ pristojbe: [], ukupnoPristojbe: 0, predujmovi: [], ukupnoPredujmovi: 0 })}
      />,
    );
    expect(screen.queryByText("Sudske pristojbe")).not.toBeInTheDocument();
    expect(screen.queryByText("Izdaci i predujmovi")).not.toBeInTheDocument();
  });

  it("sudske pristojbe i izdaci u obračunu, uz pojedinačne stavke koje ostaju u tablici", () => {
    const { container } = render(
      <TroskovnikIspis
        model={model({
          pristojbe: [{ opis: "Sudska pristojba — Tar. br. 1 st. 2", datum: null, iznos: 50 }],
          ukupnoPristojbe: 50,
          predujmovi: [{ opis: "Predujam za vještačenje", datum: null, iznos: 100 }],
          ukupnoPredujmovi: 100,
          sveukupno: 350,
        })}
      />,
    );
    // Pojedinačna stavka i dalje redak u tablici.
    expect(screen.getByText("Sudska pristojba — Tar. br. 1 st. 2")).toBeInTheDocument();
    expect(screen.getByText("Predujam za vještačenje")).toBeInTheDocument();
    // Zbroj po kategoriji je u obračunu, izvan tablice.
    const obracun = container.querySelector(".obracun");
    expect(obracun!.textContent).toContain("Sudske pristojbe");
    expect(obracun!.textContent).toContain("Izdaci i predujmovi");
    // Stari zbrojni retci unutar tablice više ne postoje.
    expect(screen.queryByText("Ukupno pristojbe:")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupno predujmovi:")).not.toBeInTheDocument();
  });

  it("jedino SVEUKUPNO je podebljano i vidljivo veće u obračunu", () => {
    const { container } = render(
      <TroskovnikIspis model={model({ pdv: { osnovica: 200, iznos: 50, ukupno: 250, stopa: 25 } })} />,
    );
    const veliki = container.querySelectorAll(".obracun-redak.veliko");
    expect(veliki).toHaveLength(1);
    expect(veliki[0].textContent).toContain("SVEUKUPNO");
  });

  it("obračun se ne prelama (break-inside: avoid je stvar CSS-a, ovdje se provjerava da je jedan blok)", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    expect(container.querySelectorAll(".obracun")).toHaveLength(1);
  });

  it("bez pristojbi i predujmova nema ni razdjelnog praznog retka u tablici", () => {
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

  it("potpis ima mjesto i datum uz crtu za potpis", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    const potpis = container.querySelector(".potpis");
    expect(potpis!.textContent).toContain("Mjesto: Daruvar");
    expect(potpis!.textContent).toContain("Datum: 23.08.2026");
    expect(container.querySelector(".potpis-crta .crta")).not.toBeNull();
  });

  it("potpis prikazuje osobno ime odvjetnika ispod naziva ureda, kad postoji", () => {
    const { container } = render(
      <TroskovnikIspis
        model={model({
          odvjetnik: { ime: "Odvjetnički ured Anić", grad: "Daruvar", osobnoIme: "Ana Anić" },
        })}
      />,
    );
    const potpis = container.querySelector(".potpis");
    expect(potpis!.textContent).toContain("Odvjetnički ured Anić");
    expect(potpis!.textContent).toContain("Ana Anić");
    expect(container.querySelector(".osobno-ime")).not.toBeNull();
  });

  it("bez osobnog imena potpis izostavlja taj redak", () => {
    const { container } = render(<TroskovnikIspis model={model()} />);
    expect(container.querySelector(".osobno-ime")).toBeNull();
  });

  it("zaglavlje prikazuje telefon i e-mail ureda kad postoje, ne korisnički mail", () => {
    const { container } = render(
      <TroskovnikIspis
        model={model({
          odvjetnik: { ime: "Ana Anić", telefon: "01 234 5678", email: "ured@primjer.hr" },
        })}
      />,
    );
    const zaglavlje = container.querySelector(".zaglavlje");
    expect(zaglavlje!.textContent).toContain("01 234 5678");
    expect(zaglavlje!.textContent).toContain("ured@primjer.hr");
  });

  it("predmet blok prikazuje VPS i vrstu postupka kad postoje", () => {
    render(
      <TroskovnikIspis
        model={model({ vrijednostSpora: 15000, vrstaPostupkaNaziv: "Nenovčani zahtjev" })}
      />,
    );
    expect(screen.getByText("VPS:")).toBeInTheDocument();
    expect(screen.getByText("15.000,00 €")).toBeInTheDocument();
    expect(screen.getByText("Vrsta postupka:")).toBeInTheDocument();
    expect(screen.getByText("Nenovčani zahtjev")).toBeInTheDocument();
  });
});
