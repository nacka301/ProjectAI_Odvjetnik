-- ========================================
-- V69: Semantic RAG — pgvector law chunks table
-- Run this in Supabase SQL Editor
-- Prerequisites: Supabase project with pgvector extension
-- ========================================

-- Enable pgvector extension (available on all Supabase plans)
CREATE EXTENSION IF NOT EXISTS vector;

-- Law chunks table — stores individual law articles with embeddings
CREATE TABLE IF NOT EXISTS law_chunks (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    law                 TEXT            NOT NULL,           -- 'ZOO', 'ZV', 'ZZK'
    full_name           TEXT            NOT NULL,           -- 'Zakon o obveznim odnosima'
    nn_reference        TEXT            NOT NULL,           -- 'NN 35/05, ...'
    article             TEXT            NOT NULL,           -- '303', '41', etc.
    title               TEXT            NOT NULL,           -- 'Kapara', 'Clausula intabulandi'
    content             TEXT            NOT NULL,           -- Full article text
    topic_tags          TEXT[]          NOT NULL DEFAULT '{}', -- ['kapara', 'plaćanje']
    embedding           vector(1536),                      -- OpenAI text-embedding-3-small
    UNIQUE(law, article)                                   -- One entry per law+article
);

-- Vector similarity search index (IVFFlat — good for <100K rows)
-- Use lists=10 for small datasets (<1000 rows)
CREATE INDEX IF NOT EXISTS law_chunks_embedding_idx 
    ON law_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);

-- Full-text search index (fallback when embeddings unavailable)
CREATE INDEX IF NOT EXISTS law_chunks_content_fts_idx 
    ON law_chunks USING GIN (to_tsvector('simple', content));

-- Tag-based filtering index
CREATE INDEX IF NOT EXISTS law_chunks_tags_idx 
    ON law_chunks USING GIN (topic_tags);

-- Fast lookup by law abbreviation
CREATE INDEX IF NOT EXISTS law_chunks_law_idx ON law_chunks (law);

-- ========================================
-- RPC function: similarity search
-- Called from backend via POST /rest/v1/rpc/match_law_chunks
-- ========================================

CREATE OR REPLACE FUNCTION match_law_chunks(
    query_embedding vector(1536),
    match_threshold float DEFAULT 0.3,
    match_count int DEFAULT 10
)
RETURNS TABLE (
    id uuid,
    law text,
    full_name text,
    nn_reference text,
    article text,
    title text,
    content text,
    topic_tags text[],
    similarity float
)
LANGUAGE sql STABLE
AS $$
    SELECT
        lc.id,
        lc.law,
        lc.full_name,
        lc.nn_reference,
        lc.article,
        lc.title,
        lc.content,
        lc.topic_tags,
        1 - (lc.embedding <=> query_embedding) AS similarity
    FROM law_chunks lc
    WHERE lc.embedding IS NOT NULL
      AND 1 - (lc.embedding <=> query_embedding) > match_threshold
    ORDER BY lc.embedding <=> query_embedding
    LIMIT match_count;
$$;

-- ========================================
-- RPC function: full-text search (fallback)
-- Used when embeddings are not available
-- ========================================

CREATE OR REPLACE FUNCTION search_law_chunks_text(
    search_query text,
    max_results int DEFAULT 10
)
RETURNS TABLE (
    id uuid,
    law text,
    full_name text,
    nn_reference text,
    article text,
    title text,
    content text,
    topic_tags text[],
    rank float
)
LANGUAGE sql STABLE
AS $$
    SELECT
        lc.id,
        lc.law,
        lc.full_name,
        lc.nn_reference,
        lc.article,
        lc.title,
        lc.content,
        lc.topic_tags,
        ts_rank(to_tsvector('simple', lc.content), plainto_tsquery('simple', search_query))::float AS rank
    FROM law_chunks lc
    WHERE to_tsvector('simple', lc.content) @@ plainto_tsquery('simple', search_query)
    ORDER BY rank DESC
    LIMIT max_results;
$$;

-- ========================================
-- RPC function: tag-based search
-- Find articles by topic tags
-- ========================================

CREATE OR REPLACE FUNCTION search_law_chunks_by_tags(
    search_tags text[],
    max_results int DEFAULT 10
)
RETURNS TABLE (
    id uuid,
    law text,
    full_name text,
    nn_reference text,
    article text,
    title text,
    content text,
    topic_tags text[],
    overlap_count int
)
LANGUAGE sql STABLE
AS $$
    SELECT
        lc.id,
        lc.law,
        lc.full_name,
        lc.nn_reference,
        lc.article,
        lc.title,
        lc.content,
        lc.topic_tags,
        (SELECT count(*)::int FROM unnest(lc.topic_tags) t WHERE t = ANY(search_tags)) AS overlap_count
    FROM law_chunks lc
    WHERE lc.topic_tags && search_tags
    ORDER BY overlap_count DESC, lc.law, lc.article
    LIMIT max_results;
$$;

-- ========================================
-- Row Level Security
-- ========================================

ALTER TABLE law_chunks ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON law_chunks;
CREATE POLICY "service_role_full_access" ON law_chunks
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "anon_select" ON law_chunks;
CREATE POLICY "anon_select" ON law_chunks
    FOR SELECT USING (true);
