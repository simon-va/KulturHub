-- V004__user_email.sql
-- Stores the user's email alongside the user record so that downstream
-- features (search, invitations, admin views) can resolve users without an
-- extra round-trip to Supabase Auth. The authoritative email still lives in
-- Supabase; this column mirrors it for lookup convenience and is therefore
-- plain text without a uniqueness constraint.

ALTER TABLE users
    ADD COLUMN email text NOT NULL DEFAULT '';
