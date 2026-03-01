-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 013: Notification preferences + system notifications
-- user_notification_prefs: per-user toggles for email/push alerts.
-- notifications: in-app notification queue (bell icon in frontend).
-- ═══════════════════════════════════════════════════════════════════

-- ── Notification preferences ──
CREATE TABLE IF NOT EXISTS user_notification_prefs (
    user_id             UUID            PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    email_enabled       BOOLEAN         DEFAULT TRUE,      -- Receive emails at all
    email_case_updates  BOOLEAN         DEFAULT TRUE,      -- Case status changes
    email_invoice_reminders BOOLEAN     DEFAULT TRUE,      -- Invoice due date reminders
    email_credit_low    BOOLEAN         DEFAULT TRUE,      -- Credit quota running low
    email_weekly_summary BOOLEAN        DEFAULT FALSE,     -- Weekly activity digest
    push_enabled        BOOLEAN         DEFAULT FALSE,     -- Browser push (future)
    push_case_updates   BOOLEAN         DEFAULT FALSE
);

ALTER TABLE user_notification_prefs ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON user_notification_prefs;
CREATE POLICY "service_role_full_access" ON user_notification_prefs
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_prefs" ON user_notification_prefs;
CREATE POLICY "users_own_prefs" ON user_notification_prefs
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

DROP TRIGGER IF EXISTS trg_notification_prefs_updated_at ON user_notification_prefs;
CREATE TRIGGER trg_notification_prefs_updated_at
    BEFORE UPDATE ON user_notification_prefs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── In-app notifications ──
CREATE TABLE IF NOT EXISTS notifications (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    type            TEXT            NOT NULL DEFAULT 'info',
        -- 'info', 'warning', 'success', 'error',
        -- 'case_update', 'invoice_due', 'credit_low', 'system'
    title           TEXT            NOT NULL DEFAULT '',
    body            TEXT,
    link            TEXT,                                  -- Frontend route to navigate to
    is_read         BOOLEAN         DEFAULT FALSE,
    read_at         TIMESTAMPTZ,
    metadata        JSONB           DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_notifications_user_unread
    ON notifications (user_id, is_read, created_at DESC)
    WHERE is_read = FALSE;

CREATE INDEX IF NOT EXISTS idx_notifications_user_date
    ON notifications (user_id, created_at DESC);

ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON notifications;
CREATE POLICY "service_role_full_access" ON notifications
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_notifications" ON notifications;
CREATE POLICY "users_own_notifications" ON notifications
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ── RPC: Mark notifications as read ──
CREATE OR REPLACE FUNCTION mark_notifications_read(p_user_id UUID, p_ids UUID[])
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE
    updated INT;
BEGIN
    UPDATE notifications
    SET is_read = TRUE, read_at = now()
    WHERE user_id = p_user_id
      AND id = ANY(p_ids)
      AND is_read = FALSE;

    GET DIAGNOSTICS updated = ROW_COUNT;
    RETURN updated;
END;
$$;

GRANT EXECUTE ON FUNCTION mark_notifications_read(UUID, UUID[]) TO authenticated;

-- ── RPC: Get unread notification count ──
CREATE OR REPLACE FUNCTION get_unread_notification_count(p_user_id UUID)
RETURNS INT
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT COUNT(*)::INT FROM notifications
    WHERE user_id = p_user_id AND is_read = FALSE;
$$;

GRANT EXECUTE ON FUNCTION get_unread_notification_count(UUID) TO authenticated;
