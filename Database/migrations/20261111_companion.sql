SET XACT_ABORT ON;
BEGIN TRAN;
GO
:r 24_device_sessions.sql
:r procs/22_companion.sql
COMMIT;
GO
