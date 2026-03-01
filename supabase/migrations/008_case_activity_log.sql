-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 008: Case activity log
-- Tracks events on cases: creation, status changes, linked documents,
-- troškovnik attachments, notes, etc.
-- Provides a timeline view per case in the frontend.
-- ═══════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS case_activities (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    case_id         UUID            NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    activity_type   TEXT            NOT NULL DEFAULT 'note',
        -- Allowed values:
        -- 'created'          — case was created
        -- 'status_change'    — status changed (e.g. aktivan → zatvoren)
        -- 'note'             — user added a free-text note
        -- 'troskovnik'       — troškovnik linked/created for this case
        -- 'evaluation'       — valuation linked
        -- 'contract'         — contract analysis linked
        -- 'zk_analysis'      — ZK analysis linked
        -- 'invoice'          — invoice generated
        -- 'document'         — document uploaded
    title           TEXT            NOT NULL DEFAULT '',    -- Short summary, e.g. "Status → zatvoren"
    description     TEXT,                                  -- Longer details / note body
    metadata        JSONB           DEFAULT '{}'::jsonb,   -- Flexible: {old_status, new_status, linked_id, ...}
    linked_id       UUID                                   -- Optional FK to the linked record
);

CREATE INDEX IF NOT EXISTS idx_case_activities_case
    ON case_activities (case_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_case_activities_user
    ON case_activities (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_case_activities_type
    ON case_activities (case_id, activity_type);

-- RLS
ALTER TABLE case_activities ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON case_activities;
CREATE POLICY "service_role_full_access" ON case_activities
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_case_activities" ON case_activities;
CREATE POLICY "users_own_case_activities" ON case_activities
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ── Auto-create "created" activity when a case is inserted ──
CREATE OR REPLACE FUNCTION log_case_created()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO case_activities (user_id, case_id, activity_type, title, description)
    VALUES (
        NEW.user_id,
        NEW.id,
        'created',
        'Predmet kreiran',
        COALESCE(NEW.naziv, '') || CASE WHEN NEW.broj_predmeta != '' THEN ' (' || NEW.broj_predmeta || ')' ELSE '' END
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_case_created_activity ON cases;
CREATE TRIGGER trg_case_created_activity
    AFTER INSERT ON cases
    FOR EACH ROW EXECUTE FUNCTION log_case_created();

-- ── Auto-log status changes ──
CREATE OR REPLACE FUNCTION log_case_status_change()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.status IS DISTINCT FROM NEW.status THEN
        INSERT INTO case_activities (user_id, case_id, activity_type, title, metadata)
        VALUES (
            NEW.user_id,
            NEW.id,
            'status_change',
            'Status: ' || COALESCE(OLD.status, '?') || ' → ' || COALESCE(NEW.status, '?'),
            jsonb_build_object('old_status', OLD.status, 'new_status', NEW.status)
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_case_status_change ON cases;
CREATE TRIGGER trg_case_status_change
    AFTER UPDATE ON cases
    FOR EACH ROW EXECUTE FUNCTION log_case_status_change();
