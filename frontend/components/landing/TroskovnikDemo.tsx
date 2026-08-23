"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";

/**
 * Illustrative loop: 10 steps, 620ms apart, replaying forever. See README
 * "Interactions & Behavior — Animacija demo troškovnika".
 *
 * Respects prefers-reduced-motion via useSyncExternalStore (the React-blessed
 * way to read external, non-React state like matchMedia — unlike reading it
 * in a useEffect and calling setState, this needs no server/client
 * reconciliation dance and never causes a cascading extra render). When
 * reduced, `displayKorak`/`displayUkupno` are derived straight from the flag
 * at render time — the interval never starts, so nothing animates.
 */

const STATUSI = [
  "Predmet otvoren — čeka unos radnji",
  "Radnja dodana — Tbr. 7/1",
  "Radnja dodana — Tbr. 9/1",
  "Radnja dodana — Tbr. 8/1",
  "Radnja dodana — Tbr. 10/1",
  "Zbrajanje bodova po Tarifi",
  "Sudska pristojba po Uredbi",
  "Obračun PDV-a",
  "Troškovnik spremljen uz predmet P-412/2026",
];
const CILJ = 3012.5;
const TEMPO = 620;

const RETCI = [
  { tbr: "Tbr. 7/1", radnja: "Sastav tužbe", datum: "12.03.2026.", bodovi: 250, iznos: "500,00 €" },
  { tbr: "Tbr. 9/1", radnja: "Zastupanje na ročištu", datum: "28.04.2026.", bodovi: 250, iznos: "500,00 €" },
  { tbr: "Tbr. 8/1", radnja: "Obrazloženi podnesak", datum: "15.05.2026.", bodovi: 250, iznos: "500,00 €" },
  { tbr: "Tbr. 10/1", radnja: "Sastav žalbe", datum: "03.06.2026.", bodovi: 375, iznos: "750,00 €" },
];

const ZBIROVI = [
  { naziv: "Nagrada", iznos: "2.250,00 €", prag: 5 },
  { naziv: "Sudska pristojba", iznos: "200,00 €", prag: 6 },
  { naziv: "PDV 25 %", iznos: "562,50 €", prag: 6 },
];

const novac = (n: number) =>
  n.toLocaleString("hr-HR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";

const MOTION_QUERY = "(prefers-reduced-motion: reduce)";
function subscribeMotion(onChange: () => void) {
  const mq = window.matchMedia(MOTION_QUERY);
  mq.addEventListener("change", onChange);
  return () => mq.removeEventListener("change", onChange);
}
const getMotionSnapshot = () => window.matchMedia(MOTION_QUERY).matches;
const getMotionServerSnapshot = () => false;

export default function TroskovnikDemo() {
  const reduced = useSyncExternalStore(subscribeMotion, getMotionSnapshot, getMotionServerSnapshot);
  const [korak, setKorak] = useState(0);
  const [ukupno, setUkupno] = useState(0);
  const korakRef = useRef(0);
  const ukupnoRef = useRef(0);

  useEffect(() => {
    if (reduced) return; // konačno stanje se izvodi izravno ispod, bez intervala

    const korakId = setInterval(() => {
      korakRef.current = korakRef.current >= 9 ? 0 : korakRef.current + 1;
      if (korakRef.current === 0) {
        ukupnoRef.current = 0;
        setUkupno(0);
      }
      setKorak(korakRef.current);
    }, TEMPO);

    const brojacId = setInterval(() => {
      if (korakRef.current < 7 || ukupnoRef.current >= CILJ) return;
      ukupnoRef.current = Math.min(CILJ, ukupnoRef.current + CILJ / 22);
      setUkupno(ukupnoRef.current);
    }, 28);

    return () => {
      clearInterval(korakId);
      clearInterval(brojacId);
    };
  }, [reduced]);

  const displayKorak = reduced ? 9 : korak;
  const displayUkupno = reduced ? CILJ : ukupno;
  const status = STATUSI[Math.min(displayKorak, STATUSI.length - 1)];

  return (
    <div style={{ border: "2px solid var(--ptk-text)", background: "#ffffff" }}>
      {/* Zaglavlje predmeta */}
      <div
        className="flex flex-wrap items-center gap-6 px-5 py-3.5"
        style={{ background: "var(--ptk-text)", color: "var(--ptk-bg)" }}
      >
        <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[13px] tracking-[0.06em]">
          P-412/2026 · TUŽBA ZBOG SMETANJA POSJEDA
        </span>
        <span className="ml-auto text-[12px] opacity-70">Vrijednost predmeta spora</span>
        <span
          className="font-[family-name:var(--font-archivo)] font-extrabold text-[14px]"
          style={{ color: "var(--ptk-accent-400)" }}
        >
          48.000,00 €
        </span>
      </div>

      {/* Tablica radnji — vlastiti scroll na uskim zaslonima, tablica se ne skuplja ispod prirodne širine stupaca. */}
      <div className="overflow-x-auto">
      <table className="ptk-table text-[14px] min-w-[480px]">
        <thead>
          <tr>
            <th style={{ width: "110px" }}>Tbr.</th>
            <th>Radnja</th>
            <th style={{ width: "130px" }}>Datum</th>
            <th style={{ width: "100px", textAlign: "right" }}>Bodovi</th>
            <th style={{ width: "140px", textAlign: "right" }}>Iznos</th>
          </tr>
        </thead>
        <tbody>
          {RETCI.map((r, i) => {
            const vidljiv = displayKorak > i;
            return (
              <tr
                key={r.tbr}
                style={{
                  opacity: vidljiv ? 1 : 0,
                  transform: vidljiv ? "translateY(0)" : "translateY(10px)",
                  transition: "opacity .35s ease, transform .35s ease",
                }}
              >
                <td className="font-[family-name:var(--font-archivo)] font-extrabold" style={{ color: "var(--ptk-accent-700)" }}>
                  {r.tbr}
                </td>
                <td>{r.radnja}</td>
                <td className="opacity-60">{r.datum}</td>
                <td style={{ textAlign: "right" }}>{r.bodovi}</td>
                <td style={{ textAlign: "right", fontVariantNumeric: "tabular-nums" }}>{r.iznos}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      </div>

      {/* Zbrojevi */}
      <div
        className="grid border-t-2 border-[var(--ptk-divider)]"
        style={{ gridTemplateColumns: "repeat(3, minmax(0, 1fr)) minmax(0, 1.2fr)" }}
      >
        {ZBIROVI.map((z, i) => (
          <div
            key={z.naziv}
            className={i > 0 ? "border-l border-[var(--ptk-divider)]" : ""}
            style={{ padding: "14px 20px", opacity: displayKorak >= z.prag ? 1 : 0.15, transition: "opacity .4s ease" }}
          >
            <div className="text-[11px] uppercase tracking-[0.08em] opacity-60">{z.naziv}</div>
            <div className="font-[family-name:var(--font-archivo)] font-extrabold text-[18px]">{z.iznos}</div>
          </div>
        ))}
        <div style={{ padding: "14px 20px", background: "var(--ptk-accent)", color: "var(--ptk-bg)" }}>
          <div className="text-[11px] uppercase tracking-[0.08em] opacity-85">Ukupno za naplatu</div>
          <div
            className="font-[family-name:var(--font-archivo)] font-extrabold text-[24px]"
            style={{ fontVariantNumeric: "tabular-nums" }}
          >
            {novac(displayUkupno)}
          </div>
        </div>
      </div>

      {/* Statusni redak */}
      <div className="flex items-center gap-4 px-5 py-3 border-t-2 border-[var(--ptk-divider)]">
        <span className="text-[12px] opacity-60">{status}</span>
        <span className="ptk-caret w-2 h-3.5" style={{ background: "var(--ptk-accent)" }} aria-hidden />
        <span className="ptk-btn ptk-btn-secondary ml-auto text-[12px]" style={{ padding: "6px 12px" }}>
          Izvoz u PDF
        </span>
      </div>
    </div>
  );
}
