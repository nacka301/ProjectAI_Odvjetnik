import "./landing/landing-tokens.css";
import Nav from "./landing/Nav";
import AnnouncementBar from "./landing/AnnouncementBar";
import Hero from "./landing/Hero";
import Demo from "./landing/Demo";
import SourceMarquee from "./landing/SourceMarquee";
import HowItWorks from "./landing/HowItWorks";
import Modules from "./landing/Modules";
import Comparison from "./landing/Comparison";
import Security from "./landing/Security";
import Pricing from "./landing/Pricing";
import Faq from "./landing/Faq";
import PosterCta from "./landing/PosterCta";
import Footer from "./landing/Footer";

/**
 * The public front door — what an unsigned-in visitor sees at `/`.
 *
 * PravoTek has not launched yet (rujan 2026.); the only goal here is an
 * email on the waitlist, so every CTA on the page scrolls to the same hero
 * form instead of pointing at /auth/register. Deliberately a server
 * component with no data fetching — it must render for someone with no
 * session, no cookies and no JavaScript. The one client-side piece is the
 * waitlist form itself (components/landing/WaitlistForm.tsx) plus the
 * illustrative troškovnik demo's animation.
 *
 * Design: "Modernist" system from the handoff in
 * frontend/Frontend aplikacije popravka/design_handoff_pravotek_landing/ —
 * tokens in components/landing/landing-tokens.css, sections in
 * components/landing/*.
 */
export default function LandingPage() {
  return (
    <div className="ptk-landing max-w-[1440px] mx-auto">
      <Nav />
      <AnnouncementBar />
      <Hero />
      <Demo />
      <SourceMarquee />
      <HowItWorks />
      <Modules />
      <Comparison />
      <Security />
      <Pricing />
      <Faq />
      <PosterCta />
      <Footer />
    </div>
  );
}
