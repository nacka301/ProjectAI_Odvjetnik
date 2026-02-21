import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "PravoTek — Prijava",
  description: "Prijavite se u PravoTek pravnu platformu",
};

/**
 * Shared layout for /auth/* pages.
 * Provides a centred card on a gradient background.
 */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-indigo-950 px-4 py-10 sm:py-16">
      {/* Decorative blobs */}
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div className="absolute -top-32 -right-32 w-96 h-96 bg-blue-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-32 -left-32 w-96 h-96 bg-indigo-500/10 rounded-full blur-3xl" />
      </div>

      {/* Content */}
      <div className="relative z-10 w-full max-w-md">{children}</div>

      {/* Footer */}
      <p className="relative z-10 mt-8 text-[11px] text-slate-500 text-center">
        © {new Date().getFullYear()} PravoTek · Pravna inteligencija za odvjetnike
      </p>
    </div>
  );
}
