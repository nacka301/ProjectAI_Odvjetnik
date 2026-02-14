"use client";

import { useState, useRef, useEffect, useCallback, useMemo } from "react";
import { ChevronDown, X, Search, MapPin } from "lucide-react";

// ══════════════════════════════════════════════════════════
//  COMBOBOX ITEM — structured option with unique key
// ══════════════════════════════════════════════════════════

export interface ComboboxItem {
  key: string; // unique identifier
  label: string; // primary display text
  subtitle?: string; // secondary text (e.g. county name)
}

// ══════════════════════════════════════════════════════════
//  GENERIC COMBOBOX (search + select from list)
//  Supports structured items with subtitle + smart sort
// ══════════════════════════════════════════════════════════

interface ComboboxProps {
  label: string;
  placeholder: string;
  items: ComboboxItem[];
  value: string; // matches ComboboxItem.key
  onChange: (key: string) => void;
  disabled?: boolean;
  hint?: string;
  required?: boolean;
  displayValue?: string; // text shown in the trigger button
}

function Combobox({
  label,
  placeholder,
  items,
  value,
  onChange,
  disabled = false,
  hint,
  required = false,
  displayValue,
}: ComboboxProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [highlightIdx, setHighlightIdx] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  // Smart filter + sort: exact → starts-with → contains
  const filtered = useMemo(() => {
    const q = search.toLowerCase().trim();
    if (!q) return items;

    const exact: ComboboxItem[] = [];
    const prefix: ComboboxItem[] = [];
    const contains: ComboboxItem[] = [];

    for (const item of items) {
      const lbl = item.label.toLowerCase();
      if (lbl === q) {
        exact.push(item);
      } else if (lbl.startsWith(q)) {
        prefix.push(item);
      } else if (lbl.includes(q)) {
        contains.push(item);
      } else if (item.subtitle?.toLowerCase().includes(q)) {
        contains.push(item); // also search county name
      }
    }

    return [...exact, ...prefix, ...contains];
  }, [items, search]);

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
        setSearch("");
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // Reset highlight when filtered changes
  useEffect(() => {
    setHighlightIdx(0);
  }, [search]);

  // Scroll highlighted item into view
  useEffect(() => {
    if (!listRef.current) return;
    const elems = listRef.current.querySelectorAll("li");
    elems[highlightIdx]?.scrollIntoView({ block: "nearest" });
  }, [highlightIdx]);

  const select = useCallback(
    (key: string) => {
      onChange(key);
      setOpen(false);
      setSearch("");
    },
    [onChange],
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open) {
      if (e.key === "ArrowDown" || e.key === "Enter") {
        e.preventDefault();
        setOpen(true);
      }
      return;
    }

    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        setHighlightIdx((i) => Math.min(i + 1, filtered.length - 1));
        break;
      case "ArrowUp":
        e.preventDefault();
        setHighlightIdx((i) => Math.max(i - 1, 0));
        break;
      case "Enter":
        e.preventDefault();
        if (filtered[highlightIdx]) select(filtered[highlightIdx].key);
        break;
      case "Escape":
        setOpen(false);
        setSearch("");
        break;
    }
  };

  const triggerText = displayValue || placeholder;
  const hasValue = !!value && !!displayValue;

  return (
    <div ref={containerRef} className="relative">
      <label className="block text-xs font-medium text-gray-500 mb-1">
        {label}
        {required && " *"}
      </label>

      {/* Trigger / display */}
      <button
        type="button"
        disabled={disabled}
        onClick={() => {
          if (!disabled) {
            setOpen(!open);
            setTimeout(() => inputRef.current?.focus(), 50);
          }
        }}
        className={`w-full flex items-center justify-between rounded-lg border px-3 py-2 text-sm text-left transition
          ${
            disabled
              ? "bg-gray-50 border-gray-200 text-gray-400 cursor-not-allowed"
              : open
                ? "border-blue-500 ring-2 ring-blue-100 bg-white"
                : "border-gray-300 bg-white hover:border-gray-400 cursor-pointer"
          }`}
      >
        <span className={hasValue ? "text-gray-900" : "text-gray-400"}>
          {triggerText}
        </span>
        <div className="flex items-center gap-1">
          {hasValue && !disabled && (
            <span
              role="button"
              onClick={(e) => {
                e.stopPropagation();
                onChange("");
                setSearch("");
              }}
              className="p-0.5 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600"
            >
              <X className="h-3.5 w-3.5" />
            </span>
          )}
          <ChevronDown
            className={`h-4 w-4 text-gray-400 transition-transform ${open ? "rotate-180" : ""}`}
          />
        </div>
      </button>

      {/* Hint */}
      {hint && !hasValue && (
        <p className="text-[11px] text-blue-500 mt-1">{hint}</p>
      )}

      {/* Dropdown */}
      {open && !disabled && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border border-gray-200 bg-white shadow-lg animate-in fade-in-0 zoom-in-95 duration-100">
          {/* Search bar */}
          <div className="flex items-center gap-2 px-3 py-2 border-b border-gray-100">
            <Search className="h-4 w-4 text-gray-400 shrink-0" />
            <input
              ref={inputRef}
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Pretraži..."
              className="w-full text-sm outline-none bg-transparent placeholder:text-gray-400"
              autoComplete="off"
            />
          </div>

          {/* Options list */}
          <ul
            ref={listRef}
            className="max-h-48 overflow-y-auto py-1"
            role="listbox"
          >
            {filtered.length === 0 ? (
              <li className="px-3 py-2 text-sm text-gray-400 text-center">
                Nema rezultata
              </li>
            ) : (
              filtered.map((item, idx) => (
                <li
                  key={item.key}
                  role="option"
                  aria-selected={item.key === value}
                  onClick={() => select(item.key)}
                  onMouseEnter={() => setHighlightIdx(idx)}
                  className={`px-3 py-1.5 text-sm cursor-pointer flex items-center gap-2
                    ${idx === highlightIdx ? "bg-blue-50 text-blue-700" : "text-gray-700 hover:bg-gray-50"}
                    ${item.key === value ? "font-semibold" : ""}`}
                >
                  {item.key === value && (
                    <span className="h-1.5 w-1.5 rounded-full bg-blue-500 shrink-0" />
                  )}
                  <span className="flex flex-col min-w-0">
                    <span className="truncate">{item.label}</span>
                    {item.subtitle && (
                      <span className="text-[10px] text-gray-400 truncate leading-tight">
                        {item.subtitle}
                      </span>
                    )}
                  </span>
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════
//  LOCATION SELECTOR (cascading City → Settlement)
// ══════════════════════════════════════════════════════════

import { getFlatCities, type FlatCity } from "@/lib/locations";

interface LocationSelectorProps {
  city: string;
  district: string;
  onCityChange: (city: string) => void;
  onDistrictChange: (district: string) => void;
}

export default function LocationSelector({
  city,
  district,
  onCityChange,
  onDistrictChange,
}: LocationSelectorProps) {
  const flatCities = getFlatCities();

  // Build unique ComboboxItems for cities
  // key = index into flatCities (unique), label = city name, subtitle = county
  const cityItems: ComboboxItem[] = useMemo(() => {
    return flatCities.map((c, idx) => ({
      key: String(idx),
      label: c.city,
      subtitle: c.county,
    }));
  }, [flatCities]);

  // Resolve current city prop → flatCities index (for external state sync)
  const resolvedIdx = useMemo(() => {
    if (!city) return -1;
    const lc = city.toLowerCase();
    return flatCities.findIndex((c) => c.city.toLowerCase() === lc);
  }, [city, flatCities]);

  const currentKey = resolvedIdx >= 0 ? String(resolvedIdx) : "";
  const selectedEntry = resolvedIdx >= 0 ? flatCities[resolvedIdx] : null;
  const settlements = selectedEntry?.settlements ?? [];
  const selectedCounty = selectedEntry?.county;

  // Settlement items for the second combobox
  const settlementItems: ComboboxItem[] = useMemo(() => {
    return settlements.map((s) => ({
      key: s,
      label: s,
    }));
  }, [settlements]);

  // City selected
  const handleCityChange = (key: string) => {
    if (!key) {
      onCityChange("");
      onDistrictChange("");
      return;
    }
    const idx = parseInt(key, 10);
    const entry = flatCities[idx];
    if (entry) {
      onCityChange(entry.city);
      // Note: parent's onCityChange already resets district to ""
      // Do NOT call onDistrictChange("") here — it would use a stale
      // closure and overwrite the city back to "".
    }
  };

  // Settlement selected
  const handleDistrictChange = (key: string) => {
    onDistrictChange(key); // key === settlement name
  };

  // Display value for trigger button
  const cityDisplay = selectedEntry ? selectedEntry.city : undefined;

  return (
    <div className="rounded-xl bg-white border border-gray-200 p-6">
      <h3 className="text-sm font-semibold text-gray-700 mb-4 flex items-center gap-2">
        <MapPin className="h-4 w-4 text-blue-500" />
        Lokacija
      </h3>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Combobox
            label="Grad / Općina"
            placeholder="Odaberite grad..."
            items={cityItems}
            value={currentKey}
            onChange={handleCityChange}
            required
            displayValue={cityDisplay}
          />
          {selectedCounty && cityDisplay && (
            <p className="text-[10px] text-gray-400 mt-0.5 pl-1">
              {selectedCounty}
            </p>
          )}
        </div>
        <Combobox
          label="Naselje / Kvart"
          placeholder={city ? "Odaberite naselje..." : "Prvo odaberite grad"}
          items={settlementItems}
          value={district}
          onChange={handleDistrictChange}
          disabled={!city || settlements.length === 0}
          hint="💡 Za precizniju procjenu, odaberite naselje"
          displayValue={district || undefined}
        />
      </div>
    </div>
  );
}
