# SYSTEM_AUDIT_LOG.md

**Audit Date:** 2025-07-10  
**Auditor:** Autonomous Full-Stack Audit Agent  
**Scope:** Full-stack scan — Frontend (Next.js), Backend (C# .NET 8), Supabase (PostgreSQL + RLS)

---

## Summary

| Metric                  | Value   |
|-------------------------|---------|
| Files scanned           | 60+     |
| Issues found            | 7       |
| Issues fixed            | 7       |
| Backend tests (post-fix)| 1128/1128 ✅ |
| Frontend build          | ✅ Compiled successfully |

---

## Findings & Fixes

### 1. 🔴 CRITICAL — Troskovnik snake_case / camelCase Mismatch

**File:** `frontend/app/troskovnik/page.tsx`  
**Severity:** Critical — entire feature broken at runtime  
**Root Cause:** Backend `Program.cs` configures `JsonNamingPolicy.SnakeCaseLower` globally, so ALL JSON responses use snake_case (`tarifni_broj`, `fiksni_body`, `ukupno_bodova`). The frontend TypeScript interfaces used camelCase (`tarifniBroj`, `fiksniBody`, `ukupnoBodova`), causing every field to resolve to `undefined`.

**Impact:**  
- All server response data rendered as blank/zero (reading `undefined` properties)  
- Request body sent camelCase keys (`vrijednostSpora`) — backend received default values  
- CSV export generated empty columns  

**Fix (30+ field changes):**  
- Converted all TypeScript interfaces to snake_case: `TarifnaStavka`, `TroskovnikLineItem`, `TroskovnikResponse`  
- Updated all field references in: `liveCalc` useMemo, `handleCalculate` request body, category filter logic, CSV export, catalog rendering, result display section  
- Local JavaScript variables (loop counters, state) kept camelCase (correct JS convention)

---

### 2. 🟠 HIGH — Auth Client Not Using SSR Cookie Flow

**File:** `frontend/lib/supabase.ts`  
**Severity:** High — dashboard/history may fail to authenticate  
**Root Cause:** The shared Supabase client used `createClient()` from `@supabase/supabase-js` instead of `createBrowserClient()` from `@supabase/ssr`. This client doesn't participate in the HttpOnly cookie auth flow set up by middleware.

**Impact:** Functions like `getRecentActivity()`, `getEvaluations()`, `getAuditResults()`, and all delete operations in the history page could fail silently when the auth token wasn't in the cookie jar.

**Fix:** Changed import and initialization:
```typescript
// Before
import { createClient } from "@supabase/supabase-js";
const supabase = createClient(url, key);

// After  
import { createBrowserClient } from "@supabase/ssr";
const supabase = createBrowserClient(url, key);
```

---

### 3. 🟠 HIGH — Empty x-user-id on Unauthenticated Delete (klijenti)

**File:** `frontend/app/klijenti/page.tsx`  
**Severity:** High — sends request with empty auth header  
**Root Cause:** `handleDelete` used `userId || ""` pattern, sending an empty `x-user-id` header if user state hadn't loaded yet.

**Fix:**  
- Added `if (!userId) return;` early guard  
- Removed `|| ""` fallback  
- Added try/catch with `setError()` for user-visible error feedback  

---

### 4. 🟠 HIGH — Empty x-user-id on Delete Case (klijenti/[id])

**File:** `frontend/app/klijenti/[id]/page.tsx`  
**Severity:** High — same pattern as #3  
**Root Cause:** `handleDeleteCase` used `userId || ""` with no null-check guard. `fetchProfile` also used `userId || ""` though it was incidentally safe due to a useEffect guard.

**Fix:**  
- `handleDeleteCase`: Added `if (!userId) return;` guard, removed `|| ""`, wrapped in try/catch  
- `fetchProfile`: Added explicit `if (!userId) return;` at function start, removed `|| ""` fallback  

---

### 5. 🟡 MEDIUM — Viewport in Metadata (Next.js 14+ Deprecation)

**File:** `frontend/app/layout.tsx`  
**Severity:** Medium — build warning, will break in future Next.js versions  
**Root Cause:** `viewport` was set inside the `Metadata` export object. Next.js 14+ requires a separate `export const viewport: Viewport` export.

**Fix:**  
```typescript
// Before
export const metadata: Metadata = {
  title: "...",
  viewport: "width=device-width, initial-scale=1, viewport-fit=cover",
};

// After
export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
};

export const metadata: Metadata = {
  title: "...",
  description: "...",
};
```

---

### 6. 🟡 MEDIUM — Dead Code: audit_logs Delete (RLS Blocked)

**File:** `frontend/lib/supabase.ts` — `deleteLegalStrategy()`  
**Severity:** Medium — silent failure, misleading code  
**Root Cause:** Function attempted `supabase.from("audit_logs").delete().eq("analysis_id", id)` before deleting the legal strategy. However, `audit_logs` RLS policy only grants SELECT to authenticated users (immutable for EU AI Act compliance). The delete silently fails with zero rows affected.

**Fix:** Removed the dead `audit_logs` delete line. Updated JSDoc comment.

---

### 7. ✅ VERIFIED — No Route Mismatches

**Scope:** All 40+ backend endpoints vs. all frontend API calls  
**Result:** Every `fetch()` URL in the frontend matches a registered controller route. No orphaned endpoints, no missing routes.

---

## Architecture Verified (No Issues Found)

| Area | Status |
|------|--------|
| RLS policies (14 tables) | ✅ All user tables enforce `auth.uid() = user_id` |
| Backend rate limiting | ✅ "valuation" 5/10min, "general" 30/min |
| CORS configuration | ✅ Frontend URL + Vercel previews + localhost |
| API key middleware | ✅ Optional, properly skips if not configured |
| SSE streaming | ✅ 6 controllers with proper Kestrel timeout config |
| Supabase service_role key | ✅ Server-side only, never exposed to frontend |
| Frontend auth middleware | ✅ Redirects unauthenticated users to /auth/login |

---

## Files Modified

| File | Changes |
|------|---------|
| `frontend/app/troskovnik/page.tsx` | 30+ field renames (camelCase → snake_case) |
| `frontend/lib/supabase.ts` | Auth client upgrade + dead code removal |
| `frontend/app/klijenti/page.tsx` | handleDelete null guard + error handling |
| `frontend/app/klijenti/[id]/page.tsx` | fetchProfile + handleDeleteCase null guards |
| `frontend/app/layout.tsx` | Viewport export separation |

---

## Test Results

```
Backend:  1128 passed, 0 failed, 0 skipped
Frontend: Compiled successfully — 19/19 pages generated
```
