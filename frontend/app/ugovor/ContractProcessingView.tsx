"use client";

import { useEffect, useRef, useState } from "react";
import { Loader2, AlertTriangle, Check, Scale } from "lucide-react";
import type { ContractStage } from "./contractConstants";
import { CONTRACT_STAGE_ICONS, CONTRACT_STAGE_MICROCOPY } from "./contractConstants";

interface ContractProcessingViewProps {
  stages: ContractStage[];
  isStreaming: boolean;
  error: string | null;
  onCancel: () => void;
  onReset: () => void;
}

export default function ContractProcessingView({
  stages,
  isStreaming,
  error,
  onCancel,
  onReset,
}: ContractProcessingViewProps) {
  // Dynamic micro-copy cycling for active stage
  const [microCopy, setMicroCopy] = useState("");
  const microCopyIdx = useRef(0);

  useEffect(() => {
    const activeStage = stages.find((s) => s.status === "active");
    if (!activeStage) {
      setMicroCopy("");
      return;
    }

    const copies = CONTRACT_STAGE_MICROCOPY[activeStage.id] || [];
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
  }, [stages]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden"
      style={{
        background:
          "linear-gradient(135deg, #ecfdf5 0%, #d1fae5 50%, #f0fdf4 100%)",
      }}
    >
      {/* Animated background blobs — emerald theme */}
      <div className="absolute inset-0 pointer-events-none overflow-hidden">
        <div
          className="absolute rounded-full"
          style={{
            top: "-8rem",
            left: "-8rem",
            width: "22rem",
            height: "22rem",
            background:
              "radial-gradient(circle, rgba(16,185,129,0.2) 0%, rgba(5,150,105,0.08) 70%, transparent 100%)",
            filter: "blur(60px)",
            animation: "float-blob 8s ease-in-out infinite",
          }}
        />
        <div
          className="absolute rounded-full"
          style={{
            bottom: "-6rem",
            right: "-6rem",
            width: "20rem",
            height: "20rem",
            background:
              "radial-gradient(circle, rgba(52,211,153,0.2) 0%, rgba(16,185,129,0.08) 70%, transparent 100%)",
            filter: "blur(60px)",
            animation: "float-blob 10s ease-in-out 2s infinite",
          }}
        />
        <div
          className="absolute rounded-full"
          style={{
            top: "40%",
            left: "25%",
            width: "16rem",
            height: "16rem",
            background:
              "radial-gradient(circle, rgba(110,231,183,0.15) 0%, rgba(52,211,153,0.06) 70%, transparent 100%)",
            filter: "blur(60px)",
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
          boxShadow:
            "0 12px 48px rgba(0,0,0,0.08), 0 2px 12px rgba(0,0,0,0.04)",
          border: "1px solid rgba(255,255,255,0.6)",
          padding: "1.75rem 1.5rem",
        }}
      >
        {/* Header */}
        <div className="text-center mb-5">
          <div className="relative mx-auto h-12 w-12 mb-3">
            <div
              className="absolute inset-0 rounded-full"
              style={{
                background:
                  "linear-gradient(135deg, rgba(16,185,129,0.3), rgba(5,150,105,0.3))",
                animation: "pulse-glow 2s ease-in-out infinite",
              }}
            />
            <div
              className="relative flex items-center justify-center h-full w-full rounded-full"
              style={{
                background: "linear-gradient(135deg, #10b981, #059669)",
              }}
            >
              {isStreaming ? (
                <Loader2 className="h-5 w-5 animate-spin text-white" />
              ) : (
                <Scale className="h-5 w-5 text-white" />
              )}
            </div>
          </div>
          <h2
            className="text-xl sm:text-2xl font-bold"
            style={{
              background:
                "linear-gradient(135deg, #065f46, #047857, #059669)",
              WebkitBackgroundClip: "text",
              WebkitTextFillColor: "transparent",
              backgroundClip: "text",
            }}
          >
            Analiza ugovora u tijeku
          </h2>
          <p className="text-xs text-gray-500 mt-1">
            AI pravni revizor provjerava vaš ugovor
          </p>
        </div>

        {/* Vertical Step Pipeline */}
        <div className="relative">
          {stages.map((s, idx) => {
            const icon = CONTRACT_STAGE_ICONS[s.id];
            const isDone = s.status === "done";
            const isActive = s.status === "active";
            const isError = s.status === "error";

            return (
              <div
                key={s.id}
                className="flex gap-3"
                style={{
                  paddingBottom:
                    idx < stages.length - 1 ? "0.75rem" : 0,
                }}
              >
                {/* Icon circle + connector line */}
                <div className="flex flex-col items-center">
                  <div
                    className="relative h-9 w-9 rounded-full flex items-center justify-center border-2 transition-all duration-500"
                    style={{
                      ...(isDone
                        ? {
                            background: "#f0fdf4",
                            borderColor: "#4ade80",
                            animation:
                              "pop-in 0.4s cubic-bezier(0.34,1.56,0.64,1) forwards",
                          }
                        : isActive
                          ? {
                              background:
                                "linear-gradient(135deg, #ecfdf5, #d1fae5)",
                              borderColor: "#34d399",
                              animation:
                                "pulse-glow 2s ease-in-out infinite",
                            }
                          : isError
                            ? {
                                background: "#fef2f2",
                                borderColor: "#f87171",
                              }
                            : {
                                background: "#f9fafb",
                                borderColor: "#e5e7eb",
                              }),
                    }}
                  >
                    {isActive ? (
                      <span className="text-emerald-600 animate-spin">
                        {icon || <Loader2 className="h-4 w-4" />}
                      </span>
                    ) : isDone ? (
                      <Check className="h-4 w-4 text-green-500" />
                    ) : isError ? (
                      <AlertTriangle className="h-4 w-4 text-red-500" />
                    ) : (
                      <span className="text-gray-300">
                        {icon || (
                          <span className="text-sm font-bold">
                            {idx + 1}
                          </span>
                        )}
                      </span>
                    )}
                  </div>
                  {idx < stages.length - 1 && (
                    <div
                      className="relative w-0.5 flex-1 bg-gray-200 rounded-full overflow-hidden"
                      style={{ minHeight: "1rem" }}
                    >
                      {isDone && (
                        <div
                          className="absolute inset-0 rounded-full"
                          style={{
                            background:
                              "linear-gradient(to bottom, #4ade80, #86efac)",
                            animation:
                              "line-fill 0.5s ease-out forwards",
                            transformOrigin: "top",
                          }}
                        />
                      )}
                    </div>
                  )}
                </div>

                {/* Text content */}
                <div className="pt-1 flex-1 min-w-0">
                  <p
                    className={`text-sm font-semibold transition-colors duration-300 ${
                      isActive
                        ? "text-emerald-700"
                        : isDone
                          ? "text-green-700"
                          : isError
                            ? "text-red-700"
                            : "text-gray-400"
                    }`}
                  >
                    {s.label}
                  </p>
                  {isActive && microCopy && (
                    <p
                      key={microCopy}
                      className="text-xs text-emerald-400 mt-0.5"
                      style={{
                        animation: "fade-up 0.3s ease-out forwards",
                      }}
                    >
                      {microCopy}
                    </p>
                  )}
                  {s.detail && !isActive && (
                    <p className="text-xs text-gray-500 mt-0.5">
                      {s.detail}
                    </p>
                  )}
                  {s.error && (
                    <p className="text-xs text-red-600 mt-0.5">
                      {s.error}
                    </p>
                  )}
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
            <button
              onClick={onReset}
              className="rounded-lg bg-gray-100 px-5 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 transition"
            >
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
