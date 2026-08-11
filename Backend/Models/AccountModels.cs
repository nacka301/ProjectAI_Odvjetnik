namespace Backend.Models;

// ══════════════════════════════════════════════════════════════
//  Brisanje računa i izvoz podataka (GDPR čl. 17 i čl. 20)
// ══════════════════════════════════════════════════════════════

/// <summary>
/// Zahtjev za brisanje vlastitog korisničkog računa.
///
/// Potvrda se traži da bi radnja bila namjerna, a ne posljedica krivog klika.
/// Identitet korisnika NIJE dio zahtjeva — dolazi iz provjerenog tokena.
/// </summary>
public sealed class DeleteAccountRequest
{
    /// <summary>Mora biti točno "OBRIŠI RAČUN".</summary>
    public string Confirmation { get; set; } = "";
}

/// <summary>
/// Zahtjev za brisanje cijelog ureda sa svim podacima.
///
/// Namjerno odvojeno od brisanja računa: to su dvije različite namjere, pa
/// zaslužuju dvije različite radnje. Potvrda je NAZIV UREDA, ne fiksna riječ —
/// da bi je korisnik morao pročitati i prepisati, a ne otipkati napamet.
/// </summary>
public sealed class DeleteFirmRequest
{
    /// <summary>Mora se točno podudarati s nazivom ureda.</summary>
    public string FirmNameConfirmation { get; set; } = "";
}

/// <summary>Odgovor na pokušaj brisanja koji sustav odbija, s razlogom u kodu.</summary>
public sealed class DeleteBlockedResponse
{
    public string Error { get; set; } = "";

    /// <summary>
    /// Strojno čitljiv razlog, da sučelje može ponuditi pravi sljedeći korak:
    ///   LAST_MEMBER          — jedini ste član; brisanje računa bi ostavilo ured bez ikoga
    ///   OWNER_MUST_TRANSFER  — vlasnik ste, a u uredu ima još ljudi
    ///   CONFIRMATION_MISMATCH— potvrda se ne podudara
    /// </summary>
    public string Code { get; set; } = "";
}

/// <summary>Sažetak onoga što je brisanje stvarno obavilo.</summary>
public sealed class DeleteAccountResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    /// <summary>Broj zapisa kojima je uklonjeno autorstvo, po tablicama.</summary>
    public int RecordsAnonymised { get; set; }
}
