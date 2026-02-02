-- =====================================================
-- YML PRE WORK IN - DATABASE SCHEMA
-- Version: 1.0
-- Date: 2026-01-23
-- =====================================================
--
-- This script creates the RFI workflow tables that extend
-- the existing SyncHub XPM data and manual upload tables.
--
-- EXISTING DATA SOURCES (DO NOT MODIFY):
-- 1. [xeropracticemanager_xpm_8963_2].* - SyncHub XPM tables
-- 2. [atorfi].[ATO Report] - Manual CSV upload from ATO Portal
-- 3. [atorfi].[XPM] - Manual CSV upload (TFN bridge)
--
-- NEW TABLES CREATED BY THIS SCRIPT:
-- - [atorfi].[RFI_Workflows]        - Core table tracking RFI status for each job/client, including dates, reminders, and flags
-- - [atorfi].[RFI_CommunicationLog] - Records all emails and SMS sent to clients during the RFI process
-- - [atorfi].[RFI_ClientUploadLinks]- Stores unique secure upload links generated for each client per tax year
-- - [atorfi].[RFI_DocumentUploads]  - Tracks all documents uploaded by clients via their upload links
-- - [atorfi].[RFI_PartnerEscalations]- Queue of escalation alerts sent to partners for non-responsive clients
-- - [atorfi].[RFI_DailySnapshots]   - Daily aggregated counts for reporting dashboards and trend analysis
-- - [atorfi].[RFI_AuditLog]         - Complete audit trail of all actions taken in the RFI system
-- - [atorfi].[RFI_Configuration]    - System settings for reminder intervals, URLs, and other parameters
-- =====================================================

USE [synchubdbxpm];
GO

-- =====================================================
-- 1. RFI WORKFLOW TRACKING
-- =====================================================
-- PURPOSE: Core table that tracks the RFI (Request for Information) status for each job/client.
-- USE CASE: When a partner initiates an RFI request, a record is created here. The system
--           tracks the workflow through stages: Pending -> Requested -> PartiallyReceived -> Completed.
--           Stores key dates (when sent, when docs received), reminder tracking, and client contact info.
-- RELATIONSHIPS: Links to ClientDetails and JobDetails via UUIDs. Parent to CommunicationLog and DocumentUploads.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_Workflows]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_Workflows] (
        -- Primary Key
        RFIWorkflowID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),

        -- Job Reference (from SyncHub JobDetails)
        JobID NVARCHAR(50) NOT NULL,                    -- JobDetails.ID
        JobUUID UNIQUEIDENTIFIER,                        -- JobDetails.UUID

        -- Client Reference (from SyncHub ClientDetails)
        ClientUUID UNIQUEIDENTIFIER NOT NULL,           -- ClientDetails.UUID
        ClientCode NVARCHAR(50),                         -- ClientDetails.ClientCode
        ClientGroupUUID UNIQUEIDENTIFIER,               -- From Client table

        -- Primary Contact (denormalized for performance)
        PrimaryContactName NVARCHAR(200),
        PrimaryContactEmail NVARCHAR(255),
        PrimaryContactMobile NVARCHAR(50),
        CCContactEmail NVARCHAR(255),

        -- RFI Status
        RFIStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        -- Possible values: Pending, Requested, PartiallyReceived, WorkIn, Completed, Stopped, DoNotSend

        -- Flags
        DoNotSendFlag BIT DEFAULT 0,
        StoppedFlag BIT DEFAULT 0,

        -- Partner who initiated
        InitiatedByPartnerUUID UNIQUEIDENTIFIER,
        InitiatedByPartnerName NVARCHAR(100),
        InitiatedByPartnerEmail NVARCHAR(255),
        PartnerNotes NVARCHAR(500),

        -- Key Dates
        InitiatedDate DATETIME2,                         -- When RFI was first sent
        FirstDocumentUploadDate DATETIME2,              -- When first doc was uploaded
        AllDocumentsReceivedDate DATETIME2,             -- When all docs received
        CompletedDate DATETIME2,                         -- When workflow completed
        StoppedDate DATETIME2,                           -- When manually stopped

        -- Reminder Tracking
        Reminder1SentDate DATETIME2,                     -- T+7 email
        Reminder2SentDate DATETIME2,                     -- T+14 SMS
        FinalNoticeSentDate DATETIME2,                  -- T+21 email
        PartnerAlertSentDate DATETIME2,                 -- T+21 Email

        -- Tax Year
        TaxYear INT,

        -- ATO Data (denormalized from ATO Report)
        ATODueDate DATE,
        ATOLodgementCode NVARCHAR(50),

        -- Metadata
        CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
        ModifiedDate DATETIME2 DEFAULT GETUTCDATE(),

        -- Indexes will be created separately
        CONSTRAINT UQ_RFI_Job UNIQUE (JobID, TaxYear)
    );

    PRINT 'Created table: RFI_Workflows';
END
GO

-- Create indexes for RFI_Workflows
CREATE NONCLUSTERED INDEX IX_RFI_Workflows_Status ON [atorfi].[RFI_Workflows] (RFIStatus) INCLUDE (JobID, ClientUUID);
CREATE NONCLUSTERED INDEX IX_RFI_Workflows_ClientUUID ON [atorfi].[RFI_Workflows] (ClientUUID);
CREATE NONCLUSTERED INDEX IX_RFI_Workflows_InitiatedDate ON [atorfi].[RFI_Workflows] (InitiatedDate) WHERE InitiatedDate IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_RFI_Workflows_ATODueDate ON [atorfi].[RFI_Workflows] (ATODueDate) WHERE ATODueDate IS NOT NULL;
GO

-- =====================================================
-- 2. COMMUNICATION LOG
-- =====================================================
-- PURPOSE: Records every email and SMS sent to clients during the RFI process.
-- USE CASE: Tracks initial RFI emails, T+7 reminder emails, T+14 SMS reminders, and T+21 final notices.
--           Stores delivery status (Sent, Failed, Bounced), scheduled dates, and message content previews.
--           Used to prevent duplicate sends and provide audit trail of client communications.
-- RELATIONSHIPS: Child of RFI_Workflows (one workflow can have many communications).

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_CommunicationLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_CommunicationLog] (
        CommunicationID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RFIWorkflowID UNIQUEIDENTIFIER NOT NULL,

        -- Communication Type
        CommunicationType NVARCHAR(20) NOT NULL,         -- Email, SMS
        CommunicationStage NVARCHAR(50) NOT NULL,        -- Initial, Reminder1, Reminder2, FinalNotice, PartnerAlert

        -- Recipients
        ToAddress NVARCHAR(500),
        CCAddress NVARCHAR(500),

        -- Content
        Subject NVARCHAR(500),
        BodyTemplate NVARCHAR(100),                       -- Template name used
        BodyPreview NVARCHAR(MAX),                        -- First 500 chars of body

        -- Status
        SendStatus NVARCHAR(50) DEFAULT 'Pending',       -- Pending, Queued, Sent, Failed, Bounced
        SentDate DATETIME2,
        FailureReason NVARCHAR(500),
        BounceReason NVARCHAR(500),

        -- External Reference
        ExternalMessageID NVARCHAR(200),                  -- From email/SMS provider

        -- Scheduling
        ScheduledDate DATETIME2,
        DaysSinceInitial INT,

        -- Metadata
        CreatedDate DATETIME2 DEFAULT GETUTCDATE(),

        CONSTRAINT FK_CommLog_Workflow FOREIGN KEY (RFIWorkflowID)
            REFERENCES [atorfi].[RFI_Workflows](RFIWorkflowID)
    );

    PRINT 'Created table: RFI_CommunicationLog';
END
GO

CREATE NONCLUSTERED INDEX IX_CommLog_WorkflowID ON [atorfi].[RFI_CommunicationLog] (RFIWorkflowID);
CREATE NONCLUSTERED INDEX IX_CommLog_Status ON [atorfi].[RFI_CommunicationLog] (SendStatus) WHERE SendStatus = 'Pending';
GO

-- =====================================================
-- 3. CLIENT UPLOAD LINKS
-- =====================================================
-- PURPOSE: Generates and stores unique secure upload URLs for each client per tax year.
-- USE CASE: Each client receives a personalized link (e.g., https://upload.yml.com.au/upload/{token})
--           that allows them to upload documents without logging in. Links are reusable within
--           the tax year and track access counts. Links can be deactivated or expired.
-- RELATIONSHIPS: One per client per tax year. Parent to RFI_DocumentUploads.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_ClientUploadLinks]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_ClientUploadLinks] (
        UploadLinkID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),

        -- Client Reference
        ClientUUID UNIQUEIDENTIFIER NOT NULL,            -- ClientDetails.UUID
        ClientCode NVARCHAR(50),
        ClientName NVARCHAR(255),
        TaxYear INT NOT NULL,

        -- Link Details
        UniqueToken NVARCHAR(100) NOT NULL,              -- Random token for URL
        -- Full URL will be: https://upload.yml.com.au/upload/{UniqueToken}

        -- Validity
        ExpiryDate DATETIME2,
        IsActive BIT DEFAULT 1,

        -- Usage Tracking
        LastAccessedDate DATETIME2,
        AccessCount INT DEFAULT 0,

        -- Metadata
        CreatedDate DATETIME2 DEFAULT GETUTCDATE(),

        CONSTRAINT UQ_UploadLink_Token UNIQUE (UniqueToken),
        CONSTRAINT UQ_UploadLink_Client_Year UNIQUE (ClientUUID, TaxYear)
    );

    PRINT 'Created table: RFI_ClientUploadLinks';
END
GO

CREATE NONCLUSTERED INDEX IX_UploadLinks_Token ON [atorfi].[RFI_ClientUploadLinks] (UniqueToken) WHERE IsActive = 1;
GO

-- =====================================================
-- 4. DOCUMENT UPLOADS
-- =====================================================
-- PURPOSE: Tracks every document uploaded by clients via their secure upload links.
-- USE CASE: When a client uploads tax documents (payslips, receipts, statements), metadata is
--           stored here including file details, Azure Blob storage location, and FYI integration status.
--           Documents are automatically filed to FYI document management system.
-- RELATIONSHIPS: Child of RFI_ClientUploadLinks and RFI_Workflows. Links to Azure Blob Storage.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_DocumentUploads]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_DocumentUploads] (
        DocumentID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UploadLinkID UNIQUEIDENTIFIER NOT NULL,
        RFIWorkflowID UNIQUEIDENTIFIER,

        -- Client/Job Reference
        ClientUUID UNIQUEIDENTIFIER NOT NULL,
        JobID NVARCHAR(50),

        -- Document Details
        OriginalFileName NVARCHAR(255) NOT NULL,
        StoredFileName NVARCHAR(255),                     -- Renamed file in storage
        FileSize BIGINT,
        FileType NVARCHAR(50),                            -- Extension
        MimeType NVARCHAR(100),
        DocumentCategory NVARCHAR(100),                   -- User-selected category

        -- Storage Location
        StorageContainer NVARCHAR(100),                   -- Azure Blob container
        StoragePath NVARCHAR(500),                        -- Full path in container
        BlobURL NVARCHAR(1000),

        -- FYI Integration
        FYIDocumentID NVARCHAR(100),
        FiledToFYI BIT DEFAULT 0,
        FiledToFYIDate DATETIME2,
        FYIFilingError NVARCHAR(500),

        -- Metadata
        UploadedDate DATETIME2 DEFAULT GETUTCDATE(),
        UploadedByIP NVARCHAR(50),
        UploadedByUserAgent NVARCHAR(500),

        CONSTRAINT FK_DocUpload_Link FOREIGN KEY (UploadLinkID)
            REFERENCES [atorfi].[RFI_ClientUploadLinks](UploadLinkID),
        CONSTRAINT FK_DocUpload_Workflow FOREIGN KEY (RFIWorkflowID)
            REFERENCES [atorfi].[RFI_Workflows](RFIWorkflowID)
    );

    PRINT 'Created table: RFI_DocumentUploads';
END
GO

CREATE NONCLUSTERED INDEX IX_DocUpload_ClientUUID ON [atorfi].[RFI_DocumentUploads] (ClientUUID);
CREATE NONCLUSTERED INDEX IX_DocUpload_UploadLinkID ON [atorfi].[RFI_DocumentUploads] (UploadLinkID);
CREATE NONCLUSTERED INDEX IX_DocUpload_FYI ON [atorfi].[RFI_DocumentUploads] (FiledToFYI) WHERE FiledToFYI = 0;
GO

-- =====================================================
-- 5. PARTNER ESCALATIONS
-- =====================================================
-- PURPOSE: Queue of escalation alerts for partners when clients are non-responsive.
-- USE CASE: At T+21 days (or when emails bounce), an escalation record is created alerting
--           the responsible partner. Partners can acknowledge, add notes, and record actions taken.
--           Used for the partner dashboard to show clients requiring manual follow-up.
-- RELATIONSHIPS: Child of RFI_Workflows. Links to partner via PartnerUUID.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_PartnerEscalations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_PartnerEscalations] (
        EscalationID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RFIWorkflowID UNIQUEIDENTIFIER NOT NULL,

        -- Partner Details
        PartnerUUID UNIQUEIDENTIFIER,
        PartnerName NVARCHAR(100),
        PartnerEmail NVARCHAR(255),

        -- Escalation Reason
        EscalationReason NVARCHAR(100),                   -- NoResponse21Days, EmailBounce, ApproachingDeadline
        DaysSinceInitial INT,
        ATODueDate DATE,
        DaysUntilDeadline INT,

        -- Client Details (denormalized)
        ClientName NVARCHAR(255),
        PrimaryContactEmail NVARCHAR(255),
        PrimaryContactMobile NVARCHAR(50),
        JobCount INT,

        -- Status
        IsAcknowledged BIT DEFAULT 0,
        AcknowledgedDate DATETIME2,
        AcknowledgedBy NVARCHAR(100),
        ActionTaken NVARCHAR(500),

        -- Teams Notification
        TeamsMessageSent BIT DEFAULT 0,
        TeamsMessageSentDate DATETIME2,
        TeamsMessageID NVARCHAR(200),

        -- Metadata
        CreatedDate DATETIME2 DEFAULT GETUTCDATE(),

        CONSTRAINT FK_Escalation_Workflow FOREIGN KEY (RFIWorkflowID)
            REFERENCES [atorfi].[RFI_Workflows](RFIWorkflowID)
    );

    PRINT 'Created table: RFI_PartnerEscalations';
END
GO

CREATE NONCLUSTERED INDEX IX_Escalation_Partner ON [atorfi].[RFI_PartnerEscalations] (PartnerEmail) WHERE IsAcknowledged = 0;
GO

-- =====================================================
-- 6. DAILY STATUS SNAPSHOTS
-- =====================================================
-- PURPOSE: Stores daily aggregated counts of jobs and RFI statuses for reporting.
-- USE CASE: Each morning, a scheduled job captures current counts (Pre-Work-In, Requested,
--           Work-In, etc.) enabling trend analysis and daily status reports. Powers the
--           dashboard charts showing workflow progress over time and identifies bottlenecks.
-- RELATIONSHIPS: Standalone reporting table. No foreign keys - contains denormalized counts.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_DailySnapshots]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_DailySnapshots] (
        SnapshotID INT IDENTITY(1,1) PRIMARY KEY,
        SnapshotDate DATE NOT NULL,

        -- Job Counts by State
        PreWorkInELCount INT DEFAULT 0,
        PreWorkInNonELCount INT DEFAULT 0,
        RequestedCount INT DEFAULT 0,
        WorkInCount INT DEFAULT 0,
        WaitOnInfoCount INT DEFAULT 0,
        WaitOnSignCount INT DEFAULT 0,
        ReadyToLodgeCount INT DEFAULT 0,
        LodgedCount INT DEFAULT 0,
        CompleteCount INT DEFAULT 0,

        -- RFI Workflow Counts
        RFIPendingCount INT DEFAULT 0,
        RFIRequestedCount INT DEFAULT 0,
        RFIPartiallyReceivedCount INT DEFAULT 0,
        RFICompletedCount INT DEFAULT 0,
        RFIStoppedCount INT DEFAULT 0,
        RFIDoNotSendCount INT DEFAULT 0,

        -- Attention Needed
        AtRiskOver21DaysCount INT DEFAULT 0,
        ApproachingDeadline7DaysCount INT DEFAULT 0,
        EmailBounceCount INT DEFAULT 0,

        -- Change from Previous Day
        PreWorkInChange INT DEFAULT 0,
        RequestedChange INT DEFAULT 0,
        WorkInChange INT DEFAULT 0,

        -- Metadata
        CreatedDate DATETIME2 DEFAULT GETUTCDATE(),

        CONSTRAINT UQ_Snapshot_Date UNIQUE (SnapshotDate)
    );

    PRINT 'Created table: RFI_DailySnapshots';
END
GO

-- =====================================================
-- 7. AUDIT LOG
-- =====================================================
-- PURPOSE: Complete audit trail recording every action taken in the RFI system.
-- USE CASE: Tracks who did what and when - status changes, document uploads, emails sent,
--           manual overrides, etc. Stores before/after values for changes. Essential for
--           compliance, troubleshooting, and answering "what happened to this client?" questions.
-- RELATIONSHIPS: References all entities via EntityType and EntityID (polymorphic).

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_AuditLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_AuditLog] (
        AuditID BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- What changed
        EntityType NVARCHAR(50) NOT NULL,                 -- RFIWorkflow, Document, Communication, etc.
        EntityID NVARCHAR(100) NOT NULL,

        -- Action
        Action NVARCHAR(50) NOT NULL,                     -- Created, Updated, StatusChanged, Sent, Uploaded, etc.
        ActionDetail NVARCHAR(200),

        -- Before/After
        OldValue NVARCHAR(MAX),
        NewValue NVARCHAR(MAX),

        -- Who did it
        PerformedBy NVARCHAR(100),
        PerformedByType NVARCHAR(50),                     -- System, Partner, Client, Scheduler
        PerformedByEmail NVARCHAR(255),

        -- When
        PerformedDate DATETIME2 DEFAULT GETUTCDATE(),

        -- Context
        IPAddress NVARCHAR(50),
        UserAgent NVARCHAR(500),
        AdditionalData NVARCHAR(MAX)                      -- JSON for extra context
    );

    PRINT 'Created table: RFI_AuditLog';
END
GO

CREATE NONCLUSTERED INDEX IX_AuditLog_Entity ON [atorfi].[RFI_AuditLog] (EntityType, EntityID);
CREATE NONCLUSTERED INDEX IX_AuditLog_Date ON [atorfi].[RFI_AuditLog] (PerformedDate);
GO

-- =====================================================
-- 8. CONFIGURATION
-- =====================================================
-- PURPOSE: Key-value store for system configuration settings.
-- USE CASE: Stores configurable parameters like reminder intervals (T+7, T+14, T+21),
--           upload link expiry periods, current tax year, email addresses, and URLs.
--           Allows admins to adjust system behavior without code changes.
-- RELATIONSHIPS: Standalone configuration table. Referenced by application logic.

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[atorfi].[RFI_Configuration]') AND type in (N'U'))
BEGIN
    CREATE TABLE [atorfi].[RFI_Configuration] (
        ConfigKey NVARCHAR(100) PRIMARY KEY,
        ConfigValue NVARCHAR(MAX),
        ConfigType NVARCHAR(50),                          -- String, Int, Bool, JSON
        Description NVARCHAR(500),
        ModifiedDate DATETIME2 DEFAULT GETUTCDATE(),
        ModifiedBy NVARCHAR(100)
    );

    -- Insert default configuration
    INSERT INTO [atorfi].[RFI_Configuration] (ConfigKey, ConfigValue, ConfigType, Description) VALUES
    ('Reminder1Days', '7', 'Int', 'Days after initial RFI for first email reminder'),
    ('Reminder2Days', '14', 'Int', 'Days after initial RFI for SMS reminder'),
    ('FinalNoticeDays', '21', 'Int', 'Days after initial RFI for final notice and partner alert'),
    ('UploadLinkExpiryMonths', '6', 'Int', 'Months until upload links expire'),
    ('CurrentTaxYear', '2025', 'Int', 'Current tax year for RFI workflows'),
    ('UploadBaseURL', 'https://upload.yml.com.au/upload/', 'String', 'Base URL for client upload links'),
    ('DailyReportTime', '08:00', 'String', 'Time (AEST) for daily status report'),
    ('DailyReportChannelID', '', 'String', 'Teams channel ID for daily reports'),
    ('EmailFromAddress', 'tax@yml.com.au', 'String', 'Default from address for RFI emails'),
    ('SMSFromNumber', '', 'String', 'SMS sender number');

    PRINT 'Created table: RFI_Configuration with defaults';
END
GO

PRINT '=====================================================';
PRINT 'Schema creation completed successfully!';
PRINT '=====================================================';
GO
