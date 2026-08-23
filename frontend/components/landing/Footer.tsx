import Link from "next/link";

export default function Footer() {
  return (
    <div
      className="flex flex-wrap items-start gap-16 px-6 py-10 min-[1100px]:px-16"
      style={{ background: "var(--ptk-text)", color: "var(--ptk-bg)" }}
    >
      <div className="mr-auto">
        <div className="font-[family-name:var(--font-archivo)] font-extrabold text-[18px]">PRAVOTEK</div>
        <div className="text-[12px] opacity-60 mt-1">Pravna inteligencija · Republika Hrvatska</div>
      </div>

      <div className="flex flex-col gap-2 text-[13px]">
        <Link href="/privatnost" style={{ color: "var(--ptk-accent-400)" }}>
          Politika privatnosti
        </Link>
        <Link href="/uvjeti" style={{ color: "var(--ptk-accent-400)" }}>
          Uvjeti korištenja
        </Link>
        <Link href="/dpa" style={{ color: "var(--ptk-accent-400)" }}>
          Ugovor o obradi podataka
        </Link>
      </div>

      <p className="max-w-[42ch] text-[12px] opacity-60 m-0">
        Softver ne pruža pravni savjet. Izračun po Tarifi i Uredbi pomoćno je sredstvo;
        odgovornost za sadržaj troškovnika ostaje na odvjetniku.
      </p>
    </div>
  );
}
