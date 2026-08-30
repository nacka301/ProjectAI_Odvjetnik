using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Katalog tarifnih stavki iz Tarife o nagradama i naknadi troškova za rad odvjetnika.
///
/// Ovo je podatkovni izvor, ne izračun. Izdvojen je iz TroskovnikService jer su se ondje
/// mijenjala dva neovisna razloga: dopuna kataloga novim tarifnim brojem i izmjena pravila
/// izračuna. Klasa koja se mijenja iz dva razloga krši načelo jedne odgovornosti.
/// </summary>
public static class TarifaKatalog
{
    /// <summary>Naziv kategorije kaznenog i prekršajnog postupka u katalogu.</summary>
    public const string KategorijaKazneni = "Kazneni postupak";

    /// <summary>Naziv kategorije parničnog postupka u katalogu.</summary>
    public const string KategorijaParnicni = "Parnični postupak";

    /// <summary>Naziv kategorije ovršnog postupka u katalogu.</summary>
    public const string KategorijaOvrsni = "Ovršni postupak";

    /// <summary>Sve tarifne stavke, redoslijedom kojim se prikazuju.</summary>
    public static readonly IReadOnlyList<TarifnaStavka> Sve =
    [
        // ═══════════════════════════════════════════════════════════
        // I.  KAZNENI I PREKRŠAJNI POSTUPAK
        // ═══════════════════════════════════════════════════════════

        // ── Tbr. 1 — Sastavljanje podnesaka ──
        // Toč. 1, 2, 3 → "nagrada po Tbr. 4. toč. 1." = bodovi ovise o zapriječenoj kazni
        // Tbr. 1. toč. 1 glasi samo "odvjetniku pripada nagrada po Tbr. 4. toč. 1." —
        // nikakav minimum se ne spominje. Zatečeni MinBodova = 100 nije imao izvor i
        // poništavao je umanjenje od 50% za branitelja po službenoj dužnosti.
        new() { Id = "kaz_privatna_tuzba", Kategorija = "Kazneni postupak", Opis = "Privatna tužba / kaznena prijava / prijedlog za kazneni progon / optužni prijedlog (Tbr. 1.1)", TarifniBroj = "Tbr. 1.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 1. toč. 1 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 100, Jedinica = "komad" },
        new() { Id = "kaz_podnesak_obrana", Kategorija = "Kazneni postupak", Opis = "Podnesci s navođenjem činjenica / pisana obrana (Tbr. 1.2)", TarifniBroj = "Tbr. 1.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 1. toč. 2 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 101, Jedinica = "komad" },
        new() { Id = "kaz_zahtjev_ostecenik", Kategorija = "Kazneni postupak", Opis = "Zahtjev oštećenika za dokazne radnje / preuzimanje progona (Tbr. 1.3)", TarifniBroj = "Tbr. 1.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 1. toč. 3 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 102, Jedinica = "komad" },
        new() { Id = "kaz_imovinskopravni", Kategorija = "Kazneni postupak", Opis = "Podnesak za imovinskopravni zahtjev oštećenika (Tbr. 1.4)", TarifniBroj = "Tbr. 1.4", FiksniBody = 75, SortOrder = 103, Jedinica = "komad" },
        new() { Id = "kaz_ostali_podnesci", Kategorija = "Kazneni postupak", Opis = "Ostali podnesci u kaznenom postupku (Tbr. 1.5)", TarifniBroj = "Tbr. 1.5", FiksniBody = 50, SortOrder = 104, Jedinica = "komad" },

        // ── Tbr. 2 — Prijedlozi izvan rasprave ──
        // Toč. 1, 2, 4 → fiksni; Toč. 3 → "nagrada po Tbr. 4. toč. 1."
        new() { Id = "kaz_odgoda_izvrsenja", Kategorija = "Kazneni postupak", Opis = "Prijedlog za odgodu izvršenja kazne / uvjetni otpust (Tbr. 2.1)", TarifniBroj = "Tbr. 2.1", FiksniBody = 100, SortOrder = 110, Jedinica = "komad" },
        new() { Id = "kaz_rehabilitacija", Kategorija = "Kazneni postupak", Opis = "Prijedlog za rehabilitaciju (Tbr. 2.2)", TarifniBroj = "Tbr. 2.2", FiksniBody = 50, SortOrder = 111, Jedinica = "komad" },
        new() { Id = "kaz_pomilovanje", Kategorija = "Kazneni postupak", Opis = "Molba za pomilovanje (Tbr. 2.3)", TarifniBroj = "Tbr. 2.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 2. toč. 3 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 112, Jedinica = "komad" },
        new() { Id = "kaz_opoziv_uvjetne", Kategorija = "Kazneni postupak", Opis = "Prijedlog za opoziv uvjetne osude (Tbr. 2.4)", TarifniBroj = "Tbr. 2.4", FiksniBody = 50, SortOrder = 113, Jedinica = "komad" },

        // ── Tbr. 3 — Zastupanje i obrana pred policijom / DO / sucem istrage ──
        // Toč. 1: prvi sat = Tbr. 4.1, svaki sljedeći sat = 50 bodova
        // Toč. 3: punomoćnik oštećenika = 50% nagrade iz Tbr. 3 (= 50% Tbr. 4.1)
        // Toč. 4: odgoda radnje = 50% nagrade iz toč. 1 (= 50% Tbr. 4.1)
        new() { Id = "kaz_sudjelovanje_istraga", Kategorija = "Kazneni postupak", Opis = "Sudjelovanje pred policijom / DO / sucem istrage – prvi sat (Tbr. 3.1)", TarifniBroj = "Tbr. 3.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 1 — nagrada iz Tbr. 4. toč. 1 za prvi sat", KazneniBase = true, SortOrder = 120, Jedinica = "sat" },
        new() { Id = "kaz_istraga_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Svaki sljedeći započeti sat u istrazi (Tbr. 3.1)", TarifniBroj = "Tbr. 3.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 1 — 50 bodova za svaki sljedeći započeti sat", FiksniBody = 50, SortOrder = 121, Jedinica = "sat" },
        // Tbr. 3. toč. 2 — "Za radnje iz toč. 1 ovog tarifnog broja koje policija, državno
        // odvjetništvo, Ured europskog javnog tužitelja ili sud, provode izvan zgrade svog
        // sjedišta odvjetniku pripada, osim nagrade iz toč. 1. ovog tarifnog broja i
        // naknada po Tbr. 49."
        // Tbr. 49 su stvarni putni troškovi — nisu bodovi i obračunavaju se zasebno.
        new() { Id = "kaz_istraga_izvan_sjedista", Kategorija = "Kazneni postupak", Opis = "Radnje pred policijom / DO / EPPO / sucem istrage izvan zgrade sjedišta (Tbr. 3.2) – nagrada kao po Tbr. 3.1, uz naknadu putnih troškova po Tbr. 49", TarifniBroj = "Tbr. 3.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 2 — nagrada iz Tbr. 3. toč. 1 (Tbr. 4. toč. 1) uz naknadu po Tbr. 49 (stvarni putni troškovi, izvan bodovnog sustava)", KazneniBase = true, SortOrder = 124, Jedinica = "sat" },

        new() { Id = "kaz_istraga_ostecenik", Kategorija = "Kazneni postupak", Opis = "Punomoćnik oštećenika / žrtve u istrazi – 50% Tbr. 4.1 (Tbr. 3.3)", TarifniBroj = "Tbr. 3.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 3 — 50% nagrade iz Tbr. 3 (Tbr. 4. toč. 1)", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 122, Jedinica = "sat" },
        new() { Id = "kaz_istraga_odgoda", Kategorija = "Kazneni postupak", Opis = "Pristup odgođenoj radnji u istrazi – 50% Tbr. 4.1, uz naknadu po Tbr. 49 (Tbr. 3.4)", TarifniBroj = "Tbr. 3.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 4 — 50% nagrade iz Tbr. 3. toč. 1 uz naknadu po Tbr. 49", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 123, Jedinica = "komad" },

        // ── Tbr. 4 — Obrana na raspravi ──
        new() { Id = "kaz_obrana", Kategorija = "Kazneni postupak", Opis = "Obrana okrivljenika na raspravi – prvi sat (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", KazneniBase = true, SortOrder = 130, Jedinica = "sat" },
        new() { Id = "kaz_obrana_prekrsaj", Kategorija = "Kazneni postupak", Opis = "Obrana u prekršajnom postupku (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 7 — obrana u prekršajnom postupku, 100 bodova", FiksniBody = 100, SortOrder = 131, Jedinica = "sat" },
        new() { Id = "kaz_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Svaki sljedeći započeti sat rasprave (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 — 50 bodova za svaki sljedeći započeti sat", FiksniBody = 50, SortOrder = 132, Jedinica = "sat" },
        new() { Id = "kaz_objava_presude", Kategorija = "Kazneni postupak", Opis = "Ročište za objavu presude (Tbr. 4.2)", TarifniBroj = "Tbr. 4.2", FiksniBody = 50, SortOrder = 133, Jedinica = "komad" },
        new() { Id = "kaz_sjednica_drugostupanjska", Kategorija = "Kazneni postupak", Opis = "Sjednica vijeća drugog stupnja (Tbr. 4.2) – po Tbr. 4.1", TarifniBroj = "Tbr. 4.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 2 al. 2 — kao za obranu pred sudom prvog stupnja", KazneniBase = true, SortOrder = 134, Jedinica = "komad" },
        // Tbr. 4. toč. 2 al. 3: "— na raspravi pred sudom drugog stupnja odvjetniku pripada
        // nagrada kao za obranu pred sudom prvog stupnja s povišenjem od 25%, a na raspravi
        // pred sudom drugog i trećeg stupnja za kaznena djela za koja je predviđena kazna
        // dugotrajnog zatvora s povišenjem od 50%."
        new() { Id = "kaz_rasprava_drugostupanjska", Kategorija = "Kazneni postupak", Opis = "Rasprava pred sudom drugog (i trećeg) stupnja (Tbr. 4.2) – Tbr. 4.1 + 25%, a kod dugotrajnog zatvora + 50%", TarifniBroj = "Tbr. 4.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 2 al. 3 — prvi stupanj + 25%, a kod dugotrajnog zatvora + 50%", KazneniBase = true, KazneniPovecanje = 0.25m, KazneniPovecanjeDugotrajni = 0.50m, SortOrder = 135, Jedinica = "komad" },
        // Tbr. 4. toč. 2 al. 4 — "na sjednici povodom izvanrednih pravnih lijekova odvjetniku
        // pripada nagrada kao za obranu pred sudom drugog stupnja."
        // "Obrana pred sudom drugog stupnja" definirana je u al. 3 — nagrada iz toč. 1
        // uvećana za 25%, odnosno za 50% kod dugotrajnog zatvora.
        new() { Id = "kaz_sjednica_izvanredni", Kategorija = "Kazneni postupak", Opis = "Sjednica povodom izvanrednih pravnih lijekova (Tbr. 4.2) – kao obrana pred sudom drugog stupnja", TarifniBroj = "Tbr. 4.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 2 al. 4 — nagrada kao za obranu pred sudom drugog stupnja (al. 3: toč. 1 + 25%, kod dugotrajnog zatvora + 50%)", KazneniBase = true, KazneniPovecanje = 0.25m, KazneniPovecanjeDugotrajni = 0.50m, SortOrder = 136, Jedinica = "komad" },

        // Tbr. 4. toč. 3 — "U postupku prema maloljetniku i u postupku prema osobama s
        // duševnim smetnjama, odvjetniku kao branitelju pripada nagrada kao za obranu
        // po toč. 1. ovog tarifnog broja."
        new() { Id = "kaz_maloljetnik", Kategorija = "Kazneni postupak", Opis = "Obrana u postupku prema maloljetniku ili osobi s duševnim smetnjama (Tbr. 4.3) – po Tbr. 4.1", TarifniBroj = "Tbr. 4.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 3 — nagrada kao za obranu po toč. 1", KazneniBase = true, SortOrder = 137, Jedinica = "sat" },

        // Tbr. 4. toč. 4 — "Ako je poslije podignute optužnice, optužnog prijedloga ili
        // privatne tužbe, ukidanja obaveznog prekršajnog naloga ili prekršajnog naloga, a
        // prije izvođenja dokaza na raspravi, došlo do obustave postupka, ili do odgode
        // rasprave, odvjetniku pripada 50% nagrade iz toč. 1. ovog tarifnog broja, osim u
        // slučaju ako sud nakon odgode rasprave nastavi s izvanraspravnim izvođenjem dokaza,
        // u kojem slučaju odvjetniku pripada nagrada iz toč. 1."
        new() { Id = "kaz_obustava_odgoda", Kategorija = "Kazneni postupak", Opis = "Obustava postupka ili odgoda rasprave prije izvođenja dokaza (Tbr. 4.4) – 50% Tbr. 4.1; ako sud nastavi s izvanraspravnim izvođenjem dokaza, obračunaj punu obranu po Tbr. 4.1", TarifniBroj = "Tbr. 4.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 4 — 50% nagrade iz toč. 1", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 138, Jedinica = "komad" },

        // Tbr. 4. toč. 5 — "Za svaki obavljeni razgovor s okrivljenikom koji se nalazi u
        // pritvoru ili istražnom zatvoru odvjetniku pripada nagrada od 50 bodova, te za
        // drugi i svaki sljedeći započeti sat nagrada od 50 bodova."
        new() { Id = "kaz_razgovor_pritvor", Kategorija = "Kazneni postupak", Opis = "Razgovor s okrivljenikom u pritvoru ili istražnom zatvoru – prvi (Tbr. 4.5)", TarifniBroj = "Tbr. 4.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 5 — 50 bodova po obavljenom razgovoru", FiksniBody = 50, SortOrder = 139, Jedinica = "komad" },
        new() { Id = "kaz_pritvor_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Razgovor u pritvoru – svaki sljedeći započeti sat (Tbr. 4.5)", TarifniBroj = "Tbr. 4.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 5 — 50 bodova za drugi i svaki sljedeći započeti sat", FiksniBody = 50, SortOrder = 140, Jedinica = "sat" },

        // Tbr. 4. toč. 6 — "Za prisustvovanje očevidu, rekonstrukciji, kao i ostalim radnjama,
        // koje policija, državno odvjetništvo, Ured europskog javnog tužitelja ili sud provode
        // izvan zgrade svog sjedišta, odvjetniku pripada nagrada iz toč. 1. ovog tarifnog broja
        // kao i naknada po Tbr. 49."
        new() { Id = "kaz_ocevid_izvan_zgrade", Kategorija = "Kazneni postupak", Opis = "Očevid / rekonstrukcija / ostale radnje izvan zgrade suda (Tbr. 4.6) – po Tbr. 4.1, uz naknadu putnih troškova po Tbr. 49", TarifniBroj = "Tbr. 4.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 6 — nagrada iz toč. 1 uz naknadu po Tbr. 49 (stvarni putni troškovi, izvan bodovnog sustava)", KazneniBase = true, SortOrder = 141, Jedinica = "komad" },

        // Tbr. 4. toč. 8 — "Odvjetniku koji zastupa oštećenika u postupku koji se vodi pred
        // policijom, državnim odvjetništvom, Uredom europskog javnog tužitelja te sucem istrage,
        // u postupku donošenja odluke o davanju suglasnosti za uvjetni odustanak od kaznenog
        // progona po načelu svrhovitosti, postupku provođenja pojedinačne procjene žrtve te na
        // raspravi pred sudom, pripada 50% nagrade iz ovog tarifnog broja."
        new() { Id = "kaz_zastupanje_ostecenika", Kategorija = "Kazneni postupak", Opis = "Zastupanje oštećenika (uvjetni odustanak, pojedinačna procjena žrtve, rasprava) – 50% Tbr. 4 (Tbr. 4.8)", TarifniBroj = "Tbr. 4.8", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 8 — 50% nagrade iz Tbr. 4", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 142, Jedinica = "komad" },

        // ── Tbr. 4.1 — Prekršaj s oduzimanjem predmeta ──
        // Ako se uz novčanu kaznu / kaznu zatvora može izreći i kazna oduzimanja predmeta,
        // odvjetniku pripada nagrada po Tbr. 7. toč. 1. ovisno o vrijednosti oduzetog predmeta.
        new() { Id = "kaz_prekrsaj_oduzimanje", Kategorija = "Kazneni postupak", Opis = "Prekršaj s oduzimanjem – nagrada po Tbr. 7.1 prema vrijednosti (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 8 — nagrada po Tbr. 7. toč. 1 prema vrijednosti oduzetog predmeta", PrekrsajOduzimanje = true, SortOrder = 137, Jedinica = "sat" },

        // ── Tbr. 4.1, al. 9 — Obrana pravne osobe ──
        // Za obranu pravne osobe u kaznenom ili prekršajnom postupku,
        // prema visini predviđene kazne za odgovornu osobu = KazneniBase (tier).
        new() { Id = "kaz_obrana_pravna_osoba", Kategorija = "Kazneni postupak", Opis = "Obrana pravne osobe u kazn./prekrš. postupku – po kazni za odg. osobu (Tbr. 4.1 al. 9)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 9 — prema visini kazne za odgovornu osobu", KazneniBase = true, SortOrder = 138, Jedinica = "sat" },

        // ── Tbr. 4.1, al. 10 — Predstavnik pravne osobe (postavi sud) ──
        // Kad odvjetnika u kaznenom postupku za predstavnika pravne osobe postavi sud:
        // 100 bodova za PRVI SAT SVAKOG DANA rasprave (jedinica je dan rasprave, količina =
        // broj dana), a svaki sljedeći započeti sat naplaćuje se preko `kaz_dodatni_sat` (50).
        new() { Id = "kaz_predstavnik_sud", Kategorija = "Kazneni postupak", Opis = "Predstavnik pravne osobe (postavi sud) – 100 bodova za prvi sat svakog dana rasprave (Tbr. 4.1 al. 10)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 10 — 100 bodova za prvi sat svakog dana rasprave; svaki sljedeći započeti sat 50 bodova", FiksniBody = 100, SortOrder = 139, Jedinica = "dan rasprave" },

        // ── Tbr. 4.1, al. 10 — Predstavnik pravne osobe (postavi pravna osoba) ──
        // Kad ga postavi pravna osoba, nagrada prema al. 9 (= tier za odgovornu osobu).
        new() { Id = "kaz_predstavnik_pravna_osoba", Kategorija = "Kazneni postupak", Opis = "Predstavnik pravne osobe – postavi pravna osoba, po kazni za odg. osobu (Tbr. 4.1 al. 10→9)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 10 — nagrada po al. 9 kad predstavnika postavi pravna osoba", KazneniBase = true, SortOrder = 140, Jedinica = "sat" },

        // ── Tbr. 5 — Pravni lijekovi ──
        new() { Id = "kaz_zalba_presuda", Kategorija = "Kazneni postupak", Opis = "Žalba protiv presude (Tbr. 5.1) – Tbr. 4.1 + 50%", TarifniBroj = "Tbr. 5.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 1 — nagrada iz Tbr. 4. toč. 1 uvećana za 50%", KazneniBase = true, KazneniPovecanje = 0.5m, SortOrder = 150, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_zalba", Kategorija = "Kazneni postupak", Opis = "Odgovor na žalbu protiv presude (Tbr. 5.2) – 50% od Tbr. 5.1", TarifniBroj = "Tbr. 5.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 2 — 50% nagrade iz Tbr. 5. toč. 1", KazneniBase = true, KazneniPovecanje = -0.25m, SortOrder = 151, Jedinica = "komad" },
        new() { Id = "kaz_zalba_istrazni_zatvor", Kategorija = "Kazneni postupak", Opis = "Žalba protiv rješenja o istražnom zatvoru (Tbr. 5.3) – po Tbr. 4.1", TarifniBroj = "Tbr. 5.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 3 — nagrada iz Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 152, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_zalba_rjesenje", Kategorija = "Kazneni postupak", Opis = "Odgovor na žalbu iz Tbr. 5.3 (Tbr. 5.4) – 50% Tbr. 4.1", TarifniBroj = "Tbr. 5.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 4 — 50% nagrade iz Tbr. 5. toč. 3", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 153, Jedinica = "komad" },
        new() { Id = "kaz_zalba_troskovi", Kategorija = "Kazneni postupak", Opis = "Žalba protiv odluke o trošku (Tbr. 5.5)", TarifniBroj = "Tbr. 5.5", FiksniBody = 50, SortOrder = 154, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_optuznica", Kategorija = "Kazneni postupak", Opis = "Odgovor na optužnicu (Tbr. 5.6) – po Tbr. 4.1", TarifniBroj = "Tbr. 5.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 6 — nagrada iz Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 155, Jedinica = "komad" },

        // ── Tbr. 6 — Izvanredni pravni lijekovi ──
        new() { Id = "kaz_izvanredni_pravni_lijek", Kategorija = "Kazneni postupak", Opis = "Izvanredni pravni lijek (Tbr. 6.1) – po Tbr. 5.1 (Tbr. 4.1 + 50%)", TarifniBroj = "Tbr. 6.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 6. toč. 1 — nagrada po Tbr. 5. toč. 1", KazneniBase = true, KazneniPovecanje = 0.5m, SortOrder = 170, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_izvanredni", Kategorija = "Kazneni postupak", Opis = "Odgovor na izvanredni pravni lijek (Tbr. 6.2) – 50% Tbr. 4.1", TarifniBroj = "Tbr. 6.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 6. toč. 2 — 50% nagrade iz Tbr. 4. toč. 1", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 171, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // II. PARNIČNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "par_tuzba", Kategorija = "Parnični postupak", Opis = "Tužba / protutužba / prijedlog (Tbr. 7.1)", TarifniBroj = "Tbr. 7.1", Tbr71Postotak = 1.0m, SortOrder = 200, Jedinica = "komad" },
        // ── Tbr. 7. toč. 2 — jednokratna nagrada od 250 bodova ──
        // "Odvjetniku pripada jednokratna nagrada u visini od 250 bodova, koja obuhvaća
        //  najviše do četiri radnje za svaki prvostupanjski postupak, i to u sljedećim
        //  postupcima: - radi smetanja posjeda, - radi služnosti, - iz stambenih odnosa
        //  (otkazi, iseljenja, utvrđenja), - iz radnih odnosa (poništenje otkaza,
        //  utvrđivanje prava na rad i sl.), OSIM u procjenjivim predmetima na koje se
        //  primjenjuje Tbr. 7. toč. 1.
        //  Za svaku daljnju radnju u navedenim postupcima odvjetniku pripada nagrada
        //  u visini od 50 bodova."
        new() { Id = "par_tuzba_smetanje", Kategorija = "Parnični postupak", Opis = "Tužba – smetanje posjeda / služnost / stambeni / radni spor (Tbr. 7.2) – jednokratno 250 bod. za do 4 radnje; radni sporovi s procjenjivom vrijednošću idu po Tbr. 7. toč. 1", TarifniBroj = "Tbr. 7.2", FiksniBody = 250, JednokratnaNagrada = true, UkljuceneRadnje = 4, DaljnjaRadnjaBodovi = 50, SortOrder = 201, Jedinica = "radnja" },

        // ── Tbr. 7. toč. 3 — jednokratna nagrada od 200 bodova ──
        // "Odvjetniku pripada jednokratna nagrada u visini od 200 bodova, koja obuhvaća
        //  najviše do četiri radnje za svaki prvostupanjski postupak, i to u sljedećim
        //  postupcima: - radi razvoda ili poništenja braka, o postojanju ili nepostojanju
        //  braka, - za utvrđivanje ili osporavanje očinstva, te o tome s kime će živjeti
        //  maloljetna djeca, - za zakonsko uzdržavanje ili dokidanje obveze uzdržavanja.
        //  Za svaku daljnju radnju (...) nagrada u visini od 50 bodova."
        new() { Id = "par_tuzba_obiteljski", Kategorija = "Parnični postupak", Opis = "Tužba – razvod / poništenje braka / očinstvo / s kime žive djeca / uzdržavanje (Tbr. 7.3) – jednokratno 200 bod. za do 4 radnje", TarifniBroj = "Tbr. 7.3", FiksniBody = 200, JednokratnaNagrada = true, UkljuceneRadnje = 4, DaljnjaRadnjaBodovi = 50, SortOrder = 202, Jedinica = "radnja" },
        new() { Id = "par_tuzba_platni_nalog", Kategorija = "Parnični postupak", Opis = "Tužba za platni nalog (Tbr. 7.5) – 50% Tbr. 7.1, max 100", TarifniBroj = "Tbr. 7.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 5 — 50% nagrade iz Tbr. 7. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 205, Jedinica = "komad" },

        // ── Tbr. 7. toč. 6 — mjenični, čekovni i drugi vrijednosni papiri ──
        // "Za tužbu na izdavanje mjeničnih, čekovnih i platnih naloga na osnovi drugih
        //  vrijednosnih papira, javnih i privatnih isprava, osim onih koji su navedeni u
        //  toč. 5. ovog tarifnog broja, odvjetniku pripada nagrada iz toč. 1. ovog
        //  tarifnog broja."
        // PUNA nagrada — bez stropa od 100 bodova koji vrijedi za toč. 5.
        new() { Id = "par_tuzba_vrijednosni_papiri", Kategorija = "Parnični postupak", Opis = "Tužba za mjenični / čekovni platni nalog i naloge na osnovi drugih vrijednosnih papira i isprava (Tbr. 7.6) – puna nagrada iz Tbr. 7.1", TarifniBroj = "Tbr. 7.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 6 — nagrada iz Tbr. 7. toč. 1, bez ograničenja iz toč. 5", Tbr71Postotak = 1.0m, SortOrder = 206, Jedinica = "komad" },

        new() { Id = "par_tuzba_arbitraza", Kategorija = "Parnični postupak", Opis = "Tužba pred domaćom arbitražom (Tbr. 7.7)", TarifniBroj = "Tbr. 7.7", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 7 — nagrada po Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 207, Jedinica = "komad" },
        new() { Id = "par_tuzba_med_arbitraza", Kategorija = "Parnični postupak", Opis = "Tužba pred međunarodnom arbitražom (Tbr. 7.7) – +100%", TarifniBroj = "Tbr. 7.7", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 7 — Tbr. 7. toč. 1 s povišenjem od 100%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 1.0m, SortOrder = 208, Jedinica = "komad" },

        // ── Tbr. 7. toč. 9 — parnica vrijednosti manje od 100,00 EUR ──
        // "Za zastupanje u parničnom postupku u kojem je vrijednost predmeta spora manja
        //  od 100,00 eura, neovisno o odredbi Tbr. 7. toč. 1., ukoliko točkama od 2. do 8.
        //  ovog Tbr. za pojedine postupke i sporove nije drugačije određeno, odvjetniku
        //  pripada jednokratna nagrada za cijeli prvostupanjski postupak u visini od 50 bodova."
        // Jednokratno za CIJELI prvostupanjski postupak — ne po radnji.
        new() { Id = "par_mali_spor", Kategorija = "Parnični postupak", Opis = "Zastupanje u prvostupanjskom postupku – VPS manji od 100,00 EUR (Tbr. 7.9) – jednokratno 50 bodova za cijeli postupak", TarifniBroj = "Tbr. 7.9", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 9 — jednokratna nagrada od 50 bodova za cijeli prvostupanjski postupak", FiksniBody = 50, JednokratnaNagrada = true, SortOrder = 209, Jedinica = "postupak" },

        // ── Tbr. 7. toč. 10 — obiteljski sporovi s međunarodnim elementom ──
        // "Zahtjevi kojima se pokreće postupak po Haškoj konvenciji (...) kao i svim drugim
        //  parničnim i izvanparničnim postupcima koji se odnose na obiteljske sporove s
        //  međunarodnim elementom odvjetniku pripada nagrada od 250 bodova."
        new() { Id = "par_zahtjev_haska", Kategorija = "Parnični postupak", Opis = "Zahtjev kojim se pokreće postupak po Haškoj konvenciji / Uredbama Vijeća – obiteljski spor s međunarodnim elementom (Tbr. 7.10) – 250 bodova", TarifniBroj = "Tbr. 7.10", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 10 — nagrada od 250 bodova", FiksniBody = 250, SortOrder = 210, Jedinica = "komad" },
        // ── Tbr. 8. toč. 1 — odgovor na tužbu + prigovor + četiri podneska ──
        // "Za sastavljanje odgovora na tužbu, prigovora protiv platnog naloga i četiri
        //  obrazložena podneska u prvostupanjskom postupku odvjetniku pripada nagrada
        //  kao za tužbu po Tbr. 7. toč. 1., 4., 7. i 8."
        // Jedna nagrada pokriva sve navedeno — bez naknade za daljnje radnje, jer je
        // toč. 1 ne propisuje; podnesci izvan toga idu po Tbr. 8. toč. 3 (niže).
        new() { Id = "par_odgovor_tuzba", Kategorija = "Parnični postupak", Opis = "Odgovor na tužbu / prigovor protiv platnog naloga + do četiri obrazložena podneska (Tbr. 8.1) – jedna nagrada kao za tužbu po Tbr. 7.1", TarifniBroj = "Tbr. 8.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 1 — nagrada kao za tužbu po Tbr. 7. toč. 1., 4., 7. i 8.", Tbr71Postotak = 1.0m, JednokratnaNagrada = true, SortOrder = 209, Jedinica = "postupak" },

        // ── Tbr. 8. toč. 3 — obrazloženi podnesci IZVAN Tbr. 8. toč. 1 ──
        // "Za sastavljanje obrazloženih podnesaka izvan Tbr. 8. toč. 1. odvjetniku
        //  pripada 50 % nagrade iz Tbr. 7. toč. 1., 4., 7. i 8., ali ne više od 100 bodova."
        new() { Id = "par_obrazlozeni_podnesak", Kategorija = "Parnični postupak", Opis = "Obrazloženi podnesci izvan Tbr. 8.1 (Tbr. 8.3) – 50% Tbr. 7.1, max 100", TarifniBroj = "Tbr. 8.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 3 — 50% nagrade iz Tbr. 7. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 210, Jedinica = "komad" },
        new() { Id = "par_ostali_podnesci", Kategorija = "Parnični postupak", Opis = "Ostali podnesci (Tbr. 8.4) – 25% Tbr. 7.1, max 25", TarifniBroj = "Tbr. 8.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 4 — 25% nagrade iz Tbr. 7. toč. 1, najviše 25 bodova", Tbr71Postotak = 0.25m, MaxBodova = 25, SortOrder = 211, Jedinica = "komad" },

        // ── Tbr. 8. toč. 2 — povrat u prijašnje stanje ──
        // "Za sastavljanje prijedloga za povrat u prijašnje stanje odvjetniku pripada 50%
        //  nagrade iz toč. 1. ovog tarifnog broja, ali ne više od 100 bodova."
        new() { Id = "par_povrat_u_prijasnje_stanje", Kategorija = "Parnični postupak", Opis = "Prijedlog za povrat u prijašnje stanje (Tbr. 8.2) – 50% nagrade iz Tbr. 8.1, max 100", TarifniBroj = "Tbr. 8.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 2 — 50% nagrade iz Tbr. 8. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 212, Jedinica = "komad" },

        // ── Tbr. 8. toč. 5 — obiteljski sporovi s međunarodnim elementom ──
        // "(...) odvjetniku pripada nagrada od 250 bodova, a za ostale podneske tijekom
        //  postupka nagrada od 125 bodova."
        // Apsolutni iznosi — ne ovise o vrijednosti predmeta spora.
        new() { Id = "par_podnesak_haska", Kategorija = "Parnični postupak", Opis = "Obrazloženi podnesak u postupku po Haškoj konvenciji / Uredbama Vijeća (Tbr. 8.5) – 250 bodova", TarifniBroj = "Tbr. 8.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 5 — 250 bodova za obrazloženi podnesak", FiksniBody = 250, SortOrder = 213, Jedinica = "komad" },
        new() { Id = "par_ostali_podnesak_haska", Kategorija = "Parnični postupak", Opis = "Ostali podnesak tijekom postupka po Haškoj konvenciji / Uredbama Vijeća (Tbr. 8.5) – 125 bodova", TarifniBroj = "Tbr. 8.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 5 — 125 bodova za ostale podneske tijekom postupka", FiksniBody = 125, SortOrder = 214, Jedinica = "komad" },

        new() { Id = "par_rociste_glavno", Kategorija = "Parnični postupak", Opis = "Ročište – glavna stvar / dokazi / nagodba (Tbr. 9.1)", TarifniBroj = "Tbr. 9.1", Tbr71Postotak = 1.0m, SortOrder = 220, Jedinica = "komad" },
        new() { Id = "par_rociste_procesno", Kategorija = "Parnični postupak", Opis = "Ročište – procesna pitanja (Tbr. 9.2) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 9.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 2 — 50% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 221, Jedinica = "komad" },
        new() { Id = "par_rociste_presuda", Kategorija = "Parnični postupak", Opis = "Ročište – objava presude (Tbr. 9.3) – 50% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 9.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 3 — 50% nagrade iz Tbr. 7. toč. 1, najviše 50 bodova", Tbr71Postotak = 0.5m, MaxBodova = 50, SortOrder = 222, Jedinica = "komad" },
        new() { Id = "par_pristup_odgodjen", Kategorija = "Parnični postupak", Opis = "Pristup odgođenom ročištu (Tbr. 9.5) – 25% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 9.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 5 — 25% nagrade iz Tbr. 7. toč. 1, najviše 50 bodova", Tbr71Postotak = 0.25m, MaxBodova = 50, SortOrder = 223, Jedinica = "komad" },
        // ── Tbr. 9. toč. 4 — ročišta pred arbitražom ──
        // "Za svako ročište pred arbitražom na kojem se raspravljalo o glavnoj stvari ili
        //  su se izvodili dokazi odvjetniku pripada nagrada iz Tbr. 7. toč. 7., a sva
        //  ostala ročišta 50% nagrade iz Tbr. 7. toč. 7."
        new() { Id = "par_rociste_arbitraza", Kategorija = "Parnični postupak", Opis = "Ročište pred arbitražom – glavna stvar / izvođenje dokaza (Tbr. 9.4)", TarifniBroj = "Tbr. 9.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 4 — nagrada iz Tbr. 7. toč. 7", Tbr71Postotak = 1.0m, SortOrder = 225, Jedinica = "komad" },
        new() { Id = "par_rociste_arbitraza_ostalo", Kategorija = "Parnični postupak", Opis = "Ostala ročišta pred arbitražom (Tbr. 9.4) – 50% nagrade iz Tbr. 7.7", TarifniBroj = "Tbr. 9.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 4 — 50% nagrade iz Tbr. 7. toč. 7", Tbr71Postotak = 0.5m, SortOrder = 226, Jedinica = "komad" },

        // ── Tbr. 9. toč. 6 — rasprava pred sudom drugog stupnja ──
        // "Za raspravu pred sudom drugog stupnja odvjetniku pripada nagrada po
        //  Tbr. 7. toč. 1., 4., 7. i 8."
        new() { Id = "par_rasprava_drugostupanjska", Kategorija = "Parnični postupak", Opis = "Rasprava pred sudom drugog stupnja (Tbr. 9.6) – puna nagrada iz Tbr. 7.1", TarifniBroj = "Tbr. 9.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 6 — nagrada po Tbr. 7. toč. 1., 4., 7. i 8.", Tbr71Postotak = 1.0m, SortOrder = 227, Jedinica = "komad" },

        new() { Id = "par_ocevid", Kategorija = "Parnični postupak", Opis = "Nazočnost očevidu (Tbr. 9.7)", TarifniBroj = "Tbr. 9.7", Tbr71Postotak = 1.0m, SortOrder = 228, Jedinica = "komad" },

        // ── Tbr. 9. toč. 8 — zastupanje u postupcima s međunarodnim elementom ──
        // "(...) odvjetniku pripada nagrada od 250 bodova." Apsolutni iznos.
        new() { Id = "par_zastupanje_haska", Kategorija = "Parnični postupak", Opis = "Zastupanje u postupku po Haškoj konvenciji / Uredbama Vijeća (Tbr. 9.8) – 250 bodova", TarifniBroj = "Tbr. 9.8", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 8 — 250 bodova", FiksniBody = 250, SortOrder = 229, Jedinica = "komad" },
        new() { Id = "par_zalba_presuda", Kategorija = "Parnični postupak", Opis = "Žalba protiv presude (Tbr. 10.1) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 10.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 1 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 230, Jedinica = "komad" },
        // ── Tbr. 10. toč. 2 i 3 — pravni lijekovi u parnicama iz Tbr. 7. toč. 2, 3 i 9 ──
        // Toč. 2: "(...) u parnicama iz Tbr. 7. toč. 2. i 3. odvjetniku pripada jednokratna
        //          nagrada u visini od 100 bodova."
        // Toč. 3: "(...) u parnicama iz Tbr. 7. toč. 9. odvjetniku pripada jednokratna
        //          nagrada u visini od 30 bodova."
        new() { Id = "par_zalba_jednokratna", Kategorija = "Parnični postupak", Opis = "Redovni pravni lijek u parnicama iz Tbr. 7.2 i 7.3 (Tbr. 10.2) – jednokratno 100 bodova", TarifniBroj = "Tbr. 10.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 2 — jednokratna nagrada od 100 bodova", FiksniBody = 100, JednokratnaNagrada = true, SortOrder = 231, Jedinica = "postupak" },
        new() { Id = "par_zalba_mali_spor", Kategorija = "Parnični postupak", Opis = "Redovni pravni lijek u parnicama iz Tbr. 7.9 – VPS ispod 100 EUR (Tbr. 10.3) – jednokratno 30 bodova", TarifniBroj = "Tbr. 10.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 3 — jednokratna nagrada od 30 bodova", FiksniBody = 30, JednokratnaNagrada = true, SortOrder = 232, Jedinica = "postupak" },

        new() { Id = "par_zalba_rjesenje", Kategorija = "Parnični postupak", Opis = "Žalba protiv rješenja (Tbr. 10.4) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 10.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 4 — 50% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 233, Jedinica = "komad" },
        new() { Id = "par_revizija", Kategorija = "Parnični postupak", Opis = "Prijedlog za dopuštenje revizije / izvanredni pravni lijek (Tbr. 10.5) – Tbr. 7.1 + 50%", TarifniBroj = "Tbr. 10.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 5 — Tbr. 7. toč. 1 s povišenjem od 50%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.5m, SortOrder = 234, Jedinica = "komad" },

        // Tbr. 10. toč. 5 st. 2 — u predmetima iz Tbr. 7. toč. 2 i 3 nagrada je 150 bodova,
        // a u predmetima iz Tbr. 7. toč. 9 nagrada je 100 bodova.
        new() { Id = "par_revizija_jednokratna", Kategorija = "Parnični postupak", Opis = "Prijedlog za dopuštenje revizije u predmetima iz Tbr. 7.2 i 7.3 (Tbr. 10.5 st. 2) – 150 bodova", TarifniBroj = "Tbr. 10.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 5 st. 2 — 150 bodova u predmetima iz Tbr. 7. toč. 2 i 3", FiksniBody = 150, SortOrder = 235, Jedinica = "komad" },
        new() { Id = "par_revizija_mali_spor", Kategorija = "Parnični postupak", Opis = "Prijedlog za dopuštenje revizije u predmetima iz Tbr. 7.9 (Tbr. 10.5 st. 2) – 100 bodova", TarifniBroj = "Tbr. 10.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 5 st. 2 — 100 bodova u predmetima iz Tbr. 7. toč. 9", FiksniBody = 100, SortOrder = 236, Jedinica = "komad" },

        new() { Id = "par_odgovor_zalba", Kategorija = "Parnični postupak", Opis = "Odgovor na žalbu / prijedlog za dopuštenje revizije / izvanredni pravni lijek (Tbr. 10.6)", TarifniBroj = "Tbr. 10.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 6 — nagrada po Tbr. 7. toč. 1., 4. i 7.", Tbr71Postotak = 1.0m, SortOrder = 237, Jedinica = "komad" },

        // Tbr. 10. toč. 6 st. 2 — u predmetima iz Tbr. 7. toč. 2, 3 i 9 nagrada je 30 bodova.
        new() { Id = "par_odgovor_zalba_jednokratno", Kategorija = "Parnični postupak", Opis = "Odgovor na žalbu u predmetima iz Tbr. 7.2, 7.3 i 7.9 (Tbr. 10.6 st. 2) – 30 bodova", TarifniBroj = "Tbr. 10.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 6 st. 2 — 30 bodova u predmetima iz Tbr. 7. toč. 2, 3 i 9", FiksniBody = 30, SortOrder = 238, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // III. OVRŠNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ovr_prijedlog", Kategorija = "Ovršni postupak", Opis = "Prijedlog za ovrhu / protuovrhu / osiguranje (Tbr. 11.1)", TarifniBroj = "Tbr. 11.1", Tbr71Postotak = 1.0m, SortOrder = 300, Jedinica = "komad" },
        new() { Id = "ovr_prokazna_izjava", Kategorija = "Ovršni postupak", Opis = "Prijedlog za prokaznu izjavu (Tbr. 11.3) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 3 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 303, Jedinica = "komad" },
        new() { Id = "ovr_odgoda", Kategorija = "Ovršni postupak", Opis = "Prijedlog za odgodu ovrhe (Tbr. 11.4) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 4 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 304, Jedinica = "komad" },
        new() { Id = "ovr_sudska_ovrha", Kategorija = "Ovršni postupak", Opis = "Prijedlog sudu za provedbu sudske ovrhe (Tbr. 11.5) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 5 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 305, Jedinica = "komad" },
        new() { Id = "ovr_dostava", Kategorija = "Ovršni postupak", Opis = "Dostava ovršne isprave (Tbr. 11.6) – 50% Tbr. 11.1, max 100", TarifniBroj = "Tbr. 11.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 6 — 50% nagrade iz Tbr. 11. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 306, Jedinica = "komad" },
        new() { Id = "ovr_prituzba", Kategorija = "Ovršni postupak", Opis = "Pritužba ovršenika (Tbr. 11.7) – 50% Tbr. 11.1, max 100", TarifniBroj = "Tbr. 11.7", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 7 — 50% nagrade iz Tbr. 11. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 307, Jedinica = "komad" },
        new() { Id = "ovr_zaduznica", Kategorija = "Ovršni postupak", Opis = "Zahtjev za naplatu po zadužnici (Tbr. 11.8) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.8", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 8 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 308, Jedinica = "komad" },
        new() { Id = "ovr_drazbeni_uvjeti", Kategorija = "Ovršni postupak", Opis = "Dražbeni uvjeti (Tbr. 11.9)", TarifniBroj = "Tbr. 11.9", Tbr71Postotak = 1.0m, SortOrder = 309, Jedinica = "komad" },
        new() { Id = "ovr_ostali_podnesci", Kategorija = "Ovršni postupak", Opis = "Ostali podnesci u ovrsi (Tbr. 11.10) – 25% Tbr. 7.1, max 250", TarifniBroj = "Tbr. 11.10", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 10 — 25% nagrade iz Tbr. 7. toč. 1, najviše 250 bodova", Tbr71Postotak = 0.25m, MaxBodova = 250, SortOrder = 310, Jedinica = "komad" },
        new() { Id = "ovr_rociste", Kategorija = "Ovršni postupak", Opis = "Ročište u ovrsi (Tbr. 12.1)", TarifniBroj = "Tbr. 12.1", Tbr71Postotak = 1.0m, SortOrder = 312, Jedinica = "komad" },
        new() { Id = "ovr_pravni_lijek", Kategorija = "Ovršni postupak", Opis = "Pravni lijek u ovrsi (Tbr. 13.1)", TarifniBroj = "Tbr. 13.1", Tbr71Postotak = 1.0m, SortOrder = 315, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // IV. STEČAJNI I PREDSTEČAJNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ste_prijedlog", Kategorija = "Stečajni postupak", Opis = "Prijedlog za pokretanje stečaja/predstečaja (Tbr. 14.1) – Tbr. 7.1, max 1000", TarifniBroj = "Tbr. 14.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 14. toč. 1 — nagrada po Tbr. 7. toč. 1, najviše 1000 bodova", Tbr71Postotak = 1.0m, MaxBodova = 1000, SortOrder = 400, Jedinica = "komad" },
        new() { Id = "ste_prijava_potrazivanja", Kategorija = "Stečajni postupak", Opis = "Prijava potraživanja / obrazloženi podnesak (Tbr. 14.2) – Tbr. 7.1, max 2000", TarifniBroj = "Tbr. 14.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 14. toč. 2 — nagrada po Tbr. 7. toč. 1, najviše 2000 bodova", Tbr71Postotak = 1.0m, MaxBodova = 2000, SortOrder = 401, Jedinica = "komad" },
        new() { Id = "ste_pravni_lijek", Kategorija = "Stečajni postupak", Opis = "Pravni lijek u stečaju (Tbr. 14.3) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 14.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 14. toč. 3 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 402, Jedinica = "komad" },
        new() { Id = "ste_rociste", Kategorija = "Stečajni postupak", Opis = "Ročište / sjednica vjerovnika (Tbr. 15)", TarifniBroj = "Tbr. 15", Tbr71Postotak = 1.0m, SortOrder = 405, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // V. IZVANPARNIČNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "izv_dioba", Kategorija = "Izvanparnični postupak", Opis = "Pokretanje postupka – dioba / ostavina / procjenjivi (Tbr. 16.1) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 16.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 16. toč. 1 — 50% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 500, Jedinica = "komad" },
        new() { Id = "izv_meda", Kategorija = "Izvanparnični postupak", Opis = "Uređenje međe (Tbr. 16.1)", TarifniBroj = "Tbr. 16.1", FiksniBody = 50, SortOrder = 501, Jedinica = "komad" },
        new() { Id = "izv_neprocjenjivi", Kategorija = "Izvanparnični postupak", Opis = "Ostali neprocjenjivi izvanparnični (Tbr. 16.1)", TarifniBroj = "Tbr. 16.1", FiksniBody = 50, SortOrder = 502, Jedinica = "komad" },
        new() { Id = "izv_amortizacija", Kategorija = "Izvanparnični postupak", Opis = "Amortizacija isprava (Tbr. 16.1) – 50% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 16.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 16. toč. 1 — 50% nagrade iz Tbr. 7. toč. 1, najviše 50 bodova", Tbr71Postotak = 0.5m, MaxBodova = 50, SortOrder = 503, Jedinica = "komad" },
        new() { Id = "izv_rociste", Kategorija = "Izvanparnični postupak", Opis = "Ročište u izvanparnici – procjenjivo (Tbr. 17.1) – 75% Tbr. 7.1", TarifniBroj = "Tbr. 17.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 17. toč. 1 — 75% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.75m, SortOrder = 510, Jedinica = "komad" },
        new() { Id = "izv_pravni_lijek", Kategorija = "Izvanparnični postupak", Opis = "Pravni lijek u izvanparnici (Tbr. 18.1) – Tbr. 16 + 25%", TarifniBroj = "Tbr. 18.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 18. toč. 1 — nagrada iz Tbr. 16 s povišenjem od 25%", Tbr71Postotak = 0.5m, Tbr71Povecanje = 0.25m, SortOrder = 515, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VI. ZEMLJIŠNOKNJIŽNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "zk_redoviti", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – redoviti postupak (Tbr. 19.1) – 25% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 19.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 19. toč. 1 — 25% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.25m, MaxBodova = 500, SortOrder = 600, Jedinica = "komad" },
        new() { Id = "zk_redoviti_neproc", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – neprocjenjiv (Tbr. 19.2)", TarifniBroj = "Tbr. 19.2", FiksniBody = 50, SortOrder = 601, Jedinica = "komad" },
        new() { Id = "zk_ostali", Kategorija = "Zemljišnoknjižni postupak", Opis = "Ostali podnesci u ZK postupku (Tbr. 19.3)", TarifniBroj = "Tbr. 19.3", FiksniBody = 50, SortOrder = 602, Jedinica = "komad" },
        new() { Id = "zk_posebni", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – posebni postupak (Tbr. 20.1) – 25% Tbr. 7.1", TarifniBroj = "Tbr. 20.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 20. toč. 1 — 25% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.25m, SortOrder = 605, Jedinica = "komad" },
        new() { Id = "zk_pravni_lijek", Kategorija = "Zemljišnoknjižni postupak", Opis = "Pravni lijek u ZK postupku (Tbr. 21) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 21", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 21 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 610, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VII. UPRAVNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "upr_neprocjenjiv", Kategorija = "Upravni postupak", Opis = "Zahtjev u upravnom postupku – neprocjenjiv (Tbr. 22.1)", TarifniBroj = "Tbr. 22.1", FiksniBody = 50, SortOrder = 700, Jedinica = "komad" },
        new() { Id = "upr_procjenjiv", Kategorija = "Upravni postupak", Opis = "Zahtjev u upravnom postupku – procjenjiv (Tbr. 22.1)", TarifniBroj = "Tbr. 22.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 22. toč. 1 — nagrada po Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 701, Jedinica = "komad" },
        new() { Id = "upr_ostali", Kategorija = "Upravni postupak", Opis = "Ostali podnesci u upravnom postupku (Tbr. 22.2) – 25%, max 25", TarifniBroj = "Tbr. 22.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 22. toč. 2 — 25% nagrade iz Tbr. 7. toč. 1, najviše 25 bodova", Tbr71Postotak = 0.25m, MaxBodova = 25, SortOrder = 702, Jedinica = "komad" },
        new() { Id = "upr_zalba", Kategorija = "Upravni postupak", Opis = "Žalba / prigovor / drug. instanca (Tbr. 24) – Tbr. 22 + 25%", TarifniBroj = "Tbr. 24", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 24 — nagrada iz Tbr. 22 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 710, Jedinica = "komad" },

        // Tbr. 23. toč. 1: "Za zastupanje u ispitnom postupku, u postupku osiguranja
        // dokaza, na usmenoj raspravi te očevidu u prvostupanjskom upravnom postupku
        // odvjetniku pripada nagrada iz Tbr. 22. te naknada po Tbr. 39."
        // Toč. 2: "Za obranu u postupcima radi poreznih, carinskih ili deviznih prekršaja
        // odvjetniku pripada nagrada po Tbr. 22., te naknada po Tbr. 39."
        //
        // Tbr. 22. toč. 1 ima dvije alineje (neprocjenjivo 50 bodova, procjenjivo po
        // Tbr. 7. toč. 1), pa svaka točka Tbr. 23 daje dvije stavke — isti obrazac kao
        // upr_neprocjenjiv / upr_procjenjiv gore.
        //
        // "naknada po Tbr. 39" se NE dodaje ovdje: Tbr. 39 je već zasebna stavka
        // zaj_cekanje i obračunava se odvojeno, po započetom satu.
        //
        // Toč. 2 govori o prekršajima, ali Tarifa je smješta u glavu VII. UPRAVNI POSTUPAK
        // i nagradu veže uz Tbr. 22, ne uz Tbr. 4. Kategorija mora ostati "Upravni
        // postupak" — u kategoriji "Kazneni postupak" tiho bi ugasila sudsku pristojbu.
        new() { Id = "upr_zastupanje_neproc", Kategorija = "Upravni postupak", Opis = "Zastupanje u ispitnom postupku / na raspravi / očevidu – neprocjenjiv (Tbr. 23.1); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 1 — nagrada iz Tbr. 22. toč. 1 al. 1, 50 bodova", FiksniBody = 50, SortOrder = 720, Jedinica = "komad" },
        new() { Id = "upr_zastupanje_proc", Kategorija = "Upravni postupak", Opis = "Zastupanje u ispitnom postupku / na raspravi / očevidu – procjenjiv (Tbr. 23.1); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 1 — nagrada iz Tbr. 22. toč. 1 al. 2 (Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 721, Jedinica = "komad" },
        new() { Id = "upr_obrana_prekrsaj_neproc", Kategorija = "Upravni postupak", Opis = "Obrana u poreznom / carinskom / deviznom prekršaju – neprocjenjiv (Tbr. 23.2); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 2 — nagrada po Tbr. 22. toč. 1 al. 1, 50 bodova", FiksniBody = 50, SortOrder = 722, Jedinica = "komad" },
        new() { Id = "upr_obrana_prekrsaj_proc", Kategorija = "Upravni postupak", Opis = "Obrana u poreznom / carinskom / deviznom prekršaju – procjenjiv (Tbr. 23.2); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 2 — nagrada po Tbr. 22. toč. 1 al. 2 (Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 723, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VIII. UPRAVNI SPOR
        // ═══════════════════════════════════════════════════════════
        // Tbr. 27. toč. 1: "— u procjenjivim predmetima odvjetniku pripada nagrada iz
        // Tbr. 7. toč. 1., — u neprocjenjivim predmetima odvjetniku pripada nagrada od
        // 500 bodova." Toč. 2, 3 i 4 sve upućuju na toč. 1, pa svaka od njih ima
        // procjenjivu i neprocjenjivu varijantu.
        new() { Id = "usp_tuzba_proc", Kategorija = "Upravni spor", Opis = "Tužba / odgovor na tužbu / obrazloženi podnesak – procjenjiv (Tbr. 27.1)", TarifniBroj = "Tbr. 27.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 1 al. 1 — nagrada iz Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 800, Jedinica = "komad" },
        new() { Id = "usp_tuzba_neproc", Kategorija = "Upravni spor", Opis = "Tužba / odgovor na tužbu / obrazloženi podnesak – neprocjenjiv (Tbr. 27.1) – 500 bodova", TarifniBroj = "Tbr. 27.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 1 al. 2 — 500 bodova u neprocjenjivim predmetima", FiksniBody = 500, SortOrder = 801, Jedinica = "komad" },

        // Tbr. 27. toč. 2: "Za zastupanje na raspravi odvjetniku pripada nagrada iz
        // toč. 1. ovog tarifnog broja."
        new() { Id = "usp_rasprava", Kategorija = "Upravni spor", Opis = "Zastupanje na raspravi – procjenjiv (Tbr. 27.2)", TarifniBroj = "Tbr. 27.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 2 — nagrada iz Tbr. 27. toč. 1 (procjenjivo: Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 802, Jedinica = "komad" },
        new() { Id = "usp_rasprava_neproc", Kategorija = "Upravni spor", Opis = "Zastupanje na raspravi – neprocjenjiv (Tbr. 27.2 → 27.1) – 500 bodova", TarifniBroj = "Tbr. 27.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 2 — nagrada iz Tbr. 27. toč. 1 al. 2, 500 bodova", FiksniBody = 500, SortOrder = 803, Jedinica = "komad" },

        // Tbr. 27. toč. 3: "(...) pripada nagrada iz toč. 1. ovog tarifnog broja
        // uvećana za 25%."  Neprocjenjivo: 500 × 1,25 = 625 bodova.
        new() { Id = "usp_zalba", Kategorija = "Upravni spor", Opis = "Žalba / zahtjev VUS – procjenjiv (Tbr. 27.3) – Tbr. 27.1 + 25%", TarifniBroj = "Tbr. 27.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 3 — nagrada iz Tbr. 27. toč. 1 uvećana za 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 805, Jedinica = "komad" },
        new() { Id = "usp_zalba_neproc", Kategorija = "Upravni spor", Opis = "Žalba / zahtjev VUS – neprocjenjiv (Tbr. 27.3) – 500 + 25% = 625 bodova", TarifniBroj = "Tbr. 27.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 3 — 500 bodova iz Tbr. 27. toč. 1 al. 2 uvećano za 25% = 625", FiksniBody = 625, SortOrder = 806, Jedinica = "komad" },

        // Tbr. 27. toč. 4: "(...) pripada 50% nagrade iz toč. 1. ovog tarifnog broja."
        // Neprocjenjivo: 50% od 500 = 250 bodova.
        new() { Id = "usp_izvrsenje", Kategorija = "Upravni spor", Opis = "Zahtjev za izvršenje presude – procjenjiv (Tbr. 27.4) – 50% Tbr. 27.1", TarifniBroj = "Tbr. 27.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 4 — 50% nagrade iz Tbr. 27. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 807, Jedinica = "komad" },
        new() { Id = "usp_izvrsenje_neproc", Kategorija = "Upravni spor", Opis = "Zahtjev za izvršenje presude – neprocjenjiv (Tbr. 27.4) – 50% od 500 = 250 bodova", TarifniBroj = "Tbr. 27.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 4 — 50% od 500 bodova iz Tbr. 27. toč. 1 al. 2 = 250", FiksniBody = 250, SortOrder = 808, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // IX. REGISTARSKI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "reg_upis", Kategorija = "Registarski postupak", Opis = "Podnesak za upis / promjenu u registru (Tbr. 28.1)", TarifniBroj = "Tbr. 28.1", FiksniBody = 50, SortOrder = 900, Jedinica = "komad" },

        // Tbr. 28. toč. 2: "Za sastavljanje isprava (izjava o osnivanju društva, društveni
        // ugovor, statut, te ostale isprave) potrebnih za upis osnivanja ili promjena u
        // registru, odvjetniku pripada nagrada prema Tbr. 33. toč. 1. i 2."
        //
        // Tbr. 33. toč. 1: "nagrada iz Tbr. 7. toč. 1. uvećana za 25%."
        // Tbr. 33. toč. 2 al. 3: "ako u ugovoru nije naznačena vrijednost ugovora
        // odvjetniku pripada nagrada od 200 bodova."
        // Isti par kakav već postoji za raz_ugovor / raz_ugovor_neproc.
        new() { Id = "reg_isprave_proc", Kategorija = "Registarski postupak", Opis = "Osnivačke isprave za upis u registar – naznačena vrijednost (Tbr. 28.2) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 28.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 28. toč. 2 — nagrada prema Tbr. 33. toč. 1 (Tbr. 7. toč. 1 uvećan za 25%)", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 901, Jedinica = "komad" },
        new() { Id = "reg_isprave_neproc", Kategorija = "Registarski postupak", Opis = "Osnivačke isprave za upis u registar – bez naznačene vrijednosti (Tbr. 28.2) – 200 bodova", TarifniBroj = "Tbr. 28.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 28. toč. 2 — nagrada prema Tbr. 33. toč. 2 al. 3, 200 bodova", FiksniBody = 200, SortOrder = 902, Jedinica = "komad" },

        new() { Id = "reg_ostali", Kategorija = "Registarski postupak", Opis = "Ostali podnesci za upis (Tbr. 28.3)", TarifniBroj = "Tbr. 28.3", FiksniBody = 50, SortOrder = 903, Jedinica = "komad" },
        new() { Id = "reg_pravni_lijek", Kategorija = "Registarski postupak", Opis = "Pravni lijek u registarskom postupku (Tbr. 28.4) – 50 + 50%", TarifniBroj = "Tbr. 28.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 28. toč. 4 — nagrada iz Tbr. 28. toč. 1 uvećana za 50%", FiksniBody = 75, SortOrder = 904, Jedinica = "komad" },

        // ── Tbr. 29 — poništaj upisa u registar ──────────────────────
        //
        // Toč. 1: "Za sastavljanje tužbe na poništaj upisa u registar, za odgovore na tužbu
        // i obrazložene podneske o činjeničnim i pravnim navodima, odvjetniku pripada
        // nagrada iz Tbr. 7. toč. 1. kod tužbi na poništaj upisa u sudski registar, a ne
        // manje od 100 bodova, odnosno po Tbr. 27. kod tužbi na poništaj upisa u drugi
        // registar."
        //
        // Toč. 1 time daje TRI osnovice:
        //   a) sudski registar          → Tbr. 7. toč. 1, prag 100 bodova
        //   b) drugi registar, procj.   → Tbr. 27. toč. 1 al. 1 → Tbr. 7. toč. 1, bez praga
        //   c) drugi registar, neprocj. → Tbr. 27. toč. 1 al. 2 → 500 bodova
        //
        // Toč. 2: "Za zastupanje na ročištu na kojem se raspravljalo o glavnoj stvari, ili
        // su se izvodili dokazi (...) nagrada iz toč. 1., a za sva ostala ročišta na kojima
        // se raspravljalo samo o procesnim pitanjima, ili za ročište na kojem se prije
        // raspravljanja o glavnoj stvari postupak završio povlačenjem tužbe, nagodbom ili
        // presudom na osnovi priznanja (...) 50% nagrade iz toč. 1."
        //
        // Toč. 3: "Za sastavljanje pravnih lijekova protiv presuda (...) nagrada iz toč. 1.
        // uvećana za 25%, a (...) protiv rješenja (...) 50% nagrade iz toč. 1."
        //
        // PRAG SE SKALIRA S POSTOTKOM. ClampBodovi primjenjuje MinBodova nakon postotka, a
        // toč. 2 i 3 uzimaju postotak nagrade IZ TOČ. 1 — u kojoj prag već stoji. Kako je
        // prag konstanta, vrijedi max(x, 100) × p ≡ max(p·x, 100p), pa se u svaku stavku
        // upisuje prag pomnožen istim postotkom: 100 → 50 (50%) odnosno 125 (125%).
        // Naivan prag od 100 na svakoj stavci dao bi u malim predmetima previše.

        // a) sudski registar — prag 100 bodova
        new() { Id = "reg_ponistaj_tuzba_sudski", Kategorija = "Registarski postupak", Opis = "Tužba na poništaj upisa u sudski registar / odgovor / obrazloženi podnesak (Tbr. 29.1) – Tbr. 7.1, min 100", TarifniBroj = "Tbr. 29.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 1 — nagrada iz Tbr. 7. toč. 1 kod sudskog registra, a ne manje od 100 bodova", Tbr71Postotak = 1.0m, MinBodova = 100, SortOrder = 910, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_rociste_glavno_sudski", Kategorija = "Registarski postupak", Opis = "Ročište o glavnoj stvari / izvođenje dokaza – sudski registar (Tbr. 29.2) – Tbr. 29.1", TarifniBroj = "Tbr. 29.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 2 — nagrada iz Tbr. 29. toč. 1 (Tbr. 7. toč. 1, ne manje od 100 bodova)", Tbr71Postotak = 1.0m, MinBodova = 100, SortOrder = 911, Jedinica = "komad" },
        // 50% nagrade iz toč. 1 ⇒ i prag je 50% od 100 = 50 bodova.
        new() { Id = "reg_ponistaj_rociste_ostalo_sudski", Kategorija = "Registarski postupak", Opis = "Ostala ročišta (procesna pitanja, povlačenje, nagodba, priznanje) – sudski registar (Tbr. 29.2) – 50% Tbr. 29.1", TarifniBroj = "Tbr. 29.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 2 — 50% nagrade iz Tbr. 29. toč. 1; prag od 100 bodova skaliran istim postotkom = 50", Tbr71Postotak = 0.5m, MinBodova = 50, SortOrder = 912, Jedinica = "komad" },
        // Toč. 1 uvećana za 25% ⇒ prag 100 × 1,25 = 125 bodova.
        new() { Id = "reg_ponistaj_lijek_presuda_sudski", Kategorija = "Registarski postupak", Opis = "Pravni lijek protiv presude – sudski registar (Tbr. 29.3) – Tbr. 29.1 + 25%", TarifniBroj = "Tbr. 29.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 3 — nagrada iz Tbr. 29. toč. 1 uvećana za 25%; prag od 100 bodova skaliran istim postotkom = 125", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, MinBodova = 125, SortOrder = 913, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_lijek_rjesenje_sudski", Kategorija = "Registarski postupak", Opis = "Pravni lijek protiv rješenja – sudski registar (Tbr. 29.3) – 50% Tbr. 29.1", TarifniBroj = "Tbr. 29.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 3 — 50% nagrade iz Tbr. 29. toč. 1; prag od 100 bodova skaliran istim postotkom = 50", Tbr71Postotak = 0.5m, MinBodova = 50, SortOrder = 914, Jedinica = "komad" },

        // b) drugi registar, procjenjiv — po Tbr. 27. toč. 1 al. 1; prag iz toč. 1 vezan je
        //    izrijekom uz sudski registar, pa se ovdje NE primjenjuje.
        new() { Id = "reg_ponistaj_tuzba_drugi_proc", Kategorija = "Registarski postupak", Opis = "Tužba na poništaj upisa u drugi registar / odgovor / obrazloženi podnesak – procjenjiv (Tbr. 29.1 → Tbr. 27.1)", TarifniBroj = "Tbr. 29.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 1 — po Tbr. 27 kod drugog registra; Tbr. 27. toč. 1 al. 1 → Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 920, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_rociste_glavno_drugi_proc", Kategorija = "Registarski postupak", Opis = "Ročište o glavnoj stvari / izvođenje dokaza – drugi registar, procjenjiv (Tbr. 29.2)", TarifniBroj = "Tbr. 29.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 2 — nagrada iz Tbr. 29. toč. 1 (drugi registar: Tbr. 27. toč. 1 al. 1 → Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 921, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_rociste_ostalo_drugi_proc", Kategorija = "Registarski postupak", Opis = "Ostala ročišta (procesna pitanja, povlačenje, nagodba, priznanje) – drugi registar, procjenjiv (Tbr. 29.2) – 50%", TarifniBroj = "Tbr. 29.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 2 — 50% nagrade iz Tbr. 29. toč. 1 (drugi registar: Tbr. 27. toč. 1 al. 1)", Tbr71Postotak = 0.5m, SortOrder = 922, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_lijek_presuda_drugi_proc", Kategorija = "Registarski postupak", Opis = "Pravni lijek protiv presude – drugi registar, procjenjiv (Tbr. 29.3) – Tbr. 29.1 + 25%", TarifniBroj = "Tbr. 29.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 3 — nagrada iz Tbr. 29. toč. 1 uvećana za 25% (drugi registar: Tbr. 27. toč. 1 al. 1)", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 923, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_lijek_rjesenje_drugi_proc", Kategorija = "Registarski postupak", Opis = "Pravni lijek protiv rješenja – drugi registar, procjenjiv (Tbr. 29.3) – 50% Tbr. 29.1", TarifniBroj = "Tbr. 29.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 3 — 50% nagrade iz Tbr. 29. toč. 1 (drugi registar: Tbr. 27. toč. 1 al. 1)", Tbr71Postotak = 0.5m, SortOrder = 924, Jedinica = "komad" },

        // c) drugi registar, neprocjenjiv — Tbr. 27. toč. 1 al. 2 daje 500 bodova.
        //    Izvedeni iznosi: 50% = 250, +25% = 625. Isti obrazac kao usp_*_neproc.
        new() { Id = "reg_ponistaj_tuzba_drugi_neproc", Kategorija = "Registarski postupak", Opis = "Tužba na poništaj upisa u drugi registar / odgovor / obrazloženi podnesak – neprocjenjiv (Tbr. 29.1 → Tbr. 27.1) – 500 bodova", TarifniBroj = "Tbr. 29.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 1 — po Tbr. 27 kod drugog registra; Tbr. 27. toč. 1 al. 2 → 500 bodova", FiksniBody = 500, SortOrder = 930, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_rociste_glavno_drugi_neproc", Kategorija = "Registarski postupak", Opis = "Ročište o glavnoj stvari / izvođenje dokaza – drugi registar, neprocjenjiv (Tbr. 29.2) – 500 bodova", TarifniBroj = "Tbr. 29.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 2 — nagrada iz Tbr. 29. toč. 1 (drugi registar neprocjenjiv: 500 bodova)", FiksniBody = 500, SortOrder = 931, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_rociste_ostalo_drugi_neproc", Kategorija = "Registarski postupak", Opis = "Ostala ročišta (procesna pitanja, povlačenje, nagodba, priznanje) – drugi registar, neprocjenjiv (Tbr. 29.2) – 50% od 500 = 250", TarifniBroj = "Tbr. 29.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 2 — 50% od 500 bodova iz Tbr. 29. toč. 1 = 250", FiksniBody = 250, SortOrder = 932, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_lijek_presuda_drugi_neproc", Kategorija = "Registarski postupak", Opis = "Pravni lijek protiv presude – drugi registar, neprocjenjiv (Tbr. 29.3) – 500 + 25% = 625", TarifniBroj = "Tbr. 29.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 3 — 500 bodova iz Tbr. 29. toč. 1 uvećano za 25% = 625", FiksniBody = 625, SortOrder = 933, Jedinica = "komad" },
        new() { Id = "reg_ponistaj_lijek_rjesenje_drugi_neproc", Kategorija = "Registarski postupak", Opis = "Pravni lijek protiv rješenja – drugi registar, neprocjenjiv (Tbr. 29.3) – 50% od 500 = 250", TarifniBroj = "Tbr. 29.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 3 — 50% od 500 bodova iz Tbr. 29. toč. 1 = 250", FiksniBody = 250, SortOrder = 934, Jedinica = "komad" },

        // Toč. 4: "Za zastupanje dioničara ili člana društva na skupštini trgovačkog
        // društva, odvjetniku pripada nagrada prema dogovoru sa strankom, ali ne manje od
        // 100 bodova za svaki započeti sat skupštine."
        //
        // Tarifa određuje samo DONJU granicu; gornju prepušta dogovoru sa strankom, pa se
        // obračunava minimum. Povišenje iznad toga ide kroz Tbr. 41 ili ugovor po Tbr. 43.
        new() { Id = "reg_skupstina", Kategorija = "Registarski postupak", Opis = "Zastupanje dioničara / člana društva na skupštini (Tbr. 29.4) – najmanje 100 bodova po započetom satu", TarifniBroj = "Tbr. 29.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 29. toč. 4 — nagrada prema dogovoru sa strankom, ali ne manje od 100 bodova za svaki započeti sat skupštine", FiksniBody = 100, SortOrder = 940, Jedinica = "sat" },

        // ═══════════════════════════════════════════════════════════
        // X. ZAŠTITA PRAVA PRED POSLODAVCEM
        // ═══════════════════════════════════════════════════════════
        new() { Id = "pos_obrazlozeni", Kategorija = "Zaštita pred poslodavcem", Opis = "Obrazloženi podnesci (Tbr. 30.1)", TarifniBroj = "Tbr. 30.1", FiksniBody = 50, SortOrder = 1000, Jedinica = "komad" },
        new() { Id = "pos_zahtjev", Kategorija = "Zaštita pred poslodavcem", Opis = "Zahtjev za zaštitu prava (Tbr. 30.2)", TarifniBroj = "Tbr. 30.2", FiksniBody = 50, SortOrder = 1001, Jedinica = "komad" },
        new() { Id = "pos_nacrt_odluke", Kategorija = "Zaštita pred poslodavcem", Opis = "Nacrt odluke za poslodavca (Tbr. 30.3)", TarifniBroj = "Tbr. 30.3", FiksniBody = 100, SortOrder = 1002, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XI. USTAVNI SUD / ECHR / SUD EU
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ust_pokretanje", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Pokretanje postupka pred Ustavnim sudom / ECHR / Sud EU (Tbr. 31.1)", TarifniBroj = "Tbr. 31.1", FiksniBody = 1000, SortOrder = 1100, Jedinica = "komad" },
        new() { Id = "ust_ostali_podnesci", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Ostali podnesci (Tbr. 31.2)", TarifniBroj = "Tbr. 31.2", FiksniBody = 50, SortOrder = 1101, Jedinica = "komad" },
        new() { Id = "ust_rasprava", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Zastupanje na raspravi (Tbr. 31.3)", TarifniBroj = "Tbr. 31.3", FiksniBody = 1000, SortOrder = 1102, Jedinica = "komad" },

        // Tbr. 31. toč. 4: "Za zastupanje i sudjelovanje na raspravama na kojima se
        // raspravljalo samo o procesnim pitanjima, ili je postupak obustavljen prije
        // raspravljanja o glavnoj stvari odvjetniku pripada 50% nagrade iz toč. 1. ovog
        // tarifnog broja."  Toč. 1 = 1000 bodova ⇒ 50% × 1000 = 500 bodova.
        new() { Id = "ust_rasprava_procesna", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Rasprava samo o procesnim pitanjima / obustava prije glavne stvari (Tbr. 31.4) – 50% od 1000 = 500", TarifniBroj = "Tbr. 31.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 31. toč. 4 — 50% nagrade iz Tbr. 31. toč. 1 (1000 bodova) = 500", FiksniBody = 500, SortOrder = 1103, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XII. OSTALI POSTUPCI
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ost_naknada_stete", Kategorija = "Ostali postupci", Opis = "Zahtjev za naknadu štete prema državi / osiguranju (Tbr. 32.1)", TarifniBroj = "Tbr. 32.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 32. toč. 1 — nagrada po Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 1200, Jedinica = "komad" },
        new() { Id = "ost_stegovni", Kategorija = "Ostali postupci", Opis = "Stegovni postupak – podnesci / pravni lijekovi (Tbr. 32.2)", TarifniBroj = "Tbr. 32.2", FiksniBody = 100, SortOrder = 1201, Jedinica = "komad" },

        // Tbr. 32. toč. 3, 4 i 5 sve se izvode iz toč. 2 (100 bodova):
        //
        // Toč. 3: "Za sastavljanje ostalih podnesaka u tijeku postupka iz toč. 2. ovog
        // tarifnog broja odvjetniku pripada 25 % nagrade iz toč. 2."  → 25
        // Toč. 4: "Za zastupanje i sudjelovanje na raspravama u postupku iz toč. 2. ovog
        // tarifnog broja odvjetniku pripada nagrada iz toč. 2."  → 100
        // Toč. 5: "Za zastupanje na raspravi u postupku iz toč. 2. (...) na kojoj se
        // raspravljalo samo o procesnim pitanjima, ili je postupak obustavljen prije
        // raspravljanja o glavnoj stvari, odvjetniku pripada 25% nagrade iz toč. 2."  → 25
        new() { Id = "ost_stegovni_ostali", Kategorija = "Ostali postupci", Opis = "Stegovni postupak – ostali podnesci (Tbr. 32.3) – 25% od 100 = 25", TarifniBroj = "Tbr. 32.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 32. toč. 3 — 25% nagrade iz Tbr. 32. toč. 2 (100 bodova) = 25", FiksniBody = 25, SortOrder = 1202, Jedinica = "komad" },
        new() { Id = "ost_stegovni_rasprava", Kategorija = "Ostali postupci", Opis = "Stegovni postupak – zastupanje na raspravi (Tbr. 32.4) – nagrada iz Tbr. 32.2", TarifniBroj = "Tbr. 32.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 32. toč. 4 — nagrada iz Tbr. 32. toč. 2, 100 bodova", FiksniBody = 100, SortOrder = 1203, Jedinica = "komad" },
        new() { Id = "ost_stegovni_rasprava_procesna", Kategorija = "Ostali postupci", Opis = "Stegovni postupak – rasprava samo o procesnim pitanjima / obustava (Tbr. 32.5) – 25% od 100 = 25", TarifniBroj = "Tbr. 32.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 32. toč. 5 — 25% nagrade iz Tbr. 32. toč. 2 (100 bodova) = 25", FiksniBody = 25, SortOrder = 1204, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XIII. RAZNO – ISPRAVE, SAVJETI, OPOMENE
        // ═══════════════════════════════════════════════════════════
        new() { Id = "raz_ugovor", Kategorija = "Razno", Opis = "Sastavljanje ugovora (Tbr. 33.1) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 33.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 33. toč. 1 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 1300, Jedinica = "komad" },
        new() { Id = "raz_ugovor_neproc", Kategorija = "Razno", Opis = "Ugovor – neprocjenjiv (Tbr. 33.2)", TarifniBroj = "Tbr. 33.2", FiksniBody = 200, SortOrder = 1301, Jedinica = "komad" },
        new() { Id = "raz_izjava", Kategorija = "Razno", Opis = "Jednostavna izjava volje (Tbr. 33.3)", TarifniBroj = "Tbr. 33.3", FiksniBody = 50, SortOrder = 1302, Jedinica = "komad" },
        new() { Id = "raz_oporuka", Kategorija = "Razno", Opis = "Sastavljanje oporuke (Tbr. 33.4)", TarifniBroj = "Tbr. 33.4", FiksniBody = 100, SortOrder = 1303, Jedinica = "komad" },
        new() { Id = "raz_usmeni_savjet", Kategorija = "Razno", Opis = "Usmeni pravni savjet (Tbr. 34.1) – 50 bodova/sat", TarifniBroj = "Tbr. 34.1", FiksniBody = 50, SortOrder = 1310, Jedinica = "sat" },
        // Tbr. 34. toč. 2, odlomak 1: "Za sastavljanje pisanih pravnih savjeta i pisanih
        // pravnih mišljenja s činjeničnim i pravnim obrazloženjima odvjetniku pripada
        // nagrada iz Tbr. 7. toč. 1. i 4." — bez minimuma.
        new() { Id = "raz_pisani_savjet", Kategorija = "Razno", Opis = "Pisani pravni savjet / mišljenje – procjenjiv predmet (Tbr. 34.2)", TarifniBroj = "Tbr. 34.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 34. toč. 2 odl. 1 — nagrada iz Tbr. 7. toč. 1. i 4.", Tbr71Postotak = 1.0m, SortOrder = 1311, Jedinica = "komad" },

        // Tbr. 34. toč. 2, odlomak 2: "(...) u neprocjenjivim predmetima odvjetniku
        // pripada nagrada ne manja od 200 bodova." Minimum vrijedi SAMO ovdje.
        new() { Id = "raz_pisani_savjet_neproc", Kategorija = "Razno", Opis = "Pisani pravni savjet / mišljenje – neprocjenjiv predmet (Tbr. 34.2) – najmanje 200 bodova", TarifniBroj = "Tbr. 34.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 34. toč. 2 odl. 2 — u neprocjenjivim predmetima ne manje od 200 bodova", FiksniBody = 200, MinBodova = 200, SortOrder = 1312, Jedinica = "komad" },
        new() { Id = "raz_konferencija", Kategorija = "Razno", Opis = "Konferencija / sastanak (Tbr. 35) – 50 bodova/sat", TarifniBroj = "Tbr. 35", FiksniBody = 50, SortOrder = 1320, Jedinica = "sat" },
        new() { Id = "raz_razmatranje_spisa", Kategorija = "Razno", Opis = "Razmatranje spisa / uvid u ZK / registre (Tbr. 36) – 25 bodova/sat", TarifniBroj = "Tbr. 36", FiksniBody = 25, SortOrder = 1330, Jedinica = "sat" },
        new() { Id = "raz_izvid_pravomocnost", Kategorija = "Razno", Opis = "Izvid pravomoćnosti / izvršnosti (Tbr. 36)", TarifniBroj = "Tbr. 36", FiksniBody = 25, SortOrder = 1331, Jedinica = "komad" },
        new() { Id = "raz_pribava_izvadaka", Kategorija = "Razno", Opis = "Pribava izvadaka iz ZK / registara (Tbr. 36)", TarifniBroj = "Tbr. 36", FiksniBody = 25, SortOrder = 1332, Jedinica = "komad" },
        new() { Id = "raz_opomena", Kategorija = "Razno", Opis = "Obavijest / opomena s činjeničnim stanjem (Tbr. 37.1) – 25 bod/str.", TarifniBroj = "Tbr. 37.1", FiksniBody = 25, SortOrder = 1340, Jedinica = "stranica" },
        new() { Id = "raz_tipizirana_opomena", Kategorija = "Razno", Opis = "Tipizirana opomena (Tbr. 37.2)", TarifniBroj = "Tbr. 37.2", FiksniBody = 10, SortOrder = 1341, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XIV. ZAJEDNIČKE ODREDBE
        // ═══════════════════════════════════════════════════════════
        new() { Id = "zaj_cekanje", Kategorija = "Zajedničke odredbe", Opis = "Čekanje na raspravu / putovanje (Tbr. 39) – 25 bod/sat, max 7h", TarifniBroj = "Tbr. 39", FiksniBody = 25, SortOrder = 1400, Jedinica = "sat" },
        // Tbr. 40 (zastupanje više osoba) NIJE stavka kataloga nego povišenje cijele
        // osnovice — vidi PovisenjeViseOsobaPostotak i polje broj_zastupanih_osoba.
    ];
}
