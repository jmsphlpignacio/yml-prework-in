-- =====================================================
-- YML PRE WORK IN - STORED PROCEDURES FOR AI AGENT
-- Version: 1.0
-- Date: 2026-01-23
-- =====================================================
--
-- These stored procedures are called by the AI Agent tools
-- to search jobs, initiate RFI workflows, and manage status.
--
-- NAMING CONVENTION: sp_RFI_[Action]
-- =====================================================

USE [synchubdbxpm];
GO

-- =====================================================
-- SP 1: Search Jobs by Client Name or Code
-- =====================================================
-- PURPOSE: Find RFI-eligible jobs by searching client name, code, or casual name.
-- USE CASE: Partner types "Smith" to find all clients with Smith in their name.
--           Used by AI agent when partner asks "find jobs for client ABC" or "search for John Smith".
-- PARAMETERS:
--   @SearchTerm - Partial match on ClientName, ClientCode, CasualName, or GreetingName
--   @IncludeAlreadyRequested - If 1, includes jobs where RFI already sent (default 0)
-- RETURNS: List of matching jobs with client details, contact info, ATO dates, and RFI status.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_SearchByClient')
    DROP PROCEDURE [atorfi].[sp_RFI_SearchByClient];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_SearchByClient]
    @SearchTerm NVARCHAR(255),
    @IncludeAlreadyRequested BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        JobID,
        JobName,
        JobState,
        SimplifiedJobState,
        JobWebURL,
        TaxYear,
        ClientUUID,
        ClientCode,
        ClientName,
        CasualName,
        GreetingName,
        IsBookkeepingClient,
        ClientGroupName,
        ClientGroupReference,
        PrimaryContactName,
        PrimaryContactEmail,
        PrimaryContactMobile,
        CCContactEmail,
        PartnerName,
        ATODueDate,
        ATOLodgementCode,
        RFIStatus,
        RFIInitiatedDate,
        DaysSinceRFIInitiated,
        DaysUntilATODeadline
    FROM [atorfi].[vw_RFIEligibleJobs]
    WHERE
        (
            ClientName LIKE '%' + @SearchTerm + '%'
            OR ClientCode LIKE '%' + @SearchTerm + '%'
            OR CasualName LIKE '%' + @SearchTerm + '%'
            OR GreetingName LIKE '%' + @SearchTerm + '%'
        )
        AND (@IncludeAlreadyRequested = 1 OR RFIStatus IS NULL)
    ORDER BY ATODueDate, ClientName;
END;
GO

PRINT 'Created procedure: sp_RFI_SearchByClient';
GO

-- =====================================================
-- SP 2: Search Jobs by ATO Due Date
-- =====================================================
-- PURPOSE: Find RFI-eligible jobs within a specific ATO due date range.
-- USE CASE: Partner asks "show me all jobs due in February" or "jobs due between 1 Jan and 31 Mar".
--           Used for bulk RFI sends targeting specific deadline windows.
-- PARAMETERS:
--   @StartDate - Beginning of date range (inclusive)
--   @EndDate - End of date range (inclusive)
--   @IncludeAlreadyRequested - If 1, includes jobs where RFI already sent (default 0)
-- RETURNS: List of jobs ordered by due date, then contact email (for grouping), then client name.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_SearchByDueDate')
    DROP PROCEDURE [atorfi].[sp_RFI_SearchByDueDate];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_SearchByDueDate]
    @StartDate DATE,
    @EndDate DATE,
    @IncludeAlreadyRequested BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        JobID,
        JobName,
        JobState,
        SimplifiedJobState,
        JobWebURL,
        TaxYear,
        ClientUUID,
        ClientCode,
        ClientName,
        CasualName,
        GreetingName,
        IsBookkeepingClient,
        ClientGroupName,
        ClientGroupReference,
        PrimaryContactName,
        PrimaryContactEmail,
        PrimaryContactMobile,
        CCContactEmail,
        PartnerName,
        ATODueDate,
        ATOLodgementCode,
        RFIStatus,
        RFIInitiatedDate,
        DaysUntilATODeadline
    FROM [atorfi].[vw_RFIEligibleJobs]
    WHERE
        ATODueDate BETWEEN @StartDate AND @EndDate
        AND (@IncludeAlreadyRequested = 1 OR RFIStatus IS NULL)
    ORDER BY ATODueDate, PrimaryContactEmail, ClientName;
END;
GO

PRINT 'Created procedure: sp_RFI_SearchByDueDate';
GO

-- =====================================================
-- SP 3: Search Jobs by Month (Natural Language Helper)
-- =====================================================
-- PURPOSE: Simplified date search using just month number - converts to full date range.
-- USE CASE: AI agent helper when partner says "February jobs" or "jobs due in March".
--           Automatically calculates first and last day of the specified month.
-- PARAMETERS:
--   @Month - Month number (1-12)
--   @Year - Optional year (defaults to current year)
--   @IncludeAlreadyRequested - If 1, includes jobs where RFI already sent (default 0)
-- RETURNS: Calls sp_RFI_SearchByDueDate with calculated date range.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_SearchByMonth')
    DROP PROCEDURE [atorfi].[sp_RFI_SearchByMonth];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_SearchByMonth]
    @Month INT,
    @Year INT = NULL,
    @IncludeAlreadyRequested BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TargetYear INT = COALESCE(@Year, YEAR(GETDATE()));
    DECLARE @StartDate DATE = DATEFROMPARTS(@TargetYear, @Month, 1);
    DECLARE @EndDate DATE = EOMONTH(@StartDate);

    EXEC [atorfi].[sp_RFI_SearchByDueDate]
        @StartDate = @StartDate,
        @EndDate = @EndDate,
        @IncludeAlreadyRequested = @IncludeAlreadyRequested;
END;
GO

PRINT 'Created procedure: sp_RFI_SearchByMonth';
GO

-- =====================================================
-- SP 4: Search Bookkeeping Clients
-- =====================================================
-- PURPOSE: Find RFI-eligible jobs for clients flagged as bookkeeping clients.
-- USE CASE: Bookkeeping clients may have different info requirements or communication tone.
--           Partner asks "show me all bookkeeping clients due this month".
-- PARAMETERS:
--   @DueDateStart - Optional start of due date filter
--   @DueDateEnd - Optional end of due date filter
--   @IncludeAlreadyRequested - If 1, includes jobs where RFI already sent (default 0)
-- RETURNS: List of bookkeeping client jobs with IsBookkeepingClient = 1.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_SearchBookkeepingClients')
    DROP PROCEDURE [atorfi].[sp_RFI_SearchBookkeepingClients];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_SearchBookkeepingClients]
    @DueDateStart DATE = NULL,
    @DueDateEnd DATE = NULL,
    @IncludeAlreadyRequested BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        JobID,
        JobName,
        JobState,
        SimplifiedJobState,
        JobWebURL,
        TaxYear,
        ClientUUID,
        ClientCode,
        ClientName,
        CasualName,
        GreetingName,
        IsBookkeepingClient,
        ClientGroupName,
        ClientGroupReference,
        PrimaryContactName,
        PrimaryContactEmail,
        PrimaryContactMobile,
        CCContactEmail,
        PartnerName,
        ATODueDate,
        ATOLodgementCode,
        RFIStatus,
        RFIInitiatedDate,
        DaysUntilATODeadline
    FROM [atorfi].[vw_RFIEligibleJobs]
    WHERE
        IsBookkeepingClient = 1
        AND (@DueDateStart IS NULL OR ATODueDate >= @DueDateStart)
        AND (@DueDateEnd IS NULL OR ATODueDate <= @DueDateEnd)
        AND (@IncludeAlreadyRequested = 1 OR RFIStatus IS NULL)
    ORDER BY ATODueDate, ClientName;
END;
GO

PRINT 'Created procedure: sp_RFI_SearchBookkeepingClients';
GO

-- =====================================================
-- SP 5: Get Job Details
-- =====================================================
-- PURPOSE: Retrieve comprehensive details for a single job including RFI history.
-- USE CASE: Partner clicks on a job to see full details before initiating RFI or reviewing status.
--           Shows related entities in client group, all communications sent, and documents uploaded.
-- PARAMETERS:
--   @JobID - The unique job identifier
-- RETURNS: Four result sets:
--   1. Job details with RFI workflow status
--   2. Related entities in the same client group
--   3. Communication history (emails/SMS sent)
--   4. Documents uploaded by the client

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_GetJobDetails')
    DROP PROCEDURE [atorfi].[sp_RFI_GetJobDetails];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_GetJobDetails]
    @JobID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Result Set 1: Job Details
    SELECT
        jm.*,
        rfi.RFIWorkflowID,
        rfi.RFIStatus,
        rfi.InitiatedDate AS RFIInitiatedDate,
        rfi.InitiatedByPartnerName,
        rfi.PartnerNotes AS RFIPartnerNotes,
        rfi.Reminder1SentDate,
        rfi.Reminder2SentDate,
        rfi.FinalNoticeSentDate,
        rfi.FirstDocumentUploadDate,
        rfi.DoNotSendFlag,
        rfi.StoppedFlag
    FROM [atorfi].[vw_JobMaster] jm
    LEFT JOIN [atorfi].[RFI_Workflows] rfi ON jm.JobID = rfi.JobID
    WHERE jm.JobID = @JobID;

    -- Result Set 2: Communication History
    SELECT
        CommunicationType,
        CommunicationStage,
        ToAddress,
        Subject,
        SendStatus,
        SentDate,
        FailureReason
    FROM [atorfi].[RFI_CommunicationLog]
    WHERE RFIWorkflowID = (
        SELECT RFIWorkflowID FROM [atorfi].[RFI_Workflows] WHERE JobID = @JobID
    )
    ORDER BY CreatedDate;

    -- Result Set 3: Uploaded Documents
    SELECT
        DocumentID,
        OriginalFileName,
        FileType,
        FileSize,
        DocumentCategory,
        UploadedDate,
        FiledToFYI
    FROM [atorfi].[RFI_DocumentUploads]
    WHERE JobID = @JobID
    ORDER BY UploadedDate DESC;
END;
GO

PRINT 'Created procedure: sp_RFI_GetJobDetails';
GO

-- =====================================================
-- SP 6: Initiate RFI for Job
-- =====================================================
-- PURPOSE: Start the RFI workflow for a job - creates workflow record and queues initial email.
-- USE CASE: Partner confirms "yes, send RFI to this client". Creates the workflow, generates
--           a unique upload link, and queues the initial email for sending.
--           For multiple jobs, call this procedure once per job.
-- PARAMETERS:
--   @JobID - The job to initiate RFI for
--   @PartnerUUID - UUID of the partner initiating (optional)
--   @PartnerName - Name of the partner initiating (required for audit)
--   @PartnerEmail - Email of the partner (optional)
--   @PartnerNotes - Any notes from partner about this client (optional)
-- RETURNS: Success/Error status with RFIWorkflowID and email recipient.
-- SIDE EFFECTS: Creates RFI_Workflows record, RFI_ClientUploadLinks record, RFI_CommunicationLog entry.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_InitiateJob')
    DROP PROCEDURE [atorfi].[sp_RFI_InitiateJob];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_InitiateJob]
    @JobID NVARCHAR(50),
    @PartnerUUID UNIQUEIDENTIFIER = NULL,
    @PartnerName NVARCHAR(100),
    @PartnerEmail NVARCHAR(255) = NULL,
    @PartnerNotes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @RFIWorkflowID UNIQUEIDENTIFIER = NEWID();
    DECLARE @ClientUUID UNIQUEIDENTIFIER;
    DECLARE @ClientCode NVARCHAR(50);
    DECLARE @ClientGroupUUID UNIQUEIDENTIFIER;
    DECLARE @PrimaryContactName NVARCHAR(200);
    DECLARE @PrimaryContactEmail NVARCHAR(255);
    DECLARE @PrimaryContactMobile NVARCHAR(50);
    DECLARE @CCContactEmail NVARCHAR(255);
    DECLARE @ATODueDate DATE;
    DECLARE @ATOLodgementCode NVARCHAR(50);
    DECLARE @TaxYear INT;
    DECLARE @JobUUID UNIQUEIDENTIFIER;

    -- Check if already initiated
    IF EXISTS (SELECT 1 FROM [atorfi].[RFI_Workflows] WHERE JobID = @JobID AND RFIStatus NOT IN ('DoNotSend', 'Completed'))
    BEGIN
        SELECT 'Error' AS Status, 'RFI already initiated for this job' AS Message;
        ROLLBACK;
        RETURN;
    END

    -- Get job details
    SELECT
        @ClientUUID = ClientUUID,
        @ClientCode = ClientCode,
        @ClientGroupUUID = ClientGroupUUID,
        @PrimaryContactName = PrimaryContactName,
        @PrimaryContactEmail = PrimaryContactEmail,
        @PrimaryContactMobile = PrimaryContactMobile,
        @CCContactEmail = CCContactEmail,
        @ATODueDate = ATODueDate,
        @ATOLodgementCode = ATOLodgementCode,
        @TaxYear = TaxYear,
        @JobUUID = JobUUID
    FROM [atorfi].[vw_RFIEligibleJobs]
    WHERE JobID = @JobID;

    IF @ClientUUID IS NULL
    BEGIN
        SELECT 'Error' AS Status, 'Job not found or not eligible for RFI' AS Message;
        ROLLBACK;
        RETURN;
    END

    IF @PrimaryContactEmail IS NULL
    BEGIN
        SELECT 'Error' AS Status, 'No primary contact email found for this client' AS Message;
        ROLLBACK;
        RETURN;
    END

    -- Create RFI Workflow
    INSERT INTO [atorfi].[RFI_Workflows] (
        RFIWorkflowID,
        JobID,
        JobUUID,
        ClientUUID,
        ClientCode,
        ClientGroupUUID,
        PrimaryContactName,
        PrimaryContactEmail,
        PrimaryContactMobile,
        CCContactEmail,
        RFIStatus,
        InitiatedByPartnerUUID,
        InitiatedByPartnerName,
        InitiatedByPartnerEmail,
        PartnerNotes,
        InitiatedDate,
        TaxYear,
        ATODueDate,
        ATOLodgementCode
    )
    VALUES (
        @RFIWorkflowID,
        @JobID,
        @JobUUID,
        @ClientUUID,
        @ClientCode,
        @ClientGroupUUID,
        @PrimaryContactName,
        @PrimaryContactEmail,
        @PrimaryContactMobile,
        @CCContactEmail,
        'Requested',
        @PartnerUUID,
        @PartnerName,
        @PartnerEmail,
        @PartnerNotes,
        GETUTCDATE(),
        @TaxYear,
        @ATODueDate,
        @ATOLodgementCode
    );

    -- Create upload link if doesn't exist
    IF NOT EXISTS (SELECT 1 FROM [atorfi].[RFI_ClientUploadLinks] WHERE ClientUUID = @ClientUUID AND TaxYear = @TaxYear AND IsActive = 1)
    BEGIN
        INSERT INTO [atorfi].[RFI_ClientUploadLinks] (
            ClientUUID,
            ClientCode,
            ClientName,
            TaxYear,
            UniqueToken,
            ExpiryDate
        )
        SELECT
            @ClientUUID,
            @ClientCode,
            ClientName,
            @TaxYear,
            LOWER(REPLACE(NEWID(), '-', '')),  -- Generate unique token
            DATEADD(MONTH, 6, GETUTCDATE())  -- 6 month expiry
        FROM [atorfi].[vw_ClientMaster]
        WHERE ClientUUID = @ClientUUID;
    END

    -- Queue initial email (to be processed by communication service)
    INSERT INTO [atorfi].[RFI_CommunicationLog] (
        RFIWorkflowID,
        CommunicationType,
        CommunicationStage,
        ToAddress,
        CCAddress,
        Subject,
        BodyTemplate,
        SendStatus,
        ScheduledDate,
        DaysSinceInitial
    )
    VALUES (
        @RFIWorkflowID,
        'Email',
        'Initial',
        @PrimaryContactEmail,
        @CCContactEmail,
        'Tax Document Request — Action Required',
        'InitialRFI',
        'Pending',
        GETUTCDATE(),
        0
    );

    -- Audit log
    INSERT INTO [atorfi].[RFI_AuditLog] (EntityType, EntityID, Action, ActionDetail, NewValue, PerformedBy, PerformedByType, PerformedByEmail)
    VALUES ('RFIWorkflow', CAST(@RFIWorkflowID AS NVARCHAR(100)), 'Created', 'RFI Initiated', @PartnerNotes, @PartnerName, 'Partner', @PartnerEmail);

    COMMIT;

    -- Return success with details
    SELECT
        'Success' AS Status,
        @RFIWorkflowID AS RFIWorkflowID,
        @JobID AS JobID,
        @PrimaryContactEmail AS EmailTo,
        'Initial RFI email queued for sending' AS Message;
END;
GO

PRINT 'Created procedure: sp_RFI_InitiateJob';
GO

-- =====================================================
-- SP 7: Mark Job as Do Not Send
-- =====================================================
-- PURPOSE: Exclude a job from RFI workflow - client should not receive automated emails.
-- USE CASE: Partner says "don't send RFI to this client - they're already in contact" or
--           "this client prefers phone calls". Prevents job appearing in eligible lists.
-- PARAMETERS:
--   @JobID - The job to exclude
--   @PartnerName - Name of partner making the decision (required for audit)
--   @PartnerEmail - Email of partner (optional)
--   @Notes - Reason for exclusion (optional but recommended)
-- RETURNS: Success status with RFIWorkflowID.
-- SIDE EFFECTS: Creates or updates RFI_Workflows with DoNotSendFlag = 1.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_MarkDoNotSend')
    DROP PROCEDURE [atorfi].[sp_RFI_MarkDoNotSend];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_MarkDoNotSend]
    @JobID NVARCHAR(50),
    @PartnerName NVARCHAR(100),
    @PartnerEmail NVARCHAR(255) = NULL,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @RFIWorkflowID UNIQUEIDENTIFIER;
    DECLARE @ClientUUID UNIQUEIDENTIFIER;
    DECLARE @PrimaryContactEmail NVARCHAR(255);

    -- Check if workflow exists
    SELECT @RFIWorkflowID = RFIWorkflowID
    FROM [atorfi].[RFI_Workflows]
    WHERE JobID = @JobID;

    IF @RFIWorkflowID IS NOT NULL
    BEGIN
        -- Update existing workflow
        UPDATE [atorfi].[RFI_Workflows]
        SET
            DoNotSendFlag = 1,
            RFIStatus = 'DoNotSend',
            PartnerNotes = COALESCE(@Notes, PartnerNotes),
            ModifiedDate = GETUTCDATE()
        WHERE RFIWorkflowID = @RFIWorkflowID;
    END
    ELSE
    BEGIN
        -- Create new workflow with DoNotSend flag
        SET @RFIWorkflowID = NEWID();

        SELECT
            @ClientUUID = ClientUUID,
            @PrimaryContactEmail = PrimaryContactEmail
        FROM [atorfi].[vw_JobMaster]
        WHERE JobID = @JobID;

        INSERT INTO [atorfi].[RFI_Workflows] (
            RFIWorkflowID,
            JobID,
            ClientUUID,
            PrimaryContactEmail,
            RFIStatus,
            DoNotSendFlag,
            PartnerNotes,
            InitiatedByPartnerName,
            InitiatedByPartnerEmail
        )
        VALUES (
            @RFIWorkflowID,
            @JobID,
            @ClientUUID,
            @PrimaryContactEmail,
            'DoNotSend',
            1,
            @Notes,
            @PartnerName,
            @PartnerEmail
        );
    END

    -- Audit log
    INSERT INTO [atorfi].[RFI_AuditLog] (EntityType, EntityID, Action, ActionDetail, NewValue, PerformedBy, PerformedByType, PerformedByEmail)
    VALUES ('RFIWorkflow', CAST(@RFIWorkflowID AS NVARCHAR(100)), 'MarkedDoNotSend', 'Job excluded from RFI', @Notes, @PartnerName, 'Partner', @PartnerEmail);

    COMMIT;

    SELECT 'Success' AS Status, 'Job marked as Do Not Send' AS Message, @RFIWorkflowID AS RFIWorkflowID;
END;
GO

PRINT 'Created procedure: sp_RFI_MarkDoNotSend';
GO

-- =====================================================
-- SP 8: Stop RFI Flow
-- =====================================================
-- PURPOSE: Stop an active RFI workflow - cancel pending reminders and mark as stopped.
-- USE CASE: Partner says "stop sending reminders to this client - they called and are sending docs".
--           Differs from DoNotSend: this stops an already-initiated workflow mid-process.
-- PARAMETERS:
--   @JobID - The job whose RFI flow should stop
--   @PartnerName - Name of partner stopping the flow (required for audit)
--   @PartnerEmail - Email of partner (optional)
--   @Reason - Why the flow is being stopped (optional but recommended)
-- RETURNS: Success/Error status with RFIWorkflowID.
-- SIDE EFFECTS: Updates RFI_Workflows (StoppedFlag=1), cancels pending RFI_CommunicationLog entries.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_StopFlow')
    DROP PROCEDURE [atorfi].[sp_RFI_StopFlow];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_StopFlow]
    @JobID NVARCHAR(50),
    @PartnerName NVARCHAR(100),
    @PartnerEmail NVARCHAR(255) = NULL,
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RFIWorkflowID UNIQUEIDENTIFIER;

    SELECT @RFIWorkflowID = RFIWorkflowID
    FROM [atorfi].[RFI_Workflows]
    WHERE JobID = @JobID AND RFIStatus = 'Requested';

    IF @RFIWorkflowID IS NULL
    BEGIN
        SELECT 'Error' AS Status, 'No active RFI workflow found for this job' AS Message;
        RETURN;
    END

    -- Update workflow
    UPDATE [atorfi].[RFI_Workflows]
    SET
        StoppedFlag = 1,
        RFIStatus = 'Stopped',
        StoppedDate = GETUTCDATE(),
        PartnerNotes = COALESCE(@Reason, PartnerNotes),
        ModifiedDate = GETUTCDATE()
    WHERE RFIWorkflowID = @RFIWorkflowID;

    -- Cancel pending communications
    UPDATE [atorfi].[RFI_CommunicationLog]
    SET SendStatus = 'Cancelled'
    WHERE RFIWorkflowID = @RFIWorkflowID AND SendStatus = 'Pending';

    -- Audit log
    INSERT INTO [atorfi].[RFI_AuditLog] (EntityType, EntityID, Action, ActionDetail, NewValue, PerformedBy, PerformedByType, PerformedByEmail)
    VALUES ('RFIWorkflow', CAST(@RFIWorkflowID AS NVARCHAR(100)), 'Stopped', 'RFI flow stopped', @Reason, @PartnerName, 'Partner', @PartnerEmail);

    SELECT 'Success' AS Status, 'RFI flow stopped successfully' AS Message, @RFIWorkflowID AS RFIWorkflowID;
END;
GO

PRINT 'Created procedure: sp_RFI_StopFlow';
GO

-- =====================================================
-- SP 9: Get Daily Status Summary
-- =====================================================
-- PURPOSE: Get comprehensive dashboard summary of all job states and RFI workflow metrics.
-- USE CASE: Daily morning report, partner dashboard, "give me the current status" queries.
--           Shows counts by job state, RFI status, at-risk clients, and reminders due.
-- PARAMETERS: None
-- RETURNS: Five result sets:
--   1. Job state counts (Pre Work In, Requested, Work In, etc.)
--   2. RFI workflow status counts
--   3. At-risk client count (>21 days no response)
--   4. Approaching deadline count (due within 7 days, no RFI sent)
--   5. Reminders due today (T+7, T+14, T+21)

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_GetDailyStatus')
    DROP PROCEDURE [atorfi].[sp_RFI_GetDailyStatus];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_GetDailyStatus]
AS
BEGIN
    SET NOCOUNT ON;

    -- Job State Summary
    SELECT * FROM [atorfi].[vw_RFIStatusSummary];

    -- RFI Workflow Status
    SELECT
        RFIStatus,
        COUNT(*) AS WorkflowCount
    FROM [atorfi].[RFI_Workflows]
    WHERE RFIStatus NOT IN ('Completed', 'DoNotSend')
    GROUP BY RFIStatus;

    -- At-Risk Clients (>21 days no response)
    SELECT COUNT(*) AS AtRiskCount
    FROM [atorfi].[vw_AtRiskClients];

    -- Approaching Deadline (7 days)
    SELECT COUNT(*) AS ApproachingDeadlineCount
    FROM [atorfi].[vw_RFIEligibleJobs]
    WHERE
        ATODueDate BETWEEN GETDATE() AND DATEADD(DAY, 7, GETDATE())
        AND RFIStatus IS NULL;

    -- Reminders Due Today
    SELECT
        SUM(CASE WHEN Reminder1Due = 1 THEN 1 ELSE 0 END) AS Reminder1DueCount,
        SUM(CASE WHEN Reminder2Due = 1 THEN 1 ELSE 0 END) AS Reminder2DueCount,
        SUM(CASE WHEN FinalNoticeDue = 1 THEN 1 ELSE 0 END) AS FinalNoticeDueCount
    FROM [atorfi].[vw_RFIActiveWorkflows];
END;
GO

PRINT 'Created procedure: sp_RFI_GetDailyStatus';
GO

-- =====================================================
-- SP 10: Get At-Risk Clients
-- =====================================================
-- PURPOSE: List all clients who haven't responded after 21+ days of RFI initiation.
-- USE CASE: Partner review queue - these clients need manual phone follow-up.
--           Shows contact details, reminder history, and days until ATO deadline.
-- PARAMETERS: None
-- RETURNS: List of at-risk clients ordered by ATO due date (most urgent first),
--          then by days since initiated (longest waiting first).

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_GetAtRiskClients')
    DROP PROCEDURE [atorfi].[sp_RFI_GetAtRiskClients];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_GetAtRiskClients]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        RFIWorkflowID,
        JobID,
        JobName,
        ClientName,
        CasualName,
        GreetingName,
        PrimaryContactEmail,
        PrimaryContactMobile,
        PartnerName,
        ATODueDate,
        DaysSinceInitiated,
        DaysUntilDeadline,
        InitiatedDate,
        Reminder1SentDate,
        Reminder2SentDate,
        FinalNoticeSentDate
    FROM [atorfi].[vw_AtRiskClients]
    ORDER BY ATODueDate, DaysSinceInitiated DESC;
END;
GO

PRINT 'Created procedure: sp_RFI_GetAtRiskClients';
GO

-- =====================================================
-- SP 11: Record Document Upload
-- =====================================================
-- PURPOSE: Record a document uploaded by client via their unique upload link.
-- USE CASE: Called by upload portal API when client submits a file. Validates the upload
--           token, stores document metadata, updates workflow status if first upload.
-- PARAMETERS:
--   @UploadToken - The unique token from the upload URL
--   @OriginalFileName - Client's original filename
--   @StoredFileName - Renamed filename in Azure storage
--   @FileSize - File size in bytes
--   @FileType - File extension
--   @MimeType - MIME type of the file
--   @DocumentCategory - Optional category selected by client
--   @StorageContainer - Azure Blob container name
--   @StoragePath - Full path in the container
--   @BlobURL - Direct URL to the blob
--   @IPAddress - Client's IP address (for audit)
--   @UserAgent - Client's browser info (for audit)
-- RETURNS: Success/Error status with DocumentID and IsFirstUpload flag.
-- SIDE EFFECTS: Creates RFI_DocumentUploads record, updates RFI_Workflows if first upload.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_RecordDocumentUpload')
    DROP PROCEDURE [atorfi].[sp_RFI_RecordDocumentUpload];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_RecordDocumentUpload]
    @UploadToken NVARCHAR(100),
    @OriginalFileName NVARCHAR(255),
    @StoredFileName NVARCHAR(255),
    @FileSize BIGINT,
    @FileType NVARCHAR(50),
    @MimeType NVARCHAR(100),
    @DocumentCategory NVARCHAR(100) = NULL,
    @StorageContainer NVARCHAR(100),
    @StoragePath NVARCHAR(500),
    @BlobURL NVARCHAR(1000),
    @IPAddress NVARCHAR(50) = NULL,
    @UserAgent NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @DocumentID UNIQUEIDENTIFIER = NEWID();
    DECLARE @UploadLinkID UNIQUEIDENTIFIER;
    DECLARE @ClientUUID UNIQUEIDENTIFIER;
    DECLARE @TaxYear INT;
    DECLARE @RFIWorkflowID UNIQUEIDENTIFIER;
    DECLARE @JobID NVARCHAR(50);
    DECLARE @IsFirstUpload BIT = 0;

    -- Get upload link details
    SELECT
        @UploadLinkID = UploadLinkID,
        @ClientUUID = ClientUUID,
        @TaxYear = TaxYear
    FROM [atorfi].[RFI_ClientUploadLinks]
    WHERE UniqueToken = @UploadToken AND IsActive = 1;

    IF @UploadLinkID IS NULL
    BEGIN
        SELECT 'Error' AS Status, 'Invalid or expired upload link' AS Message;
        ROLLBACK;
        RETURN;
    END

    -- Update last accessed
    UPDATE [atorfi].[RFI_ClientUploadLinks]
    SET LastAccessedDate = GETUTCDATE(), AccessCount = AccessCount + 1
    WHERE UploadLinkID = @UploadLinkID;

    -- Find related RFI workflow
    SELECT TOP 1
        @RFIWorkflowID = RFIWorkflowID,
        @JobID = JobID
    FROM [atorfi].[RFI_Workflows]
    WHERE ClientUUID = @ClientUUID AND TaxYear = @TaxYear AND RFIStatus IN ('Requested', 'PartiallyReceived')
    ORDER BY InitiatedDate DESC;

    -- Check if this is first upload for workflow
    IF @RFIWorkflowID IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM [atorfi].[RFI_DocumentUploads] WHERE RFIWorkflowID = @RFIWorkflowID
    )
    BEGIN
        SET @IsFirstUpload = 1;
    END

    -- Insert document record
    INSERT INTO [atorfi].[RFI_DocumentUploads] (
        DocumentID,
        UploadLinkID,
        RFIWorkflowID,
        ClientUUID,
        JobID,
        OriginalFileName,
        StoredFileName,
        FileSize,
        FileType,
        MimeType,
        DocumentCategory,
        StorageContainer,
        StoragePath,
        BlobURL,
        UploadedByIP,
        UploadedByUserAgent
    )
    VALUES (
        @DocumentID,
        @UploadLinkID,
        @RFIWorkflowID,
        @ClientUUID,
        @JobID,
        @OriginalFileName,
        @StoredFileName,
        @FileSize,
        @FileType,
        @MimeType,
        @DocumentCategory,
        @StorageContainer,
        @StoragePath,
        @BlobURL,
        @IPAddress,
        @UserAgent
    );

    -- Update workflow if first upload
    IF @IsFirstUpload = 1 AND @RFIWorkflowID IS NOT NULL
    BEGIN
        UPDATE [atorfi].[RFI_Workflows]
        SET
            RFIStatus = 'PartiallyReceived',
            FirstDocumentUploadDate = GETUTCDATE(),
            ModifiedDate = GETUTCDATE()
        WHERE RFIWorkflowID = @RFIWorkflowID;
    END

    -- Audit log
    INSERT INTO [atorfi].[RFI_AuditLog] (EntityType, EntityID, Action, ActionDetail, NewValue, PerformedBy, PerformedByType, IPAddress)
    VALUES ('Document', CAST(@DocumentID AS NVARCHAR(100)), 'Uploaded', @OriginalFileName, @StoragePath, 'Client', 'Client', @IPAddress);

    COMMIT;

    SELECT
        'Success' AS Status,
        @DocumentID AS DocumentID,
        @IsFirstUpload AS IsFirstUpload,
        CASE WHEN @IsFirstUpload = 1 THEN 'First document received - workflow updated' ELSE 'Document uploaded successfully' END AS Message;
END;
GO

PRINT 'Created procedure: sp_RFI_RecordDocumentUpload';
GO

-- =====================================================
-- SP 12: Get Pending Reminders
-- =====================================================
-- PURPOSE: Get all reminders that are due to be sent today - used by automated scheduler.
-- USE CASE: Scheduled job runs daily, calls this proc to get list of emails/SMS to send.
--           Returns three separate lists: T+7 emails, T+14 SMS, T+21 final notices.
-- PARAMETERS: None
-- RETURNS: Three result sets:
--   1. T+7 Email reminders due (Reminder1)
--   2. T+14 SMS reminders due (Reminder2) - only if mobile number exists
--   3. T+21 Final notices due (FinalNotice) - includes partner alert info

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_GetPendingReminders')
    DROP PROCEDURE [atorfi].[sp_RFI_GetPendingReminders];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_GetPendingReminders]
AS
BEGIN
    SET NOCOUNT ON;

    -- T+7 Email Reminders
    SELECT
        RFIWorkflowID,
        JobID,
        PrimaryContactEmail,
        CCContactEmail,
        ClientName,
        GreetingName,
        ATODueDate,
        'Reminder1' AS ReminderType
    FROM [atorfi].[vw_RFIActiveWorkflows]
    WHERE Reminder1Due = 1;

    -- T+14 SMS Reminders
    SELECT
        RFIWorkflowID,
        JobID,
        PrimaryContactMobile,
        ClientName,
        GreetingName,
        InitiatedDate,
        'Reminder2' AS ReminderType
    FROM [atorfi].[vw_RFIActiveWorkflows]
    WHERE Reminder2Due = 1 AND PrimaryContactMobile IS NOT NULL;

    -- T+21 Final Notice
    SELECT
        RFIWorkflowID,
        JobID,
        PrimaryContactEmail,
        CCContactEmail,
        PrimaryContactMobile,
        ClientName,
        GreetingName,
        ATODueDate,
        DaysUntilDeadline,
        PartnerName,
        'FinalNotice' AS ReminderType
    FROM [atorfi].[vw_RFIActiveWorkflows]
    WHERE FinalNoticeDue = 1;
END;
GO

PRINT 'Created procedure: sp_RFI_GetPendingReminders';
GO

-- =====================================================
-- SP 13: Create Daily Snapshot
-- =====================================================
-- PURPOSE: Capture current job and RFI counts for historical tracking and trend analysis.
-- USE CASE: Scheduled job runs each morning to record daily metrics. Enables "how many jobs
--           moved from Pre Work In to Work In this week?" type reporting and dashboard charts.
-- PARAMETERS: None
-- RETURNS: None (prints confirmation message).
-- SIDE EFFECTS: Inserts one row into RFI_DailySnapshots with current counts and day-over-day changes.
-- NOTE: Safe to call multiple times per day - skips if snapshot already exists for today.

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_RFI_CreateDailySnapshot')
    DROP PROCEDURE [atorfi].[sp_RFI_CreateDailySnapshot];
GO

CREATE PROCEDURE [atorfi].[sp_RFI_CreateDailySnapshot]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    -- Check if snapshot already exists for today
    IF EXISTS (SELECT 1 FROM [atorfi].[RFI_DailySnapshots] WHERE SnapshotDate = @Today)
    BEGIN
        PRINT 'Snapshot already exists for today';
        RETURN;
    END

    -- Get yesterday's snapshot for change calculation
    DECLARE @YesterdayPreWorkIn INT = 0;
    DECLARE @YesterdayRequested INT = 0;
    DECLARE @YesterdayWorkIn INT = 0;

    SELECT
        @YesterdayPreWorkIn = PreWorkInELCount + PreWorkInNonELCount,
        @YesterdayRequested = RequestedCount,
        @YesterdayWorkIn = WorkInCount
    FROM [atorfi].[RFI_DailySnapshots]
    WHERE SnapshotDate = DATEADD(DAY, -1, @Today);

    -- Insert today's snapshot
    INSERT INTO [atorfi].[RFI_DailySnapshots] (
        SnapshotDate,
        PreWorkInELCount,
        PreWorkInNonELCount,
        RequestedCount,
        WorkInCount,
        WaitOnInfoCount,
        WaitOnSignCount,
        ReadyToLodgeCount,
        LodgedCount,
        CompleteCount,
        RFIPendingCount,
        RFIRequestedCount,
        RFIPartiallyReceivedCount,
        RFICompletedCount,
        RFIStoppedCount,
        RFIDoNotSendCount,
        AtRiskOver21DaysCount,
        ApproachingDeadline7DaysCount,
        PreWorkInChange,
        RequestedChange,
        WorkInChange
    )
    SELECT
        @Today,
        ss.PreWorkInELCount,
        ss.PreWorkInNonELCount,
        ss.RequestedCount,
        ss.WorkInCount,
        ss.WaitOnInfoCount,
        ss.WaitOnSignCount,
        ss.ReadyToLodgeCount,
        ss.LodgedCount,
        ss.CompleteCount,
        -- RFI counts
        (SELECT COUNT(*) FROM [atorfi].[RFI_Workflows] WHERE RFIStatus = 'Pending'),
        (SELECT COUNT(*) FROM [atorfi].[RFI_Workflows] WHERE RFIStatus = 'Requested'),
        (SELECT COUNT(*) FROM [atorfi].[RFI_Workflows] WHERE RFIStatus = 'PartiallyReceived'),
        (SELECT COUNT(*) FROM [atorfi].[RFI_Workflows] WHERE RFIStatus = 'Completed'),
        (SELECT COUNT(*) FROM [atorfi].[RFI_Workflows] WHERE RFIStatus = 'Stopped'),
        (SELECT COUNT(*) FROM [atorfi].[RFI_Workflows] WHERE RFIStatus = 'DoNotSend'),
        -- Attention counts
        (SELECT COUNT(*) FROM [atorfi].[vw_AtRiskClients]),
        (SELECT COUNT(*) FROM [atorfi].[vw_RFIEligibleJobs] WHERE ATODueDate BETWEEN GETDATE() AND DATEADD(DAY, 7, GETDATE()) AND RFIStatus IS NULL),
        -- Changes
        (ss.PreWorkInELCount + ss.PreWorkInNonELCount) - @YesterdayPreWorkIn,
        ss.RequestedCount - @YesterdayRequested,
        ss.WorkInCount - @YesterdayWorkIn
    FROM [atorfi].[vw_RFIStatusSummary] ss;

    PRINT 'Daily snapshot created for ' + CAST(@Today AS VARCHAR(10));
END;
GO

PRINT 'Created procedure: sp_RFI_CreateDailySnapshot';
GO

PRINT '=====================================================';
PRINT 'All stored procedures created successfully!';
PRINT '=====================================================';
GO
