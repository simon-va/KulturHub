CREATE TABLE users (
    user_id    UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    first_name TEXT NOT NULL,
    last_name  TEXT NOT NULL
);
