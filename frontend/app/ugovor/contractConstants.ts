// ═══════════════════════════════════════════
//  CONTRACT ANALYSIS — Stage definitions
//  V57: SSE streaming stages matching ProcessingView design
// ═══════════════════════════════════════════

import { FileText, Search, Brain, Calculator, Save, BookOpen, ShieldCheck } from "lucide-react";
import { createElement } from "react";
import type { ProcessingStage } from "@/components/SharedProcessingView";

/** @deprecated Use ProcessingStage from SharedProcessingView instead */
export type ContractStage = ProcessingStage;

export const INITIAL_CONTRACT_STAGES: ContractStage[] = [
  { id: "reading", label: "Čitam ugovor...", status: "pending" },
  { id: "rag", label: "Dohvat relevantnih zakona", status: "pending" },
  { id: "audit", label: "Pass 1: Pravni audit", status: "pending" },
  { id: "scoring", label: "Pass 2: Bodovanje rizika", status: "pending" },
  { id: "critic", label: "Agent 2: Kritička revizija", status: "pending" },
  { id: "saving", label: "Spremam rezultate", status: "pending" },
];

export const CONTRACT_STAGE_ICONS: Record<string, React.ReactNode> = {
  reading: createElement(FileText, { className: "h-4 w-4" }),
  rag: createElement(BookOpen, { className: "h-4 w-4" }),
  audit: createElement(Search, { className: "h-4 w-4" }),
  scoring: createElement(Calculator, { className: "h-4 w-4" }),
  critic: createElement(ShieldCheck, { className: "h-4 w-4" }),
  saving: createElement(Save, { className: "h-4 w-4" }),
};

export const CONTRACT_STAGE_MICROCOPY: Record<string, string[]> = {
  reading: [
    "Učitavam tekst ugovora...",
    "Identificiram vrstu dokumenta...",
    "Pripremam za analizu...",
  ],
  rag: [
    "Pretraživanje baze zakona...",
    "Dohvaćam relevantne propise...",
    "Semantičko pretraživanje...",
  ],
  audit: [
    "Provjeravam terete i hipoteke...",
    "Analiziram kaparu i plaćanje...",
    "Kontroliram clausulu intabulandi...",
    "Provjeravam zaštitne klauzule...",
    "Cross-article analiza u tijeku...",
    "Uspoređujem s obveznim elementima...",
    "Tražim skrivene zamke...",
    "Analiziram logičke nekonzistencije...",
    "Uspoređujem s idealnim ugovorom...",
    "Provjeravam prešutne rizike...",
    "Detektiram štetne kombinacije članaka...",
  ],
  scoring: [
    "Izračunavam ocjenu rizika...",
    "Bodovanje prema formuli...",
  ],
  critic: [
    "Agent 2 provjerava analizu...",
    "Provjera Common Law kontaminacije...",
    "Eliminacija lažnih uzbuna...",
    "Verifikacija ozbiljnosti nalaza...",
    "Korekcija prestroge ocjene...",
  ],
  saving: [
    "Spremam analizu u bazu...",
    "Generiranje izvještaja...",
  ],
};
