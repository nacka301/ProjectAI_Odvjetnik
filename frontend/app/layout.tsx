import type { Metadata, Viewport } from "next";
import { Archivo, Geist, Geist_Mono, Playfair_Display } from "next/font/google";
import "./globals.css";
import SessionGuard from "@/components/SessionGuard";
import { ConditionalSecurityFooter } from "@/components/ConditionalFooter";
import { CreditProvider } from "@/contexts/CreditContext";
import { DemoProvider } from "@/contexts/DemoContext";
import { VodicProvider } from "@/contexts/VodicContext";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

const playfair = Playfair_Display({
  variable: "--font-playfair",
  subsets: ["latin"],
  weight: ["400", "600", "700"],
});

// Used only by the public landing page (components/landing/**). Exposed as a
// CSS variable on <body> like the other fonts so nothing outside the landing
// has to know it exists — no other component references --font-archivo.
const archivo = Archivo({
  variable: "--font-archivo",
  subsets: ["latin"],
  weight: ["400", "600", "800"],
});

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
};

export const metadata: Metadata = {
  title: "PravoTek — Pravna inteligencija za odvjetnike",
  description: "PravoTek — platforma za pravnu analitiku: procjena nekretnina, analiza ugovora, ZK izvadci",
  // Anti-cache meta tags for legacy browsers & in-app webviews (WhatsApp, Viber)
  other: {
    "Cache-Control": "no-cache, no-store, must-revalidate",
    Pragma: "no-cache",
    Expires: "0",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="hr" suppressHydrationWarning>
      <body
        suppressHydrationWarning
        className={`${geistSans.variable} ${geistMono.variable} ${playfair.variable} ${archivo.variable} antialiased overflow-x-hidden min-h-screen flex flex-col`}
      >
        {/* One idle timer. There used to be two — AutoLogoutProvider at 15 min
            and SessionGuard at 25 — sharing a localStorage key. The shorter one
            always won, so the warning modal never appeared and users were
            dropped without notice. SessionGuard now owns it: 60 minutes idle,
            then a 60-second warning countdown, plus a 12-hour absolute cap.
            All timings live in lib/session-timeout.ts. */}
        <SessionGuard>
          <CreditProvider>
            {/* Vodič stoji IZNAD ruta, ne u pojedinoj stranici: samo tako
                jedan njegov korak može živjeti na drugom ekranu (korak 8/8
                uvoda je na /klijenti, a prethodnih sedam na /troskovniku).
                DemoProvider je iznad njega jer uvod vozi na podacima za
                primjer i gasi ih kad završi.

                Bez trajne trake na vrhu — uvodni korak 1/8 već kaže da su
                podaci primjer i da se ništa ne sprema, a zatvaranje vodiča
                (Preskoči/Esc/Završi) u svakom trenutku samo po sebi gasi
                prikaz primjera, pa traka nije jedini izlaz koji bi trebalo
                čuvati. */}
            <DemoProvider>
              <VodicProvider>
                <main className="flex-1">{children}</main>
                <ConditionalSecurityFooter />
              </VodicProvider>
            </DemoProvider>
          </CreditProvider>
        </SessionGuard>
      </body>
    </html>
  );
}
