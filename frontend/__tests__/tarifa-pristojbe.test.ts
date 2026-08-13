import { describe, expect, it } from "vitest";
import {
  KATEGORIJA_KAZNENI,
  KATEGORIJA_OVRSNI,
  KATEGORIJA_PARNICNI,
  izracunajPristojbu,
  obracunajPristojbe,
  pristojbenaOdredba,
  type TarifnaStavka,
  type UredbaVerzija,
} from "@/lib/tarifa";
import paritet from "../../Backend.Tests/TestData/pristojbe-paritet.json";

/**
 * Sudske pristojbe po Uredbi NN 50/2026.
 *
 * Iznosi po pojedinim odredbama NE stoje u ovoj datoteci: čita ih se iz
 * `Backend.Tests/TestData/pristojbe-paritet.json`, koji istodobno vrti i
 * `Backend.Tests/SudskePristojbeParitetTests.cs`. Sa 35 tarifnih brojeva dva ručno
 * održavana popisa razišla bi se u tišini, pa bi klijent na ekranu vidio jedan iznos,
 * a u PDF-u drugi.
 */

interface ParitetSlucaj {
  odredba: string;
  osnovica: number;
  uredba: string;
  ocekivano: number;
  opis: string;
}

const slucajevi = paritet.slucajevi as ParitetSlucaj[];

function stavka(kategorija: string, id: string): TarifnaStavka {
  return {
    id,
    kategorija,
    opis: "",
    tarifni_broj: "",
    fiksni_body: null,
    tbr71_postotak: null,
    tbr71_povecanje: null,
    max_bodova: null,
    min_bodova: null,
    jedinica: "komad",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
  };
}

function obracun(
  vps: number,
  stavke: TarifnaStavka[],
  dodatno: Partial<Parameters<typeof obracunajPristojbe>[0]> = {},
) {
  return obracunajPristojbe({
    vrijednostSpora: vps,
    odabraneStavke: stavke,
    oslobodjen: false,
    ...dodatno,
  });
}

// ── Paritet s backendom ───────────────────────────────────────────

describe("paritet s backendom", () => {
  it("čita zajedničku datoteku i ona nije prazna", () => {
    expect(slucajevi.length).toBeGreaterThanOrEqual(50);
  });

  it.each(slucajevi)("$odredba @ $osnovica ($uredba) → $ocekivano — $opis", (slucaj) => {
    const verzija = slucaj.uredba as UredbaVerzija;

    expect(pristojbenaOdredba(slucaj.odredba, verzija)).toBeDefined();
    expect(izracunajPristojbu(slucaj.odredba, slucaj.osnovica, verzija)).toBe(slucaj.ocekivano);
  });
});

// ── Struktura po poglavljima ──────────────────────────────────────

describe("Tar. br. 1 se primjenjuje samo u Poglavlju I.", () => {
  it("tužba u parnici ide po ljestvici prema VPS-u", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_PARNICNI, "par_tuzba")]);

    expect(rezultat.ukupno).toBe(620);
    expect(rezultat.stavke[0].tarifni_broj).toBe("Tar. br. 1 st. 1");
  });

  it("tužba u upravnom sporu je fiksnih 110,00 bez obzira na VPS", () => {
    for (const vps of [5_000, 50_000, 5_000_000]) {
      const rezultat = obracun(vps, [stavka("Upravni spor", "usp_tuzba_proc")]);

      expect(rezultat.ukupno).toBe(110);
      expect(rezultat.stavke[0].poglavlje).toBe("VIII. Upravni spor");
    }
  });

  it("izvanparnična dioba ide po Tar. br. 5, ne po Tar. br. 1", () => {
    const rezultat = obracun(10_000, [stavka("Izvanparnični postupak", "izv_dioba")]);

    expect(rezultat.ukupno).toBe(150);
    expect(rezultat.stavke[0].poglavlje).toBe("II. Izvanparnični postupak");
  });

  it("zemljišnoknjižni prijedlog je fiksnih 10,00", () => {
    expect(obracun(300_000, [stavka("Zemljišnoknjižni postupak", "zk_redoviti")]).ukupno).toBe(10);
  });
});

// ── Tar. br. 3 — pravni lijekovi ──────────────────────────────────

describe("Tar. br. 3 — pravni lijekovi", () => {
  it("žalba protiv presude je uvećana za 100 %", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_PARNICNI, "par_zalba_presuda")]);

    expect(rezultat.ukupno).toBe(1_240);
    expect(rezultat.stavke[0].tarifni_broj).toBe("Tar. br. 3 st. 1");
  });

  it("žalba protiv rješenja je puna pristojba iz st. 1, bez uvećanja", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_PARNICNI, "par_zalba_rjesenje")]);

    expect(rezultat.ukupno).toBe(620);
    expect(rezultat.stavke[0].tarifni_broj).toBe("Tar. br. 3 st. 2");
  });

  it("odgovor na žalbu nije pravni lijek — plaća se polovica", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_PARNICNI, "par_odgovor_zalba")]);

    expect(rezultat.ukupno).toBe(310);
    expect(rezultat.stavke[0].tarifni_broj).toBe("Tar. br. 1 st. 2");
  });
});

// ── Više radnji, više pristojbi ───────────────────────────────────

describe("Uredba naplaćuje po radnji", () => {
  it("tužba i žalba daju dva retka", () => {
    const rezultat = obracun(50_000, [
      stavka(KATEGORIJA_PARNICNI, "par_tuzba"),
      stavka(KATEGORIJA_PARNICNI, "par_zalba_presuda"),
    ]);

    expect(rezultat.stavke.map((s) => s.odredba_id)).toEqual(["tbr1_st1", "tbr3_st1"]);
    expect(rezultat.ukupno).toBe(1_860);
  });

  it("dvije stavke koje vode na istu odredbu naplaćuju se jednom", () => {
    const rezultat = obracun(50_000, [
      stavka(KATEGORIJA_PARNICNI, "par_tuzba"),
      stavka(KATEGORIJA_PARNICNI, "par_tuzba_smetanje"),
    ]);

    expect(rezultat.stavke).toHaveLength(1);
    expect(rezultat.ukupno).toBe(620);
  });

  it("kazneni postupak po javnoj optužbi nema pristojbe", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_KAZNENI, "kaz_obrana")]);

    expect(rezultat.stavke).toHaveLength(0);
    expect(rezultat.ukupno).toBe(0);
  });

  it("oslobođena stranka zadržava retke, ali s iznosom 0,00", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_PARNICNI, "par_tuzba")], {
      oslobodjen: true,
    });

    expect(rezultat.stavke).toHaveLength(1);
    expect(rezultat.stavke[0].tarifni_broj).toBe("Tar. br. 1 st. 1");
    expect(rezultat.ukupno).toBe(0);
  });
});

// ── Čl. 2 — prijelazni slučajevi ──────────────────────────────────

describe("čl. 2 — odabir verzije po datumu", () => {
  const odgovorNaTuzbu = [stavka(KATEGORIJA_PARNICNI, "par_odgovor_tuzba")];

  it("odgovor na tužbu podnesen prije 1.6.2026. ide po staroj Uredbi", () => {
    const rezultat = obracun(50_000, odgovorNaTuzbu, {
      datumOdgovoraNaTuzbu: "2026-03-12",
    });

    expect(rezultat.ukupno).toBe(273.23);
    expect(rezultat.stavke[0].uredba).toBe("NN 37/2023");
  });

  it.each(["2026-06-01", "2026-07-12"])("od %s ide po novoj Uredbi", (datum) => {
    const rezultat = obracun(50_000, odgovorNaTuzbu, { datumOdgovoraNaTuzbu: datum });

    expect(rezultat.ukupno).toBe(310);
    expect(rezultat.stavke[0].uredba).toBe("NN 50/2026");
  });

  it("bez datuma ide po novoj Uredbi", () => {
    expect(obracun(50_000, odgovorNaTuzbu).ukupno).toBe(310);
  });

  it("upravni spor pokrenut prije 1.6.2026. ide po staroj Uredbi", () => {
    const rezultat = obracun(50_000, [stavka("Upravni spor", "usp_tuzba_proc")], {
      datumPokretanjaUpravnogSpora: "2026-03-12",
    });

    expect(rezultat.ukupno).toBe(53.09);
    expect(rezultat.stavke[0].uredba).toBe("NN 37/2023");
  });

  it("odgovor na žalbu nije prijelazni slučaj iz čl. 2", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_PARNICNI, "par_odgovor_zalba")], {
      datumOdgovoraNaTuzbu: "2026-03-12",
    });

    expect(rezultat.stavke[0].uredba).toBe("NN 50/2026");
    expect(rezultat.ukupno).toBe(310);
  });

  it("jedan troškovnik može nositi obje Uredbe", () => {
    const rezultat = obracun(
      50_000,
      [
        stavka(KATEGORIJA_PARNICNI, "par_odgovor_tuzba"),
        stavka(KATEGORIJA_PARNICNI, "par_zalba_presuda"),
      ],
      { datumOdgovoraNaTuzbu: "2026-03-12" },
    );

    expect(rezultat.stavke.map((s) => s.uredba)).toEqual(["NN 37/2023", "NN 50/2026"]);
    expect(rezultat.ukupno).toBe(1_513.23);
  });
});

// ── Ručne izmjene ─────────────────────────────────────────────────

describe("ručno uređivanje liste", () => {
  it("isključena odredba se zamjenjuje ručno dodanom", () => {
    const rezultat = obracun(10_000, [stavka("Izvanparnični postupak", "izv_dioba")], {
      iskljucene: ["tbr5"],
      dodane: [{ odredba_id: "tbr11" }],
    });

    expect(rezultat.stavke).toHaveLength(1);
    expect(rezultat.stavke[0].odredba_id).toBe("tbr11");
    expect(rezultat.stavke[0].automatski).toBe(false);
    expect(rezultat.ukupno).toBe(120);
  });

  it("ručni redak koristi vlastitu osnovicu", () => {
    const rezultat = obracun(0, [], { dodane: [{ odredba_id: "tbr11", osnovica: 10_000 }] });

    expect(rezultat.ukupno).toBe(120);
  });

  it("količina množi iznos", () => {
    const rezultat = obracun(0, [], {
      dodane: [{ odredba_id: "tbr28_preslika", kolicina: 12 }],
    });

    expect(rezultat.ukupno).toBe(12);
  });
});

// ── Prikaz osnovice ───────────────────────────────────────────────

describe("osnovica se prikazuje samo kad iznos o njoj ovisi", () => {
  it("fiksni iznos ne nosi VPS", () => {
    const rezultat = obracun(50_000, [stavka("Upravni spor", "usp_tuzba_proc")]);
    expect(rezultat.stavke[0].osnovica).toBe(0);
  });

  it("iznos po ljestvici nosi VPS", () => {
    const rezultat = obracun(50_000, [stavka(KATEGORIJA_OVRSNI, "ovr_prijedlog")]);
    expect(rezultat.stavke[0].osnovica).toBe(50_000);
  });
});
