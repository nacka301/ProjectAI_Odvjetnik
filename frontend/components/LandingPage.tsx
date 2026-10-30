import "./landing/landing-tokens.css";
import Nav from "./landing/Nav";
import Hero from "./landing/Hero";
import Demo from "./landing/Demo";
import SourceMarquee from "./landing/SourceMarquee";
import HowItWorks from "./landing/HowItWorks";
import Modules from "./landing/Modules";
import Comparison from "./landing/Comparison";
import Security from "./landing/Security";
import Faq from "./landing/Faq";
import PosterCta from "./landing/PosterCta";
import Footer from "./landing/Footer";

/**
 * The public front door — what an unsigned-in visitor sees at `/`.
 *
 * PravoTek is live: every CTA on the page points at /auth/register (or
 * /auth/login for the secondary "already have an account" links) via
 * CtaLink (components/landing/CtaLink.tsx), a thin next/link wrapper.
 * Deliberately a server component with no data fetching — it must render
 * for someone with no session, no cookies and no JavaScript. The one
 * client-side piece left is the illustrative troškovnik demo's animation.
 *
 * Design: "Modernist" system from the handoff in
 * frontend/Frontend aplikacije popravka/design_handoff_pravotek_landing/ —
 * tokens in components/landing/landing-tokens.css, sections in
 * components/landing/*.
 *
 * The 1440px cap the design is authored at lives on each section's inner
 * .ptk-shell, not on this wrapper — see the note in landing-tokens.css.
 */
export default function LandingPage() {
  return (
    <div className="ptk-landing">
      <Nav />
      <Hero />
      <Demo />
      <SourceMarquee />
      <HowItWorks />
      <Modules />
      <Comparison />
      <Security />
      <Faq />
      <PosterCta />
      <Footer />
    </div>
  );
}
