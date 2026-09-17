/**
 * Bilješka o tome po čemu je troškovnik izračunat (plan pravne zaštite, dio A).
 *
 * Dvije su inačice i ne smiju se pomiješati: neutralna ide na papir koji se
 * prilaže sudu (bez imena alata i bez ograde), puna stoji u aplikaciji i Excelu.
 * I nijedna ne smije izmisliti propis za troškovnik kojem propis nije zapisan.
 */
import { describe, expect, it } from "vitest";
import {
  NAZIV_UREDBE_NN37_2023,
  NAZIV_UREDBE_NN50_2026,
  bezZavrsneZagrade,
  datumBezVremena,
  neutralnaBiljeska,
  punaBiljeska,
  uredbeIzStavaka,
  type PravnaOsnova,
} from "@/lib/pravna-osnova";
import { buildIzvozModel, type IzvozUlaz } from "@/lib/troskovnik-izvoz-model";
import { izvozModelIzZapisa, pravnaOsnovaIzZapisa } from "@/lib/troskovnik-zapis";
import { NAZIV_TARIFE, type TarifaVerzija } from "@/lib/tarifa";
import type { TroskovnikRow } from "@/lib/supabase";

function osnova(preinake: Partial<PravnaOsnova> = {}): PravnaOsnova {
  return {
    zabiljezena: true,
    tarifaNaziv: NAZIV_TARIFE,
    tarifaVrijediOd: new Date(2023, 9, 14),
    vrijednostBoda: 2,
    uredbe: [NAZIV_UREDBE_NN50_2026],
    datum: new Date(2026, 8, 17, 10, 30),
    ...preinake,
  };
}

describe("neutralnaBiljeska — papir za sud", () => {
  it("navodi propise i dan, bez imena alata, komore i ograde", () => {
    expect(neutralnaBiljeska(osnova())).toBe(
      "Obračun prema propisima: Tarifa o nagradama i naknadi troškova za rad odvjetnika " +
        "(na snazi od 14.10.2023.); Uredba o Tarifi sudskih pristojbi (NN 50/2026). " +
        "Stanje propisa na dan 17.09.2026.",
    );
    const tekst = neutralnaBiljeska(osnova())!;
    expect(tekst).not.toMatch(/PravoTek/i);
    expect(tekst).not.toMatch(/Hrvatska odvjetnička komora/i);
    expect(tekst).not.toMatch(/odgovara/i);
  });

  it("s obje Uredbe navodi obje, stariju prvu", () => {
    const tekst = neutralnaBiljeska(osnova({ uredbe: [NAZIV_UREDBE_NN37_2023, NAZIV_UREDBE_NN50_2026] }))!;
    expect(tekst).toContain("(NN 37/2023); Uredba o Tarifi sudskih pristojbi (NN 50/2026).");
  });

  it("bez pristojbi navodi samo Tarifu", () => {
    const tekst = neutralnaBiljeska(osnova({ uredbe: [] }))!;
    expect(tekst).not.toContain("Uredba");
    expect(tekst).toContain("(na snazi od 14.10.2023.). Stanje propisa");
  });

  it("kad propis nije zabilježen, na papir ne ide ništa", () => {
    expect(neutralnaBiljeska(osnova({ zabiljezena: false }))).toBeNull();
    expect(neutralnaBiljeska(osnova({ tarifaNaziv: undefined }))).toBeNull();
  });
});

describe("punaBiljeska — aplikacija i Excel", () => {
  it("kaže alat, dan, propise s vrijednošću boda i tko odgovara", () => {
    expect(punaBiljeska(osnova())).toBe(
      "Izračunato u alatu PravoTek 17.09.2026. prema propisima: Tarifa o nagradama i naknadi " +
        "troškova za rad odvjetnika (na snazi od 14.10.2023., 1 bod = 2,00 €); " +
        "Uredba o Tarifi sudskih pristojbi (NN 50/2026). " +
        "Izračun provjerava i za njega odgovara odvjetnik.",
    );
  });

  it("za stari zapis ne izmišlja propis, nego kaže da nije zabilježen", () => {
    const tekst = punaBiljeska(osnova({ zabiljezena: false, tarifaNaziv: undefined, uredbe: [] }));
    expect(tekst).toContain("nije zabilježen");
    expect(tekst).not.toContain("Tarifa");
    expect(tekst).toContain("odgovara odvjetnik");
  });
});

describe("pomoćne", () => {
  it("datum bez vremena ostaje na svom danu", () => {
    const d = datumBezVremena("2023-10-14")!;
    expect([d.getFullYear(), d.getMonth(), d.getDate()]).toEqual([2023, 9, 14]);
    expect(datumBezVremena(null)).toBeNull();
    expect(datumBezVremena("nije datum")).toBeNull();
  });

  it("naziv gubi samo završnu zagradu", () => {
    expect(bezZavrsneZagrade(NAZIV_TARIFE)).toBe("Tarifa o nagradama i naknadi troškova za rad odvjetnika");
    expect(bezZavrsneZagrade("Bez zagrade")).toBe("Bez zagrade");
  });

  it("Uredbe iz stavaka: stara prva, bez ponavljanja, prazno kad nema stavaka", () => {
    expect(uredbeIzStavaka([{ uredba: "NN 50/2026" }, { uredba: "NN 37/2023" }, { uredba: "NN 50/2026" }])).toEqual([
      NAZIV_UREDBE_NN37_2023,
      NAZIV_UREDBE_NN50_2026,
    ]);
    expect(uredbeIzStavaka([{ uredba: "NN 37/2023" }])).toEqual([NAZIV_UREDBE_NN37_2023]);
    expect(uredbeIzStavaka([{}])).toEqual([NAZIV_UREDBE_NN50_2026]);
    expect(uredbeIzStavaka([])).toEqual([]);
  });
});

// ── Odakle model dobiva pravnu osnovu ─────────────────────────────

const PRAZNI_ULAZ: IzvozUlaz = {
  result: null,
  liveCalc: {
    lines: [],
    ukupnoBodova: 0,
    naknadaTroskovaUkupno: 0,
    osnovicaSaNaknadom: 0,
    pdv: 0,
    ukupnoSPdv: 0,
    ukupnoPristojbe: 0,
    ukupnoPredujmovi: 0,
    sveukupniTrosak: 0,
  },
  pristojbeneStavke: [],
  dodatnePristojbe: [],
  predujmovi: [],
  isPdvUkljucen: false,
  napomena: "",
  polja: {
    odvjetnikIme: "",
    odvjetnikOib: "",
    odvjetnikAdresa: "",
    odvjetnikGrad: "",
    odvjetnikIban: "",
    klijentIme: "",
    zastupanaPolozaj: "",
    protustrankaIme: "",
    protustrankaPolozaj: "",
    predmetSud: "",
    predmetPoslovniBroj: "",
  },
};

function rezultat(preinake: Record<string, unknown> = {}) {
  return {
    stavke: [],
    ukupno_bodova: 0,
    ukupno_eur_bez_pdv: 0,
    pdv: 0,
    ukupno_eur_s_pdv: 0,
    datum_izrade: "2026-09-17T08:30:00Z",
    klijent: null,
    odvjetnik: null,
    ...preinake,
  } as unknown as IzvozUlaz["result"];
}

describe("buildIzvozModel — pravna osnova", () => {
  it("izračun s backenda ima prednost: njegova Tarifa, Uredbe i datum", () => {
    const m = buildIzvozModel({
      ...PRAZNI_ULAZ,
      result: rezultat({
        tarifa_naziv: "Tarifa iz backenda",
        tarifa_vrijedi_od: "2027-01-01",
        vrijednost_boda: 2.5,
        uredbe_pristojbe: [NAZIV_UREDBE_NN37_2023],
      }),
    });
    expect(m.pravnaOsnova.zabiljezena).toBe(true);
    expect(m.pravnaOsnova.tarifaNaziv).toBe("Tarifa iz backenda");
    expect(m.pravnaOsnova.tarifaVrijediOd?.getFullYear()).toBe(2027);
    expect(m.pravnaOsnova.vrijednostBoda).toBe(2.5);
    expect(m.pravnaOsnova.uredbe).toEqual([NAZIV_UREDBE_NN37_2023]);
    expect(m.pravnaOsnova.datum).toEqual(new Date("2026-09-17T08:30:00Z"));
  });

  it("bez izračuna s backenda: Tarifa s obrasca i Uredbe iz pristojbenih stavaka", () => {
    const tarifa: TarifaVerzija = { naziv: "Tarifa s obrasca", bodValue: 2, vrijediOd: "2023-10-14" };
    const m = buildIzvozModel({
      ...PRAZNI_ULAZ,
      tarifa,
      pristojbeneStavke: [
        { tarifni_broj: "Tar. br. 1", kolicina: 1, iznos: 100, uredba: "NN 50/2026" },
        { tarifni_broj: "Tar. br. 1", kolicina: 1, iznos: 90, uredba: "NN 37/2023" },
      ],
    });
    expect(m.pravnaOsnova.tarifaNaziv).toBe("Tarifa s obrasca");
    expect(m.pravnaOsnova.uredbe).toEqual([NAZIV_UREDBE_NN37_2023, NAZIV_UREDBE_NN50_2026]);
  });

  it("stariji backend bez polja: vrijedi Tarifa obrasca, Uredbe iz stavaka rezultata", () => {
    const m = buildIzvozModel({
      ...PRAZNI_ULAZ,
      result: rezultat({
        pristojbene_stavke: [{ tarifni_broj: "Tar. br. 1", kolicina: 1, iznos: 90, uredba: "NN 37/2023" }],
      }),
    });
    expect(m.pravnaOsnova.tarifaNaziv).toBe(NAZIV_TARIFE);
    expect(m.pravnaOsnova.uredbe).toEqual([NAZIV_UREDBE_NN37_2023]);
  });
});

// ── Spremljeni zapis ──────────────────────────────────────────────

function red(preinake: Partial<TroskovnikRow> = {}): TroskovnikRow {
  return {
    id: "t-1",
    created_at: "2026-09-10T09:00:00Z",
    client_id: null,
    case_id: null,
    opis_predmeta: null,
    vrijednost_spora: 1000,
    ukupno_bodova: 100,
    ukupno_eur_bez_pdv: 200,
    pdv: 0,
    ukupno_eur_s_pdv: 200,
    predmet: null,
    stranke: null,
    odvjetnik: null,
    raw_json_output: {
      stavke: [],
      ukupno_bodova: 100,
      ukupno_eur_bez_pdv: 200,
      pdv: 0,
      ukupno_eur_s_pdv: 200,
      datum_izrade: "2026-09-10T09:00:00Z",
      // Namjerno i u JSON-u: zapis ga ne smije čitati odande, nego iz stupaca.
      tarifa_naziv: NAZIV_TARIFE,
    },
    ...preinake,
  };
}

describe("spremljeni zapis — pravna osnova samo iz stupaca", () => {
  it("zapis prije migracije 025: propis nije zabilježen, ni iz raw_json_output", () => {
    const m = izvozModelIzZapisa(red())!;
    expect(m.pravnaOsnova.zabiljezena).toBe(false);
    expect(neutralnaBiljeska(m.pravnaOsnova)).toBeNull();
  });

  it("zapis s popunjenim stupcima nosi njihove vrijednosti", () => {
    const o = pravnaOsnovaIzZapisa(
      red({
        izracunato_at: "2026-09-17T08:30:00Z",
        tarifa_naziv: NAZIV_TARIFE,
        tarifa_vrijedi_od: "2023-10-14",
        vrijednost_boda: "2.00",
        uredbe_pristojbe: [NAZIV_UREDBE_NN37_2023, NAZIV_UREDBE_NN50_2026],
      }),
    );
    expect(o.zabiljezena).toBe(true);
    expect(o.vrijednostBoda).toBe(2);
    expect(o.tarifaVrijediOd?.getDate()).toBe(14);
    expect(o.uredbe).toHaveLength(2);
    expect(o.datum).toEqual(new Date("2026-09-17T08:30:00Z"));
  });
});
