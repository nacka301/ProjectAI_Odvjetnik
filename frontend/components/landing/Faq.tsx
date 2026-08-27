import SectionGrid, { SectionNumber } from "./SectionGrid";

const PITANJA = [
  {
    q: "Zamjenjuje li ovo prosudbu odvjetnika?",
    a: "Ne. Izračun je pomoćno sredstvo — koje su radnje obavljene i kako se predmet vodi ostaje na odvjetniku. Zato uz svaku stavku stoji tarifni broj, da se izračun može provjeriti.",
  },
  {
    q: "Što se događa kad se Tarifa ili Uredba promijene?",
    a: "Iznosi i bodovi drže se u aplikaciji odvojeno od izračuna, pa izmjena propisa ne traži novu verziju programa. Postojeći troškovnici ostaju izračunati po propisu koji je vrijedio na dan izrade.",
  },
  {
    q: "Isplati li se ako troškovnike znam napamet?",
    a: "Troškovnik je samo ulaz. PravoTek uz njega vodi stranke i predmete s kronologijom, čita ZK izvadak, prolazi kroz nacrt ugovora i traži rizične odredbe, daje raspon tržišne vrijednosti nekretnine i prevodi pravne tekstove — sve uz isti predmet, u istom računu i pod istom pretplatom. Ured koji troškovnik radi u sekundi ostatak vremena ipak traži podatke po mapama.",
  },
  {
    q: "Mogu li isprobati prije plaćanja?",
    a: "Da — sedam dana punog pristupa odmah nakon registracije, bez unosa kartice. Nakon probe pretplata je 19 € mjesečno po računu; odvjetnička društva s više korisnika ugovaraju se posebno.",
  },
  {
    q: "Radi li na mobitelu?",
    a: "Da, sučelje je prilagođeno manjim zaslonima. Troškovnik izrađen na računalu otvara se na mobitelu i obrnuto.",
  },
];

export default function Faq() {
  return (
    <SectionGrid left={<SectionNumber n="06" />}>
      <h2 className="text-[54px] leading-[1] tracking-[-0.03em] m-0">Česta pitanja</h2>
      <div className="mt-10 border-t-2 border-[var(--ptk-divider)] max-w-[90ch]">
        {PITANJA.map((p, i) => (
          <details key={p.q} open={i === 0} className="border-b border-[var(--ptk-divider)] py-5">
            <summary className="flex items-center gap-5">
              <span className="flex-1 font-[family-name:var(--font-archivo)] font-extrabold text-[19px]">{p.q}</span>
              <span
                className="ptk-plus font-[family-name:var(--font-archivo)] font-extrabold text-[20px] transition-transform duration-[250ms] ease-in-out"
                style={{ color: "var(--ptk-accent)" }}
                aria-hidden
              >
                +
              </span>
            </summary>
            <p className="mt-3 max-w-[78ch] text-[15px] leading-[1.6] opacity-80">{p.a}</p>
          </details>
        ))}
      </div>
    </SectionGrid>
  );
}
