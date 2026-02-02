-- =====================================================
-- YML PRE WORK IN - VIEWS FOR AI AGENT
-- Version: 1.0
-- Date: 2026-01-23
-- =====================================================
--
-- These views join the three data sources:
-- 1. SyncHub XPM tables (schema: xeropracticemanager_xpm_8963_2)
-- 2. ATO Report (dbo.[ATO Report])
-- 3. XPM Report (dbo.[XPM]) - TFN bridge
--
-- DATA FLOW:
-- ATO Report.TFN → XPM.Client_TaxNumber → XPM.Client_Client_Code → ClientDetails.ClientCode
-- =====================================================

USE [synchubdbxpm];
GO

-- =====================================================
-- VIEW 1: vw_ClientMaster
-- =====================================================
-- PURPOSE: Master client view - single source of truth for all client data
-- USE: Foundation view for all client queries, joins SyncHub + XPM + ATO data
-- DEPENDS ON: ClientDetails, Client, ClientGroupDetails, XPMReport, ATOReport
-- NOTES: Handles billing entity logic, client groups, and TFN matching

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ClientMaster')
    DROP VIEW [atorfi].[vw_ClientMaster];
GO

CREATE VIEW [atorfi].[vw_ClientMaster] AS
WITH
-- Get latest client record per UUID (preferring those with ClientGroupUUID)
LatestClientPerUUID AS (
    SELECT *
    FROM (
        SELECT *,
            ROW_NUMBER() OVER (
                PARTITION BY UUID
                ORDER BY
                    CASE WHEN ClientGroupUUID IS NOT NULL THEN 0 ELSE 1 END,
                    WhenUpsertedIntoDataStore DESC
            ) AS rn
        FROM [xeropracticemanager_xpm_8963_2].[Client]
    ) AS ranked
    WHERE rn = 1
),
-- Get clients who have their own TAX 2025 job (these are billing entities)
ClientsWithOwnJob AS (
    SELECT DISTINCT C.UUID AS ClientUUID
    FROM [xeropracticemanager_xpm_8963_2].[Client] C
    INNER JOIN [xeropracticemanager_xpm_8963_2].[JobDetails] JD
        ON JD.ClientUUID = C.UUID
        AND JD.Name LIKE '%CA - TAX - TAX 2025%'
        AND JD.IsDeleted = 0
),
-- Get billing entity (MIN Email/UUID) per client group for clients with jobs
GroupBillingEntity AS (
    SELECT
        C.ClientGroupDetailsRemoteID,
        MIN(CD.Email) AS BillingEntityEmail,
        MIN(CD.UUID) AS BillingEntityUUID
    FROM [xeropracticemanager_xpm_8963_2].[Client] C
    INNER JOIN [xeropracticemanager_xpm_8963_2].[JobDetails] JD
        ON JD.ClientUUID = C.UUID
        AND JD.Name LIKE '%CA - TAX - TAX 2025%'
        AND JD.IsDeleted = 0
    INNER JOIN [xeropracticemanager_xpm_8963_2].[ClientDetails] CD
        ON CD.UUID = C.UUID
    WHERE C.ClientGroupDetailsRemoteID IS NOT NULL
    GROUP BY C.ClientGroupDetailsRemoteID
)
SELECT
    -- Client Details (from SyncHub)
    cd.UUID AS ClientUUID,
    cd.RemoteID AS ClientRemoteID,
    cd.ClientCode,
    cd.Name AS ClientName,
    cd.FirstName,
    cd.LastName,
    cd.Email AS ClientEmail,
    cd.Phone AS ClientPhone,
    cd.BusinessStructure,
    cd.ActiveAtoClient,
    cd.BillingClientUUID,

    -- Account Manager (Partner)
    cd.AccountManagerUUID AS PartnerUUID,
    cd.AccountManagerName AS PartnerName,

    -- Job Manager
    cd.JobManagerUUID,
    cd.JobManagerName,

    -- Client Group (from Client bridge table)
    lc.ClientGroupUUID,
    cg.Name AS ClientGroupName,
    lc.ClientGroupDetailsRemoteID,
    LEFT(cd.ClientCode, 8) AS ClientGroupReference,

    -- XPM Report Data (manual upload - has unencrypted TFN)
    REPLACE(xpm.Client_Tax_Number, ' ','') AS TFN,
    xpm.Client_Casual_Name AS CasualName,
    TRY_CONVERT(DATETIME2, xpm.Client_Tax_Form_Lodgment_Due_Date, 106) AS XPMDueDate,
    CASE WHEN xpm.Client_BPOD_BAS_Prepared = 'Yes' THEN 1 ELSE 0 END AS IsBookkeepingClient,

    -- ATO Report Data (matched via TFN)
    ato.Client_Type AS ATOClientType,
    ato.Lodgment_Code AS ATOLodgementCode,
    TRY_CONVERT(DATETIME2, ato.Due_Date, 103) AS ATODueDate,
    ato._2025_Status AS ATO2025Status,
    ato._2024_Status AS ATO2024Status,
    ato.Flexible_Lodgment_Eligibility_2025 AS FlexibleLodgement2025,

    -- Computed Fields
    COALESCE(xpm.Client_Casual_Name, cd.FirstName, cd.Name) AS GreetingName,

    -- Billing Entity flag (Yes = has own job, No = grouped under another entity)
    CASE
        WHEN coj.ClientUUID IS NOT NULL THEN 'Yes'
        ELSE 'No'
    END AS BillingEntity,

    -- Billing Entity Details (based on job ownership)
    CASE
        WHEN coj.ClientUUID IS NOT NULL THEN cd.UUID
        WHEN cg.Name IN (
            'ZZZYFP   - ZZZ-FP',
            'ZZZSUS   - ZZZ-SUS(SMSF)',
            'ZZZ-INDIVIDUALS',
            'ZZZIND   - ZZZ-INDIVIDUALS'
        ) THEN NULL
        ELSE gbe.BillingEntityUUID
    END AS BillingEntityUUID,

    CASE
        WHEN coj.ClientUUID IS NOT NULL THEN cd.Name
        WHEN cg.Name IN (
            'ZZZYFP   - ZZZ-FP',
            'ZZZSUS   - ZZZ-SUS(SMSF)',
            'ZZZ-INDIVIDUALS',
            'ZZZIND   - ZZZ-INDIVIDUALS'
        ) THEN NULL
        ELSE be.Name
    END AS BillingEntityName,

    CASE
        WHEN coj.ClientUUID IS NOT NULL THEN cd.Email
        WHEN cg.Name IN (
            'ZZZYFP   - ZZZ-FP',
            'ZZZSUS   - ZZZ-SUS(SMSF)',
            'ZZZ-INDIVIDUALS',
            'ZZZIND   - ZZZ-INDIVIDUALS'
        ) THEN NULL
        ELSE gbe.BillingEntityEmail
    END AS BillingEntityEmail,

    CASE
        WHEN coj.ClientUUID IS NOT NULL THEN cd.Phone
        WHEN cg.Name IN (
            'ZZZYFP   - ZZZ-FP',
            'ZZZSUS   - ZZZ-SUS(SMSF)',
            'ZZZ-INDIVIDUALS',
            'ZZZIND   - ZZZ-INDIVIDUALS'
        ) THEN NULL
        ELSE be.Phone
    END AS BillingEntityPhone,

    -- Metadata
    cd.WhenModified AS LastSyncDate

FROM [xeropracticemanager_xpm_8963_2].[ClientDetails] cd

-- Join to get latest client record with ClientGroupUUID
LEFT JOIN LatestClientPerUUID lc
    ON cd.UUID = lc.UUID

-- Join to get ClientGroup name
LEFT JOIN [xeropracticemanager_xpm_8963_2].[ClientGroupDetails] cg
    ON lc.ClientGroupUUID = cg.UUID

-- Check if client has their own job (billing entity)
LEFT JOIN ClientsWithOwnJob coj
    ON cd.UUID = coj.ClientUUID

-- Get group billing entity info
LEFT JOIN GroupBillingEntity gbe
    ON lc.ClientGroupDetailsRemoteID = gbe.ClientGroupDetailsRemoteID

-- Get billing entity details for name/phone
LEFT JOIN [xeropracticemanager_xpm_8963_2].[ClientDetails] be
    ON be.UUID = gbe.BillingEntityUUID

-- Join XPM Report (bridge via Name)
LEFT JOIN [atorfi].[XPMReport] xpm
    ON cd.Name = xpm.Client_Client COLLATE SQL_Latin1_General_CP1_CI_AS

-- Join ATO Report (via TFN from XPM Report, removing spaces)
LEFT JOIN [atorfi].[ATOReport] ato
    ON ato.TFN = REPLACE(xpm.Client_Tax_Number, ' ','') COLLATE SQL_Latin1_General_CP1_CI_AS

WHERE (cd.IsArchived != 'Yes' OR cd.IsArchived IS NULL);
GO

PRINT 'Created view: vw_ClientMaster';
GO

-- =====================================================
-- VIEW 2: vw_ClientContacts
-- =====================================================
-- PURPOSE: Normalize client contacts with Primary/CC contact flags
-- USE: Contact lookup, identify who to email for each client
-- DEPENDS ON: Contact, ClientContact
-- NOTES: IsCCContact flag identifies contacts starting with "CC" prefix

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ClientContacts')
    DROP VIEW [atorfi].[vw_ClientContacts];
GO

CREATE VIEW [atorfi].[vw_ClientContacts] AS
SELECT
    cc.ClientDetailsUUID AS ClientUUID,
    con.UUID AS ContactUUID,
    con.Name AS ContactName,
    con.Email AS ContactEmail,
    con.Mobile AS ContactMobile,
    con.Phone AS ContactPhone,
    con.Position,
    con.Salutation,
    CASE WHEN con.IsPrimary = 'Yes' THEN 1 ELSE 0 END AS IsPrimary,

    -- Identify CC contacts (name starts with CC or cc)
    CASE
        WHEN con.Name LIKE 'CC%' OR con.Name LIKE 'cc%' OR con.Name LIKE 'Cc%' THEN 1
        ELSE 0
    END AS IsCCContact

FROM [xeropracticemanager_xpm_8963_2].[Contact] con
INNER JOIN [xeropracticemanager_xpm_8963_2].[ClientContact] cc
    ON con.UUID = cc.ContactUUID
WHERE con.IsDeleted = 0 OR con.IsDeleted IS NULL;
GO

PRINT 'Created view: vw_ClientContacts';
GO

-- =====================================================
-- VIEW 3: vw_ClientWithPrimaryContact
-- =====================================================
-- PURPOSE: Combine client master data with resolved primary/CC contacts
-- USE: Main client view for RFI emails - includes all contact info needed
-- DEPENDS ON: vw_ClientMaster, vw_ClientContacts
-- NOTES: Falls back to ClientDetails email/phone if no Contact record exists

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ClientWithPrimaryContact')
    DROP VIEW [atorfi].[vw_ClientWithPrimaryContact];
GO

CREATE VIEW [atorfi].[vw_ClientWithPrimaryContact] AS
SELECT
    cm.*,

    -- Primary Contact (from Contact table if exists, otherwise from ClientDetails)
    COALESCE(pc.ContactName, cm.ClientName) AS PrimaryContactName,
    COALESCE(pc.ContactEmail, cm.ClientEmail) AS PrimaryContactEmail,
    COALESCE(pc.ContactMobile, cm.ClientPhone) AS PrimaryContactMobile,

    -- CC Contact
    cc.ContactName AS CCContactName,
    cc.ContactEmail AS CCContactEmail

FROM [atorfi].[vw_ClientMaster] cm

-- Get Primary Contact
LEFT JOIN (
    SELECT ClientUUID, ContactName, ContactEmail, ContactMobile,
           ROW_NUMBER() OVER (PARTITION BY ClientUUID ORDER BY IsPrimary DESC, ContactName) AS rn
    FROM [atorfi].[vw_ClientContacts]
    WHERE IsCCContact = 0
) pc ON cm.ClientUUID = pc.ClientUUID AND pc.rn = 1

-- Get CC Contact
LEFT JOIN (
    SELECT ClientUUID, ContactName, ContactEmail,
           ROW_NUMBER() OVER (PARTITION BY ClientUUID ORDER BY ContactName) AS rn
    FROM [atorfi].[vw_ClientContacts]
    WHERE IsCCContact = 1
) cc ON cm.ClientUUID = cc.ClientUUID AND cc.rn = 1;
GO

PRINT 'Created view: vw_ClientWithPrimaryContact';
GO

-- =====================================================
-- VIEW 4: vw_JobMaster
-- =====================================================
-- PURPOSE: Master job view combining client data with TAX 2025 jobs
-- USE: Core view for all job-related queries, RFI workflows, and reporting
-- DEPENDS ON: vw_ClientWithPrimaryContact
-- NOTES: Handles both individual jobs and group jobs (fallback for grouped clients)

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_JobMaster')
    DROP VIEW [atorfi].[vw_JobMaster];
GO

CREATE VIEW [atorfi].[vw_JobMaster] AS
WITH
-- Get client group jobs (for clients without their own job)
ClientGroupJobs AS (
    SELECT
        C.ClientGroupDetailsRemoteID,
        JD.UUID AS GroupJobUUID,
        JD.ID AS GroupJobID,
        JD.Name AS GroupJobName,
        JD.Description AS GroupJobDescription,
        JD.State AS GroupJobState,
        JD.Type AS GroupJobType,
        JD.WebUrl AS GroupJobWebURL,
        JD.StartDate AS GroupJobStartDate,
        JD.DueDate AS GroupJobDueDate,
        JD.CompletedDate AS GroupJobCompletedDate,
        JD.Budget AS GroupJobBudget,
        JD.ManagerUUID AS GroupManagerUUID,
        JD.ManagerName AS GroupManagerName,
        JD.PartnerUUID AS GroupPartnerUUID,
        JD.WhenModified AS GroupJobWhenModified,
        ROW_NUMBER() OVER (PARTITION BY C.ClientGroupDetailsRemoteID ORDER BY JD.ID) AS rn
    FROM [xeropracticemanager_xpm_8963_2].[Client] C
    INNER JOIN [xeropracticemanager_xpm_8963_2].[JobDetails] JD
        ON JD.ClientUUID = C.UUID
        AND JD.Name LIKE '%CA - TAX - TAX 2025%'
        AND JD.IsDeleted = 0
    WHERE C.ClientGroupDetailsRemoteID IS NOT NULL
),
-- Main query with row ordering
JobData AS (
    SELECT
        -- Job Details (COALESCE: own job or group job)
        COALESCE(jd.UUID, cgj.GroupJobUUID) AS JobUUID,
        COALESCE(jd.ID, cgj.GroupJobID) AS JobID,
        COALESCE(jd.Name, cgj.GroupJobName) AS JobName,
        COALESCE(jd.Description, cgj.GroupJobDescription) AS JobDescription,
        COALESCE(jd.State, cgj.GroupJobState) AS JobState,
        COALESCE(jd.Type, cgj.GroupJobType) AS JobType,
        COALESCE(jd.WebUrl, cgj.GroupJobWebURL) AS JobWebURL,
        COALESCE(jd.StartDate, cgj.GroupJobStartDate) AS JobStartDate,
        COALESCE(jd.DueDate, cgj.GroupJobDueDate) AS JobDueDate,
        COALESCE(jd.CompletedDate, cgj.GroupJobCompletedDate) AS JobCompletedDate,
        COALESCE(jd.Budget, cgj.GroupJobBudget) AS JobBudget,

        -- Job Manager
        COALESCE(jd.ManagerUUID, cgj.GroupManagerUUID) AS ManagerUUID,
        COALESCE(jd.ManagerName, cgj.GroupManagerName) AS ManagerName,

        -- Job Partner
        COALESCE(jd.PartnerUUID, cgj.GroupPartnerUUID) AS JobPartnerUUID,

        -- Extract Tax Year from Job Name (format: "CA - TAX - TAX 2025")
        CASE
            WHEN COALESCE(jd.Name, cgj.GroupJobName) LIKE '%TAX 20%' THEN
                TRY_CAST(SUBSTRING(COALESCE(jd.Name, cgj.GroupJobName), PATINDEX('%TAX 20[0-9][0-9]%', COALESCE(jd.Name, cgj.GroupJobName)) + 4, 4) AS INT)
            WHEN COALESCE(jd.Name, cgj.GroupJobName) LIKE '%202%' THEN
                TRY_CAST(SUBSTRING(COALESCE(jd.Name, cgj.GroupJobName), PATINDEX('%202[0-9]%', COALESCE(jd.Name, cgj.GroupJobName)), 4) AS INT)
            ELSE NULL
        END AS TaxYear,

        -- Client Details (from vw_ClientWithPrimaryContact)
        c.ClientUUID,
        c.ClientCode,
        c.ClientName,
        c.CasualName,
        c.GreetingName,
        c.ClientEmail,
        c.ClientPhone,
        c.TFN,
        c.IsBookkeepingClient,
        c.BillingClientUUID,

        -- Client Group
        c.ClientGroupUUID,
        c.ClientGroupName,
        c.ClientGroupDetailsRemoteID,
        c.ClientGroupReference,

        -- Primary Contact
        c.PrimaryContactName,
        c.PrimaryContactEmail,
        c.PrimaryContactMobile,
        c.CCContactEmail,

        -- Partner (Account Manager)
        c.PartnerUUID,
        c.PartnerName,

        -- Billing Entity Details (from vw_ClientMaster via vw_ClientWithPrimaryContact)
        c.BillingEntity,
        c.BillingEntityUUID,
        c.BillingEntityName,
        c.BillingEntityEmail,
        c.BillingEntityPhone,

        -- ATO Data
        c.ATODueDate,
        c.ATOLodgementCode,
        c.ATO2025Status,
        c.ATOClientType,

        -- Simplified Job State for UI
        CASE
            WHEN COALESCE(jd.State, cgj.GroupJobState) IN ('CA - Pre Work In - EL', 'CA - Pre Work In - Non EL') THEN 'Pre Work In'
            WHEN COALESCE(jd.State, cgj.GroupJobState) = 'CA - Requested' THEN 'Requested'
            WHEN COALESCE(jd.State, cgj.GroupJobState) = 'CA - Work In' THEN 'Work In'
            WHEN COALESCE(jd.State, cgj.GroupJobState) LIKE '%Wait on Info%' THEN 'Wait on Info'
            WHEN COALESCE(jd.State, cgj.GroupJobState) = 'CA - Wait on Sign' THEN 'Wait on Sign'
            WHEN COALESCE(jd.State, cgj.GroupJobState) = 'CA - Ready to lodge' THEN 'Ready to Lodge'
            WHEN COALESCE(jd.State, cgj.GroupJobState) IN ('CA - Lodged', 'CA - Lodged by Client', 'CA - Lodged via portal') THEN 'Lodged'
            WHEN COALESCE(jd.State, cgj.GroupJobState) = 'CA - Complete' THEN 'Complete'
            WHEN COALESCE(jd.State, cgj.GroupJobState) = 'CA - Cancelled' THEN 'Cancelled'
            ELSE COALESCE(jd.State, cgj.GroupJobState)
        END AS SimplifiedJobState,

        -- Metadata
        COALESCE(jd.WhenModified, cgj.GroupJobWhenModified) AS JobLastSyncDate,

        -- Row ordering: Billing entity first (0), then others (1)
        ROW_NUMBER() OVER (
            PARTITION BY c.BillingEntityUUID 
            ORDER BY 
                CASE WHEN c.ClientUUID = c.BillingEntityUUID THEN 0 ELSE 1 END,
                c.ClientName
        ) AS BillingGroupOrder

    FROM [atorfi].[vw_ClientWithPrimaryContact] c

    -- Join individual client jobs (if exists)
    LEFT JOIN [xeropracticemanager_xpm_8963_2].[JobDetails] jd
        ON jd.ClientUUID = c.ClientUUID
        AND jd.Name LIKE '%CA - TAX - TAX 2025%'
        AND jd.IsDeleted = 0

    -- Join client group jobs (fallback if no individual job, exclude ZZZ groups)
    LEFT JOIN ClientGroupJobs cgj
        ON c.ClientGroupDetailsRemoteID = cgj.ClientGroupDetailsRemoteID
        AND cgj.rn = 1
        AND jd.ID IS NULL
        AND c.ClientGroupName NOT IN (
            'ZZZYFP   - ZZZ-FP',
            'ZZZSUS   - ZZZ-SUS(SMSF)',
            'ZZZ-INDIVIDUALS',
            'ZZZIND   - ZZZ-INDIVIDUALS'
        )

    -- Only return clients that have a job (either own or group)
    WHERE jd.ID IS NOT NULL OR cgj.GroupJobID IS NOT NULL
)
SELECT
    JobUUID,
    JobID,
    JobName,
    JobDescription,
    JobState,
    JobType,
    JobWebURL,
    JobStartDate,
    JobDueDate,
    JobCompletedDate,
    JobBudget,
    ManagerUUID,
    ManagerName,
    JobPartnerUUID,
    TaxYear,
    ClientUUID,
    ClientCode,
    ClientName,
    CasualName,
    GreetingName,
    ClientEmail,
    ClientPhone,
    TFN,
    IsBookkeepingClient,
    BillingClientUUID,
    ClientGroupUUID,
    ClientGroupName,
    ClientGroupDetailsRemoteID,
    ClientGroupReference,
    PrimaryContactName,
    PrimaryContactEmail,
    PrimaryContactMobile,
    CCContactEmail,
    PartnerUUID,
    PartnerName,
    BillingEntity,
    BillingEntityUUID,
    BillingEntityName,
    BillingEntityEmail,
    BillingEntityPhone,
    ATODueDate,
    ATOLodgementCode,
    ATO2025Status,
    ATOClientType,
    SimplifiedJobState,
    JobLastSyncDate,
    BillingGroupOrder
FROM JobData;
GO

PRINT 'Created view: vw_JobMaster';
GO

-- =====================================================
-- VIEW 5: vw_RFIEligibleJobs
-- =====================================================
-- PURPOSE: Filter jobs eligible for Request For Information workflow
-- USE: Identify clients needing initial RFI email, queue for bulk send
-- DEPENDS ON: vw_JobMaster, RFI_Workflows
-- FILTERS: Pre Work In state, not marked Do Not Send, not stopped

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RFIEligibleJobs')
    DROP VIEW [atorfi].[vw_RFIEligibleJobs];
GO

CREATE VIEW [atorfi].[vw_RFIEligibleJobs] AS
SELECT
    jm.*,

    -- RFI Workflow Status (if exists)
    rfi.RFIWorkflowID,
    rfi.RFIStatus,
    rfi.InitiatedDate AS RFIInitiatedDate,
    rfi.DoNotSendFlag,
    rfi.StoppedFlag,
    rfi.PartnerNotes AS RFIPartnerNotes,

    -- Days calculations
    CASE
        WHEN rfi.InitiatedDate IS NOT NULL THEN DATEDIFF(DAY, rfi.InitiatedDate, GETUTCDATE())
        ELSE NULL
    END AS DaysSinceRFIInitiated,

    CASE
        WHEN jm.ATODueDate IS NOT NULL THEN DATEDIFF(DAY, GETUTCDATE(), jm.ATODueDate)
        ELSE NULL
    END AS DaysUntilATODeadline

FROM [atorfi].[vw_JobMaster] jm

LEFT JOIN [atorfi].[RFI_Workflows] rfi
    ON jm.JobID = rfi.JobID AND jm.TaxYear = rfi.TaxYear

WHERE
    -- Only Pre Work In jobs
    jm.JobState IN ('CA - Pre Work In - EL', 'CA - Pre Work In - Non EL')

    -- Not marked as Do Not Send
    AND (rfi.DoNotSendFlag IS NULL OR rfi.DoNotSendFlag = 0)

    -- Not stopped
    AND (rfi.StoppedFlag IS NULL OR rfi.StoppedFlag = 0)

    -- Tax jobs only (Job name contains TAX)
    AND jm.JobName LIKE '%TAX%';
GO

PRINT 'Created view: vw_RFIEligibleJobs';
GO

-- =====================================================
-- VIEW 6: vw_RFIJobsGroupedByContact
-- =====================================================
-- PURPOSE: Aggregate RFI-eligible jobs by Primary Contact email
-- USE: Bulk email operations - send one email per contact with all their entities
-- DEPENDS ON: vw_RFIEligibleJobs
-- NOTES: Groups multiple jobs/entities into single email to avoid spam

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RFIJobsGroupedByContact')
    DROP VIEW [atorfi].[vw_RFIJobsGroupedByContact];
GO

CREATE VIEW [atorfi].[vw_RFIJobsGroupedByContact] AS
SELECT
    PrimaryContactEmail,
    PrimaryContactName,
    MAX(GreetingName) AS GreetingName,
    MAX(CCContactEmail) AS CCContactEmail,
    COUNT(DISTINCT JobID) AS JobCount,
    COUNT(DISTINCT ClientGroupUUID) AS GroupCount,
    MIN(ATODueDate) AS EarliestDueDate,
    MAX(ATODueDate) AS LatestDueDate,
    STRING_AGG(JobID, ',') WITHIN GROUP (ORDER BY ATODueDate, ClientName) AS JobIDs,
    STRING_AGG(ClientName, ' | ') WITHIN GROUP (ORDER BY ATODueDate, ClientName) AS ClientNames,
    MAX(PartnerName) AS PartnerName,
    MAX(PartnerUUID) AS PartnerUUID

FROM [atorfi].[vw_RFIEligibleJobs]

WHERE
    PrimaryContactEmail IS NOT NULL
    AND RFIStatus IS NULL  -- Not yet initiated

GROUP BY PrimaryContactEmail, PrimaryContactName;
GO

PRINT 'Created view: vw_RFIJobsGroupedByContact';
GO

-- =====================================================
-- VIEW 7: vw_RFIActiveWorkflows
-- =====================================================
-- PURPOSE: Track all in-progress RFI workflows with reminder schedules
-- USE: Identify which reminders are due, monitor workflow progress
-- DEPENDS ON: RFI_Workflows, vw_JobMaster
-- NOTES: Calculates reminder due dates (7, 14, 21 days) and flags overdue items

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RFIActiveWorkflows')
    DROP VIEW [atorfi].[vw_RFIActiveWorkflows];
GO

CREATE VIEW [atorfi].[vw_RFIActiveWorkflows] AS
SELECT
    rfi.*,
    jm.JobName,
    jm.JobState,
    jm.JobWebURL,
    jm.ClientName,
    jm.CasualName,
    jm.GreetingName,
    jm.ClientGroupName,
    jm.ClientGroupReference,
    jm.PartnerName,

    -- Calculate reminder schedule
    DATEADD(DAY, 7, rfi.InitiatedDate) AS Reminder1DueDate,
    DATEADD(DAY, 14, rfi.InitiatedDate) AS Reminder2DueDate,
    DATEADD(DAY, 21, rfi.InitiatedDate) AS FinalNoticeDueDate,

    -- Check if reminders are due
    CASE WHEN rfi.Reminder1SentDate IS NULL AND GETUTCDATE() >= DATEADD(DAY, 7, rfi.InitiatedDate) THEN 1 ELSE 0 END AS Reminder1Due,
    CASE WHEN rfi.Reminder2SentDate IS NULL AND GETUTCDATE() >= DATEADD(DAY, 14, rfi.InitiatedDate) THEN 1 ELSE 0 END AS Reminder2Due,
    CASE WHEN rfi.FinalNoticeSentDate IS NULL AND GETUTCDATE() >= DATEADD(DAY, 21, rfi.InitiatedDate) THEN 1 ELSE 0 END AS FinalNoticeDue,

    DATEDIFF(DAY, rfi.InitiatedDate, GETUTCDATE()) AS DaysSinceInitiated,
    DATEDIFF(DAY, GETUTCDATE(), rfi.ATODueDate) AS DaysUntilDeadline

FROM [atorfi].[RFI_Workflows] rfi
INNER JOIN [atorfi].[vw_JobMaster] jm
    ON rfi.JobID = jm.JobID

WHERE
    rfi.RFIStatus IN ('Requested', 'PartiallyReceived')
    AND rfi.StoppedFlag = 0
    AND rfi.DoNotSendFlag = 0;
GO

PRINT 'Created view: vw_RFIActiveWorkflows';
GO

-- =====================================================
-- VIEW 8: vw_AtRiskClients
-- =====================================================
-- PURPOSE: Identify non-responsive clients requiring escalation
-- USE: Partner review queue, phone follow-up list, deadline risk assessment
-- DEPENDS ON: vw_RFIActiveWorkflows
-- FILTERS: 21+ days since RFI initiated with no document uploads

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_AtRiskClients')
    DROP VIEW [atorfi].[vw_AtRiskClients];
GO

CREATE VIEW [atorfi].[vw_AtRiskClients] AS
SELECT
    *
FROM [atorfi].[vw_RFIActiveWorkflows]
WHERE
    DaysSinceInitiated >= 21
    AND FirstDocumentUploadDate IS NULL;
GO

PRINT 'Created view: vw_AtRiskClients';
GO

-- =====================================================
-- VIEW 9: vw_RFIStatusSummary
-- =====================================================
-- PURPOSE: Dashboard summary counts by job state
-- USE: Daily reporting, management dashboards, workflow metrics
-- DEPENDS ON: JobDetails (direct)
-- NOTES: Provides quick counts without client details for performance

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RFIStatusSummary')
    DROP VIEW [atorfi].[vw_RFIStatusSummary];
GO

CREATE VIEW [atorfi].[vw_RFIStatusSummary] AS
SELECT
    -- Job State Counts (from SyncHub)
    SUM(CASE WHEN jd.State = 'CA - Pre Work In - EL' THEN 1 ELSE 0 END) AS PreWorkInELCount,
    SUM(CASE WHEN jd.State = 'CA - Pre Work In - Non EL' THEN 1 ELSE 0 END) AS PreWorkInNonELCount,
    SUM(CASE WHEN jd.State = 'CA - Requested' THEN 1 ELSE 0 END) AS RequestedCount,
    SUM(CASE WHEN jd.State = 'CA - Work In' THEN 1 ELSE 0 END) AS WorkInCount,
    SUM(CASE WHEN jd.State LIKE '%Wait on Info%' THEN 1 ELSE 0 END) AS WaitOnInfoCount,
    SUM(CASE WHEN jd.State = 'CA - Wait on Sign' THEN 1 ELSE 0 END) AS WaitOnSignCount,
    SUM(CASE WHEN jd.State = 'CA - Ready to lodge' THEN 1 ELSE 0 END) AS ReadyToLodgeCount,
    SUM(CASE WHEN jd.State IN ('CA - Lodged', 'CA - Lodged by Client', 'CA - Lodged via portal') THEN 1 ELSE 0 END) AS LodgedCount,
    SUM(CASE WHEN jd.State = 'CA - Complete' THEN 1 ELSE 0 END) AS CompleteCount,
    COUNT(*) AS TotalJobs

FROM [xeropracticemanager_xpm_8963_2].[JobDetails] jd
WHERE
    jd.Name LIKE '%CA - TAX - TAX 2025%'
    AND (jd.IsDeleted = 0 OR jd.IsDeleted IS NULL);
GO

PRINT 'Created view: vw_RFIStatusSummary';
GO

-- =====================================================
-- VIEW 10: vw_ClientGroupEntities
-- =====================================================
-- PURPOSE: List all entities within each client group
-- USE: RFI email consolidation - show all entities in a group in one email
-- DEPENDS ON: vw_ClientMaster, RFI_ClientUploadLinks
-- NOTES: Identifies billing client and tracks upload link status per entity

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ClientGroupEntities')
    DROP VIEW [atorfi].[vw_ClientGroupEntities];
GO

CREATE VIEW [atorfi].[vw_ClientGroupEntities] AS
SELECT
    cm.ClientGroupUUID,
    cm.ClientGroupName,
    cm.ClientUUID,
    cm.ClientCode,
    cm.ClientName,
    cm.BusinessStructure,
    cm.BillingEntity,
    cm.BillingEntityUUID,
    cm.BillingClientUUID,

    -- ATO data from vw_ClientMaster
    cm.ATODueDate,
    cm.ATOLodgementCode,

    -- Upload link (if exists)
    ul.UploadLinkID,
    ul.UniqueToken AS UploadToken,
    CASE WHEN ul.IsActive = 1 THEN 1 ELSE 0 END AS HasActiveUploadLink

FROM [atorfi].[vw_ClientMaster] cm

LEFT JOIN [atorfi].[RFI_ClientUploadLinks] ul
    ON cm.ClientUUID = ul.ClientUUID AND ul.IsActive = 1

WHERE cm.ClientGroupUUID IS NOT NULL;
GO

PRINT 'Created view: vw_ClientGroupEntities';
GO

PRINT '=====================================================';
PRINT 'All views created successfully!';
PRINT '=====================================================';
GO
