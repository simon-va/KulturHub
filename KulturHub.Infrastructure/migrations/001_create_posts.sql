CREATE TABLE posts (
    id UUID PRIMARY KEY,
    type SMALLINT NOT NULL,
    status SMALLINT NOT NULL,
    caption TEXT NOT NULL,
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL,
    published_at TIMESTAMPTZ
);

CREATE TABLE post_images (
    id UUID PRIMARY KEY,
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    storage_url TEXT NOT NULL,
    sort_order INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);
