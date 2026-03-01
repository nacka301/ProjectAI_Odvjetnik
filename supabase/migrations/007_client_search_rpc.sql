-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 007: Client search RPC function
-- Fuzzy search on clients by name, OIB, or address.
-- Uses pg_trgm for similarity matching + ILIKE fallback.
-- Called by frontend: supabase.rpc('search_clients', { ... })
-- ═══════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION search_clients(
    p_user_id UUID,
    p_query TEXT,
    p_limit INT DEFAULT 20
)
RETURNS TABLE (
    id UUID,
    ime_naziv TEXT,
    oib TEXT,
    adresa TEXT,
    grad TEXT,
    telefon TEXT,
    email TEXT,
    tip TEXT,
    created_at TIMESTAMPTZ,
    similarity_score REAL
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    IF p_query IS NULL OR trim(p_query) = '' THEN
        -- No search term → return all for user, sorted by name
        RETURN QUERY
        SELECT c.id, c.ime_naziv, c.oib, c.adresa, c.grad, c.telefon, c.email, c.tip, c.created_at, 1.0::REAL
        FROM clients c
        WHERE c.user_id = p_user_id
        ORDER BY c.ime_naziv
        LIMIT p_limit;
    ELSE
        RETURN QUERY
        SELECT c.id, c.ime_naziv, c.oib, c.adresa, c.grad, c.telefon, c.email, c.tip, c.created_at,
               GREATEST(
                   similarity(c.ime_naziv, p_query),
                   CASE WHEN c.oib ILIKE '%' || p_query || '%' THEN 0.9 ELSE 0 END,
                   CASE WHEN c.adresa ILIKE '%' || p_query || '%' THEN 0.5 ELSE 0 END,
                   CASE WHEN c.grad ILIKE '%' || p_query || '%' THEN 0.4 ELSE 0 END
               ) AS similarity_score
        FROM clients c
        WHERE c.user_id = p_user_id
          AND (
              c.ime_naziv % p_query                        -- trigram similarity
              OR c.ime_naziv ILIKE '%' || p_query || '%'   -- substring match
              OR c.oib ILIKE '%' || p_query || '%'
              OR c.adresa ILIKE '%' || p_query || '%'
              OR c.grad ILIKE '%' || p_query || '%'
          )
        ORDER BY similarity_score DESC, c.ime_naziv
        LIMIT p_limit;
    END IF;
END;
$$;

GRANT EXECUTE ON FUNCTION search_clients(UUID, TEXT, INT) TO authenticated;
