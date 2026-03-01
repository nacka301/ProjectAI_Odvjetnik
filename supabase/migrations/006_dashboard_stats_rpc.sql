-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 006: Dashboard stats RPC function
-- Returns a single JSON object with counts and totals for the
-- user's dashboard: # clients, # cases, # troškovnici, $ sum, etc.
-- Called by frontend: supabase.rpc('get_dashboard_stats')
-- ═══════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION get_dashboard_stats(p_user_id UUID)
RETURNS JSON
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    result JSON;
BEGIN
    SELECT json_build_object(
        'total_clients',       (SELECT COUNT(*) FROM clients WHERE user_id = p_user_id),
        'total_cases',         (SELECT COUNT(*) FROM cases WHERE user_id = p_user_id),
        'active_cases',        (SELECT COUNT(*) FROM cases WHERE user_id = p_user_id AND status = 'aktivan'),
        'total_troskovnici',   (SELECT COUNT(*) FROM troskovnici WHERE user_id = p_user_id),
        'total_eur_bez_pdv',   COALESCE((SELECT SUM(ukupno_eur_bez_pdv) FROM troskovnici WHERE user_id = p_user_id), 0),
        'total_eur_s_pdv',     COALESCE((SELECT SUM(ukupno_eur_s_pdv) FROM troskovnici WHERE user_id = p_user_id), 0),
        'total_evaluations',   (SELECT COUNT(*) FROM evaluations WHERE user_id = p_user_id),
        'total_contracts',     (SELECT COUNT(*) FROM contract_analyses WHERE user_id = p_user_id),
        'total_zk_analyses',   (SELECT COUNT(*) FROM zk_analyses WHERE user_id = p_user_id),
        'total_invoices',      (SELECT COUNT(*) FROM invoices WHERE user_id = p_user_id),
        'invoices_draft',      (SELECT COUNT(*) FROM invoices WHERE user_id = p_user_id AND status = 'DRAFT'),
        'invoices_issued',     (SELECT COUNT(*) FROM invoices WHERE user_id = p_user_id AND status = 'ISSUED'),
        'invoices_total_eur',  COALESCE((SELECT SUM(gross_amount) FROM invoices WHERE user_id = p_user_id AND status != 'CANCELLED'), 0),
        'recent_troskovnici',  (
            SELECT COALESCE(json_agg(t), '[]'::json) FROM (
                SELECT id, created_at, opis_predmeta, ukupno_eur_s_pdv
                FROM troskovnici WHERE user_id = p_user_id
                ORDER BY created_at DESC LIMIT 5
            ) t
        ),
        'recent_cases',  (
            SELECT COALESCE(json_agg(c), '[]'::json) FROM (
                SELECT id, created_at, naziv, broj_predmeta, status, vrsta
                FROM cases WHERE user_id = p_user_id
                ORDER BY created_at DESC LIMIT 5
            ) c
        )
    ) INTO result;

    RETURN result;
END;
$$;

-- Grant execute to authenticated users
GRANT EXECUTE ON FUNCTION get_dashboard_stats(UUID) TO authenticated;
