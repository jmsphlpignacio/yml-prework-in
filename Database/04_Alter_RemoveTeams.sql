-- =====================================================
-- YML PRE WORK IN - ALTER SCRIPT: REMOVE TEAMS NOTIFICATIONS
-- Version: 1.0
-- Date: 2026-01-26
-- =====================================================
--
-- PURPOSE: Remove all Teams-related columns and configurations.
--          Communication channels are now limited to Email and SMS only.
--
-- TABLES AFFECTED:
-- - [atorfi].[RFI_PartnerEscalations] - Remove Teams notification columns
-- - [atorfi].[RFI_Configuration] - Remove Teams channel configuration
--
-- NOTE: Run this script AFTER the initial schema has been created.
-- =====================================================

USE [synchubdbxpm];
GO

-- =====================================================
-- 1. REMOVE TEAMS COLUMNS FROM RFI_PartnerEscalations
-- =====================================================
-- These columns tracked Teams message delivery for partner alerts.
-- Partner alerts will now be sent via Email only.

-- First drop the default constraint on TeamsMessageSent
DECLARE @ConstraintName NVARCHAR(200);
SELECT @ConstraintName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'[atorfi].[RFI_PartnerEscalations]')
AND c.name = 'TeamsMessageSent';

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [atorfi].[RFI_PartnerEscalations] DROP CONSTRAINT ' + @ConstraintName);
    PRINT 'Dropped default constraint: ' + @ConstraintName;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_PartnerEscalations]') AND name = 'TeamsMessageSent')
BEGIN
    ALTER TABLE [atorfi].[RFI_PartnerEscalations]
    DROP COLUMN TeamsMessageSent;
    PRINT 'Dropped column: TeamsMessageSent from RFI_PartnerEscalations';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_PartnerEscalations]') AND name = 'TeamsMessageSentDate')
BEGIN
    ALTER TABLE [atorfi].[RFI_PartnerEscalations]
    DROP COLUMN TeamsMessageSentDate;
    PRINT 'Dropped column: TeamsMessageSentDate from RFI_PartnerEscalations';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_PartnerEscalations]') AND name = 'TeamsMessageID')
BEGIN
    ALTER TABLE [atorfi].[RFI_PartnerEscalations]
    DROP COLUMN TeamsMessageID;
    PRINT 'Dropped column: TeamsMessageID from RFI_PartnerEscalations';
END
GO

-- =====================================================
-- 2. REMOVE TEAMS CONFIGURATION FROM RFI_Configuration
-- =====================================================
-- Remove the Teams channel ID configuration entry.

IF EXISTS (SELECT 1 FROM [atorfi].[RFI_Configuration] WHERE ConfigKey = 'DailyReportChannelID')
BEGIN
    DELETE FROM [atorfi].[RFI_Configuration]
    WHERE ConfigKey = 'DailyReportChannelID';
    PRINT 'Deleted configuration: DailyReportChannelID';
END
GO

-- =====================================================
-- 3. UPDATE COMMUNICATION TYPE COMMENT (Optional Reference)
-- =====================================================
-- Note: The CommunicationType column in RFI_CommunicationLog accepts:
--   - 'Email'
--   - 'SMS'
-- TeamsMessage is no longer a valid value.

PRINT '=====================================================';
PRINT 'Teams notification columns removed successfully!';
PRINT 'Communication channels: Email and SMS only.';
PRINT '=====================================================';
GO
