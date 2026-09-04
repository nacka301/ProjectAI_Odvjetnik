"use client";

import { createContext, useCallback, useContext, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import Vodic from "@/components/Vodic";
import { useDemo } from "@/contexts/DemoContext";
import { markOnboarded } from "@/lib/onboarding";
import {
  DOBRODOSLICA_KORACI,
  STRANKE_KORACI,
  TROSKOVNIK_KORACI,
  type VodicKorak,
} from "@/lib/vodic-koraci";

/**
 * Koji je vodič pokrenut — i vodič sam.
 *
 * Vodič je prije živio u svakoj stranici zasebno (`/troskovnik` je držao svoj,
 * `/klijenti` svoj) i prelazak s jednog na drugi bio je „predaja štafete": prva
 * dionica bi navigirala s `?vodic=1` u adresi, a druga bi se otvorila jer je taj
 * parametar vidjela. To je radilo, ali je značilo da vodič ne može imati korak
 * na drugoj stranici — samo cijelu novu dionicu.
 *
 * Sada vodič stoji IZNAD ruta, u ovom provideru. Korak s `ruta` je zato običan
 * korak: motor navigira, pričeka da se cilj pojavi i nastavi. Vodič
 * dobrodošlice tako ima jedan tijek od osam koraka kroz dva ekrana, a ne dva
 * vodiča koja se dogovaraju preko adresne trake.
 *
 * Stranice od ovoga trebaju samo `target` — da znaju koje polje prstenovati.
 */

export type VodicId = "dobrodoslica" | "troskovnik" | "stranke";

const KORACI: Record<VodicId, VodicKorak[]> = {
  dobrodoslica: DOBRODOSLICA_KORACI,
  troskovnik: TROSKOVNIK_KORACI,
  stranke: STRANKE_KORACI,
};

export const VODIC_NAZIVI: Record<VodicId, string> = {
  dobrodoslica: "Uvod na primjeru",
  troskovnik: "Troškovnik",
  stranke: "Stranke",
};

interface VodicStanje {
  /** Sidro koje trenutni korak ističe; null kad vodič ne radi. */
  target: string | null;
  aktivan: VodicId | null;
  pokreni: (id: VodicId) => void;
  zatvori: () => void;
}

const VodicCtx = createContext<VodicStanje>({
  target: null,
  aktivan: null,
  pokreni: () => {},
  zatvori: () => {},
});

export function VodicProvider({ children }: { children: React.ReactNode }) {
  const [aktivan, setAktivan] = useState<VodicId | null>(null);
  const [target, setTarget] = useState<string | null>(null);
  const router = useRouter();
  const { demo, udjiUDemo, izadjiIzDemo } = useDemo();

  const pokreni = useCallback(
    (id: VodicId) => {
      // Uvod se vozi na podacima za primjer i uvijek kreće s troškovnika, ma
      // odakle bio pokrenut. Ostala dva rade ondje gdje korisnik već jest.
      if (id === "dobrodoslica") {
        udjiUDemo();
        router.push("/troskovnik");
      }
      setAktivan(id);
    },
    [router, udjiUDemo],
  );

  const zatvori = useCallback(() => setAktivan(null), []);

  const onClose = useCallback(
    (zavrseno: boolean) => {
      setAktivan(null);
      setTarget(null);
      // Završetak i preskakanje znače isto: ne nudi mi ovo više. Razdvajanje u
      // dva stanja stvorilo bi treći ekran koji nitko nije tražio.
      void markOnboarded();

      if (demo) {
        izadjiIzDemo();
        // „Zatvaram primjere i otvaram vam prazan troškovnik." Vrijedi samo kad
        // je korisnik došao do kraja — tko je preskočio, ostaje gdje jest.
        if (zavrseno) router.push("/troskovnik");
      }
    },
    [demo, izadjiIzDemo, router],
  );

  // Korisnik smije izaći iz prikaza primjera i trakom na vrhu ekrana. Vodič koji
  // bi nakon toga nastavio objašnjavati troškovnik koji je upravo nestao bio bi
  // gori od nikakvog, pa se gasi s njim.
  //
  // Izvedeno, a ne efekt koji gasi vodič: uvod POSTOJI samo dok postoji prikaz
  // primjera, pa je to jedan uvjet, a ne dva stanja koja se moraju stići
  // uskladiti. Efekt bi tu istu istinu izrazio jednim renderom zakašnjelo.
  const stvarnoAktivan = aktivan === "dobrodoslica" && !demo ? null : aktivan;

  const value = useMemo(
    () => ({ target, aktivan: stvarnoAktivan, pokreni, zatvori }),
    [target, stvarnoAktivan, pokreni, zatvori],
  );

  return (
    <VodicCtx.Provider value={value}>
      {children}
      <Vodic
        open={stvarnoAktivan !== null}
        koraci={stvarnoAktivan ? KORACI[stvarnoAktivan] : []}
        onClose={onClose}
        onStepChange={setTarget}
      />
    </VodicCtx.Provider>
  );
}

export function useVodic(): VodicStanje {
  return useContext(VodicCtx);
}
