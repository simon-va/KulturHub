-- V006__membership_invited_by.sql
-- Tracks which user invited another user to an organisation so the
-- invitation list can be shown to the invitee (see Plan: ListMyPendingMemberships).
--
-- Nullable on purpose:
--   - The owner of an organisation is created as the first membership
--     by the system when the organisation is created (CreateOrganisationHandler)
--     rather than via an invite, so invited_by stays NULL.
--   - Pending memberships created by an invite (InviteMemberHandler) do
--     have a real inviter, and the column is populated there.
--   - Legacy rows written before this column existed have no known
--     inviter and remain queryable.

ALTER TABLE memberships
    ADD COLUMN invited_by uuid NULL REFERENCES users(user_id);

CREATE INDEX idx_memberships_invited_by ON memberships (invited_by);
