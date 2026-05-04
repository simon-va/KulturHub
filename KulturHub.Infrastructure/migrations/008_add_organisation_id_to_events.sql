ALTER TABLE events
ADD COLUMN organisation_id UUID NOT NULL REFERENCES organisations(id);
