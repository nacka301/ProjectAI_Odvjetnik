import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono, Playfair_Display } from "next/font/google";
import "./globals.css";
import AutoLogoutProvider from "@/components/AutoLogoutProvider";
import SessionGuard from "@/components/SessionGuard";
import { ConditionalSecurityFooter } from "@/components/ConditionalFooter";
import CommandPalette from "@/components/CommandPalette";

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
    <html lang="hr">
      <body
        className={`${geistSans.variable} ${geistMono.variable} ${playfair.variable} antialiased overflow-x-hidden min-h-screen flex flex-col`}
      >
        <AutoLogoutProvider>
          <SessionGuard>
            <CommandPalette />
            <main className="flex-1">{children}</main>
            <ConditionalSecurityFooter />
          </SessionGuard>
        </AutoLogoutProvider>
      </body>
    </html>
  );
}
