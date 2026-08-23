"use client";

import { useState } from "react";
import { WAITLIST_EMAIL_ID } from "./CtaLink";
import { isWaitlistEmailValid } from "@/lib/waitlist";

type Stanje = "" | "slanje" | "ok" | "greska" | "mreza";

const PORUKE: Record<Stanje, string> = {
  "": "Samo e-mail. Bez newslettera i bez obveze.",
  slanje: "Šaljemo…",
  ok: "Zapisano. Javljamo se na tu adresu kad aplikacija izađe.",
  greska: "Provjerite adresu e-pošte.",
  mreza: "Prijava nije prošla. Pokušajte ponovno ili pišite na hej@pravotek.hr.",
};

const GRESKA_BOJA = new Set<Stanje>(["greska", "mreza"]);

export default function WaitlistForm() {
  const [mail, setMail] = useState("");
  const [web, setWeb] = useState(""); // honeypot — mora ostati prazan
  const [stanje, setStanje] = useState<Stanje>("");

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const vrijednost = mail.trim();

    if (!isWaitlistEmailValid(vrijednost)) {
      setStanje("greska");
      return;
    }

    setStanje("slanje");
    try {
      const res = await fetch("/api/waitlist", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email: vrijednost,
          izvor: "landing",
          vrijeme: new Date().toISOString(),
          web, // honeypot — prazno za ljude
        }),
      });

      if (res.ok || res.status === 409) {
        setStanje("ok");
      } else if (res.status === 400) {
        setStanje("greska");
      } else {
        setStanje("mreza");
      }
    } catch {
      setStanje("mreza");
    }
  }

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-[10px]">
      <label
        htmlFor={WAITLIST_EMAIL_ID}
        className="font-[family-name:var(--font-archivo)] font-extrabold text-[12px] uppercase tracking-[0.14em]"
      >
        Javite mi kad bude gotovo
      </label>
      <input
        id={WAITLIST_EMAIL_ID}
        className="ptk-input"
        style={{ padding: "15px 16px", fontSize: "15px" }}
        type="email"
        name="email"
        autoComplete="email"
        inputMode="email"
        placeholder="ime@odvjetnik.hr"
        value={mail}
        onChange={(e) => {
          setMail(e.target.value);
          if (stanje === "greska" || stanje === "mreza") setStanje("");
        }}
      />

      {/* Honeypot — skriveno za ljude, vidljivo botovima koji popunjavaju sva polja. */}
      <label className="absolute -left-[9999px] w-px h-px overflow-hidden" aria-hidden="true">
        Ostavite prazno
        <input
          tabIndex={-1}
          autoComplete="off"
          name="web"
          value={web}
          onChange={(e) => setWeb(e.target.value)}
        />
      </label>

      <button
        type="submit"
        disabled={stanje === "slanje"}
        className="ptk-btn ptk-btn-primary ptk-btn-block"
        style={{ padding: "16px 20px", fontSize: "15px", marginTop: 0 }}
      >
        Prijavite se na listu čekanja →
      </button>
      <span
        role="status"
        className="text-[12px] opacity-70"
        style={GRESKA_BOJA.has(stanje) ? { color: "var(--ptk-accent-700)" } : undefined}
      >
        {PORUKE[stanje]}
      </span>
    </form>
  );
}
