/* ── Reusable skeleton primitives ── */

export function SkeletonBox({ className = "" }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-slate-200 ${className}`} />;
}

export function SkeletonCircle({ className = "" }: { className?: string }) {
  return <div className={`animate-pulse rounded-full bg-slate-200 ${className}`} />;
}

/* ── Klijenti card skeleton ── */
export function ClientCardSkeleton() {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 space-y-4">
      <div className="flex items-center gap-3">
        <SkeletonCircle className="h-10 w-10 shrink-0" />
        <div className="flex-1 space-y-2">
          <SkeletonBox className="h-4 w-3/5" />
          <SkeletonBox className="h-3 w-2/5" />
        </div>
      </div>
      <div className="space-y-2">
        <SkeletonBox className="h-3 w-4/5" />
        <SkeletonBox className="h-3 w-3/5" />
        <SkeletonBox className="h-3 w-2/5" />
      </div>
      <div className="flex items-center justify-between pt-2 border-t border-slate-100">
        <SkeletonBox className="h-3 w-20" />
        <SkeletonBox className="h-3 w-16" />
      </div>
    </div>
  );
}

export function ClientListSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: count }).map((_, i) => (
        <ClientCardSkeleton key={i} />
      ))}
    </div>
  );
}

/* ── History list item skeleton ── */
export function HistoryItemSkeleton() {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 space-y-3">
      <div className="flex items-start gap-3">
        <SkeletonCircle className="h-9 w-9 shrink-0" />
        <div className="flex-1 space-y-2">
          <div className="flex items-center justify-between">
            <SkeletonBox className="h-4 w-2/5" />
            <SkeletonBox className="h-3 w-20" />
          </div>
          <SkeletonBox className="h-3 w-4/5" />
          <SkeletonBox className="h-3 w-3/5" />
        </div>
      </div>
      <div className="flex items-center gap-2 pt-1">
        <SkeletonBox className="h-5 w-16 rounded-full" />
        <SkeletonBox className="h-5 w-20 rounded-full" />
      </div>
    </div>
  );
}

export function HistoryListSkeleton({ count = 5 }: { count?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: count }).map((_, i) => (
        <HistoryItemSkeleton key={i} />
      ))}
    </div>
  );
}

/* ── Client detail page skeleton ── */
export function ClientDetailSkeleton() {
  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header skeleton */}
      <div className="bg-white border-b border-slate-200">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6">
          <div className="flex items-center gap-4">
            <SkeletonCircle className="h-14 w-14 shrink-0" />
            <div className="flex-1 space-y-2">
              <SkeletonBox className="h-6 w-48" />
              <SkeletonBox className="h-3 w-32" />
            </div>
            <SkeletonBox className="h-9 w-28 rounded-lg" />
          </div>
        </div>
      </div>
      {/* Body skeleton */}
      <div className="max-w-5xl mx-auto px-4 sm:px-6 py-8 space-y-6">
        {/* Info cards */}
        <div className="grid gap-4 sm:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="rounded-xl border border-slate-200 bg-white p-5 space-y-3">
              <SkeletonBox className="h-3 w-20" />
              <SkeletonBox className="h-5 w-32" />
            </div>
          ))}
        </div>
        {/* Tabs */}
        <div className="flex gap-2">
          <SkeletonBox className="h-9 w-24 rounded-lg" />
          <SkeletonBox className="h-9 w-24 rounded-lg" />
          <SkeletonBox className="h-9 w-24 rounded-lg" />
        </div>
        {/* List items */}
        <div className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="rounded-xl border border-slate-200 bg-white p-5 space-y-3">
              <div className="flex items-center gap-3">
                <SkeletonCircle className="h-9 w-9" />
                <div className="flex-1 space-y-2">
                  <SkeletonBox className="h-4 w-2/5" />
                  <SkeletonBox className="h-3 w-1/3" />
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
