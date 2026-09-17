import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { klasificirajPotvrdu } from "@/lib/pravna-potvrda";

/**
 * Potvrda Izjave o odgovornosti (plan pravne zaštite, dio C).
 *
 * Kolačić je jedino što middlewareu kaže da ne mora pitati bazu, pa svaki način
 * da se krivotvori ili prenese dobiva svoj test — kao `pt_mfa`.
 */

const SECRET = "test-secret-do-not-use-in-production";
const USER = "11111111-1111-1111-1111-111111111111";
const OTHER_USER = "22222222-2222-2222-2222-222222222222";
const VERZIJA = "2026-09-17";

async function ucitajKolacic() {
  vi.resetModules();
  return import("@/lib/potvrda-cookie");
}

describe("potvrda-cookie", () => {
  beforeEach(() => {
    process.env.MFA_COOKIE_SECRET = SECRET;
  });
  afterEach(() => {
    delete process.env.MFA_COOKIE_SECRET;
  });

  it("prihvaća kolačić koji je upravo potpisan za istog korisnika i verziju", async () => {
    const { signPotvrdaCookie, verifyPotvrdaCookie } = await ucitajKolacic();
    const kolacic = await signPotvrdaCookie(USER, VERZIJA);
    expect(await verifyPotvrdaCookie(kolacic, USER, VERZIJA)).toBe(true);
  });

  it("ne vrijedi za drugog korisnika", async () => {
    const { signPotvrdaCookie, verifyPotvrdaCookie } = await ucitajKolacic();
    const kolacic = await signPotvrdaCookie(USER, VERZIJA);
    expect(await verifyPotvrdaCookie(kolacic, OTHER_USER, VERZIJA)).toBe(false);
  });

  it("ne vrijedi za novu verziju teksta — tada se traži nova potvrda", async () => {
    const { signPotvrdaCookie, verifyPotvrdaCookie } = await ucitajKolacic();
    const kolacic = await signPotvrdaCookie(USER, VERZIJA);
    expect(await verifyPotvrdaCookie(kolacic, USER, "2027-01-01")).toBe(false);
  });

  it("odbija prepravljen user id ili verziju uz stari potpis", async () => {
    const { signPotvrdaCookie, verifyPotvrdaCookie } = await ucitajKolacic();
    const [, , potpis] = (await signPotvrdaCookie(USER, VERZIJA)).split(".");
    expect(await verifyPotvrdaCookie(`${OTHER_USER}.${VERZIJA}.${potpis}`, OTHER_USER, VERZIJA)).toBe(false);
    expect(await verifyPotvrdaCookie(`${USER}.2027-01-01.${potpis}`, USER, "2027-01-01")).toBe(false);
  });

  it("odbija prazan i neispravno složen kolačić", async () => {
    const { verifyPotvrdaCookie } = await ucitajKolacic();
    expect(await verifyPotvrdaCookie(undefined, USER, VERZIJA)).toBe(false);
    expect(await verifyPotvrdaCookie("", USER, VERZIJA)).toBe(false);
    expect(await verifyPotvrdaCookie(`${USER}.${VERZIJA}`, USER, VERZIJA)).toBe(false);
  });

  it("MFA kolačić se ne može podmetnuti kao potvrda", async () => {
    vi.resetModules();
    const { signMfaCookie } = await import("@/lib/mfa-cookie");
    const { verifyPotvrdaCookie } = await import("@/lib/potvrda-cookie");
    const mfa = await signMfaCookie(USER); // `{user}.{istek}.{potpis}` — isti broj dijelova
    const [, , potpis] = mfa.split(".");
    expect(await verifyPotvrdaCookie(`${USER}.${VERZIJA}.${potpis}`, USER, VERZIJA)).toBe(false);
  });
});

describe("klasificirajPotvrdu", () => {
  it("redak za verziju → potvrđeno", () => {
    expect(klasificirajPotvrdu(200, [{ id: "x" }])).toBe("potvrdeno");
  });

  it("prazan popis → nije potvrđeno", () => {
    expect(klasificirajPotvrdu(200, [])).toBe("nije");
  });

  it("greška ili nečitljiv odgovor → nepoznato, nikad potvrđeno", () => {
    expect(klasificirajPotvrdu(401, null)).toBe("nepoznato");
    expect(klasificirajPotvrdu(500, [{ id: "x" }])).toBe("nepoznato");
    expect(klasificirajPotvrdu(200, { id: "x" })).toBe("nepoznato");
    expect(klasificirajPotvrdu(200, null)).toBe("nepoznato");
  });
});
