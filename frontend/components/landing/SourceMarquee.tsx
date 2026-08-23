const IZVORI = [
  "Tarifa o nagradama i naknadi troškova za rad odvjetnika",
  "Uredba o Tarifi sudskih pristojbi",
  "Zakon o sudskim pristojbama",
  "Zakon o odvjetništvu",
  "Uredba (EU) 2016/679 — GDPR",
];

function Skupina({ groupIndex }: { groupIndex: number }) {
  return (
    <>
      {IZVORI.map((naziv, i) => (
        <span key={`${groupIndex}-${i}`} className="flex items-center gap-12">
          <span className="text-[12px] uppercase tracking-[0.1em]">{naziv}</span>
          <span className="text-[12px] uppercase tracking-[0.1em]" style={{ color: "var(--ptk-accent)" }}>
            /
          </span>
        </span>
      ))}
    </>
  );
}

export default function SourceMarquee() {
  return (
    <div
      className="overflow-hidden border-b-2 border-[var(--ptk-divider)] py-3"
      style={{ background: "var(--ptk-surface)" }}
    >
      {/* Popis je udvostručen u DOM-u da petlja bude neprekidna (translateX 0 → -50%). */}
      <div className="ptk-marquee-track flex w-max gap-12 whitespace-nowrap">
        <Skupina groupIndex={0} />
        <Skupina groupIndex={1} />
      </div>
    </div>
  );
}
