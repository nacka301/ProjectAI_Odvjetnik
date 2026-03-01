-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 011: Monthly revenue reporting view
-- Materialized view for fast dashboard reporting.
-- Aggregates troškovnici + invoices per month per user.
-- Refresh: REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue;
-- ═══════════════════════════════════════════════════════════════════

-- Drop if exists to allow re-running
DROP MATERIALIZED VIEW IF EXISTS monthly_revenue;

CREATE MATERIALIZED VIEW monthly_revenue AS
SELECT
    user_id,
    date_trunc('month', created_at)::DATE AS month,
    -- Troškovnici stats
    COUNT(*) FILTER (WHERE source = 'troskovnik')           AS troskovnik_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'troskovnik'), 0) AS troskovnik_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'troskovnik'), 0)   AS troskovnik_eur_s_pdv,
    -- Invoice stats
    COUNT(*) FILTER (WHERE source = 'invoice')              AS invoice_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'invoice'), 0)    AS invoice_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'invoice'), 0)      AS invoice_eur_s_pdv
FROM (
    -- Troškovnici
    SELECT user_id, created_at, 'troskovnik' AS source,
           ukupno_eur_bez_pdv AS amount_bez_pdv,
           ukupno_eur_s_pdv AS amount_s_pdv
    FROM troskovnici
    UNION ALL
    -- Invoices (exclude cancelled)
    SELECT user_id, created_at, 'invoice' AS source,
           net_amount AS amount_bez_pdv,
           gross_amount AS amount_s_pdv
    FROM invoices
    WHERE status != 'CANCELLED'
) combined
GROUP BY user_id, date_trunc('month', created_at)::DATE
ORDER BY month DESC;

-- Unique index needed for CONCURRENTLY refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_monthly_revenue_user_month
    ON monthly_revenue (user_id, month);

-- Index for quick user lookups
CREATE INDEX IF NOT EXISTS idx_monthly_revenue_user
    ON monthly_revenue (user_id);

-- ── RPC to get monthly revenue for a user ──
CREATE OR REPLACE FUNCTION get_monthly_revenue(
    p_user_id UUID,
    p_months INT DEFAULT 12
)
RETURNS TABLE (
    month DATE,
    troskovnik_count BIGINT,
    troskovnik_eur_bez_pdv NUMERIC,
    troskovnik_eur_s_pdv NUMERIC,
    invoice_count BIGINT,
    invoice_eur_bez_pdv NUMERIC,
    invoice_eur_s_pdv NUMERIC
)
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT mr.month, mr.troskovnik_count, mr.troskovnik_eur_bez_pdv, mr.troskovnik_eur_s_pdv,
           mr.invoice_count, mr.invoice_eur_bez_pdv, mr.invoice_eur_s_pdv
    FROM monthly_revenue mr
    WHERE mr.user_id = p_user_id
    ORDER BY mr.month DESC
    LIMIT p_months;
$$;

GRANT EXECUTE ON FUNCTION get_monthly_revenue(UUID, INT) TO authenticated;

-- ═══════════════════════════════════════════════════════════════════
-- NOTE: To keep the view fresh, set up a Supabase cron job:
--   SELECT cron.schedule(
--       'refresh-monthly-revenue',
--       '0 2 * * *',    -- daily at 2 AM
--       'REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue'
--   );
-- Or call REFRESH manually after bulk inserts.
-- ═══════════════════════════════════════════════════════════════════
