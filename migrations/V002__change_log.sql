-- V002__change_log.sql
-- Adds the `change_log` table to record mutations on an organisation.
-- The `data` column is a jsonb document describing what changed; field
-- names are kept in camelCase by application code. Deleting an
-- organisation cascades to its change log entries. `user_id` has no
-- foreign key because the authoritative user store lives in Supabase.

CREATE TABLE change_logs (
    id              uuid        PRIMARY KEY,
    organisation_id uuid        NOT NULL REFERENCES organisations(id) ON DELETE CASCADE,
    user_id         uuid        NOT NULL,
    message         text        NOT NULL,
    data            jsonb       NOT NULL DEFAULT '{}'::jsonb,
    created_at      timestamptz NOT NULL
);

CREATE INDEX idx_change_log_organisation_id ON change_logs (organisation_id);
CREATE INDEX idx_change_log_created_at      ON change_logs (created_at DESC);
