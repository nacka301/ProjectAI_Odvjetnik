-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 009: Document attachments
-- Stores references to uploaded documents (PDFs, images, etc.)
-- linked to cases, clients, or specific analyses.
-- Files stored in Supabase Storage; this table holds metadata.
-- ═══════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS documents (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    -- Linkage (all optional — document can be standalone)
    client_id       UUID            REFERENCES clients(id) ON DELETE SET NULL,
    case_id         UUID            REFERENCES cases(id) ON DELETE SET NULL,
    analysis_id     UUID,           -- Generic FK to any analysis table
    analysis_type   TEXT,           -- 'evaluation', 'contract', 'zk', 'troskovnik', 'chronology', 'invoice'
    -- File metadata
    file_name       TEXT            NOT NULL DEFAULT '',       -- Original file name
    file_type       TEXT            NOT NULL DEFAULT '',       -- MIME type
    file_size_bytes BIGINT          DEFAULT 0,                 -- File size
    storage_path    TEXT            NOT NULL DEFAULT '',       -- Supabase Storage path (bucket/key)
    public_url      TEXT,                                      -- Public URL if available
    -- Classification
    document_type   TEXT            NOT NULL DEFAULT 'ostalo',
        -- 'ugovor', 'zk_izvadak', 'presuda', 'rjesenje', 'racun',
        -- 'troskovnik', 'podnesak', 'dopis', 'punomoć', 'ostalo'
    description     TEXT,                                      -- User note
    tags            TEXT[]          DEFAULT '{}'               -- Flexible tagging
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_documents_user
    ON documents (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_documents_case
    ON documents (case_id, created_at DESC)
    WHERE case_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_documents_client
    ON documents (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_documents_type
    ON documents (user_id, document_type);

CREATE INDEX IF NOT EXISTS idx_documents_tags
    ON documents USING GIN (tags);

-- RLS
ALTER TABLE documents ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON documents;
CREATE POLICY "service_role_full_access" ON documents
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_documents" ON documents;
CREATE POLICY "users_own_documents" ON documents
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- Auto-update trigger
DROP TRIGGER IF EXISTS trg_documents_updated_at ON documents;
CREATE TRIGGER trg_documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ═══════════════════════════════════════════════════════════════════
-- Supabase Storage bucket for documents
-- Run this via Supabase Dashboard → Storage → New Bucket:
--   Name: documents
--   Public: false (private — access via signed URLs)
--   File size limit: 50 MB
--   Allowed MIME types: application/pdf, image/png, image/jpeg, image/webp,
--                       application/msword, application/vnd.openxmlformats-officedocument.wordprocessingml.document
-- ═══════════════════════════════════════════════════════════════════
