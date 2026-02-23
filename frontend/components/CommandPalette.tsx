"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Search,
  ArrowRight,
  Users,
  Briefcase,
  FileText,
  Scale,
  Calculator,
  Clock,
  Shield,
  Settings,
  BarChart3,
  Globe,
  X,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

/* ── Types ── */
interface SearchResult {
  id: string;
  label: string;
  subtitle?: string;
  href: string;
  category: "stranke" | "predmeti" | "navigacija";
  icon: typeof Search;
}

/* ── Static navigation items ── */
const NAV_ITEMS: SearchResult[] = [
  { id: "nav-home",       label: "Početna",              href: "/",             category: "navigacija", icon: Scale },
  { id: "nav-wizard",     label: "Nova procjena",        href: "/wizard",       category: "navigacija", icon: BarChart3 },
  { id: "nav-ugovor",     label: "Analiza ugovora",      href: "/ugovor",       category: "navigacija", icon: FileText },
  { id: "nav-zk",         label: "ZK Procjena",          href: "/zk-procjena",  category: "navigacija", icon: FileText },
  { id: "nav-troskovnik", label: "Troškovnik",           href: "/troskovnik",   category: "navigacija", icon: Calculator },
  { id: "nav-klijenti",   label: "Stranke",              href: "/klijenti",     category: "navigacija", icon: Users },
  { id: "nav-history",    label: "Povijest predmeta",    href: "/history",      category: "navigacija", icon: Clock },
  { id: "nav-audit",      label: "Revizija",             href: "/audit",        category: "navigacija", icon: Shield },
  { id: "nav-postavke",   label: "Postavke",             href: "/postavke",     category: "navigacija", icon: Settings },
  { id: "nav-prijevod",   label: "Prijevod",             href: "/prijevod",     category: "navigacija", icon: Globe },
  { id: "nav-fiskalizacija", label: "Fiskalizacija",     href: "/fiskalizacija", category: "navigacija", icon: FileText },
];

const CATEGORY_LABELS: Record<string, string> = {
  stranke: "Stranke",
  predmeti: "Predmeti",
  navigacija: "Navigacija",
};

export default function CommandPalette() {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResult[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);
  const [searching, setSearching] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const router = useRouter();

  // Ctrl+K listener
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        setOpen((prev) => !prev);
      }
      if (e.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  // Focus input when opened
  useEffect(() => {
    if (open) {
      setQuery("");
      setResults([]);
      setActiveIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [open]);

  // Search logic
  const doSearch = useCallback(async (q: string) => {
    if (!q.trim()) {
      setResults([]);
      return;
    }

    setSearching(true);
    const lower = q.toLowerCase();

    // Filter nav items
    const navResults = NAV_ITEMS.filter((item) =>
      item.label.toLowerCase().includes(lower)
    );

    // Search clients & cases from Supabase
    let clientResults: SearchResult[] = [];
    let caseResults: SearchResult[] = [];

    try {
      const supabase = createSupabaseBrowser();

      // Clients
      const { data: clients } = await supabase
        .from("clients")
        .select("id, ime_naziv, oib, grad, tip")
        .or(`ime_naziv.ilike.%${q}%,oib.ilike.%${q}%,grad.ilike.%${q}%`)
        .limit(5);

      if (clients) {
        clientResults = clients.map((c) => ({
          id: `client-${c.id}`,
          label: c.ime_naziv || "Bez imena",
          subtitle: [c.oib, c.grad].filter(Boolean).join(" · "),
          href: `/klijenti/${c.id}`,
          category: "stranke" as const,
          icon: c.tip === "pravna" ? Briefcase : Users,
        }));
      }

      // Cases
      const { data: cases } = await supabase
        .from("cases")
        .select("id, naziv, broj_predmeta, vrsta, status, client_id")
        .or(`naziv.ilike.%${q}%,broj_predmeta.ilike.%${q}%`)
        .limit(5);

      if (cases) {
        caseResults = cases.map((c) => ({
          id: `case-${c.id}`,
          label: c.naziv || c.broj_predmeta || "Bez naziva",
          subtitle: [c.broj_predmeta, c.vrsta, c.status].filter(Boolean).join(" · "),
          href: c.client_id ? `/klijenti/${c.client_id}` : "/klijenti",
          category: "predmeti" as const,
          icon: Briefcase,
        }));
      }
    } catch {
      // silent — DB search optional
    }

    setResults([...clientResults, ...caseResults, ...navResults]);
    setActiveIndex(0);
    setSearching(false);
  }, []);

  // Debounced search
  useEffect(() => {
    const timer = setTimeout(() => doSearch(query), 200);
    return () => clearTimeout(timer);
  }, [query, doSearch]);

  // Keyboard navigation
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((i) => Math.min(results.length - 1, i + 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) => Math.max(0, i - 1));
    } else if (e.key === "Enter" && results[activeIndex]) {
      e.preventDefault();
      navigate(results[activeIndex].href);
    }
  };

  const navigate = (href: string) => {
    setOpen(false);
    router.push(href);
  };

  // Scroll active item into view
  useEffect(() => {
    const el = listRef.current?.querySelector(`[data-index="${activeIndex}"]`);
    el?.scrollIntoView({ block: "nearest" });
  }, [activeIndex]);

  // Group results by category
  const grouped = results.reduce<Record<string, SearchResult[]>>((acc, r) => {
    (acc[r.category] ??= []).push(r);
    return acc;
  }, {});

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[100]" role="dialog" aria-modal="true">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-slate-900/40 backdrop-blur-sm"
        onClick={() => setOpen(false)}
      />

      {/* Modal */}
      <div className="relative mx-auto mt-[15vh] w-full max-w-lg px-4">
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-2xl">
          {/* Search input */}
          <div className="flex items-center gap-3 border-b border-slate-100 px-4 py-3">
            <Search className="h-5 w-5 text-slate-400 shrink-0" />
            <input
              ref={inputRef}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Pretraži stranke, predmete, stranice..."
              className="flex-1 bg-transparent text-sm text-slate-900 placeholder:text-slate-400 outline-none"
            />
            <kbd className="hidden sm:inline-flex items-center gap-0.5 rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 text-[10px] font-mono text-slate-400">
              ESC
            </kbd>
            <button onClick={() => setOpen(false)} className="sm:hidden p-1 text-slate-400 hover:text-slate-600">
              <X className="h-4 w-4" />
            </button>
          </div>

          {/* Results */}
          <div ref={listRef} className="max-h-[50vh] overflow-y-auto">
            {query && !searching && results.length === 0 && (
              <div className="px-4 py-10 text-center">
                <p className="text-sm text-slate-400">Nema rezultata za &ldquo;{query}&rdquo;</p>
              </div>
            )}

            {!query && (
              <div className="px-4 py-6 text-center">
                <p className="text-xs text-slate-400">Počnite pisati za pretragu ili koristite strelice ↑↓</p>
              </div>
            )}

            {Object.entries(grouped).map(([category, items]) => (
              <div key={category}>
                <div className="sticky top-0 bg-slate-50/90 backdrop-blur-sm px-4 py-1.5">
                  <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                    {CATEGORY_LABELS[category] || category}
                  </p>
                </div>
                {items.map((item, idx) => {
                  const globalIdx = results.indexOf(item);
                  const Icon = item.icon;
                  return (
                    <button
                      key={item.id}
                      data-index={globalIdx}
                      onClick={() => navigate(item.href)}
                      onMouseEnter={() => setActiveIndex(globalIdx)}
                      className={`flex w-full items-center gap-3 px-4 py-2.5 text-left transition ${
                        globalIdx === activeIndex
                          ? "bg-[#1B2A4A] text-white"
                          : "text-slate-700 hover:bg-slate-50"
                      }`}
                    >
                      <Icon className={`h-4 w-4 shrink-0 ${globalIdx === activeIndex ? "text-blue-300" : "text-slate-400"}`} />
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium truncate">{item.label}</p>
                        {item.subtitle && (
                          <p className={`text-xs truncate ${globalIdx === activeIndex ? "text-blue-200" : "text-slate-400"}`}>
                            {item.subtitle}
                          </p>
                        )}
                      </div>
                      {globalIdx === activeIndex && (
                        <ArrowRight className="h-3.5 w-3.5 text-blue-300 shrink-0" />
                      )}
                    </button>
                  );
                })}
              </div>
            ))}
          </div>

          {/* Footer hint */}
          <div className="border-t border-slate-100 px-4 py-2 flex items-center justify-between">
            <div className="flex items-center gap-3 text-[10px] text-slate-400">
              <span>↑↓ navigacija</span>
              <span>↵ otvori</span>
              <span>esc zatvori</span>
            </div>
            <p className="text-[10px] text-slate-300">⌘K</p>
          </div>
        </div>
      </div>
    </div>
  );
}
