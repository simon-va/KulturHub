CREATE TABLE organisations (
    id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL
);

CREATE TABLE organisation_members (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    organisation_id UUID NOT NULL REFERENCES organisations(id) ON DELETE CASCADE
);
