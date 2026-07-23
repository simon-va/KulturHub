-- V005__membership_status.sql
-- Adds a numeric status column to memberships so that an owner can invite
-- another user without immediately granting them access. The state machine
-- is documented in code (KulturHub.Domain.Entities.MembershipStatus).
--
-- Allowed values:
--   0 = Pending   -- invite sent, invitee has not yet acted
--   1 = Accepted  -- membership is active
--   2 = Rejected  -- invitee declined the invite
--
-- Default is Pending so that every newly inserted row that omits the column
-- still carries a known, queryable state. Existing rows written before this
-- migration are treated as already accepted (back-fill) so that legacy
-- memberships remain usable in the UI.

ALTER TABLE memberships
    ADD COLUMN status smallint NOT NULL DEFAULT 0;

UPDATE memberships
SET status = 1
WHERE status = 0;

ALTER TABLE memberships
    ADD CONSTRAINT memberships_status_range_chk
    CHECK (status BETWEEN 0 AND 2);

CREATE INDEX idx_memberships_status ON memberships (status);
