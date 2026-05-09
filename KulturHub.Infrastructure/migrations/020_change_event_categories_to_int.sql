-- Step 1: Drop the foreign key constraint on events.event_category_id
ALTER TABLE events
DROP CONSTRAINT IF EXISTS events_event_category_id_fkey;

-- Step 2: Drop the event_category_id column from events
ALTER TABLE events
DROP COLUMN IF EXISTS event_category_id;

-- Step 3: Drop the old event_categories table
DROP TABLE IF EXISTS event_categories;

-- Step 4: Recreate event_categories with SERIAL (int) primary key
CREATE TABLE event_categories (
    id    SERIAL PRIMARY KEY,
    name  TEXT NOT NULL,
    color TEXT NOT NULL
);

-- Step 5: Add event_category_id as INT to events with foreign key
ALTER TABLE events
ADD COLUMN event_category_id INT REFERENCES event_categories(id);
