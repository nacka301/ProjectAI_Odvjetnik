"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { Loader2, AlertTriangle, Check } from "lucide-react";

// ═══════════════════════════════════════════
//  SHARED PROCESSING VIEW — V58
//  Reusable full-screen processing overlay for all modules.
//  Same design as wizard/ProcessingView — animated blobs,
//  glassmorphism card, vertical step pipeline.
// ═══════════════════════════════════════════

export interface ProcessingStage {
  id: string;
  label: string;
  status: "pending" | "active" | "done" | "skipped" | "error";
  detail?: string;
  error?: string;
}

interface SharedProcessingViewProps {
  stages: ProcessingStage[];
  isStreaming: boolean;
  error: string | null;
  onCancel: () => void;
  onReset: () => void;
  /** Title displayed in the header — e.g. "Analiza ugovora u tijeku" */
  title: string;
  /** Subtitle below the title */
  subtitle?: string;
  /** Header icon rendered in the circle */
  headerIcon?: ReactNode;
  /** Stage icons keyed by stage id */
  stageIcons?: Record<string, ReactNode>;
  /** Micro-copy strings keyed by stage id (rotating messages) */
  stageMicrocopy?: Record<string, string[]>;
  /** Theme — gradient colors for background and accents */
  theme?: "emerald" | "indigo" | "blue" | "purple" | "amber";
}

// Theme presets
const THEMES = {
  emerald: {
    bg: "linear-gradient(135deg, #ecfdf5 0%, #d1fae5 50%, #f0fdf4 100%)",
    blobs: [
      "radial-gradient(circle, rgba(16,185,129,0.2) 0%, rgba(5,150,105,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(52,211,153,0.2) 0%, rgba(16,185,129,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(110,231,183,0.15) 0%, rgba(52,211,153,0.06) 70%, transparent 100%)",
    ],
    headerGlow: "linear-gradient(135deg, rgba(16,185,129,0.3), rgba(5,150,105,0.3))",
    headerBg: "linear-gradient(135deg, #10b981, #059669)",
    titleGradient: "linear-gradient(135deg, #065f46, #047857, #059669)",
    activeCircleBg: "linear-gradient(135deg, #ecfdf5, #d1fae5)",
    activeCircleBorder: "#34d399",
    activeTextColor: "text-emerald-700",
    activeMicroColor: "text-emerald-400",
    activeSpinColor: "text-emerald-600",
  },
  indigo: {
    bg: "linear-gradient(135deg, #f0f4ff 0%, #e8ecf8 50%, #f5f0ff 100%)",
    blobs: [
      "radial-gradient(circle, rgba(99,102,241,0.2) 0%, rgba(59,130,246,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(139,92,246,0.2) 0%, rgba(124,58,237,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(6,182,212,0.15) 0%, rgba(59,130,246,0.06) 70%, transparent 100%)",
    ],
    headerGlow: "linear-gradient(135deg, rgba(59,130,246,0.3), rgba(99,102,241,0.3))",
    headerBg: "linear-gradient(135deg, #3b82f6, #6366f1)",
    titleGradient: "linear-gradient(135deg, #1e40af, #4f46e5, #7c3aed)",
    activeCircleBg: "linear-gradient(135deg, #eff6ff, #eef2ff)",
    activeCircleBorder: "#818cf8",
    activeTextColor: "text-indigo-700",
    activeMicroColor: "text-indigo-400",
    activeSpinColor: "text-indigo-600",
  },
  blue: {
    bg: "linear-gradient(135deg, #eff6ff 0%, #dbeafe 50%, #f0f9ff 100%)",
    blobs: [
      "radial-gradient(circle, rgba(59,130,246,0.2) 0%, rgba(37,99,235,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(96,165,250,0.2) 0%, rgba(59,130,246,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(147,197,253,0.15) 0%, rgba(96,165,250,0.06) 70%, transparent 100%)",
    ],
    headerGlow: "linear-gradient(135deg, rgba(59,130,246,0.3), rgba(37,99,235,0.3))",
    headerBg: "linear-gradient(135deg, #3b82f6, #2563eb)",
    titleGradient: "linear-gradient(135deg, #1e3a8a, #1d4ed8, #2563eb)",
    activeCircleBg: "linear-gradient(135deg, #eff6ff, #dbeafe)",
    activeCircleBorder: "#60a5fa",
    activeTextColor: "text-blue-700",
    activeMicroColor: "text-blue-400",
    activeSpinColor: "text-blue-600",
  },
  purple: {
    bg: "linear-gradient(135deg, #faf5ff 0%, #f3e8ff 50%, #fdf4ff 100%)",
    blobs: [
      "radial-gradient(circle, rgba(168,85,247,0.2) 0%, rgba(139,92,246,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(192,132,252,0.2) 0%, rgba(168,85,247,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(216,180,254,0.15) 0%, rgba(192,132,252,0.06) 70%, transparent 100%)",
    ],
    headerGlow: "linear-gradient(135deg, rgba(168,85,247,0.3), rgba(139,92,246,0.3))",
    headerBg: "linear-gradient(135deg, #a855f7, #8b5cf6)",
    titleGradient: "linear-gradient(135deg, #581c87, #7e22ce, #8b5cf6)",
    activeCircleBg: "linear-gradient(135deg, #faf5ff, #f3e8ff)",
    activeCircleBorder: "#c084fc",
    activeTextColor: "text-purple-700",
    activeMicroColor: "text-purple-400",
    activeSpinColor: "text-purple-600",
  },
  amber: {
    bg: "linear-gradient(135deg, #fffbeb 0%, #fef3c7 50%, #fefce8 100%)",
    blobs: [
      "radial-gradient(circle, rgba(245,158,11,0.2) 0%, rgba(217,119,6,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(251,191,36,0.2) 0%, rgba(245,158,11,0.08) 70%, transparent 100%)",
      "radial-gradient(circle, rgba(252,211,77,0.15) 0%, rgba(251,191,36,0.06) 70%, transparent 100%)",
    ],
    headerGlow: "linear-gradient(135deg, rgba(245,158,11,0.3), rgba(217,119,6,0.3))",
    headerBg: "linear-gradient(135deg, #f59e0b, #d97706)",
    titleGradient: "linear-gradient(135deg, #78350f, #92400e, #b45309)",
    activeCircleBg: "linear-gradient(135deg, #fffbeb, #fef3c7)",
    activeCircleBorder: "#fbbf24",
    activeTextColor: "text-amber-700",
    activeMicroColor: "text-amber-400",
    activeSpinColor: "text-amber-600",
  },
};

export default function SharedProcessingView({
  stages,
  isStreaming,
  error,
  onCancel,
  onReset,
  title,
  subtitle,
  headerIcon,
  stageIcons,
  stageMicrocopy,
  theme = "indigo",
}: SharedProcessingViewProps) {
  const t = THEMES[theme];

  // Dynamic micro-copy cycling for active stage
  const [microCopy, setMicroCopy] = useState("");
  const microCopyIdx = useRef(0);

  useEffect(() => {
    const activeStage = stages.find((s) => s.status === "active");
    if (!activeStage || !stageMicrocopy) {
      setMicroCopy("");
      return;
    }

    const copies = stageMicrocopy[activeStage.id] || [];
    if (copies.length === 0) {
      setMicroCopy("");
      return;
    }

    microCopyIdx.current = 0;
    setMicroCopy(copies[0]);

    const interval = setInterval(() => {
      microCopyIdx.current = (microCopyIdx.current + 1) % copies.length;
      setMicroCopy(copies[microCopyIdx.current]);
    }, 2500);

    return () => clearInterval(interval);
  }, [stages, stageMicrocopy]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden"
      style={{ background: t.bg }}
    >
      {/* Animated background blobs */}
      <div className="absolute inset-0 pointer-events-none overflow-hidden">
        <div
          className="absolute rounded-full"
          style={{
            top: "-8rem", left: "-8rem", width: "22rem", height: "22rem",
            background: t.blobs[0], filter: "blur(60px)",
            animation: "float-blob 8s ease-in-out infinite",
          }}
        />
        <div
          className="absolute rounded-full"
          style={{
            bottom: "-6rem", right: "-6rem", width: "20rem", height: "20rem",
            background: t.blobs[1], filter: "blur(60px)",
            animation: "float-blob 10s ease-in-out 2s infinite",
          }}
        />
        <div
          className="absolute rounded-full"
          style={{
            top: "40%", left: "25%", width: "16rem", height: "16rem",
            background: t.blobs[2], filter: "blur(60px)",
            animation: "float-blob 12s ease-in-out 4s infinite",
          }}
        />
      </div>

      {/* Card */}
      <div
        className="relative z-10 w-full max-w-lg mx-4"
        style={{
          background: "rgba(255,255,255,0.85)",
          backdropFilter: "blur(24px)",
          WebkitBackdropFilter: "blur(24px)",
          borderRadius: "1.25rem",
          boxShadow: "0 12px 48px rgba(0,0,0,0.08), 0 2px 12px rgba(0,0,0,0.04)",
          border: "1px solid rgba(255,255,255,0.6)",
          padding: "1.75rem 1.5rem",
        }}
      >
        {/* Header */}
        <div className="text-center mb-5">
          <div className="relative mx-auto h-12 w-12 mb-3">
            <div
              className="absolute inset-0 rounded-full"
              style={{ background: t.headerGlow, animation: "pulse-glow 2s ease-in-out infinite" }}
            />
            <div
              className="relative flex items-center justify-center h-full w-full rounded-full"
              style={{ background: t.headerBg }}
            >
              {headerIcon || <Loader2 className="h-5 w-5 animate-spin text-white" />}
            </div>
          </div>
          <h2
            className="text-xl sm:text-2xl font-bold"
            style={{
              background: t.titleGradient,
              WebkitBackgroundClip: "text",
              WebkitTextFillColor: "transparent",
              backgroundClip: "text",
            }}
          >
            {title}
          </h2>
          {subtitle && (
            <p className="text-xs text-gray-500 mt-1">{subtitle}</p>
          )}
        </div>

        {/* Vertical Step Pipeline */}
        <div className="relative">
          {stages.map((s, idx) => {
            const icon = stageIcons?.[s.id];
            const isDone = s.status === "done";
            const isActive = s.status === "active";
            const isError = s.status === "error";
            const isSkipped = s.status === "skipped";

            return (
              <div key={s.id} className="flex gap-3" style={{ paddingBottom: idx < stages.length - 1 ? "0.75rem" : 0 }}>
                {/* Icon circle + connector line */}
                <div className="flex flex-col items-center">
                  <div
                    className="relative h-9 w-9 rounded-full flex items-center justify-center border-2 transition-all duration-500"
                    style={{
                      ...(isDone ? {
                        background: "#f0fdf4", borderColor: "#4ade80",
                        animation: "pop-in 0.4s cubic-bezier(0.34,1.56,0.64,1) forwards",
                      } : isActive ? {
                        background: t.activeCircleBg,
                        borderColor: t.activeCircleBorder,
                        animation: "pulse-glow 2s ease-in-out infinite",
                      } : isError ? {
                        background: "#fef2f2", borderColor: "#f87171",
                      } : isSkipped ? {
                        background: "#fefce8", borderColor: "#fbbf24",
                      } : {
                        background: "#f9fafb", borderColor: "#e5e7eb",
                      }),
                    }}
                  >
                    {isActive ? (
                      <span className={`${t.activeSpinColor} animate-spin`}>
                        {icon || <Loader2 className="h-4 w-4" />}
                      </span>
                    ) : isDone ? (
                      <Check className="h-4 w-4 text-green-500" />
                    ) : isError ? (
                      <AlertTriangle className="h-4 w-4 text-red-500" />
                    ) : isSkipped ? (
                      <Check className="h-4 w-4 text-amber-500" />
                    ) : (
                      <span className="text-gray-300">
                        {icon || <span className="text-sm font-bold">{idx + 1}</span>}
                      </span>
                    )}
                  </div>
                  {idx < stages.length - 1 && (
                    <div className="relative w-0.5 flex-1 bg-gray-200 rounded-full overflow-hidden" style={{ minHeight: "1rem" }}>
                      {(isDone || isSkipped) && (
                        <div
                          className="absolute inset-0 rounded-full"
                          style={{
                            background: isDone
                              ? "linear-gradient(to bottom, #4ade80, #86efac)"
                              : "linear-gradient(to bottom, #fbbf24, #fde68a)",
                            animation: "line-fill 0.5s ease-out forwards",
                            transformOrigin: "top",
                          }}
                        />
                      )}
                    </div>
                  )}
                </div>

                {/* Text content */}
                <div className="pt-1 flex-1 min-w-0">
                  <p className={`text-sm font-semibold transition-colors duration-300 ${
                    isActive ? t.activeTextColor :
                    isDone ? "text-green-700" :
                    isError ? "text-red-700" :
                    isSkipped ? "text-amber-600" : "text-gray-400"
                  }`}>
                    {s.label}
                  </p>
                  {isActive && microCopy && (
                    <p
                      key={microCopy}
                      className={`text-xs ${t.activeMicroColor} mt-0.5`}
                      style={{ animation: "fade-up 0.3s ease-out forwards" }}
                    >
                      {microCopy}
                    </p>
                  )}
                  {s.detail && !isActive && <p className="text-xs text-gray-500 mt-0.5">{s.detail}</p>}
                  {s.error && <p className="text-xs text-red-600 mt-0.5">{s.error}</p>}
                </div>
              </div>
            );
          })}
        </div>

        {/* Error recovery */}
        {error && !isStreaming && (
          <div className="mt-5 text-center space-y-2">
            <div className="rounded-lg border border-red-300 bg-red-50 p-4">
              <p className="text-sm text-red-700">{error}</p>
            </div>
            <button onClick={onReset} className="rounded-lg bg-gray-100 px-5 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 transition">
              ← Pokušaj ponovno
            </button>
          </div>
        )}

        {/* Cancel button */}
        {isStreaming && (
          <div className="mt-5 text-center">
            <button
              onClick={onCancel}
              className="rounded-lg border border-gray-300 bg-white px-5 py-2 text-sm font-medium text-gray-600 hover:bg-red-50 hover:border-red-300 hover:text-red-700 transition"
            >
              ✕ Odustani
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
