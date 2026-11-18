SET XACT_ABORT ON;
BEGIN TRAN;
GO
:r procs/21_workflow_enhancements.sql
:r 25_assistant.sql
:r procs/23_assistant.sql
COMMIT;
GO
