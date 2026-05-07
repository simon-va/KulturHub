ALTER TABLE events ALTER COLUMN status TYPE INTEGER
    USING CASE status
        WHEN 'Draft'      THEN 0
        WHEN 'Published'  THEN 1
        WHEN 'Failed'     THEN 2
        ELSE 0
    END;

ALTER TABLE events ALTER COLUMN status SET DEFAULT 0;
