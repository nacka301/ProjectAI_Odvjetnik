import SectionGrid from "./SectionGrid";
import WaitlistForm from "./WaitlistForm";

const POVJERENJE = [
  { label: "TLS 1.3", opis: "Šifriran prijenos" },
  { label: "2FA", opis: "Dvofaktorska prijava" },
  { label: "GDPR", opis: "Uredba (EU) 2016/679" },
  { label: "Po uredu", opis: "Odvojeno na razini baze" },
];

export default function Hero() {
  return (
    <SectionGrid
      padding="40px 64px 0 40px"
      left={
        <div className="pt-10 flex justify-center">
          <span
            className="font-[family-name:var(--font-archivo)] font-extrabold text-[12px] tracking-[0.18em] [writing-mode:vertical-rl]"
            style={{ color: "var(--ptk-accent)" }}
            aria-hidden
          >
            HR · 2026
          </span>
        </div>
      }
    >
      <p className="text-[12px] uppercase font-extrabold tracking-[0.16em] m-0" style={{ color: "var(--ptk-accent)" }}>
        Softver za odvjetnike u Republici Hrvatskoj
      </p>
      <h1
        className="mt-6 max-w-[15ch] text-[64px] min-[1100px]:text-[104px] leading-[0.94] tracking-[-0.035em]"
      >
        Cijeli ured u jednom alatu. Bez ručnog zbrajanja bodova.
      </h1>

      <div className="mt-10 grid grid-cols-1 min-[720px]:grid-cols-[minmax(0,1fr)_320px] gap-12 items-end">
        <p className="max-w-[62ch] text-[19px] leading-[1.5] m-0">
          PravoTek je softver za odvjetnike u Republici Hrvatskoj: troškovnik po{" "}
          <strong>Tarifi</strong> i <strong>Uredbi o Tarifi sudskih pristojbi</strong>, evidencija
          stranaka i predmeta s kronologijom, a uz njih analiza ugovora, čitanje ZK izvatka,
          tržišna procjena i sudski tumač. Svaki dokument se sprema uz predmet i izvozi u PDF.
        </p>
        <WaitlistForm />
      </div>

      <div className="mt-12 grid grid-cols-2 min-[720px]:grid-cols-4 border-t-2 border-[var(--ptk-divider)]">
        {POVJERENJE.map((p, i) => (
          <div key={p.label} className={i > 0 ? "border-l border-[var(--ptk-divider)]" : ""} style={{ padding: "16px 20px 20px" }}>
            <div className="font-[family-name:var(--font-archivo)] font-extrabold text-[15px]">{p.label}</div>
            <div className="text-[12px] opacity-60">{p.opis}</div>
          </div>
        ))}
      </div>
    </SectionGrid>
  );
}
