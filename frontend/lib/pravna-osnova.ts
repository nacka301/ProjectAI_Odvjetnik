/**
 * Po čemu je troškovnik izračunat — i dvije rečenice koje to kažu.
 *
 * Plan pravne zaštite (docs/plan-pravna-zastita.md, dio A). Uz svaki troškovnik
 * zna se verzija Tarife, Uredbe o Tarifi sudskih pristojbi i dan izračuna, pa
 * kad se propis promijeni, stari troškovnik i dalje kaže po čemu je rađen.
 *
 * Postoje DVIJE bilješke, i to namjerno:
 *
 * - `neutralnaBiljeska` ide na papir koji se prilaže sudu. Na njemu nema imena
 *   aplikacije ni ograde o odgovornosti (pravilo iz prompta 3, i test u
 *   `troskovnik-ispis.test.tsx`) — samo pravna osnova i datum, što je sudu i
 *   protustranci korisno i bez alata.
 * - `punaBiljeska` stoji u aplikaciji i u Excelu: kaže da je izračun napravljen
 *   u PravoTeku i da za njega odgovara odvjetnik koji ga provjerava.
 *
 * Kad propis nije zabilježen (troškovnik spremljen prije migracije 025), ništa
 * se ne izvodi naknadno: na papir ne ide ništa, a u aplikaciji to piše.
 */
import { formatDatumIspis, formatEur } from "@/lib/format";

export const NAZIV_UREDBE_NN50_2026 = "Uredba o Tarifi sudskih pristojbi (NN 50/2026)";
export const NAZIV_UREDBE_NN37_2023 = "Uredba o Tarifi sudskih pristojbi (NN 37/2023)";

export interface PravnaOsnova {
  /** `false` za troškovnik spremljen prije nego što se propis počeo bilježiti. */
  zabiljezena: boolean;
  tarifaNaziv?: string;
  tarifaVrijediOd?: Date | null;
  vrijednostBoda?: number;
  /** Puni nazivi Uredbi primijenjenih na pristojbe, starija prije novije. */
  uredbe: string[];
  /** Dan izračuna. */
  datum: Date;
}

/**
 * ISO datum bez vremena (`2023-10-14`) kao lokalni dan.
 *
 * `new Date("2023-10-14")` je ponoć po UTC-u, što je zapadno od Greenwicha još
 * prethodni dan — a datum stupanja propisa na snagu ne smije se pomaknuti.
 */
export function datumBezVremena(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  if (!m) return null;
  return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
}

/**
 * Uredbe iz pristojbenih stavaka, kad ih izračun ne daje zbrojene (živi pregled
 * na obrascu, ili odgovor backenda starijeg od koraka 3). Stavka nosi kratku
 * oznaku ("NN 37/2023"); stavka bez oznake računa se po važećoj Uredbi.
 */
export function uredbeIzStavaka(stavke: readonly { uredba?: string }[]): string[] {
  const oznake = new Set(stavke.map((s) => s.uredba ?? "NN 50/2026"));
  const uredbe: string[] = [];
  if (oznake.has("NN 37/2023")) uredbe.push(NAZIV_UREDBE_NN37_2023);
  if ([...oznake].some((o) => o !== "NN 37/2023")) uredbe.push(NAZIV_UREDBE_NN50_2026);
  return uredbe;
}

/**
 * Naziv bez završne zagrade. „Tarifa … (Hrvatska odvjetnička komora, 14. listopada
 * 2023.)" postaje „Tarifa …": datum stupanja na snagu ionako stoji uz naziv.
 */
export function bezZavrsneZagrade(naziv: string): string {
  return naziv.replace(/\s*\([^()]*\)\s*$/, "").trim();
}

function datum(d: Date): string {
  return `${formatDatumIspis(d)}.`;
}

function tarifa(o: PravnaOsnova, sBodom: boolean): string {
  const dodaci: string[] = [];
  if (o.tarifaVrijediOd) dodaci.push(`na snazi od ${datum(o.tarifaVrijediOd)}`);
  if (sBodom && o.vrijednostBoda) dodaci.push(`1 bod = ${formatEur(o.vrijednostBoda)}`);
  const naziv = bezZavrsneZagrade(o.tarifaNaziv ?? "");
  return dodaci.length > 0 ? `${naziv} (${dodaci.join(", ")})` : naziv;
}

/**
 * Za ispis koji ide sudu: samo pravna osnova i datum. `null` kad propis nije
 * zabilježen — tada se na papir ne piše ništa.
 */
export function neutralnaBiljeska(o: PravnaOsnova): string | null {
  if (!o.zabiljezena || !o.tarifaNaziv) return null;
  const propisi = [tarifa(o, false), ...o.uredbe];
  return `Obračun prema propisima: ${propisi.join("; ")}. Stanje propisa na dan ${datum(o.datum)}`;
}

/** Za aplikaciju i Excel: tko je računao, po čemu, i tko za izračun odgovara. */
export function punaBiljeska(o: PravnaOsnova): string {
  const odgovornost = "Izračun provjerava i za njega odgovara odvjetnik.";
  if (!o.zabiljezena || !o.tarifaNaziv) {
    return (
      "Propis po kojem je ovaj troškovnik izračunat nije zabilježen jer je spremljen " +
      `prije uvođenja te oznake. ${odgovornost}`
    );
  }
  const propisi = [tarifa(o, true), ...o.uredbe];
  return (
    `Izračunato u alatu PravoTek ${datum(o.datum)} prema propisima: ${propisi.join("; ")}. ` +
    odgovornost
  );
}
