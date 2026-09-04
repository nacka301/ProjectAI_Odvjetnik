/**
 * Iznosi demo troškovnika — izračunati, nikad prepisani.
 *
 * `podaci.ts` drži samo ulaze (vrijednost predmeta, popis radnji, oznake
 * pristojbi). Bodove, nagradu, pristojbe, PDV i ukupno izvodi POSTOJEĆI motor
 * iz `lib/tarifa.ts` — isti onaj koji radi za stvarne troškovnike. Zato je
 * demo troškovnik ujedno i provjera ispravnosti: pokvari li se Tarifa, vidjet
 * će se na vodiču dobrodošlice. S hardkodiranim iznosima to bi prošlo nezapaženo.
 *
 * Katalog stavki dolazi izvana (`GET /api/troskovnik/katalog`) jer motor bez
 * njega ne zna koliko koja radnja nosi bodova. To je katalog Tarife, ne
 * korisnički podatak — jedini mrežni poziv koji demo način pušta kroz sebe.
 */

import {
  BOD_VALUE,
  OSNOVICA_TBR7,
  PDV_RATE,
  izracunajBodove,
  izracunajOsnovicu,
  obracunajPristojbe,
  ukupnoBodovaZaStavku,
  zaokruziNovac,
  type TarifnaStavka,
} from "@/lib/tarifa";
import type { ClientProfile, Troskovnik } from "@/lib/stranke-tipovi";
import {
  DEMO_PREDMETI,
  DEMO_STRANKE,
  DEMO_TROSKOVNIK,
  DEMO_TROSKOVNIK_CREATED_AT,
  DEMO_TROSKOVNIK_ID,
  DEMO_URED,
} from "@/lib/demo/podaci";

export interface DemoZbrojevi {
  ukupnoBodova: number;
  /** Odvjetnička nagrada bez PDV-a. */
  nagradaBezPdv: number;
  pdv: number;
  nagradaSPdv: number;
  /** Sudske pristojbe — odvojeno, na njih se PDV ne obračunava (čl. 33 Zakona o PDV-u). */
  pristojbe: number;
}

/**
 * Zbrojevi demo troškovnika. Vraća `null` dok katalog nije stigao — pozivatelj
 * tada nema što prikazati i ne smije izmisliti brojku.
 */
export function demoZbrojevi(katalog: TarifnaStavka[]): DemoZbrojevi | null {
  if (katalog.length === 0) return null;

  const poId = new Map(katalog.map((s) => [s.id, s]));

  const osnovica = izracunajOsnovicu({
    osnovicaTbr7: OSNOVICA_TBR7.Toc1,
    vrijednostSpora: DEMO_TROSKOVNIK.vrijednostSpora,
  });
  if (!osnovica) return null;

  const odabraneStavke: TarifnaStavka[] = [];
  let ukupnoBodova = 0;

  for (const radnja of DEMO_TROSKOVNIK.radnje) {
    const def = poId.get(radnja.itemId);
    // Stavka koje u katalogu nema znači da je katalog promijenjen ispod primjera.
    // Tiho preskakanje dalo bi manji ukupan iznos koji nitko ne bi doveo u pitanje.
    if (!def) {
      console.warn(
        `[demo] Tarifna stavka "${radnja.itemId}" ne postoji u katalogu — ` +
          "demo troškovnik je nepotpun. Popravi lib/demo/podaci.ts.",
      );
      continue;
    }
    odabraneStavke.push(def);
    ukupnoBodova += ukupnoBodovaZaStavku(def, izracunajBodove(def, osnovica), 1);
  }

  const nagradaBezPdv = zaokruziNovac(ukupnoBodova * BOD_VALUE);
  const pdv = zaokruziNovac(nagradaBezPdv * PDV_RATE);

  const pristojbe = obracunajPristojbe({
    vrijednostSpora: DEMO_TROSKOVNIK.vrijednostSpora,
    odabraneStavke,
    oslobodjen: false,
    dodane: [...DEMO_TROSKOVNIK.pristojbeDodane],
    podnesciElektronicki: DEMO_TROSKOVNIK.podnesciElektronicki,
  });

  return {
    ukupnoBodova,
    nagradaBezPdv,
    pdv,
    nagradaSPdv: zaokruziNovac(nagradaBezPdv + pdv),
    pristojbe: pristojbe.ukupno,
  };
}

/** Spremljeni demo troškovnik onako kako ga dosje stranke prikazuje. */
function demoTroskovnikZapis(katalog: TarifnaStavka[]): Troskovnik[] {
  const z = demoZbrojevi(katalog);
  if (!z) return [];

  return [
    {
      id: DEMO_TROSKOVNIK_ID,
      created_at: DEMO_TROSKOVNIK_CREATED_AT,
      opis_predmeta: DEMO_TROSKOVNIK.opisPredmeta,
      vrijednost_spora: DEMO_TROSKOVNIK.vrijednostSpora,
      ukupno_bodova: z.ukupnoBodova,
      ukupno_eur_bez_pdv: z.nagradaBezPdv,
      pdv: z.pdv,
      ukupno_eur_s_pdv: z.nagradaSPdv,
      klijent_ime: DEMO_STRANKE[0].ime_naziv,
      odvjetnik_ime: DEMO_URED.ime,
    },
  ];
}

/**
 * Dosje jedne demo stranke — stranka, njezini predmeti i troškovnici.
 *
 * Vraća `null` za id koji nije demo stranka; pozivatelj to prikazuje kao
 * „nije pronađeno", jednako kao da je backend vratio 404.
 */
export function demoProfil(
  clientId: string,
  katalog: TarifnaStavka[],
): ClientProfile | null {
  const client = DEMO_STRANKE.find((s) => s.id === clientId);
  if (!client) return null;

  const cases = DEMO_PREDMETI.filter((p) => p.client_id === clientId);

  // Troškovnik visi samo o prvoj stranki — ona je ta koja pokazuje cijeli lanac
  // stranka → predmet → troškovnik.
  const troskovnici =
    clientId === DEMO_TROSKOVNIK.klijentId ? demoTroskovnikZapis(katalog) : [];

  return { client, cases, troskovnici };
}
