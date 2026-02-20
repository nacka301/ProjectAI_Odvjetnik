import { describe, it, expect } from "vitest";
import type {
  NormInfo,
  NormAmendment,
  CitedNorm,
  TemporalNormResult,
} from "@/lib/normApi";

// ═══════════════════════════════════════════
//  Norm API Types and Logic Tests
// ═══════════════════════════════════════════

describe("NormInfo shape", () => {
  it("creates valid norm info", () => {
    const norm: NormInfo = {
      abbreviation: "ZOO",
      full_name: "Zakon o obveznim odnosima",
      status: "Važeći",
      nn_reference: "NN 35/05, 41/08, 125/11, 78/15, 29/18",
      nn_reference_short: "NN 35/05, 29/18",
      amendments: [],
      last_amendment_date: "2018-04-01",
      original_date: "2005-03-15",
      category: "Obvezno pravo",
      nn_url: "https://www.zakon.hr/z/75/Zakon-o-obveznim-odnosima",
    };
    expect(norm.abbreviation).toBe("ZOO");
    expect(norm.status).toBe("Važeći");
  });
});

describe("NormAmendment shape", () => {
  it("creates valid amendment", () => {
    const amendment: NormAmendment = {
      nn_number: "29/18",
      publication_date: "2018-04-01",
      type: "Izmjena",
      description: "Izmjene i dopune",
    };
    expect(amendment.nn_number).toBe("29/18");
  });
});

describe("CitedNorm shape", () => {
  it("creates valid court-style citation", () => {
    const cited: CitedNorm = {
      abbreviation: "ZOO",
      full_name: "Zakon o obveznim odnosima",
      article: "303",
      paragraph: "1",
      point: "",
      nn_reference: "NN 35/05, 29/18",
      status: "VAŽEĆI",
      formatted_citation: "čl. 303. st. 1. Zakona o obveznim odnosima (NN 35/05, 29/18)",
      nn_url: "https://www.zakon.hr/z/75/Zakon-o-obveznim-odnosima",
    };
    expect(cited.formatted_citation).toContain("čl. 303.");
    expect(cited.status).toBe("VAŽEĆI");
  });
});

describe("TemporalNormResult shape", () => {
  it("detects outdated norm version", () => {
    const result: TemporalNormResult = {
      abbreviation: "ZOO",
      full_name: "Zakon o obveznim odnosima",
      applicable_nn_version: "NN 35/05",
      current_nn_version: "NN 29/18",
      applicable_nn_reference: "NN 35/05",
      is_outdated: true,
      warning: "Primjenjuje se starija verzija zakona",
      formatted_citation: "čl. 303. ZOO (NN 35/05)",
      event_date: "2006-01-15",
    };
    expect(result.is_outdated).toBe(true);
    expect(result.applicable_nn_version).not.toBe(result.current_nn_version);
  });

  it("current version when not outdated", () => {
    const result: TemporalNormResult = {
      abbreviation: "ZOO",
      full_name: "Zakon o obveznim odnosima",
      applicable_nn_version: "NN 29/18",
      current_nn_version: "NN 29/18",
      applicable_nn_reference: "NN 35/05, 29/18",
      is_outdated: false,
      warning: "",
      formatted_citation: "čl. 303. ZOO (NN 35/05, 29/18)",
      event_date: "2024-01-15",
    };
    expect(result.is_outdated).toBe(false);
    expect(result.applicable_nn_version).toBe(result.current_nn_version);
  });
});
