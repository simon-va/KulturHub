-- V003__soft_delete.sql
-- Introduces shadow delete (a.k.a. soft delete) for all mutable resources.
-- Rows are never physically removed; instead `is_deleted` is flipped to TRUE
-- and `deleted_at` is stamped with the deletion time. Every read query must
-- filter `WHERE is_deleted = FALSE`.
--
-- Tables touched: users, invitations, organisations, memberships.
-- `change_logs` is intentionally excluded: it is an append-only audit log
-- and must remain on disk even after a referenced organisation is deleted.
--
-- Existing UNIQUE constraints on `organisations.name` and
-- `memberships(user_id, organisation_id)` are replaced with PARTIAL unique
-- indexes that only apply to live rows, so that a deleted organisation can
-- later be re-created under the same name without violating the constraint.

ALTER TABLE users
    ADD COLUMN is_deleted boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN deleted_at timestamptz NULL;

ALTER TABLE invitations
    ADD COLUMN is_deleted boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN deleted_at timestamptz NULL;

ALTER TABLE organisations
    ADD COLUMN is_deleted boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN deleted_at timestamptz NULL;

ALTER TABLE memberships
    ADD COLUMN is_deleted boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN deleted_at timestamptz NULL;

ALTER TABLE organisations
    DROP CONSTRAINT IF EXISTS organisations_name_unique;

CREATE UNIQUE INDEX organisations_name_active_uniq
    ON organisations (name) WHERE NOT is_deleted;

ALTER TABLE memberships
    DROP CONSTRAINT IF EXISTS memberships_user_org_unique;

CREATE UNIQUE INDEX memberships_user_org_active_uniq
    ON memberships (user_id, organisation_id) WHERE NOT is_deleted;
