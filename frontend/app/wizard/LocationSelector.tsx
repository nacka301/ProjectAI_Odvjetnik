"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import { ChevronDown, X, Search, MapPin } from "lucide-react";

// ══════════════════════════════════════════════════════════
//  GENERIC COMBOBOX (search + select from list)
// ══════════════════════════════════════════════════════════

interface ComboboxProps {
  label: string;
  placeholder: string;
  options: string[];
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  hint?: string;
  required?: boolean;
  county?: string; // shown as subtitle in the header
}

function Combobox({
  label,
  placeholder,
  options,
  value,
  onChange,
  disabled = false,
  hint,
  required = false,
  county,
}: ComboboxProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [highlightIdx, setHighlightIdx] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  const filtered = options.filter((o) =>
    o.toLowerCase().includes(search.toLowerCase())
  );

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
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
    const items = listRef.current.querySelectorAll("li");
    items[highlightIdx]?.scrollIntoView({ block: "nearest" });
  }, [highlightIdx]);

  const select = useCallback(
    (val: string) => {
      onChange(val);
      setOpen(false);
      setSearch("");
    },
    [onChange]
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
        if (filtered[highlightIdx]) select(filtered[highlightIdx]);
        break;
      case "Escape":
        setOpen(false);
        setSearch("");
        break;
    }
  };

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
          ${disabled
            ? "bg-gray-50 border-gray-200 text-gray-400 cursor-not-allowed"
            : open
              ? "border-blue-500 ring-2 ring-blue-100 bg-white"
              : "border-gray-300 bg-white hover:border-gray-400 cursor-pointer"
          }`}
      >
        <span className={value ? "text-gray-900" : "text-gray-400"}>
          {value || placeholder}
        </span>
        <div className="flex items-center gap-1">
          {value && !disabled && (
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

      {/* County subtitle */}
      {county && value && (
        <p className="text-[10px] text-gray-400 mt-0.5 pl-1">{county}</p>
      )}

      {/* Hint */}
      {hint && !value && (
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
              filtered.map((opt, idx) => (
                <li
                  key={opt}
                  role="option"
                  aria-selected={opt === value}
                  onClick={() => select(opt)}
                  onMouseEnter={() => setHighlightIdx(idx)}
                  className={`px-3 py-1.5 text-sm cursor-pointer flex items-center gap-2
                    ${idx === highlightIdx ? "bg-blue-50 text-blue-700" : "text-gray-700 hover:bg-gray-50"}
                    ${opt === value ? "font-semibold" : ""}`}
                >
                  {opt === value && (
                    <span className="h-1.5 w-1.5 rounded-full bg-blue-500 shrink-0" />
                  )}
                  {opt}
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

import { getFlatCities, getSettlements, type FlatCity } from "@/lib/locations";

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
  const cityNames = flatCities.map((c) => c.city);
  const settlements = city ? getSettlements(city) : [];
  const selectedCounty = flatCities.find(
    (c) => c.city.toLowerCase() === city.toLowerCase()
  )?.county;

  // Reset district when city changes
  const handleCityChange = (newCity: string) => {
    onCityChange(newCity);
    if (newCity.toLowerCase() !== city.toLowerCase()) {
      onDistrictChange("");
    }
  };

  return (
    <div className="rounded-xl bg-white border border-gray-200 p-6">
      <h3 className="text-sm font-semibold text-gray-700 mb-4 flex items-center gap-2">
        <MapPin className="h-4 w-4 text-blue-500" />
        Lokacija
      </h3>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Combobox
          label="Grad / Općina"
          placeholder="Odaberite grad..."
          options={cityNames}
          value={city}
          onChange={handleCityChange}
          required
          county={selectedCounty}
        />
        <Combobox
          label="Naselje / Kvart"
          placeholder={city ? "Odaberite naselje..." : "Prvo odaberite grad"}
          options={settlements}
          value={district}
          onChange={onDistrictChange}
          disabled={!city || settlements.length === 0}
          hint="💡 Za precizniju procjenu, odaberite naselje"
        />
      </div>
    </div>
  );
}
