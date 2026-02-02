# YML Pre Work In - Database Schema Documentation

## Overview

This folder contains the complete database schema for the YML Pre Work In RFI automation system. The schema integrates three data sources and provides the foundation for the AI Agent.

## Data Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DATA SOURCES                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐    ┌──────────────────┐    ┌────────────────────────┐ │
│  │   ATO Report     │    │   XPM Report     │    │   SyncHub XPM Tables   │ │
│  │ [dbo].[ATO Report]│    │   [dbo].[XPM]    │    │ [xeropracticemanager_  │ │
│  │                  │    │                  │    │  xpm_8963_2].*         │ │
│  │ • TFN           │───▶│ • TFN            │───▶│                        │ │
│  │ • Due_Date      │    │ • Client_Code    │    │ • ClientDetails        │ │
│  │ • Status        │    │ • Client_Name    │    │ • JobDetails           │ │
│  │ • Lodgment_Code │    │ • Casual_Name    │    │ • ClientGroupDetails   │ │
│  │                  │    │ • Email, Phone   │    │ • Contact              │ │
│  │ (Manual CSV)    │    │ (Manual CSV)     │    │ • ClientCustomField    │ │
│  └──────────────────┘    └──────────────────┘    │ (Auto-synced daily)   │ │
│                                                   └────────────────────────┘ │
│                                                                              │
│                         JOIN KEY CHAIN:                                      │
│          ATO.TFN → XPM.Client_TaxNumber → XPM.Client_Client_Code            │
│                  → ClientDetails.ClientCode                                  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          NEW RFI TABLES                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  RFI_Workflows          RFI_CommunicationLog      RFI_ClientUploadLinks     │
│  ├─ RFI status          ├─ Email/SMS log          ├─ Unique upload URLs     │
│  ├─ Initiated date      ├─ Send status            └─ Expiry tracking        │
│  ├─ Reminder dates      └─ Bounce tracking                                  │
│  └─ Partner notes                                                           │
│                                                                              │
│  RFI_DocumentUploads    RFI_PartnerEscalations    RFI_DailySnapshots        │
│  ├─ Uploaded files      ├─ T+21 alerts            ├─ Daily counts           │
│  ├─ FYI integration     └─ Acknowledgment         └─ Trend tracking         │
│  └─ Blob storage refs                                                       │
│                                                                              │
│  RFI_AuditLog           RFI_Configuration                                   │
│  ├─ Full audit trail    └─ System settings                                  │
│  └─ Who/what/when                                                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Files

| File | Description |
|------|-------------|
| `01_Schema_Tables.sql` | Creates 8 new RFI workflow tables |
| `02_Views.sql` | Creates 10 views that join all data sources |
| `03_StoredProcedures.sql` | Creates 14 stored procedures for AI Agent |

## Installation Order

Run the SQL scripts in this order:

```bash
1. 01_Schema_Tables.sql      # Creates tables and indexes
2. 02_Views.sql              # Creates views (depends on tables)
3. 03_StoredProcedures.sql   # Creates procedures (depends on views)
```

## Key Views

| View | Purpose | Used By |
|------|---------|---------|
| `vw_ClientMaster` | Master client data with ATO/XPM join | All queries |
| `vw_ClientWithPrimaryContact` | Clients + Primary Contact + CC | Email consolidation |
| `vw_JobMaster` | Jobs with full client data | Job queries |
| `vw_RFIEligibleJobs` | Pre Work In jobs not excluded | Agent search |
| `vw_RFIJobsGroupedByContact` | Jobs grouped for bulk RFI | Bulk operations |
| `vw_RFIActiveWorkflows` | Active RFI with reminder status | Scheduler |
| `vw_AtRiskClients` | >21 days no response | Partner alerts |
| `vw_RFIStatusSummary` | Job state counts | Daily report |

## Key Stored Procedures

### Search Operations (AI Agent Tools)

| Procedure | Description | BRD Reference |
|-----------|-------------|---------------|
| `sp_RFI_SearchByClient` | Search by client name/code | BR-2.2.1 |
| `sp_RFI_SearchByDueDate` | Search by ATO due date range | BR-2.2.2 |
| `sp_RFI_SearchByMonth` | Search by month (NLP helper) | BR-2.2.2 |
| `sp_RFI_SearchBookkeepingClients` | Search bookkeeping clients | BR-2.2.5 |
| `sp_RFI_GetJobDetails` | Get full job details | BR-3.1.2 |

### RFI Workflow Operations

| Procedure | Description | BRD Reference |
|-----------|-------------|---------------|
| `sp_RFI_InitiateSingle` | Start RFI for one job | BR-3.2.2 |
| `sp_RFI_InitiateBulk` | Start RFI for multiple jobs | BR-3.2.1 |
| `sp_RFI_MarkDoNotSend` | Exclude job from RFI | BR-3.2.3 |
| `sp_RFI_StopFlow` | Stop active RFI flow | BR-5.1 |

### Status & Reporting

| Procedure | Description | BRD Reference |
|-----------|-------------|---------------|
| `sp_RFI_GetDailyStatus` | Get daily status summary | BR-6.1.1 |
| `sp_RFI_GetAtRiskClients` | Get clients >21 days | BR-4.2.4 |
| `sp_RFI_GetPendingReminders` | Get reminders due | BR-4.2.x |
| `sp_RFI_CreateDailySnapshot` | Record daily snapshot | BR-6.1.1 |

### Document Operations

| Procedure | Description | BRD Reference |
|-----------|-------------|---------------|
| `sp_RFI_RecordDocumentUpload` | Record uploaded document | BR-4.3.2 |

## Job States

The system filters for these "Pre Work In" states:

| XPM State | Simplified State | RFI Eligible |
|-----------|------------------|--------------|
| `CA - Pre Work In - EL` | Pre Work In | Yes |
| `CA - Pre Work In - Non EL` | Pre Work In | Yes |
| `CA - Requested` | Requested | No (already sent) |
| `CA - Work In` | Work In | No |

## Configuration

Default configuration values (stored in `RFI_Configuration`):

| Key | Default | Description |
|-----|---------|-------------|
| `Reminder1Days` | 7 | Days until first email reminder |
| `Reminder2Days` | 14 | Days until SMS reminder |
| `FinalNoticeDays` | 21 | Days until final notice |
| `UploadLinkExpiryMonths` | 6 | Months until upload links expire |
| `CurrentTaxYear` | 2025 | Current tax year |

## Example Queries

### Search for jobs due in October
```sql
EXEC sp_RFI_SearchByMonth @Month = 10, @Year = 2025;
```

### Search by client name
```sql
EXEC sp_RFI_SearchByClient @SearchTerm = 'Smith';
```

### Initiate RFI for a job
```sql
EXEC sp_RFI_InitiateSingle
    @JobID = 'J-12345',
    @PartnerName = 'John Partner',
    @PartnerEmail = 'john@yml.com.au',
    @PartnerNotes = 'Priority client';
```

### Get daily status
```sql
EXEC sp_RFI_GetDailyStatus;
```
