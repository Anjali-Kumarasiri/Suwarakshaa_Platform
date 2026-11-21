-- Phone-number login: one account per phone number (run once on the existing database).
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Phone' AND object_id = OBJECT_ID('Users'))
    CREATE UNIQUE INDEX UX_Users_Phone ON Users(Phone) WHERE Phone IS NOT NULL;
