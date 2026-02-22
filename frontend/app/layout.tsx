import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import AutoLogoutProvider from "@/components/AutoLogoutProvider";
import { SecurityFooter } from "@/components/TrustComponents";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "PravoTek — Pravna inteligencija za odvjetnike",
  description: "PravoTek — AI platforma za odvjetnike: procjena nekretnina, analiza ugovora, ZK izvadci",
  viewport: "width=device-width, initial-scale=1, viewport-fit=cover",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="hr">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased overflow-x-hidden min-h-screen flex flex-col`}
      >
        <AutoLogoutProvider>
          <main className="flex-1">{children}</main>
          <SecurityFooter />
        </AutoLogoutProvider>
      </body>
    </html>
  );
}
