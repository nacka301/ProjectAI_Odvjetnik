/**
 * „Iznos mi ne izgleda točno" (plan pravne zaštite, dio D): dijalog šalje opis,
 * troškovnik i izračun na backend, a prekratak opis ne šalje nikamo.
 */
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import PrijavaGreskeDialog from "@/components/troskovnik/PrijavaGreskeDialog";

vi.mock("@/lib/useAuth", () => ({
  useAuth: () => ({ userId: "u-1", authHeaders: { Authorization: "Bearer t" } }),
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("PrijavaGreskeDialog", () => {
  it("prekratak opis se ne šalje", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(<PrijavaGreskeDialog open onClose={() => {}} />);

    fireEvent.change(screen.getByRole("textbox", { name: /Što ne izgleda točno/ }), {
      target: { value: "kriv" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Pošalji" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("barem 10 znakova");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("šalje opis, očekivani iznos, troškovnik i izračun, pa zahvaljuje", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: "p-1" }) });
    vi.stubGlobal("fetch", fetchMock);
    render(
      <PrijavaGreskeDialog
        open
        onClose={() => {}}
        troskovnikId="t-1"
        ulazniPodaci={() => ({ vrijednost_spora: 50000 })}
        rezultat={() => ({ sveukupni_trosak: 273.23 })}
      />,
    );

    fireEvent.change(screen.getByRole("textbox", { name: /Što ne izgleda točno/ }), {
      target: { value: "Pristojba za tužbu je kriva" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: /Koliko bi po vama/ }), {
      target: { value: "310,00 €" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Pošalji" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    const [url, opcije] = fetchMock.mock.calls[0];
    expect(url).toMatch(/\/api\/prijave-gresaka$/);
    expect(opcije.headers.Authorization).toBe("Bearer t");
    expect(JSON.parse(opcije.body)).toEqual({
      opis: "Pristojba za tužbu je kriva",
      ocekivani_iznos: "310,00 €",
      troskovnik_id: "t-1",
      ulazni_podaci: { vrijednost_spora: 50000 },
      rezultat: { sveukupni_trosak: 273.23 },
    });
    expect(await screen.findByText(/Prijava je stigla/)).toBeInTheDocument();
  });

  it("previše prijava: poruka s adresom umjesto tihog neuspjeha", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 429, json: async () => ({}) }));
    render(<PrijavaGreskeDialog open onClose={() => {}} />);

    fireEvent.change(screen.getByRole("textbox", { name: /Što ne izgleda točno/ }), {
      target: { value: "Pristojba za tužbu je kriva" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Pošalji" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("vackadavid@gmail.com");
  });
});
