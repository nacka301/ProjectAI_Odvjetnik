/**
 * Spremljeni troškovnik pretvoren u model koji već koriste ispis i izvoz.
 *
 * Postoji zato što redak u `troskovnici` nosi obračun na dva mjesta: cijeli
 * odgovor motora u `raw_json_output`, a podatke zaglavlja (odvjetnik, sud,
 * stranke) u vlastitim JSONB stupcima. Odgovor te podatke ima samo ako su bili
 * dio zahtjeva, pa se moraju spojiti prije prikaza — inače bi spremljeni
 * troškovnik gubio zaglavlje koje u bazi uredno stoji.
 *
 * Iznosi se ovdje NE računaju. Sve brojke dolaze iz spremljenog odgovora, jer
 * troškovnik je dokument koji je već predan sudu: naknadni bi izračun mogao
 * dati drukčiji broj od onoga na papiru, a to je gore od nikakvog prikaza.
 */
import {
  buildIzvozModel,
  type IzvozUlaz,
  type TroskovnikIzvozModel,
} from "@/lib/troskovnik-izvoz-model";
import { datumBezVremena, type PravnaOsnova } from "@/lib/pravna-osnova";
import type { TroskovnikRow } from "@/lib/supabase";

/** Prazan „živi izračun" — spremljeni zapis uvijek ima `result`, pa se ne čita. */
const BEZ_ZIVOG_IZRACUNA = {
  lines: [],
  ukupnoBodova: 0,
  naknadaTroskovaUkupno: 0,
  osnovicaSaNaknadom: 0,
  pdv: 0,
  ukupnoSPdv: 0,
  ukupnoPristojbe: 0,
  ukupnoPredujmovi: 0,
  sveukupniTrosak: 0,
};

function tekst(s: string | null | undefined): string {
  return (s ?? "").trim();
}

/**
 * Po čemu je zapis izračunat, iz stupaca migracije 025 — i samo odande.
 *
 * Troškovnik spremljen prije toga nema zabilježen propis, i to se tako i kaže.
 * Izvesti ga iz današnjih konstanti bilo bi tvrditi nešto što nismo zapisali.
 */
export function pravnaOsnovaIzZapisa(zapis: TroskovnikRow): PravnaOsnova {
  if (!zapis.tarifa_naziv) {
    return { zabiljezena: false, uredbe: [], datum: new Date(zapis.created_at) };
  }
  return {
    zabiljezena: true,
    tarifaNaziv: zapis.tarifa_naziv,
    tarifaVrijediOd: datumBezVremena(zapis.tarifa_vrijedi_od),
    vrijednostBoda: zapis.vrijednost_boda == null ? undefined : Number(zapis.vrijednost_boda),
    uredbe: zapis.uredbe_pristojbe ?? [],
    datum: new Date(zapis.izracunato_at ?? zapis.created_at),
  };
}

/**
 * `null` kad zapis nema spremljen odgovor motora — takav se troškovnik ne može
 * vjerodostojno prikazati, pa se ne prikazuje ni djelomično.
 */
export function izvozModelIzZapisa(zapis: TroskovnikRow): TroskovnikIzvozModel | null {
  const odgovor = zapis.raw_json_output;
  if (!odgovor || !Array.isArray(odgovor.stavke)) return null;

  const zastupana = zapis.stranke?.zastupana_stranka;
  const protustranka = zapis.stranke?.protustranka;

  return buildIzvozModel({
    // `raw_json_output` JEST odgovor motora — spremio ga je backend serijalizacijom
    // istog tipa. Baza ga vraća kao neoblikovani JSON, pa se oblik ovdje vraća.
    result: odgovor as unknown as IzvozUlaz["result"],
    liveCalc: BEZ_ZIVOG_IZRACUNA,
    pristojbeneStavke: [],
    dodatnePristojbe: [],
    predujmovi: [],
    isPdvUkljucen: Boolean(odgovor.is_pdv_ukljucen ?? odgovor.has_vat),
    napomena: tekst(odgovor.napomena as string | null),
    pravnaOsnova: pravnaOsnovaIzZapisa(zapis),
    // Zaglavlje iz stupaca zapisa. `buildIzvozModel` ova polja koristi tek ako
    // ih odgovor nema, pa spremljeni odgovor i dalje ima prednost.
    polja: {
      odvjetnikIme: tekst(zapis.odvjetnik?.naziv),
      odvjetnikOib: tekst(zapis.odvjetnik?.oib),
      odvjetnikAdresa: tekst(zapis.odvjetnik?.adresa),
      odvjetnikGrad: "",
      odvjetnikIban: tekst(zapis.odvjetnik?.iban),
      odvjetnikTelefon: "",
      odvjetnikEmail: "",
      odvjetnikOsobnoIme: "",
      klijentIme: tekst(zastupana?.naziv),
      zastupanaPolozaj: tekst(zastupana?.polozaj),
      protustrankaIme: tekst(protustranka?.naziv),
      protustrankaPolozaj: tekst(protustranka?.polozaj),
      predmetSud: tekst(zapis.predmet?.sud),
      predmetPoslovniBroj: tekst(zapis.predmet?.poslovni_broj),
    },
    // Odgovor motora nosi VPS podatke izravno; stupac na zapisu je fallback za
    // starije zapise spremljene prije nego je odgovor to počeo bilježiti.
    vrijednostSpora: zapis.vrijednost_spora ?? undefined,
  });
}
