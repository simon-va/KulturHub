CREATE TABLE event_categories (
    id    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name  TEXT NOT NULL,
    color TEXT NOT NULL
);

ALTER TABLE events
ADD COLUMN event_category_id UUID REFERENCES event_categories(id);
