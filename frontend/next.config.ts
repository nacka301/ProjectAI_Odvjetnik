import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // ═══════════════════════════════════════════════════════════
  //  SECURITY HEADERS — Enterprise (Banking) Grade
  //  Protects against XSS, clickjacking, MIME sniffing,
  //  information leakage, and enforces HTTPS.
  // ═══════════════════════════════════════════════════════════
  async headers() {
    return [
      {
        source: "/(.*)",
        headers: [
          // ── HSTS: Force HTTPS for 2 years, include subdomains ──
          {
            key: "Strict-Transport-Security",
            value: "max-age=63072000; includeSubDomains; preload",
          },
          // ── Clickjacking protection — deny all framing ──
          {
            key: "X-Frame-Options",
            value: "DENY",
          },
          // ── Prevent MIME-type sniffing ──
          {
            key: "X-Content-Type-Options",
            value: "nosniff",
          },
          // ── XSS protection (legacy browsers) ──
          {
            key: "X-XSS-Protection",
            value: "1; mode=block",
          },
          // ── No referrer leakage to third parties ──
          {
            key: "Referrer-Policy",
            value: "strict-origin-when-cross-origin",
          },
          // ── Permission policy — disable unnecessary browser APIs ──
          {
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=(), interest-cohort=()",
          },
          // ── Content Security Policy — strict but functional ──
          {
            key: "Content-Security-Policy",
            value: [
              "default-src 'self'",
              "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://accounts.google.com https://apis.google.com",
              "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
              "font-src 'self' https://fonts.gstatic.com",
              "img-src 'self' data: blob: https://*.supabase.co https://*.googleusercontent.com",
              "connect-src 'self' https://*.supabase.co wss://*.supabase.co https://openrouter.ai https://accounts.google.com https://*.onrender.com",
              "frame-src https://accounts.google.com https://*.supabase.co",
              "frame-ancestors 'none'",
              "base-uri 'self'",
              "form-action 'self'",
            ].join("; "),
          },
        ],
      },
    ];
  },
};

export default nextConfig;
