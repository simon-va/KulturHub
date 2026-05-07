CREATE TABLE posts (
    id               UUID        PRIMARY KEY,
    type             SMALLINT    NOT NULL,
    status           SMALLINT    NOT NULL,
    caption          TEXT        NOT NULL,
    error_message    TEXT,
    created_at       TIMESTAMPTZ NOT NULL,
    published_at     TIMESTAMPTZ,
    instagram_media_id TEXT
);

CREATE TABLE post_images (
    id          UUID    PRIMARY KEY,
    post_id     UUID    NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    storage_url TEXT    NOT NULL,
    sort_order  INT     NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL
);

CREATE TABLE instagram_tokens (
  id                UUID        PRIMARY KEY,
  access_token      TEXT        NOT NULL,
  instagram_user_id TEXT        NOT NULL,
  expires_at        TIMESTAMPTZ NOT NULL,
  last_refreshed_at TIMESTAMPTZ NOT NULL,
  created_at        TIMESTAMPTZ NOT NULL
);

CREATE TABLE users (
    user_id    UUID    PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    first_name TEXT    NOT NULL,
    last_name  TEXT    NOT NULL,
    is_admin   BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE organisations (
    id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL
);

CREATE TABLE organisation_members (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    organisation_id UUID NOT NULL REFERENCES organisations(id) ON DELETE CASCADE
);

CREATE TABLE invitations (
    id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    code       TEXT        NOT NULL UNIQUE,
    used_by    UUID        REFERENCES users(user_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE event_categories (
    id    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name  TEXT NOT NULL,
    color TEXT NOT NULL
);

CREATE TABLE conversations (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    organisation_id UUID        NOT NULL REFERENCES organisations(id),
    created_at      TIMESTAMPTZ NOT NULL
);

CREATE TABLE messages (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID        NOT NULL REFERENCES conversations(id),
    role            INTEGER     NOT NULL,
    content         TEXT        NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL
);

CREATE TABLE events (
    id               UUID        PRIMARY KEY,
    title            TEXT        NOT NULL,
    start_time       TIMESTAMPTZ,
    end_time         TIMESTAMPTZ,
    address          TEXT        NOT NULL,
    description      TEXT        NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL,
    status           INTEGER     NOT NULL DEFAULT 0,
    organisation_id  UUID        NOT NULL REFERENCES organisations(id),
    event_category_id UUID       REFERENCES event_categories(id),
    conversation_id  UUID        REFERENCES conversations(id)
);