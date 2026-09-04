"use client";

import { createContext, useCallback, useContext, useMemo, useState } from "react";

/**
 * Način rada „prikaz primjera".
 *
 * Novom korisniku vodič dobrodošlice pokazuje GOTOV troškovnik i POPUNJEN popis
 * stranaka. Ti podaci ne smiju nikad doći do baze: odvjetnik kojemu se
 * izmišljena stranka pojavi u stvarnom popisu klijenata — ili, gore, na
 * troškovniku koji šalje sudu — više se ne vraća.
 *
 * Zato nema seedanja, nema `is_demo` stupca i nema naknadnog čišćenja. Postoji
 * samo ova zastavica: dok je upaljena, ekrani čitaju iz `lib/demo/podaci.ts`
 * umjesto s backenda, a svako pisanje je blokirano. Izlazak iz demo načina je
 * promjena jedne varijable u stanju, ne DELETE upit.
 *
 * ── Zašto stanje živi SAMO u memoriji ──
 *
 * Ni `localStorage`, ni `sessionStorage`, ni parametar u adresi. To nije
 * propust nego dvije ograde odjednom:
 *
 *   1. Demo se ne može uključiti slučajno — nema URL-a koji bi ga upalio, pa ga
 *      korisnik ne može ni pogoditi ni dobiti u linku od nekog drugog.
 *   2. Demo ne preživljava osvježavanje stranice. Tko god se izgubi, F5 ga vraća
 *      na njegove stvarne, prazne podatke.
 *
 * Prijelaz s troškovnika na stranke ide `router.push`-om, pa provider ostaje
 * montiran i zastavica preživi navigaciju unutar vodiča — jedino što joj treba.
 */

interface DemoStanje {
  /** True dok traje prikaz primjera. */
  demo: boolean;
  udjiUDemo: () => void;
  izadjiIzDemo: () => void;
}

const DemoCtx = createContext<DemoStanje>({
  demo: false,
  udjiUDemo: () => {},
  izadjiIzDemo: () => {},
});

export function DemoProvider({ children }: { children: React.ReactNode }) {
  const [demo, setDemo] = useState(false);

  const udjiUDemo = useCallback(() => setDemo(true), []);
  const izadjiIzDemo = useCallback(() => setDemo(false), []);

  const value = useMemo(
    () => ({ demo, udjiUDemo, izadjiIzDemo }),
    [demo, udjiUDemo, izadjiIzDemo],
  );

  return <DemoCtx.Provider value={value}>{children}</DemoCtx.Provider>;
}

export function useDemo(): DemoStanje {
  return useContext(DemoCtx);
}

/** Poruka koja stoji svugdje gdje je radnja onemogućena zbog demo načina. */
export const DEMO_ZASTO_NEAKTIVNO =
  "U prikazu primjera ovo nije dostupno — podaci nisu vaši i ne spremaju se.";
