ALTER TABLE events ADD COLUMN conversation_id UUID REFERENCES conversations(id);
