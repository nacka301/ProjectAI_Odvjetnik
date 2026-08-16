using System.Text.Json.Serialization;

namespace Backend.Models;

// Izlazni oblik: izracunati troskovnik i pojedinacni redak u njemu.

public sealed class TroskovnikResponse
{
    [JsonPropertyName("stavke")]
    public List<TroskovnikLineItem> Stavke { get; set; } = [];

    /// <summary>Ukupno bodova, uključujući povišenje po Tbr. 40.</summary>
    [JsonPropertyName("ukupno_bodova")]
    public decimal UkupnoBodova { get; set; }

    /// <summary>Broj zastupanih osoba iz zahtjeva (Tbr. 40. toč. 1).</summary>
    [JsonPropertyName("broj_zastupanih_osoba")]
    public int BrojZastupanihOsoba { get; set; } = 1;

    /// <summary>
    /// Postotak povišenja po Tbr. 40. toč. 1: 10% za drugu i svaku sljedeću osobu,
    /// najviše 50%. Prva osoba ne ulazi u postotak.
    /// </summary>
    [JsonPropertyName("povisenje_vise_osoba_postotak")]
    public decimal PovisenjeViseOsobaPostotak { get; set; }

    /// <summary>Bodovi koje donosi povišenje po Tbr. 40 — prikazuju se kao zaseban redak.</summary>
    [JsonPropertyName("povisenje_vise_osoba_bodovi")]
    public decimal PovisenjeViseOsobaBodovi { get; set; }

    /// <summary>
    /// Tbr. 40. toč. 1, drugi odlomak: "Iznos povišene nagrade dijeli se na jednake
    /// dijelove prema broju branjenika, odnosno zastupanih osoba."
    ///
    /// Dioba ne mijenja ukupnu nagradu — pokazuje koliko od nje otpada na svaku osobu.
    /// </summary>
    [JsonPropertyName("bodovi_po_osobi")]
    public decimal BodoviPoOsobi { get; set; }

    /// <summary>
    /// Nagrada po zastupanoj osobi u EUR. Dijeli se nagrada, ne sveukupni trošak —
    /// PDV i sudska pristojba nisu odvjetnička nagrada.
    /// </summary>
    [JsonPropertyName("nagrada_po_osobi")]
    public decimal NagradaPoOsobi { get; set; }

    // ── Osnovica po Tbr. 7. / Tbr. 45 ─────────────────────────────

    /// <summary>Primijenjena varijanta osnovice.</summary>
    [JsonPropertyName("osnovica_tbr7")]
    public string OsnovicaTbr7 { get; set; } = Models.OsnovicaTbr7.Toc1;

    /// <summary>Bodovi osnovice na koju su primijenjeni postoci iz Tbr. 8. i Tbr. 9.</summary>
    [JsonPropertyName("osnovica_bodovi")]
    public decimal OsnovicaBodovi { get; set; }

    /// <summary>Odredba iz koje osnovica proizlazi (npr. „Tbr. 7. toč. 7 st. 2").</summary>
    [JsonPropertyName("osnovica_pravni_izvor")]
    public string OsnovicaPravniIzvor { get; set; } = "";

    [JsonPropertyName("razina_suda")]
    public string? RazinaSuda { get; set; }

    [JsonPropertyName("tbr45_nacin")]
    public string? Tbr45Nacin { get; set; }

    // ── Tbr. 41 ───────────────────────────────────────────────────

    /// <summary>Tbr. 41. toč. 1 — primijenjeno povišenje u postotku (0–100).</summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal Tbr41PovisenjePostotak { get; set; }

    /// <summary>Tbr. 41. toč. 2 — primijenjeno sniženje u postotku (0–50).</summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal Tbr41SnizenjePostotak { get; set; }

    /// <summary>Bodovi koje donosi Tbr. 41 (pozitivni za povišenje, negativni za sniženje).</summary>
    [JsonPropertyName("tbr41_bodovi")]
    public decimal Tbr41Bodovi { get; set; }

    /// <summary>
    /// Vrijednost predmeta spora korištena u izračunu. U kaznenom postupku uvijek 0 —
    /// VPS je institut parničnog postupka i tamo nema što tražiti (vidi <see cref="IsSamoKazneni"/>).
    /// </summary>
    [JsonPropertyName("vrijednost_spora")]
    public decimal VrijednostSpora { get; set; }

    /// <summary>
    /// True kad su odabrane isključivo stavke iz kaznenog/prekršajnog postupka.
    /// Tada se ne koristi VPS niti se obračunava sudska pristojba.
    /// </summary>
    [JsonPropertyName("is_samo_kazneni")]
    public bool IsSamoKazneni { get; set; }

    [JsonPropertyName("ukupno_eur_bez_pdv")]
    public decimal UkupnoEurBezPdv { get; set; }

    [JsonPropertyName("pdv")]
    public decimal Pdv { get; set; }

    [JsonPropertyName("ukupno_eur_s_pdv")]
    public decimal UkupnoEurSPdv { get; set; }

    [JsonPropertyName("vrijednost_boda")]
    public decimal VrijednostBoda { get; set; } = TarifaConstants.BodValue;

    [JsonPropertyName("klijent")]
    public TroskovnikParty? Klijent { get; set; }

    [JsonPropertyName("odvjetnik")]
    public TroskovnikParty? Odvjetnik { get; set; }

    [JsonPropertyName("opis_predmeta")]
    public string? OpisPredmeta { get; set; }

    [JsonPropertyName("datum_izrade")]
    public DateTime DatumIzrade { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("protustranka")]
    public TroskovnikParty? Protustranka { get; set; }

    [JsonPropertyName("protustranka_polozaj")]
    public string? ProtustrankaPolozaj { get; set; }

    [JsonPropertyName("is_pdv_ukljucen")]
    public bool? IsPdvUkljucen { get; set; }

    [JsonPropertyName("has_vat")]
    public bool HasVat { get; set; }

    [JsonPropertyName("sud")]
    public string? Sud { get; set; }

    [JsonPropertyName("broj_predmeta")]
    public string? BrojPredmeta { get; set; }

    // Nested objects (new schema) — carried alongside legacy flat fields for gradual migration
    [JsonPropertyName("odvjetnik_info")]
    public OdvjetnikRequest? OdvjetnikInfo { get; set; }

    [JsonPropertyName("predmet_info")]
    public PredmetRequest? PredmetInfo { get; set; }

    [JsonPropertyName("stranke_info")]
    public StrankeRequest? StrankeInfo { get; set; }

    // Kazneni echo fields
    [JsonPropertyName("zaprijecena_kazna")]
    public string? ZaprijecenaKazna { get; set; }

    [JsonPropertyName("is_po_sluzbenoj_duznosti")]
    public bool IsPoSluzbenojDuznosti { get; set; }

    [JsonPropertyName("is_na_teret_proracuna")]
    public bool IsNaTeretProracuna { get; set; }

    [JsonPropertyName("is_zastupnik_djeteta")]
    public bool IsZastupnikDjeteta { get; set; }

    [JsonPropertyName("is_stjecaj")]
    public bool IsStjecaj { get; set; }

    /// <summary>
    /// Obrazloženje uz odabrani razred kad je riječ o stjecaju ili produljenom
    /// kaznenom djelu (Tbr. 4. toč. 1 al. 6). Null kad stjecaja nema.
    /// </summary>
    [JsonPropertyName("napomena_stjecaj")]
    public string? NapomenaStjecaj { get; set; }

    [JsonPropertyName("is_oduzimanje_prekrsaj")]
    public bool IsOduzimanjePrekrsaj { get; set; }

    [JsonPropertyName("vrijednost_oduzimanja")]
    public decimal VrijednostOduzimanja { get; set; }

    // Sudske pristojbe
    [JsonPropertyName("sudska_pristojba")]
    public decimal SudskaPristojba { get; set; }

    [JsonPropertyName("is_oslobodjen_pristojbi")]
    public bool IsOslobodjenPristojbi { get; set; }

    /// <summary>Ključ točke čl. 11 po kojoj je stranka oslobođena; null kad nije naveden.</summary>
    [JsonPropertyName("oslobodjenje_kategorija")]
    public string? OslobodjenjeKategorija { get; set; }

    /// <summary>Tekst te točke, za ispis. Prazan kad kategorija nije zadana.</summary>
    [JsonPropertyName("oslobodjenje_naziv")]
    public string OslobodjenjeNaziv { get; set; } = "";

    /// <summary>Čl. 12 — koliko je zastupanih osoba oslobođeno.</summary>
    [JsonPropertyName("broj_oslobodjenih_osoba")]
    public int BrojOslobodjenihOsoba { get; set; }

    /// <summary>
    /// Čl. 12 — dio VPS-a na koji je pristojba obračunata. Jednak
    /// <see cref="VrijednostSpora"/> kad nitko nije oslobođen.
    /// </summary>
    [JsonPropertyName("pristojba_vps_udio")]
    public decimal PristojbaVpsUdio { get; set; }

    /// <summary>
    /// Čl. 13 — pristojba na teret protivne stranke, razmjerno uspjehu oslobođene.
    /// Ne ulazi u <see cref="SveukupniTrosak"/>: obveza nastaje tek po pravomoćnosti.
    /// </summary>
    [JsonPropertyName("pristojba_na_teret_protustranke")]
    public decimal PristojbaNaTeretProtustranke { get; set; }

    /// <summary>Čl. 13 — upisani omjer uspjeha, 0–100. Null kad se ne obračunava.</summary>
    [JsonPropertyName("postotak_uspjeha")]
    public decimal? PostotakUspjeha { get; set; }

    // ── Vrijednost predmeta spora po Zakonu o sudskim pristojbama ──

    /// <summary>Vrijednost predmeta spora na koju se primjenjuje sudska pristojba (čl. 14–26).</summary>
    [JsonPropertyName("vps_zakonski")]
    public decimal VpsZakonski { get; set; }

    /// <summary>Primijenjena vrsta postupka. Vidi <see cref="Models.VrstaPostupka"/>.</summary>
    [JsonPropertyName("vps_vrsta_postupka")]
    public string VpsVrstaPostupka { get; set; } = Models.VrstaPostupka.Opci;

    /// <summary>Propis i članak iz kojeg vrijednost predmeta spora proizlazi.</summary>
    [JsonPropertyName("vps_pravni_izvor")]
    public string VpsPravniIzvor { get; set; } = "";

    /// <summary>Objašnjenje zašto se zakonski VPS razlikuje od unesenog; null kad se ne razlikuje.</summary>
    [JsonPropertyName("vps_napomena")]
    public string? VpsNapomena { get; set; }

    /// <summary>True kad je Zakon promijenio uneseni iznos.</summary>
    [JsonPropertyName("vps_prilagodjen")]
    public bool VpsPrilagodjen { get; set; }

    /// <summary>True kad je zakonski VPS primijenjen i na odvjetničku nagradu po Tbr. 7. toč. 1.</summary>
    [JsonPropertyName("vps_primijeni_na_nagradu")]
    public bool VpsPrimijeniNaNagradu { get; set; }

    /// <summary>
    /// Čl. 16 uz suparničarstvo — zahtjevi koje ovaj troškovnik ne pokriva i za koje se
    /// pristojba plaća zasebno. Prazno u svim ostalim slučajevima.
    /// </summary>
    [JsonPropertyName("zahtjevi_informativno")]
    public List<ZahtjevStavka> ZahtjeviInformativno { get; set; } = [];

    /// <summary>
    /// Po kojoj se osnovi obračunava pristojba. Razlikuje "Uredba ovdje ne propisuje
    /// pristojbu" (<see cref="Backend.Models.PristojbaOsnova.Nema"/>) od oslobođenja
    /// stranke — oboje daju 0,00 €, ali iz posve različitih razloga.
    /// </summary>
    [JsonPropertyName("pristojba_osnova")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PristojbaOsnova PristojbaOsnova { get; set; }

    /// <summary>Grand total = odvjetnički trošak (s PDV) + sudska pristojba.</summary>
    [JsonPropertyName("sveukupni_trosak")]
    public decimal SveukupniTrosak { get; set; }
}

public sealed class TroskovnikLineItem
{
    [JsonPropertyName("rbr")]
    public int Rbr { get; set; }

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("tarifni_broj")]
    public string TarifniBroj { get; set; } = "";

    /// <summary>Propis i tarifni broj iz kojeg ovaj redak proizlazi — vidi <see cref="TarifnaStavka.PravniIzvor"/>.</summary>
    [JsonPropertyName("pravni_izvor")]
    public string PravniIzvor { get; set; } = "";

    [JsonPropertyName("kolicina")]
    public int Kolicina { get; set; }

    /// <summary>Bodovi po jedinici. Decimalno — Tarifa ne propisuje zaokruživanje bodova.</summary>
    [JsonPropertyName("bodova_po_jedinici")]
    public decimal BodovaPoJedinici { get; set; }

    [JsonPropertyName("ukupno_bodova")]
    public decimal UkupnoBodova { get; set; }

    [JsonPropertyName("iznos_eur")]
    public decimal IznosEur { get; set; }

    /// <summary>
    /// True kad je nagrada jednokratna (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1), pa
    /// <see cref="UkupnoBodova"/> NIJE <see cref="BodovaPoJedinici"/> × <see cref="Kolicina"/>.
    /// Sučelje o tome mora obavijestiti korisnika.
    /// </summary>
    [JsonPropertyName("is_jednokratna")]
    public bool IsJednokratna { get; set; }

    // ── Tbr. 41 po pojedinoj stavci ───────────────────────────────

    /// <summary>Tbr. 41. toč. 1 — primijenjeno povišenje ove stavke u postotku (0–100).</summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal Tbr41PovisenjePostotak { get; set; }

    /// <summary>Tbr. 41. toč. 2 — primijenjeno sniženje ove stavke u postotku (0–50).</summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal Tbr41SnizenjePostotak { get; set; }

    /// <summary>
    /// Bodovi koje Tbr. 41 dodaje ili oduzima ovoj stavci — pozitivno za povišenje,
    /// negativno za sniženje. Zbrajaju se u <see cref="TroskovnikResponse.Tbr41Bodovi"/>.
    /// </summary>
    [JsonPropertyName("tbr41_bodovi")]
    public decimal Tbr41Bodovi { get; set; }

    /// <summary>
    /// Citat odredbe uz prilagođenu stavku — prazan kad Tbr. 41 nije primijenjen.
    /// Vidi se na ispisu, da se svaki odmak od tarifne stavke može provjeriti.
    /// </summary>
    [JsonPropertyName("tbr41_pravni_izvor")]
    public string Tbr41PravniIzvor { get; set; } = "";
}
