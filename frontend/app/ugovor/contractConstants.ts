// ═══════════════════════════════════════════
//  CONTRACT ANALYSIS — Stage definitions
//  V57: SSE streaming stages matching ProcessingView design
// ═══════════════════════════════════════════

import { FileText, Search, Brain, Calculator, Save } from "lucide-react";
import { createElement } from "react";
import type { ProcessingStage } from "@/components/SharedProcessingView";

/** @deprecated Use ProcessingStage from SharedProcessingView instead */
export type ContractStage = ProcessingStage;

export const INITIAL_CONTRACT_STAGES: ContractStage[] = [
  { id: "reading", label: "Čitam ugovor...", status: "pending" },
  { id: "audit", label: "Pass 1: Pravni audit", status: "pending" },
  { id: "reasoning", label: "Pass 2: Dubinska analiza", status: "pending" },
  { id: "scoring", label: "Pass 3: Bodovanje rizika", status: "pending" },
  { id: "saving", label: "Spremam rezultate", status: "pending" },
];

export const CONTRACT_STAGE_ICONS: Record<string, React.ReactNode> = {
  reading: createElement(FileText, { className: "h-4 w-4" }),
  audit: createElement(Search, { className: "h-4 w-4" }),
  reasoning: createElement(Brain, { className: "h-4 w-4" }),
  scoring: createElement(Calculator, { className: "h-4 w-4" }),
  saving: createElement(Save, { className: "h-4 w-4" }),
};

export const CONTRACT_STAGE_MICROCOPY: Record<string, string[]> = {
  reading: [
    "Učitavam tekst ugovora...",
    "Identificiram vrstu dokumenta...",
    "Pripremam za analizu...",
  ],
  audit: [
    "Provjeravam terete i hipoteke...",
    "Analiziram kaparu i plaćanje...",
    "Kontroliram clausulu intabulandi...",
    "Provjeravam zaštitne klauzule...",
    "Cross-article analiza u tijeku...",
    "Uspoređujem s obveznim elementima...",
  ],
  reasoning: [
    "AI dubinski razmišlja o ugovoru...",
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
  saving: [
    "Spremam analizu u bazu...",
    "Generiranje izvještaja...",
  ],
};
