/**
 * useThrottledStages — Prevents SSE re-render storms.
 *
 * Problem: SSE progress events fire 20–50x/second. Each `setStages` call triggers
 * a full React re-render of 1000–2000 LOC page components.
 *
 * Solution: Buffer progress updates in a mutable ref and flush to React state via
 * requestAnimationFrame (once per paint frame ≈ 60 fps). Error/complete/reset
 * events bypass the throttle and flush immediately.
 *
 * Usage:
 *   const { stages, setStages, applyProgress } = useThrottledStages<StreamStage>(initialStages);
 *   // In SSE handler:
 *   if (evt.type === "progress") applyProgress(stage, { status, label, detail });
 *   if (evt.type === "error")    setStages(prev => prev.map(...));  // immediate
 */

import { useState, useRef, useCallback } from "react";

/** Minimal shape every stage type satisfies */
interface StageBase {
  id: string;
  label: string;
  status: string;
  detail?: string;
  error?: string;
}

interface ProgressPayload {
  status: string;
  label?: string;
  detail?: string;
}

/**
 * Returns throttled stage management suitable for SSE streaming pages.
 *
 * @param initialStages - The stages array to initialize state with (pass [] for lazy init).
 */
export function useThrottledStages<T extends StageBase>(initialStages: T[] = [] as T[]) {
  const [stages, setStages] = useState<T[]>(initialStages);

  // Accumulator: stage-id → latest partial update (only progress events)
  const pendingRef = useRef<Map<string, ProgressPayload>>(new Map());
  const rafRef = useRef<number | null>(null);

  /** Flush all pending progress updates into React state in a single batch */
  const flush = useCallback(() => {
    rafRef.current = null;
    const pending = pendingRef.current;
    if (pending.size === 0) return;

    // Snapshot and clear before setState to avoid stale captures
    const updates = new Map(pending);
    pending.clear();

    setStages((prev) =>
      prev.map((s) => {
        const u = updates.get(s.id);
        if (!u) return s;
        return {
          ...s,
          status: u.status,
          label: u.label || s.label,
          detail: u.detail ?? s.detail,
        } as T;
      })
    );
  }, []);

  /**
   * Queue a progress update. Multiple updates for the same stage within one frame
   * are coalesced (latest wins). Flushed once per rAF paint.
   */
  const applyProgress = useCallback(
    (stageId: string, payload: ProgressPayload) => {
      pendingRef.current.set(stageId, payload);
      if (rafRef.current === null) {
        rafRef.current = requestAnimationFrame(flush);
      }
    },
    [flush]
  );

  /**
   * Force-flush any buffered progress updates and cancel pending rAF.
   * Call this when the stream ends so the final stage state is guaranteed correct.
   */
  const flushNow = useCallback(() => {
    if (rafRef.current !== null) {
      cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
    }
    // Flush remaining
    const pending = pendingRef.current;
    if (pending.size === 0) return;
    const updates = new Map(pending);
    pending.clear();
    setStages((prev) =>
      prev.map((s) => {
        const u = updates.get(s.id);
        if (!u) return s;
        return {
          ...s,
          status: u.status,
          label: u.label || s.label,
          detail: u.detail ?? s.detail,
        } as T;
      })
    );
  }, []);

  return { stages, setStages, applyProgress, flushNow };
}
