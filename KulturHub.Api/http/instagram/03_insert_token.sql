-- Ersten Token manuell in die Datenbank einfügen
-- Werte aus Schritt 01 und 02 eintragen

INSERT INTO instagram_tokens (id, access_token, instagram_user_id, expires_at, last_refreshed_at, created_at)
VALUES (
    gen_random_uuid(),
    '<instagram_user_token>',
    '<instagram_user_id>',
    NOW() + INTERVAL '60 days',
    NOW(),
    NOW()
);
