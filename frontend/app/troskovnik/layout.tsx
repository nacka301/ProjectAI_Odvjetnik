import PretplataGate from "@/components/PretplataGate";

/**
 * Wraps every route under /troskovnik in the subscription gate, so an office
 * whose trial has run out reads an explanation instead of a page whose every
 * request fails.
 *
 * A layout rather than a check inside each page: it covers the nested routes
 * too, and a page added later is covered without anyone remembering to.
 *
 * The real lock is SubscriptionGateMiddleware on the backend — see the comment
 * in components/PretplataGate.tsx.
 */
export default function Layout({ children }: { children: React.ReactNode }) {
  return <PretplataGate>{children}</PretplataGate>;
}
