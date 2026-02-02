# YML Pre Work In Project - Claude Specification

## Overview

This document consolidates the Business Requirements Document (BRD), Agent Outputs & Templates, and Data Mapping specifications for the YML Pre Work In automation project. The system automates client engagement and document collection for outstanding ATO lodgements using a Microsoft Teams AI Agent.

---

## 1. Executive Summary

### Project Background
YML's current 'Pre Work In' process manages engagement and coordination with clients to receive information required for outstanding ATO lodgements. The existing process is manual, time-intensive, and requires multiple handovers between Tax Assistants (TAs) and Partners, creating opportunities for errors and delays.

### Solution
This project automates key activities including client communication, reminder escalation, and internal handovers using Agentic AI technology through a Microsoft Teams Agent, enabling natural language interaction for Partners while maintaining a complete audit trail.

### Project Objectives
- Improve client experience through streamlined document collection
- Reduce time to engage and receive information from clientsc
- Reduce manual intervention required by TAs, TAAs, and Partners
- Minimise errors through standardised automation and consistent communication
- Pilot Agentic AI technology within YML internal processes

### Scope

**In Scope:**
- Complete 'Pre Work In' process automation
- Systems: XPM, FYI Docs, Email, SMS Gateway, Data Warehouse
- Microsoft AI ecosystem: AI Foundry, Teams Agent
- ATO data integration for lodgement due dates

**Out of Scope:**
- Work In process (post-document receipt)
- Systems and interfaces not listed above
- Non-ATO campaigns

---

## 2. Glossary

| Term | Definition |
|------|------------|
| **ATO** | Australian Taxation Office |
| **Billing Client** | The primary client entity responsible for payment and primary point of contact for a Client Group |
| **Bookkeeping Client** | A client who also engages YML for bookkeeping services (recorded in XPM under 'Affiliation' custom field) |
| **Client Code** | Unique identifier for each client in XPM, created during onboarding |
| **Client Group** | A Billing Client and all related entities (trusts, companies) sharing the same Primary Contact |
| **Fixed EL** | Fixed Engagement Letter — client has signed agreement outlining agreed fee for year-end work |
| **FYI** | FYI Docs Platform — document management system |
| **Non-Fixed EL** | Client without a signed engagement letter for year-end work |
| **Outstanding Lodgement** | A return or form the ATO expects to be lodged that has not yet been submitted |
| **Pre Work In** | XPM job state indicating work has not yet commenced; eligible for RFI |
| **Primary Contact** | The main contact for a Billing Client, identified in XPM Client Details or marked with CC/Cc prefix |
| **RFI** | Request for Information — workflow process to collect year-end documents from clients |
| **TFN** | Tax File Number — unique identifier for tax entities in Australia |
| **XPM** | Xero Practice Manager — practice management system |

---

## 3. Functional Requirements

### 3.1 Data Integration

The system integrates data from three sources to create a complete view of clients and their lodgement obligations:
- **ATO Portal** - lodgement due dates
- **XPM API** - client data model
- **XPM TFN Report** - TFN-to-client mapping

#### BR-1.1.1: Job and Client Data Model

| Field | Value |
|-------|-------|
| **ID** | BR-1.1.1 |
| **Requirement** | Maintain Access to Complete Job and Client Data |
| **Description** | System must have access to all Job, Client, and related entity data from XPM necessary to support RFI workflows. |
| **Input** | Data synchronized from XPM (nightly or real-time via SyncHub) |
| **Output** | Complete data model including: Job Data (JobID, JobName, JobState), Client Data (ClientID, ClientName, ClientCode), Client Contact Data (Name, Email, Mobile, CC Contact), Client Group Data (ClientGroupID, Related Clients), Historical Data (Prior-year Job Start Date, Prior-year Invoice Date), ATO Data (Lodgement Due Date) |
| **Frequency** | Nightly minimum, or on-demand for queries |
| **Dependencies** | BR-1.2.1, BR-1.2.2: ATO lodgement data merged with client records |

#### BR-1.1.2: RFI-Eligible Job Identification

| Field | Value |
|-------|-------|
| **ID** | BR-1.1.2 |
| **Requirement** | Identify Jobs Eligible for RFI Initiation |
| **Description** | System must identify which Jobs are eligible for document request communications. Only Jobs in appropriate state and not previously excluded should be available for Partner selection. |
| **Input** | All Jobs from XPM (BR-1.1.1) |
| **Output** | Filtered list of Jobs where: JobState is 'Pre Work In - EL' OR 'Pre Work In - Non EL', Job NOT marked 'Do Not Send', Job NOT had RFI Flow stopped |
| **Frequency** | Applied to every Partner query in real-time |
| **Dependencies** | BR-1.1.1: Data model query function, BR-5.2: 'Do Not Send' flag management, BR-5.1: 'Stopped' flag management |

#### BR-1.2.1: ATO Portal Data Upload

| Field | Value |
|-------|-------|
| **ID** | BR-1.2.1 |
| **Requirement** | ATO Portal Data Upload Interface |
| **Description** | YML Operations manually exports report from ATO Portal containing lodgement due dates for YML-registered clients, then uploads via system interface. |
| **Input** | CSV file from ATO Portal containing: TFN, Client Name (ATO), Lodgement Status, Lodgement Due Date |
| **Output** | Data uploaded to system with upload confirmation and record count. Validation errors displayed if any. |
| **Frequency** | Ad-hoc (as needed) |
| **Assumptions** | File must be CSV format, required columns present, date format DD/MM/YYYY, TFN format 8 or 9 digits |
| **Dependencies** | BR-1.2.2: TFN matching logic to link ATO data to XPM clients |

#### BR-1.2.2: XPM TFN Report Upload and Matching

| Field | Value |
|-------|-------|
| **ID** | BR-1.2.2 |
| **Requirement** | XPM Client TFN Report Upload and Matching |
| **Description** | YML Operations exports Client TFN report from XPM and uploads via same interface. System matches ATO data to XPM Clients by TFN to enrich records with lodgement due dates. |
| **Input** | CSV file from XPM containing: TFN, Client Name (XPM), Client Code |
| **Output** | XPM TFN data uploaded, Client records updated with ATOLodgementDueDate (matched from BR-1.2.1) |
| **Frequency** | Ad-hoc (uploaded initially, then as needed when clients added or TFN data changes) |
| **Dependencies** | BR-1.2.1: ATO Portal data already uploaded |

#### BR-1.3.1: Required Document Configuration

| Field | Value |
|-------|-------|
| **ID** | BR-1.3.1 |
| **Requirement** | Define and Maintain Client-Specific Required Document Lists |
| **Description** | System maintains a specific list of required documents for each Client entity. Lists are customized based on prior-year lodgement work or other assessment. |
| **Input** | Client-specific required document list defined by Accountants (typically at completion of prior-year lodgement) |
| **Output** | For each Client entity: List of specific documents required (e.g., 'Trust Distribution Statement', 'Bank Statements Jan-Jun') |
| **Frequency** | Updated per client annually, with ad-hoc updates as circumstances change |
| **Dependencies** | BR-1.2.1: ATO Portal data |

---

### 3.2 Teams Agent (AI)

Partners interact with the system through a Microsoft Teams AI Agent using natural language queries to retrieve and manage RFI-eligible jobs.

#### BR-2.1.1: Natural Language Processing

| Field | Value |
|-------|-------|
| **ID** | BR-2.1.1 |
| **Requirement** | Teams Agent Natural Language Processing |
| **Description** | Partners interact with Teams Agent via chat using natural language to retrieve Jobs. Agent interprets query and extracts query criteria. |
| **Input** | Natural language specifying date period (e.g., 'Jobs due in October', 'Lodgements due next month') |
| **Output** | Query criteria extracted from natural language (client name, date range, filters) ready for Job retrieval |
| **Frequency** | On-demand (per Partner message) |
| **Assumptions** | Partners have Teams access, Agent accessible in direct message, Agent handles typos and phrasing variations |
| **Dependencies** | BR-2.2.x: Supported query types define extractable criteria |

#### BR-2.2.1: Query by Client Name/Code

| Field | Value |
|-------|-------|
| **ID** | BR-2.2.1 |
| **Requirement** | Query Jobs by Client Name, Client Code |
| **Description** | Partners search for specific Jobs by client name or code to identify clients for RFI Flow initiation. |
| **Input** | Natural language specifying client identifier (e.g., 'Jobs for Acme', 'Show me ACME0001', 'Search for Smith clients') |
| **Output** | Matching Jobs with complete data per BR-1.1.1, or indication no Jobs found. Output heading includes Primary Contact name. |
| **Frequency** | On-demand |
| **Dependencies** | BR-1.1.1: Job and Client data access, BR-1.1.2: Pre Work In state filtering |

#### BR-2.2.2: Query by ATO Lodgement Due Date

| Field | Value |
|-------|-------|
| **ID** | BR-2.2.2 |
| **Requirement** | Query Jobs by ATO Lodgement Due Date |
| **Description** | Partners retrieve Jobs where client has ATO Lodgement Due Date within specified period to proactively initiate RFI Flows. |
| **Input** | Date period (e.g., 'October 2025', 'next month', date range) |
| **Output** | Matching Jobs with complete data per BR-1.1.1, grouped by Primary Contact |
| **Frequency** | On-demand |
| **Dependencies** | BR-1.1.1, BR-1.2.2: ATO lodgement data matched to clients, BR-1.1.2 |

#### BR-2.2.3: Query by Prior-Year Work In Start Date

| Field | Value |
|-------|-------|
| **ID** | BR-2.2.3 |
| **Requirement** | Query Jobs by Prior-Year Work In Start Date |
| **Description** | Partners identify Jobs where Billing Client had Work In Start Date in same month of prior year to reach clients who historically provide documents early. |
| **Input** | Natural language specifying month/year (e.g., 'Clients who started work in March last year', 'Jobs that began in April 2024') |
| **Output** | Matching Jobs with complete data per BR-1.1.1 |
| **Frequency** | On-demand |
| **Assumptions** | 'Last year' interpreted as prior financial year (July-June in Australia), Prior-year data available in XPM |

#### BR-2.2.4: Query by Prior-Year Invoice Date

| Field | Value |
|-------|-------|
| **ID** | BR-2.2.4 |
| **Requirement** | Query Jobs by Prior-Year Invoice Date |
| **Description** | Partners identify Jobs where Billing Client was invoiced in same month of prior year to set realistic expectations based on historical completion patterns. |
| **Input** | Natural language specifying month/year (e.g., 'Clients invoiced in May last year', 'Jobs completed in June 2024') |
| **Output** | Matching Jobs with complete data per BR-1.1.1 |
| **Frequency** | On-demand |

#### BR-2.2.5: Query for Bookkeeping Clients

| Field | Value |
|-------|-------|
| **ID** | BR-2.2.5 |
| **Requirement** | Query Jobs for Bookkeeping Clients |
| **Description** | Partners identify Jobs where Billing Client is also a Bookkeeping Client to coordinate with Business Services Division before initiating RFI. |
| **Input** | Natural language requesting bookkeeping clients (e.g., 'Show me bookkeeping clients') |
| **Output** | Matching Jobs with complete data per BR-1.1.1 |
| **Frequency** | On-demand |

#### BR-2.2.6: Combined Query Criteria

| Field | Value |
|-------|-------|
| **ID** | BR-2.2.6 |
| **Requirement** | Query Jobs on Multiple Criteria |
| **Description** | Partners combine multiple query criteria to narrow results. |
| **Input** | Natural language combining criteria (e.g., 'Show me bookkeeping clients with lodgements due in October') |
| **Output** | Matching Jobs with complete data per BR-1.1.1 |
| **Frequency** | On-demand |
| **Assumptions** | Agent handles typos and phrasing variations |
| **Dependencies** | BR-2.2.1 through BR-2.2.5, BR-1.1.1 |

---

### 3.3 Job Review and RFI Approval

#### BR-3.1.1: Display Query Results

| Field | Value |
|-------|-------|
| **ID** | BR-3.1.1 |
| **Requirement** | Display Query Results with Action Options |
| **Description** | After Partner queries for Jobs, system displays results with options to send RFI to all Jobs (bulk) or review each individually. |
| **Input** | Query results from BR-2.2.x |
| **Output** | List of matching Jobs with summary information. Two action options: 'Send RFI to All' button, 'Review Individually' button |
| **Frequency** | Per query |
| **Dependencies** | BR-2.2.x, BR-1.1.1 |

#### BR-3.1.2: Individual Job Review

| Field | Value |
|-------|-------|
| **ID** | BR-3.1.2 |
| **Requirement** | Display Individual Job Details for Review |
| **Description** | When Partner selects 'Review Individually', system displays detailed information for each Job for context review. |
| **Input** | Partner clicks 'Review Individually' button from BR-3.1.1 |
| **Output** | Detailed view showing: Complete Job data per BR-1.1.1, Primary Contact details, Related entities, Prior-year history. Actions: 'Send RFI', 'Do Not Send', Optional notes field |
| **Frequency** | When Partner chooses individual review |

#### BR-3.2.1: Bulk Send RFI

| Field | Value |
|-------|-------|
| **ID** | BR-3.2.1 |
| **Requirement** | Bulk Send RFI to All Jobs |
| **Description** | Partner triggers RFI Flow for all Jobs in query results with single action. |
| **Input** | Partner clicks 'Bulk Approve (Send RFI to All)' button |
| **Output** | Confirmation message with count of Jobs processed, All Jobs updated to 'Requested' state, Initial RFI emails sent to Primary Contacts |
| **Frequency** | On-demand |

#### BR-3.2.2: Individual Job Send RFI

| Field | Value |
|-------|-------|
| **ID** | BR-3.2.2 |
| **Requirement** | Individual Job Send RFI |
| **Description** | Partner reviews individual Job and initiates RFI Flow for that specific Job, optionally adding notes. |
| **Input** | Partner clicks 'Send RFI' on specific Job, Optional: Partner notes (max 500 characters) |
| **Output** | Confirmation message, Job updated to 'Requested' state, Initial RFI email sent with Partner notes in body |
| **Frequency** | Per individual Job decision |

#### BR-3.2.3: Mark Job 'Do Not Send'

| Field | Value |
|-------|-------|
| **ID** | BR-3.2.3 |
| **Requirement** | Mark Individual Job as 'Do Not Send' |
| **Description** | Partner marks specific Job as ineligible for RFI. Job is permanently excluded from future queries and workflows. |
| **Input** | Partner clicks 'Do Not Send' on specific Job, Optional: exclusion notes |
| **Output** | Job marked with 'Do Not Send' flag, Exclusion logged with Partner ID, timestamp, notes |
| **Frequency** | Per individual Job decision |

---

### 3.4 Client Communication and Escalation

#### BR-4.1.1: Generate Secure Upload Links

| Field | Value |
|-------|-------|
| **ID** | BR-4.1.1 |
| **Requirement** | Generate Client-Specific Secure Upload Link |
| **Description** | System generates unique secure upload URL for each Client in the master client list. Links are pre-generated and available when RFI emails are sent. |
| **Input** | Client list from XPM TFN Report (BR-1.2.2) and/or ATO Portal Report (BR-1.2.1) |
| **Output** | Unique upload URL per Client, linked to ClientID and TaxYear. URL expiry: 6 months from generation. |
| **Frequency** | Triggered by data uploads from BR-1.2.1 and BR-1.2.2 |

#### BR-4.1.2: Send Initial RFI Email

| Field | Value |
|-------|-------|
| **ID** | BR-4.1.2 |
| **Requirement** | Send Initial RFI Email to Primary Contact |
| **Description** | When Partner approves RFI, system sends initial email to Primary Contact with secure upload links. Email consolidates all Jobs sharing same Primary Contact, organized by Client Group. |
| **Input** | Partner Job approval from BR-3.2.1 or BR-3.2.2, Primary Contact Email, Secure upload links from BR-4.1.1 |
| **Output** | Single consolidated email per Primary Contact containing: Subject line and greeting, For each Job: Billing Client name (group heading), Billing Client with upload link, Related Clients with upload links, Partner Notes (if any). Job State updated to 'Requested'. |
| **Frequency** | Once per RFI initiation (consolidated per Primary Contact) |
| **Dependencies** | BR-4.1.1, BR-3.2.1 or BR-3.2.2 |

#### BR-4.2.1: First Email Reminder (T+7)

| Field | Value |
|-------|-------|
| **ID** | BR-4.2.1 |
| **Requirement** | First Email Reminder (T+7 Days) |
| **Description** | System sends automated reminder email 7 days after initial RFI if no documents uploaded. |
| **Input** | RFI sent date (T+0), Client upload status (no documents), Primary Contact Email, CC Contact Email (if exists) |
| **Output** | Reminder email to Primary Contact (To:) and CC Contact (CC:) with: Subject indicating reminder, Friendly tone, Same secure upload links, Emphasis on deadline |
| **Frequency** | Once, at T+7 days |
| **Assumptions** | Email only sent if Job State remains 'Requested', not sent if documents uploaded or RFI stopped |

#### BR-4.2.2: SMS Reminder (T+14)

| Field | Value |
|-------|-------|
| **ID** | BR-4.2.2 |
| **Requirement** | SMS Reminder (T+14 Days) |
| **Description** | System sends automated SMS reminder 14 days after initial RFI if no documents uploaded. |
| **Input** | RFI sent date (T+0), Client upload status (no documents), Primary Contact Mobile |
| **Output** | SMS message (160 chars max) with: YML identification, Brief reminder about tax documents needed |
| **Frequency** | Once, at T+14 days |
| **Assumptions** | Primary Contact Mobile exists, SMS not sent if mobile missing/invalid, documents uploaded, or RFI stopped |

#### BR-4.2.3: Final Notice Email (T+21)

| Field | Value |
|-------|-------|
| **ID** | BR-4.2.3 |
| **Requirement** | Final Notice Email (T+21 Days) |
| **Description** | System sends final automated email 21 days after initial RFI if no documents uploaded. Last client-facing communication before Partner escalation. |
| **Input** | RFI sent date (T+0), Client upload status (no documents), Primary Contact Email, CC Contact Email |
| **Output** | Final notice email with: Subject indicating urgency, Professional urgency tone, Days remaining until ATO deadline, Late lodgement consequences warning, Upload links, Partner contact information |
| **Frequency** | Once, at T+21 days |

#### BR-4.2.4: Partner Escalation Notification (T+21)

| Field | Value |
|-------|-------|
| **ID** | BR-4.2.4 |
| **Requirement** | Partner Escalation Notification (T+21 Days) |
| **Description** | System sends automated Teams notification to Partner 21 days after initial RFI if no documents uploaded. Alerts Partner that manual intervention is required. |
| **Input** | RFI sent date (T+0), Client upload status (no documents) |
| **Output** | Teams message to Partner with: At-risk client alert, Client Name and Job Number, ATO Deadline |
| **Frequency** | Once, at T+21 days |
| **Assumptions** | Partner expected to take manual action, Job remains in 'Requested' state |

---

### 3.5 Client Document Upload

#### BR-4.3.1: Secure Upload Page

| Field | Value |
|-------|-------|
| **ID** | BR-4.3.1 |
| **Requirement** | Secure Client Upload Page |
| **Description** | Client accesses secure upload page via unique link from email. Page is specific to one Client entity and displays required document categories with separate upload sections. |
| **Input** | Client clicks secure upload link (unique to specific tax entity) |
| **Output** | Upload page displaying: YML branding, Welcome message with Client entity name, List of required documents, File upload area per document (multiple files allowed), Visual indication of uploaded documents, Success confirmation after upload |
| **Frequency** | Accessed by client on-demand (multiple times if needed) |
| **Assumptions** | Document-specific upload sections enable completion tracking (stretch goal) |

#### BR-4.3.2: Document Storage

| Field | Value |
|-------|-------|
| **ID** | BR-4.3.2 |
| **Requirement** | Store Uploaded Documents Securely |
| **Description** | When client uploads documents, files are stored securely with metadata for retrieval, processing, and audit. |
| **Input** | Client file upload from BR-4.3.1, Document category, Client entity identifier |
| **Output** | Documents stored with metadata: DocumentID, ClientID, JobID, DocumentCategory, UploadTimestamp, Filename, FileSize, FileType |
| **Frequency** | Per file upload |

#### BR-4.3.3: Job State Transition on Upload

| Field | Value |
|-------|-------|
| **ID** | BR-4.3.3 |
| **Requirement** | Update Job State on First Document Upload |
| **Description** | When client uploads first document for any Client entity related to a Job, system updates that Job from 'Requested' to 'Work In' state. |
| **Input** | First document upload event for any Client entity in Job's Client Group |
| **Output** | Job updated: Requested → Work In. RFI reminder schedule stopped for that Job. |
| **Frequency** | Once per Job (on first document upload for any entity in that Job's Client Group) |
| **Assumptions** | If multiple Jobs share same Primary Contact but different Client Groups, only affected Job is updated |

#### BR-4.4.1: File to FYI

| Field | Value |
|-------|-------|
| **ID** | BR-4.4.1 |
| **Requirement** | File Documents to FYI Client Folder |
| **Description** | System files uploaded client documents to appropriate Client folder in FYI document management system. |
| **Input** | Documents stored per BR-4.3.2 |
| **Output** | Documents filed in FYI: /Clients/Workpaper(Cabinet)/Year{TaxYear}/Tax Info(Worktype). Original filename preserved. Metadata added: ClientID, JobNumber, UploadDate, DocumentCategory |
| **Frequency** | Per document upload (or batched at intervals) |
| **Assumptions** | FYI API integration available |

---

### 3.6 Manual Actions and Exceptions

#### BR-5.1.1: Partner Upload Access

| Field | Value |
|-------|-------|
| **ID** | BR-5.1.1 |
| **Requirement** | Partner Access to Client Upload Links |
| **Description** | Partners must have access to client-specific upload links to upload documents on behalf of clients when received through alternative channels (email, mail, phone). |
| **Input** | Partner views Job details |
| **Output** | Client-specific upload links displayed for: Billing Client entity, All related Client entities in Client Group. Links accessible and functional for Partner use. |
| **Frequency** | On-demand (when Partner views Job details) |
| **Assumptions** | Partners use same upload page as clients (BR-4.3.1), upload by Partner triggers same workflows (BR-4.3.2, BR-4.3.3) |

---

### 3.7 Reporting and Monitoring

#### BR-6.1.1: Daily Status Report

| Field | Value |
|-------|-------|
| **ID** | BR-6.1.1 |
| **Requirement** | Daily Teams Report with Job State Counts |
| **Description** | System automatically posts daily summary to Partner Teams channel showing Job distribution across states. |
| **Input** | Scheduled trigger (daily at configured time, e.g., 8am AEST) |
| **Output** | Teams Adaptive Card showing: Total Jobs count, Job counts by simplified XPM State (Pre Work In, Requested, Work In, Wait on Sign, For Lodgement, Complete), Attention needed counts (At-risk >21 days, Email bounces) |
| **Frequency** | Daily (automated) |

---

## 4. Process Flow Overview

The process aligns with YML Group and XPM system data model, where Billing Clients serve as the primary communication point for potentially multiple related tax entities (Client Groups), with all communications directed to the Billing Client's Primary Contact.

### 4.1 Setup Phase
The system integrates data from three sources:
1. **ATO Portal** - provides lodgement due dates and status for tax-registered clients
2. **XPM Client Data** - supplies the client data model including jobs, contacts, and client group relationships
3. **XPM TFN Report** - provides the TFN-to-Client name mapping that bridges ATO and XPM data

### 4.2 Monitor Phase
Daily Teams Status Report provides Partners with visibility into workflow status across all jobs, summarizing counts by state and highlighting attention-needed scenarios.

### 4.3 Initiate Phase
Partners use the Teams AI Agent to query and review jobs requiring document uploads. The agent interprets natural language queries and returns jobs in 'Pre Work In' state. Results can be approved in bulk or reviewed individually before RFI initiation.

### 4.4 Communicate Phase
Manages automated client outreach through structured escalation:
- **T+0** - Initial Email
- **T+7** - Email Reminder
- **T+14** - SMS Reminder
- **T+21** - Final Notice + Partner Alert

System generates unique secure upload links for each entity, enabling granular tracking.

### 4.5 Process Phase
When documents are received and job transitions to 'Work In', uploaded documents are automatically filed to FYI document management system for Partner access.

### 4.6 State Transitions

| State | Description |
|-------|-------------|
| **Pre Work In** | Job created, eligible for RFI |
| **Requested** | RFI sent to client, reminder sequence active |
| **Work In** | First document uploaded, processing can begin |
| **Stopped** | Partner manually stopped RFI flow |
| **Do Not Send** | Partner excluded job from RFI |

---

## 5. Data Mapping

### 5.1 Source Data Systems

#### XPM Report (Manual CSV Upload)
Manually downloaded from XPM as CSV, uploaded to database table `[dbo].[XPMReport]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S002 | Client_Tax_Number | nvarchar(max), null | Client TFN |
| S003 | Client_Tax_Form_Lodgment_Due_Date | nvarchar(max), null | Lodgement due date |
| S004 | Client_Client | nvarchar(max), null | Client Name |
| S005 | Client_Casual_Name | nvarchar(max), null | Casual name for communications |
| S006 | Client_Client_Code | nvarchar(max), null | Client code identifier |
| S007 | Client_Email | nvarchar(max), null | Client email |
| S008 | Client_Phone | nvarchar(max), null | Client phone |
| S009 | Client_Job_Manager | nvarchar(max), null | Job manager |
| S010 | Client_Account_Manager | nvarchar(max), null | Account manager |
| S011 | Client_Casual_Name2 | nvarchar(max), null | Secondary casual name |
| S012 | Client_Active_ATO_Client | nvarchar(max), null | Active ATO status |
| S013 | Client_BPOD_BAS_Prepared | nvarchar(max), null | BAS prepared status |

#### ATO Report (Manual CSV Upload)
Manually downloaded from ATO as CSV, uploaded to database table `[dbo].[ATOReport]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S015 | TFN | nvarchar(max), null | Client TFN |
| S016 | Client_Type | nvarchar(max), null | Client Type |
| S017 | Client_Name | nvarchar(max), null | Client Name |
| S018 | Substituted_Accounting_Period | nvarchar(max), null | Accounting period |
| S019 | Lodgment_Code | nvarchar(max), null | Lodgement code |
| S020 | _2025_Status | nvarchar(max), null | 2025 lodgement status |
| S021 | Due_Date | nvarchar(max), null | Due Date |
| S022-S029 | Various status fields | nvarchar(max), null | Historical status and eligibility |

#### SyncHub XPM Data Model
Daily sync from XPM API into YML Azure SQL Database

**ClientDetails Table** - `[xeropracticemanager_xpm_8963_2].[ClientDetails]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S032 | RemoteID | nvarchar(200), not null | Primary Key |
| S033 | Name | nvarchar(max), null | Name of Client Entity |
| S034 | Email | nvarchar(max), null | Client Email Contact |
| S035 | Phone | nvarchar(max), null | Client Phone Contact |
| S036 | ClientCode | nvarchar(max), null | Client Code (e.g., ACME0001) |
| S037 | AccountManagerName | nvarchar(max), null | YML Partner responsible for Client |
| S038 | JobManagerName | nvarchar(max), null | YML Employee responsible for Client Jobs |
| S039 | BusinessStructure | nvarchar(max), null | Entity type (Company, Trust, Partnership, Individual) |

**ClientCustomField Table** - `[xeropracticemanager_xpm_8963_2].[ClientCustomField]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S041 | Casual Name | nvarchar(max), null | Name for client communications (e.g., "Yoav") |
| S042 | Business Services Client | Boolean(bit), null | Identifies bookkeeping clients (~169 out of 6000+) |

**ClientContact / Contact Tables**

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S043 | ClientContactCCEmail | nvarchar(max), null | CC Contact Email (where Contact Name is like CC/cc) |

**Client Table** - `[xeropracticemanager_xpm_8963_2].[Client]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S045 | UUID | uniqueidentifier | UUID = ClientDetailsUUID |
| S046 | ClientGroupUUID | uniqueidentifier | ClientGroupUUID reference |

**JobDetails Table** - `[xeropracticemanager_xpm_8963_2].[JobDetails]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S050 | ID | nvarchar(200) | Job Number |
| S051 | Name | nvarchar(MAX) | Job Name |
| S052 | State | nvarchar(MAX) | Job Status |
| S053 | ClientUUID | uniqueidentifier | ClientUUID reference |
| S054 | WebUrl | nvarchar(MAX) | XPM Job Url |
| S055 | ManagerUUID | uniqueidentifier | Staff reference |
| S056 | PartnerUUID | uniqueidentifier | Staff reference |

**ClientGroupDetails Table** - `[xeropracticemanager_xpm_8963_2].[ClientGroupDetails]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S059 | UUID | uniqueidentifier | UUID = ClientGroupUUID |
| S060 | Name | nvarchar(MAX) | Group Name, first 8 characters (e.g., "ACME0001") |

**Staff Table** - `[xeropracticemanager_xpm_8963_2].[Staff]`

| Source ID | Field Name | Data Type | Description |
|-----------|------------|-----------|-------------|
| S063 | UUID | uniqueidentifier | UUID of Staff |
| S064 | Name | nvarchar(MAX) | Staff Name |
| S065 | Email | — | Staff Email |

---

### 5.2 Target Data Fields

| Dest ID | Required Field Name | Requirement Description | Mapped Source |
|---------|---------------------|------------------------|---------------|
| D001 | JobID | Job identifier for job search in XPM, PrimaryKey | S050 (JobDetails.ID) |
| D002 | JobName | BR-1.1.2 - Job naming convention to identify ATO Lodgement job types: 'CA - TAX - TAX {YEAR}' | S051 (JobDetails.Name) |
| D003 | JobState | BR-1.1.2 - Job state to identify jobs eligible for RFI and at various stages of workflow | S052 (JobDetails.State) |
| D004 | JobWebURL | Link to view Job in XPM (embed as hyperlink in Teams) | S053 (JobDetails.WebUrl) |
| D005 | JobClient = BillingClient | Client associated with Job, defines Billing Client entity | S054 (JobDetails.ClientUUID) |
| D006 | JobPartnerName | BR-4.2.3 - YML Partner responsible for Job, Partner Escalation Notification (T+21 Days) | S037 (ClientDetails.AccountManagerName) |
| D007 | JobPartnerEmail | BR-4.2.3 - YML Partner email for Partner Escalation Notification (T+21 Days) | — (needs mapping) |
| D008 | ClientATOLodgementDueDate | BR-2.2.2 - Query Jobs by ATO Lodgement Due Date | S021 (ATOReport.Due_Date) |
| D009 | JobPriorYearStartDate | BR-2.2.3 - Query by Prior-Year Work In Start Date | — (needs mapping) |
| D010 | JobPriorYearInvoiceDate | BR-2.2.4 - Query by Prior-Year Invoice Date | — (needs mapping) |
| D011 | Bookkeeping Client | BR-2.2.5 - Identify Bookkeeping Clients | S042 (ClientCustomField.Business Services Client) |
| D012 | ClientName | BR-2.2.1 - Search by client name | S033 (ClientDetails.Name) |
| D013 | ClientCode | BR-2.2.1 - Search by client code | S036 (ClientDetails.ClientCode) |
| D015 | ClientContactName | BR-4.1.2 - Send Initial RFI Email to Primary Contact | S021 (needs verification) |
| D016 | ClientContactEmail | BR-4.1.2 - Send Initial RFI Email to Primary Contact | S041 (ClientCustomField.Casual Name) |
| D017 | ClientContactPhone | BR-4.2.2 - SMS Reminder (T+14 Days) | S034 (ClientDetails.Phone) |
| D018 | ClientContactCCEmail | BR-4.1.2 - CC Contact on emails | S035/S043 |
| D019 | ClientGroupName | BR-3.1.1 - Display Query Results with Action Options | S060 (ClientGroupDetails.Name) |
| D020 | ClientTFN | BR-1.2.1/BR-1.2.2 - Match ATO data to XPM Clients by TFN | S015 (ATOReport.TFN) |
| D021 | ClientDocumentRequest | BR-1.3.1 - Client-Specific Required Document Lists | — (to be developed) |

---

## 6. Teams Agent Adaptive Cards

### 6.1 Query Results Card (BR-3.1.1)

**Trigger:** Partner queries for jobs (e.g., "Show me jobs due in October", "Find Smith clients")

**Purpose:** Display matching RFI-eligible jobs grouped by Primary Contact, with options to send RFI in bulk or review individually.

```
┌────────────────────────────────────────────────────────────────────┐
│ 📋 RFI-Eligible Jobs                                               │
│ {TotalJobs} jobs across {TotalContacts} contacts                   │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│ 👤 {CasualName} ({PrimaryContactEmail})                            │
│ ┌─────────────────────────────────────────────────────────────┐    │
│ │ {BillingClientName} │ {JobState} │ {GroupRef} │ {Due}       │    │
│ │ {BillingClientName} │ {JobState} │ {GroupRef} │ {Due}       │    │
│ └─────────────────────────────────────────────────────────────┘    │
│                                                                    │
│ 👤 {CasualName} ({PrimaryContactEmail})                            │
│ ┌─────────────────────────────────────────────────────────────┐    │
│ │ {BillingClientName} │ {JobState} │ {GroupRef} │ {Due}       │    │
│ └─────────────────────────────────────────────────────────────┘    │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ [ 📤 Send RFI to All ]  [ 🔍 Review Individually ]                 │
└────────────────────────────────────────────────────────────────────┘
```

**Data Elements:**

| Element | Source | Format |
|---------|--------|--------|
| TotalJobs | COUNT from query | Integer |
| TotalContacts | COUNT DISTINCT PrimaryContactEmail | Integer |
| CasualName | vw_RFIEmailConsolidation.GreetingName | Text |
| PrimaryContactEmail | vw_RFIEmailConsolidation.PrimaryContactEmail | Email |
| BillingClientName | vw_RFIEmailDetail.BillingClientName | Text |
| JobState | Simplified state (Preworkin) | Text |
| GroupRef | vw_RFIEmailDetail.ClientGroupReference | 8 chars |
| Due | vw_RFIEmailDetail.ATODueDate | DD MMM YYYY |

**Display Rules:**
1. **Grouping:** Jobs grouped by Primary Contact email (not by Client Group)
2. **Sorting:** Primary Contacts by earliest due date; within contact: largest group first, then alphabetically
3. **Columns:** Billing Client | Simplified State | Group Reference | ATO Due Date

**User Actions:**

| Button | Action | Next Card |
|--------|--------|-----------|
| Send RFI to All | Trigger bulk email send | Bulk Confirmation Card |
| Review Individually | Enter sequential review mode | Individual Job Card |

---

### 6.2 Individual Job Review Card (BR-3.1.2)

**Trigger:** Partner clicks "Review Individually" from Query Results Card

**Purpose:** Display detailed information for one Primary Contact's jobs, allowing Partner to approve, skip, or add notes.

```
┌────────────────────────────────────────────────────────────────────┐
│ 📝 Review: {CasualName}                                            │
│ Email: {PrimaryContactEmail}                                       │
│ {JobCount} jobs • Earliest due: {EarliestDueDate}                  │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│ 📁 {ClientGroupName} ({GroupRef})                                  │
│ ┌─────────────────────────────────────────────────────────────┐    │
│ │ ★ {BillingClientName}       │ Due: {ATODueDate}             │    │
│ │   {RelatedClientName}       │ Due: {ATODueDate}             │    │
│ │   {RelatedClientName}       │ Due: {ATODueDate}             │    │
│ └─────────────────────────────────────────────────────────────┘    │
│                                                                    │
│ 📌 Prior Year History                                              │
│ Work started: {PriorYearStartDate} • Invoiced: {PriorYearInvDate}  │
│                                                                    │
│ 👥 CC Contact: {CCContactName} ({CCContactEmail})                  │
│ 👔 Partner: {PartnerName}                                          │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ 📝 Add note to email (optional):                                   │
│ ┌─────────────────────────────────────────────────────────────┐    │
│ │                                                             │    │
│ └─────────────────────────────────────────────────────────────┘    │
├────────────────────────────────────────────────────────────────────┤
│ [ ✅ Send RFI ]  [ ⏭️ Skip ]  [ 🚫 Do Not Send ]                   │
│                                                                    │
│ Reviewing 1 of {TotalContacts}                                     │
└────────────────────────────────────────────────────────────────────┘
```

**User Actions:**

| Button | Action | Result |
|--------|--------|--------|
| Send RFI | Send email, update RFIWorkflow | Confirmation, advance to next |
| Skip | No action, advance to next | Advance to next contact |
| Do Not Send | Set DoNotSendFlag=1 | Confirmation, advance to next |

*Notes Field: Optional free text (max 500 characters). Appended to email body if provided. Stored in RFIWorkflow.PartnerNotes.*

---

### 6.3 Bulk Send Confirmation Card (BR-3.2.1)

**Trigger:** Partner clicks "Send RFI to All" from Query Results Card

**Pre-Send Confirmation:**

```
┌────────────────────────────────────────────────────────────────────┐
│ ⚠️ Confirm Bulk Send                                               │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│ You are about to send RFI emails to:                               │
│                                                                    │
│ • {TotalContacts} Primary Contacts                                 │
│ • {TotalJobs} Jobs                                                 │
│ • {TotalGroups} Client Groups                                      │
│                                                                    │
│ Earliest due date: {EarliestDueDate}                               │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ [ ✅ Confirm & Send ]  [ ❌ Cancel ]                                │
└────────────────────────────────────────────────────────────────────┘
```

**Post-Send Confirmation:**

```
┌────────────────────────────────────────────────────────────────────┐
│ ✅ RFI Emails Sent Successfully                                    │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│ {TotalEmailsSent} emails sent to Primary Contacts                  │
│ {TotalJobsUpdated} jobs updated to 'Requested' state               │
│                                                                    │
│ Summary:                                                           │
│ • {Contact1Email} — {JobCount} jobs                                │
│ • {Contact2Email} — {JobCount} jobs                                │
│ • {Contact3Email} — {JobCount} jobs                                │
│                                                                    │
│ Reminder sequence activated (T+7 email, T+14 SMS, T+21 final)      │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

### 6.4 Daily Status Report Card (BR-6.1.1)

**Trigger:** Scheduled daily at 8:00 AM AEST

**Channel:** Posted to designated Partners Teams channel

```
┌────────────────────────────────────────────────────────────────────┐
│ 📊 Daily RFI Status Report                                         │
│ {CurrentDate} — {CurrentTime} AEST                                 │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│ JOB STATUS SUMMARY                                                 │
│ ┌────────────────────────────────────────────────────────────┐     │
│ │ Status          │ Count  │ Change │                        │     │
│ ├────────────────────────────────────────────────────────────┤     │
│ │ Pre Work In     │ {cnt}  │ {+/-}  │                        │     │
│ │ Requested       │ {cnt}  │ {+/-}  │                        │     │
│ │ Work In         │ {cnt}  │ {+/-}  │                        │     │
│ │ Wait on Sign    │ {cnt}  │ {+/-}  │                        │     │
│ │ For Lodgement   │ {cnt}  │ {+/-}  │                        │     │
│ │ Complete        │ {cnt}  │ {+/-}  │                        │     │
│ └────────────────────────────────────────────────────────────┘     │
│                                                                    │
│ ⚠️ ATTENTION REQUIRED                                              │
│ ┌────────────────────────────────────────────────────────────┐     │
│ │ 🔴 At Risk (>21 days no response)    │ {count} │           │     │
│ │ 🟡 Approaching Deadline (7 days)     │ {count} │           │     │
│ │ 📧 Email Bounces                      │ {count} │           │     │
│ └────────────────────────────────────────────────────────────┘     │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ [ 📋 View Pre Work In Jobs ]  [ ⚠️ View At-Risk Clients ]          │
└────────────────────────────────────────────────────────────────────┘
```

---

### 6.5 Partner Escalation Alert Card (BR-4.2.4)

**Trigger:** Automated at T+21 days if no documents uploaded

**Channel:** Direct message to Partner

```
┌────────────────────────────────────────────────────────────────────┐
│ 🚨 Client Escalation Alert                                         │
│ Manual intervention required                                       │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│ 👤 {CasualName} ({PrimaryContactEmail})                            │
│                                                                    │
│ Has not responded to RFI sent {DaysSinceInitial} days ago.         │
│ Final notice email sent today.                                     │
│                                                                    │
│ 📁 Affected Jobs:                                                  │
│ ┌─────────────────────────────────────────────────────────────┐    │
│ │ {BillingClientName} │ {GroupRef} │ Due: {ATODueDate}        │    │
│ │ {BillingClientName} │ {GroupRef} │ Due: {ATODueDate}        │    │
│ └─────────────────────────────────────────────────────────────┘    │
│                                                                    │
│ 📞 Contact: {PrimaryContactPhone}                                  │
│                                                                    │
│ Communication History:                                             │
│ • Initial RFI: {InitialDate}                                       │
│ • Reminder 1 (email): {Reminder1Date}                              │
│ • Reminder 2 (SMS): {Reminder2Date}                                │
│ • Final Notice: {FinalNoticeDate}                                  │
│                                                                    │
├────────────────────────────────────────────────────────────────────┤
│ [ 📞 Log Phone Call ]  [ 🛑 Stop RFI Flow ]  [ 📋 View Jobs ]      │
└────────────────────────────────────────────────────────────────────┘
```

---

## 7. Client Communication Templates

### 7.1 Initial RFI Email (BR-4.1.2)

**Trigger:** Partner approves RFI (bulk or individual)  
**Recipient:** Primary Contact (To:), CC Contact (CC:)

```
To: {PrimaryContactEmail}
CC: {CCContactEmail}
Subject: Tax Document Request — {TotalGroups} Group(s) — Action Required by {EarliestDueDate}

Dear {CasualName},

We hope this message finds you well.

We are following up regarding your outstanding tax return(s) for the {TaxYear}
financial year, with the earliest due date being {EarliestDueDate} for your entities.

If you would like us to prepare and lodge your tax return(s), we kindly request
that you provide the necessary information before the due date. You may upload
the relevant documents using the link provided for each entity below.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
{ClientGroupName} ({GroupReference})
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Entity                      Due Date        Upload Documents
─────────────────────────────────────────────────────────────────
{BillingClientName}         {ATODueDate}    [Upload Here]
{RelatedClientName}         {ATODueDate}    [Upload Here]

[Repeat for each Client Group]

Should you have any questions or require assistance, please do not hesitate
to contact us on 02 8383 4400 or {TAEmailAddress}.

Kind regards,

{PartnerName}
YML Group
```

**Ordering Rules:**
1. **Groups:** Order by entity count descending, then alphabetically by group name
2. **Entities within group:** Billing client first, then related entities alphabetically

---

### 7.2 Reminder Email — T+7 (BR-4.2.1)

**Trigger:** Automated, 7 days after initial RFI, if no documents uploaded for ANY entity  
**Key Feature:** Shows upload status per entity (allows partial completion tracking)

```
Subject: Reminder: Tax Documents Outstanding — Action Required by {EarliestDueDate}

Dear {CasualName},

We are following up on our previous request regarding your outstanding tax
return(s) for the {TaxYear} financial year.

Please see below for the current status of each entity:

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
{ClientGroupName} ({GroupReference})
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Entity                      Due Date        Status
─────────────────────────────────────────────────────────────────
{BillingClientName}         {ATODueDate}    ✅ Received — Thank you!
{RelatedClientName}         {ATODueDate}    📤 Upload Documents — [Click Here]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Status Key:
✅ Received — Thank you!
📤 Upload Documents — [Click Here]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**Upload Status Logic:**

| Condition | Display |
|-----------|---------|
| DocumentUpload exists for ClientUploadLinkID | ✅ Received — Thank you! |
| No documents uploaded | 📤 Upload Documents — [Click Here] |

**Stop Condition:** Reminder sequence stops when all entities have at least one document uploaded, OR Partner manually stops the RFI flow.

---

### 7.3 Final Notice Email — T+21 (BR-4.2.3)

**Trigger:** Automated, 21 days after initial RFI, if still no documents uploaded  
**Tone:** Professional urgency, emphasizes consequences of late lodgement

```
Subject: URGENT: Tax Documents Required — Final Notice — {EarliestDueDate}

Dear {CasualName},

This is our final reminder regarding your outstanding tax return(s) for
the {TaxYear} financial year.

⚠️ IMPORTANT: The earliest lodgement due date for your entities is
{EarliestDueDate}. We have {DaysUntilDeadline} days remaining.

Late lodgement may result in:
• Penalties imposed by the Australian Taxation Office
• Interest charges on any outstanding tax liability
• Potential impact on your tax agent lodgement program status

Please upload your documents immediately using the links below.

[Entity table with upload links]

If you are unable to provide the required documents, or if your circumstances
have changed, please contact us immediately.

Contact: {PartnerName} on 02 8383 4400 or {PartnerEmail}
```

---

### 7.4 SMS Reminder — T+14 (BR-4.2.2)

**Trigger:** Automated, 14 days after initial RFI, if no documents uploaded  
**Constraint:** 160 characters maximum

```
Hi {CasualName}, we sent you an email on {InitialEmailDate} regarding your due
tax returns. Please check your inbox and upload your documents. Thank you. YML Group.
```

*Character count: ~155 characters (varies with name/date)*

**Send Conditions:**
- Primary Contact mobile number exists and is valid
- No documents uploaded for any entity
- RFI flow not stopped

---

## 8. Key Stakeholders

| Stakeholder | Role |
|-------------|------|
| Director of Operations | Project Sponsor — accountable for outcomes |
| Tax Partners/Managers | Approvers — CC'd on TA emails, receive job lists |
| TAs/TAAs | Perform triage, first-line client enquiries |
| Work In Team/Jobflow Team | Picks up once status moves to 'Work In' |
| Business Process Owner | Builds and maintains workflows, monitoring |
| IT/Security | Approves integrations, domains, data handling |
| Systems Manager (XPM) | Maintains XPM configuration and access |
| Clients | Provide documents via provided upload links |

---

## 9. Success Criteria

- Reduce average time-to-documents (request → receipt) by 30% within 90 days of go-live
- Achieve ≥85% TA triage completion within 5 business days of launch per campaign wave
- Lift client document-submission conversion rate to ≥70% per wave
- Ensure 100% of workflow steps write a 'client events' note to XPM
- Cut manual follow-ups by 50% for Partners/TAs

---

## 10. Project Timeline

| Week | Activities |
|------|------------|
| Week 1-2 | Data mapping and field validation from XPM; email/SMS copy approval |
| Week 3 | Build automations, API connections; TA triage UI/pages |
| Week 4 | User acceptance testing; refine |
| Week 5 | Rollout wave 1; monitor; iterate |

---

## 11. Requirement Priority Matrix

| Priority | Requirements | Notes |
|----------|--------------|-------|
| **Critical** | BR-1.x, BR-2.x, BR-3.x, BR-4.2.x, BR-6.x | Core data, Teams Agent, RFI flow, escalation, reporting |
| **High** | BR-4.1.x, BR-5.x | Upload links, manual actions |
| **Medium** | BR-1.3.1 (Document Config) | Stretch goal — document-specific tracking |

---

## 12. Open Questions

| # | Question | Impact |
|---|----------|--------|
| 1 | TA email mapping — how do we determine which TA email to use per client/partner? | Email template "From" field |
| 2 | SMS gateway integration — which provider? | T+14 reminder implementation |
| 3 | Upload link URL structure — what's the base URL for the client portal? | All email templates |
| 4 | Email sending — from TA mailbox directly, or system mailbox with TA in CC? | Email configuration |

---

## 13. Document Control

### Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 17 Nov 2025 | YML Operations | Initial draft |
| 2.0 | 25 Nov 2025 | YML Operations | Added functional requirements |
| 3.0 | 1 Dec 2025 | YML Operations | Added process flows and appendices |
| 4.0 | 6 Jan 2026 | YML AI | Restructured document, consolidated requirements, cleaned formatting |

### Reviewers and Approvers

| Name | Position | Responsibility |
|------|----------|----------------|
| Ashlee Ling | Director of Operations | Review/Approve |
| Yoav Lewis | Chair | Review/Approve |
| Avi Sharabi | CEO | Review/Approve |

### Related Documents

| Document | Location |
|----------|----------|
| Job and Tax Form Status Reference | Reference folder |
| XPM TAX Jobflow Status | XPM System |
| ATO Portal Reports | ATO Portal |
