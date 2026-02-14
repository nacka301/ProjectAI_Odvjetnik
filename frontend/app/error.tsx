"use client";

import { useEffect } from "react";

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Unhandled error:", error);
  }, [error]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="max-w-md w-full text-center space-y-6">
        <div className="text-5xl">⚠️</div>
        <h2 className="text-xl font-bold text-gray-900">Nešto je pošlo po krivu</h2>
        <p className="text-sm text-gray-500">
          Došlo je do neočekivane greške. Pokušajte ponovno ili se vratite na početnu stranicu.
        </p>
        <div className="flex items-center justify-center gap-3">
          <button
            onClick={reset}
            className="rounded-lg bg-indigo-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-indigo-700 transition"
          >
            Pokušaj ponovno
          </button>
          <a
            href="/"
            className="rounded-lg border border-gray-300 bg-white px-5 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition"
          >
            Početna stranica
          </a>
        </div>
      </div>
    </div>
  );
}
