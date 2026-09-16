-- Deterministic demo wallets. The seed is safe to run repeatedly.
INSERT INTO wallets (id, user_id, brl_available, brl_locked, vibranium_available, vibranium_locked)
VALUES
 ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 100000000, 0, 100000, 0),
 ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', 100000000, 0, 100000, 0)
ON CONFLICT (user_id) DO NOTHING;
