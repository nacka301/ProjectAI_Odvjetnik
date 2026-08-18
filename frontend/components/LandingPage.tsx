import Link from "next/link";
import {
  Calculator,
  Landmark,
  Languages,
  Lock,
  Scale,
  Search,
  ShieldAlert,
  Users,
} from "lucide-react";

/**
 * The public front door — what an unsigned-in visitor sees at `/`.
 *
 * Until now `/` was the dashboard and the middleware bounced anyone without a
 * session straight to the login form. That left the product with no page that
 * says what it is: a lawyer sent a link got a password prompt, and Google's
 * OAuth review refused branding verification twice for exactly this ("your home
 * page does not explain the purpose of your app", "the app name does not match
 * the app name on your home page").
 *
 * Deliberately a server component with no data fetching and no client hooks —
 * it must render for someone with no session, no cookies and no JavaScript.
 *
 * The wording here is factual: what the software computes, and under which
 * rules. No claims about accuracy, savings or outcomes — this is sold to
 * lawyers, and a promise on this page is a promise in a professional context.
 * David should own these words; they are a starting point, not a campaign.
 */

const MODULI = [
  {
    icon: Calculator,
    naziv: "Odvjetnički troškovnik",
    opis: "Nagrada i troškovi prema Tarifi o nagradama i naknadi troškova za rad odvjetnika, uz sudske pristojbe po Uredbi.",
  },
  {
    icon: Users,
    naziv: "Stranke i predmeti",
    opis: "Stranke, predmeti i izdani troškovnici na jednom mjestu, odvojeno po uredu.",
  },
  {
    icon: ShieldAlert,
    naziv: "Analiza ugovora",
    opis: "Pregled nacrta ugovora s popisom rizičnih i nedostajućih odredbi.",
  },
  {
    icon: Landmark,
    naziv: "ZK izvadak",
    opis: "Čitanje zemljišnoknjižnog izvatka — vlasnici, teretovi, plombe.",
  },
  {
    icon: Search,
    naziv: "Tržišna procjena",
    opis: "Procjena vrijednosti nekretnine iz usporedivih podataka i obilježja lokacije.",
  },
  {
    icon: Languages,
    naziv: "Sudski tumač",
    opis: "Prijevod i preslovljavanje pravnih tekstova uz zadržavanje terminologije.",
  },
];

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-slate-50">
      {/* ── Zaglavlje ── */}
      <header className="border-b border-slate-200 bg-white">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div
              className="w-9 h-9 rounded-lg flex items-center justify-center"
              style={{ background: "var(--auth-navy)" }}
            >
              <Scale className="h-4 w-4" style={{ color: "var(--auth-gold)" }} />
            </div>
            <div>
              {/* Ime mora doslovno odgovarati onome u Google OAuth konzoli. */}
              <span className="font-[family-name:var(--font-playfair)] text-lg font-bold text-slate-900">
                PravoTek
              </span>
              <span
                className="block text-[10px] uppercase tracking-[0.2em]"
                style={{ color: "var(--auth-gold)" }}
              >
                Pravna inteligencija
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Link
              href="/auth/login"
              className="text-sm font-medium text-slate-600 hover:text-slate-900 transition px-4 py-2"
            >
              Prijava
            </Link>
            <Link
              href="/auth/register"
              className="text-sm font-semibold text-white px-4 py-2 rounded-lg transition"
              style={{ background: "var(--auth-navy)" }}
            >
              Otvorite račun
            </Link>
          </div>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-4 sm:px-6">
        {/* ── Što je ovo ── */}
        <section className="py-16 sm:py-24 max-w-3xl">
          <h1 className="font-[family-name:var(--font-playfair)] text-3xl sm:text-5xl font-bold text-slate-900 tracking-tight leading-tight">
            PravoTek je softver za izradu odvjetničkih troškovnika.
          </h1>
          <p className="mt-6 text-lg text-slate-600 leading-relaxed">
            Namijenjen je odvjetnicima u Republici Hrvatskoj. Unesete vrijednost
            predmeta spora i obavljene radnje — bodovi, nagrada i sudske
            pristojbe izračunaju se prema{" "}
            <span className="font-medium text-slate-800">
              Tarifi o nagradama i naknadi troškova za rad odvjetnika
            </span>{" "}
            i{" "}
            <span className="font-medium text-slate-800">
              Uredbi o sudskim pristojbama
            </span>
            . Troškovnik se sprema uz stranku i predmet te izvozi u PDF.
          </p>

          <div className="mt-8 flex flex-wrap items-center gap-3">
            <Link
              href="/auth/register"
              className="inline-flex items-center gap-2 text-white text-sm font-semibold px-6 py-3 rounded-xl shadow-lg transition"
              style={{
                background: "var(--auth-navy)",
                boxShadow: "0 4px 14px rgba(27,42,74,0.3)",
              }}
            >
              Otvorite račun
            </Link>
            <Link
              href="/auth/login"
              className="text-sm font-medium text-slate-600 hover:text-slate-900 transition px-4 py-3"
            >
              Već imate račun? Prijavite se
            </Link>
          </div>
        </section>

        {/* ── Moduli ── */}
        <section className="pb-16 sm:pb-24">
          <h2 className="font-[family-name:var(--font-playfair)] text-xl font-semibold text-slate-900 mb-6">
            Što aplikacija radi
          </h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {MODULI.map(({ icon: Ikona, naziv, opis }) => (
              <div
                key={naziv}
                className="bg-white rounded-lg border border-slate-200 p-5 shadow-sm"
              >
                <div className="w-9 h-9 rounded-lg bg-slate-100 flex items-center justify-center mb-3">
                  <Ikona className="h-4 w-4" style={{ color: "var(--auth-navy)" }} />
                </div>
                <h3 className="font-semibold text-slate-900 text-sm mb-1.5">{naziv}</h3>
                <p className="text-sm text-slate-500 leading-relaxed">{opis}</p>
              </div>
            ))}
          </div>
        </section>

        {/* ── Podaci ── */}
        <section className="pb-16 sm:pb-24">
          <div className="bg-white rounded-xl border border-slate-200 p-6 sm:p-8 max-w-3xl">
            <div className="flex items-start gap-3">
              <Lock className="h-5 w-5 shrink-0 mt-0.5" style={{ color: "var(--auth-gold)" }} />
              <div>
                <h2 className="font-semibold text-slate-900 mb-2">Podaci Vašeg ureda</h2>
                <p className="text-sm text-slate-600 leading-relaxed">
                  Podaci su odvojeni po odvjetničkom uredu na razini baze, a ne
                  samo u sučelju — svaki upit provjerava kojem uredu zapis
                  pripada. Prijenos je kriptiran (TLS 1.3), a pristup se može
                  dodatno zaštititi dvofaktorskom prijavom. Obrada je sukladna
                  Općoj uredbi o zaštiti podataka i obvezi čuvanja odvjetničke
                  tajne.
                </p>
                <p className="mt-3 text-sm">
                  <Link href="/privatnost" className="text-slate-700 underline underline-offset-2 hover:opacity-70">
                    Politika privatnosti
                  </Link>
                  <span className="text-slate-300 mx-2">·</span>
                  <Link href="/uvjeti" className="text-slate-700 underline underline-offset-2 hover:opacity-70">
                    Uvjeti korištenja
                  </Link>
                  <span className="text-slate-300 mx-2">·</span>
                  <Link href="/dpa" className="text-slate-700 underline underline-offset-2 hover:opacity-70">
                    Ugovor o obradi podataka
                  </Link>
                </p>
              </div>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-slate-200 bg-white">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 py-6 flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-slate-400">
            © {new Date().getFullYear()} PravoTek · Pravna inteligencija za odvjetnike
          </p>
          <p className="text-xs text-slate-400">
            Izračun je pomoćno sredstvo i ne zamjenjuje prosudbu odvjetnika.
          </p>
        </div>
      </footer>
    </div>
  );
}
