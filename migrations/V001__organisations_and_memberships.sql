-- V001__organisations_and_memberships.sql
-- Adds the `organisations` and `memberships` tables.
-- Names of organisations are unique. A user may only be a member of an
-- organisation once. Deleting an organisation cascades to its memberships.

CREATE TABLE organisations (
    id          uuid        PRIMARY KEY,
    name        text        NOT NULL,
    created_at  timestamptz NOT NULL,
    CONSTRAINT organisations_name_unique UNIQUE (name)
);

CREATE TABLE memberships (
    id               uuid        PRIMARY KEY,
    user_id          uuid        NOT NULL,
    organisation_id  uuid        NOT NULL REFERENCES organisations(id) ON DELETE CASCADE,
    joined_at        timestamptz NOT NULL,
    CONSTRAINT memberships_user_org_unique UNIQUE (user_id, organisation_id)
);

CREATE INDEX idx_memberships_user_id          ON memberships (user_id);
CREATE INDEX idx_memberships_organisation_id  ON memberships (organisation_id);
