import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "PravoTek — Prijava",
  description: "Prijavite se u PravoTek pravnu platformu",
};

/**
 * Shared layout for /auth/* pages.
 * Professional legal-firm aesthetic: deep navy, muted gold, serif headings.
 */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center px-4 py-10 sm:py-16"
      style={{ background: "linear-gradient(160deg, #0F1A2E 0%, #1B2A4A 40%, #162240 100%)" }}
    >
      {/* Subtle gold-tinted decorative elements */}
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div
          className="absolute -top-40 -right-40 w-[500px] h-[500px] rounded-full blur-[120px]"
          style={{ background: "rgba(201, 168, 76, 0.06)" }}
        />
        <div
          className="absolute -bottom-40 -left-40 w-[500px] h-[500px] rounded-full blur-[120px]"
          style={{ background: "rgba(201, 168, 76, 0.04)" }}
        />
        {/* Faint vertical line accent */}
        <div
          className="absolute top-0 left-1/2 -translate-x-1/2 w-px h-full"
          style={{ background: "linear-gradient(to bottom, transparent, rgba(201,168,76,0.08) 30%, rgba(201,168,76,0.08) 70%, transparent)" }}
        />
      </div>

      {/* Content */}
      <div className="relative z-10 w-full max-w-md">{children}</div>

      {/* Professional footer with compliance badges */}
      <div className="relative z-10 mt-10 flex flex-col items-center gap-3">
        {/* Compliance badges */}
        <div className="flex items-center gap-4 text-[10px] tracking-wider uppercase" style={{ color: "rgba(201,168,76,0.45)" }}>
          <span>GDPR</span>
          <span style={{ color: "rgba(201,168,76,0.2)" }}>·</span>
          <span>AES-256</span>
          <span style={{ color: "rgba(201,168,76,0.2)" }}>·</span>
          <span>TLS 1.3</span>
        </div>
        <p className="text-[11px] text-slate-500 text-center">
          © {new Date().getFullYear()} PravoTek · Pravna inteligencija za odvjetnike
        </p>
      </div>
    </div>
  );
}
