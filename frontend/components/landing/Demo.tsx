import SectionGrid from "./SectionGrid";
import TroskovnikDemo from "./TroskovnikDemo";

export default function Demo() {
  return (
    <SectionGrid padding="40px 64px 56px 40px" left={null}>
      <div className="flex flex-wrap items-baseline justify-between gap-6 mb-5">
        <p className="text-[12px] uppercase font-extrabold tracking-[0.16em] m-0">
          Prikaz sučelja — troškovnik u izradi
        </p>
        <span className="text-[11px] opacity-55">Ilustrativni podaci</span>
      </div>
      <TroskovnikDemo />
    </SectionGrid>
  );
}
