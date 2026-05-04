ALTER TABLE posts ADD COLUMN instagram_media_id TEXT;

CREATE TABLE instagram_tokens (
    id                UUID        PRIMARY KEY,
    access_token      TEXT        NOT NULL,
    instagram_user_id TEXT        NOT NULL,
    expires_at        TIMESTAMPTZ NOT NULL,
    last_refreshed_at TIMESTAMPTZ NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL
);
