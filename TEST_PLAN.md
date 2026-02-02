# YML Pre Work In Agent - Test Plan

## Overview

This document outlines the test plan for Phases 1-3 of the Pre Work In Agent implementation.

**Last Updated:** 2026-01-27

---

## Test Environment Setup

### Prerequisites
- SQL Server with `atorfi` schema created
- Database scripts executed (01_Schema_Tables.sql, 02_Views.sql, 03_StoredProcedures.sql)
- .NET project built successfully
- Teams Dev Tools running
- Valid test data in XPM sync tables

### Test Data Requirements
- At least 5 clients in "CA - Pre Work In - EL" state
- At least 3 clients in "CA - Pre Work In - Non EL" state
- At least 2 bookkeeping clients in Pre Work In state
- Clients with various ATO due dates (past, current month, future months)
- At least 1 client with existing RFI workflow
- At least 1 client marked as "Do Not Send"

---

## Phase 1: Database Schema Tests

### 1.1 Tables Exist

| Test ID | Test Case | Expected Result | Pass/Fail |
|---------|-----------|-----------------|-----------|
| T1.1.1 | Verify `RFI_Workflows` table exists | Table exists with all columns | |
| T1.1.2 | Verify `RFI_CommunicationLog` table exists | Table exists with all columns | |
| T1.1.3 | Verify `RFI_ClientUploadLinks` table exists | Table exists with all columns | |
| T1.1.4 | Verify `RFI_DocumentUploads` table exists | Table exists with all columns | |
| T1.1.5 | Verify `RFI_PartnerEscalations` table exists | Table exists with all columns | |
| T1.1.6 | Verify `RFI_DailySnapshots` table exists | Table exists with all columns | |
| T1.1.7 | Verify `RFI_AuditLog` table exists | Table exists with all columns | |
| T1.1.8 | Verify `RFI_Configuration` table exists | Table exists with all columns | |

**SQL to verify:**
```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'atorfi' AND TABLE_TYPE = 'BASE TABLE';
```

### 1.2 Views Return Data

| Test ID | Test Case | Expected Result | Pass/Fail |
|---------|-----------|-----------------|-----------|
| T1.2.1 | `vw_ClientMaster` returns data | Returns client records with all fields populated | |
| T1.2.2 | `vw_ClientWithPrimaryContact` returns data | Returns clients with contact info | |
| T1.2.3 | `vw_JobMaster` returns CA - TAX - TAX 2025 jobs | Returns jobs with correct filters | |
| T1.2.4 | `vw_RFIEligibleJobs` returns Pre Work In jobs | Returns only Pre Work In state jobs | |
| T1.2.5 | `vw_RFIJobsGroupedByContact` groups correctly | Returns jobs grouped by primary contact | |
| T1.2.6 | `vw_RFIActiveWorkflows` shows active workflows | Returns workflows with status not Completed | |
| T1.2.7 | `vw_AtRiskClients` shows 21+ day unresponsive | Returns clients meeting at-risk criteria | |
| T1.2.8 | `vw_RFIStatusSummary` returns job counts | Returns counts for all job states | |

**SQL to verify:**
```sql
-- Test each view returns data
SELECT TOP 5 * FROM [atorfi].[vw_ClientMaster];
SELECT TOP 5 * FROM [atorfi].[vw_JobMaster];
SELECT TOP 5 * FROM [atorfi].[vw_RFIEligibleJobs];
SELECT * FROM [atorfi].[vw_RFIStatusSummary];
```

### 1.3 Stored Procedures Execute

| Test ID | Test Case | Input | Expected Result | Pass/Fail |
|---------|-----------|-------|-----------------|-----------|
| T1.3.1 | `sp_RFI_SearchByClient` - valid search | @SearchTerm = 'Smith' | Returns matching clients | |
| T1.3.2 | `sp_RFI_SearchByClient` - no results | @SearchTerm = 'ZZZZZZZ' | Returns empty result | |
| T1.3.3 | `sp_RFI_SearchByDueDate` - date range | @StartDate, @EndDate | Returns jobs in range | |
| T1.3.4 | `sp_RFI_SearchByMonth` - valid month | @Month = 2, @Year = 2025 | Returns February jobs | |
| T1.3.5 | `sp_RFI_SearchBookkeepingClients` | (no params) | Returns bookkeeping clients | |
| T1.3.6 | `sp_RFI_GetJobDetails` - valid job | @JobID = 'J028xxx' | Returns full job details | |
| T1.3.7 | `sp_RFI_GetJobDetails` - invalid job | @JobID = 'INVALID' | Returns empty/null | |
| T1.3.8 | `sp_RFI_GetDailyStatus` | (no params) | Returns 5 result sets | |
| T1.3.9 | `sp_RFI_GetAtRiskClients` | (no params) | Returns at-risk list | |

**SQL to test:**
```sql
-- Test search procedures
EXEC [atorfi].[sp_RFI_SearchByClient] @SearchTerm = 'test';
EXEC [atorfi].[sp_RFI_SearchByMonth] @Month = 2, @Year = 2025;
EXEC [atorfi].[sp_RFI_SearchBookkeepingClients];
EXEC [atorfi].[sp_RFI_GetJobDetails] @JobID = 'J028xxx';
EXEC [atorfi].[sp_RFI_GetDailyStatus];
EXEC [atorfi].[sp_RFI_GetAtRiskClients];
```

### 1.4 RFI Workflow Stored Procedures

| Test ID | Test Case | Input | Expected Result | Pass/Fail |
|---------|-----------|-------|-----------------|-----------|
| T1.4.1 | `sp_RFI_InitiateJob` - eligible job | Valid Pre Work In JobID | Status = 'Success', creates workflow | |
| T1.4.2 | `sp_RFI_InitiateJob` - already initiated | JobID with existing workflow | Status = 'Error', message = 'already initiated' | |
| T1.4.3 | `sp_RFI_InitiateJob` - ineligible job | JobID not in Pre Work In | Status = 'Error', message = 'not eligible' | |
| T1.4.4 | `sp_RFI_InitiateJob` - no email | JobID with no primary email | Status = 'Error', message = 'no email' | |
| T1.4.5 | `sp_RFI_MarkDoNotSend` - new job | JobID without workflow | Creates workflow with DoNotSendFlag = 1 | |
| T1.4.6 | `sp_RFI_MarkDoNotSend` - existing workflow | JobID with workflow | Updates DoNotSendFlag = 1 | |
| T1.4.7 | `sp_RFI_StopFlow` - active workflow | JobID with active RFI | Sets StoppedFlag = 1, status = 'Stopped' | |
| T1.4.8 | `sp_RFI_StopFlow` - no workflow | JobID without workflow | Status = 'Error' | |

**SQL to test:**
```sql
-- Test initiate (use a real eligible JobID)
EXEC [atorfi].[sp_RFI_InitiateJob]
    @JobID = 'J028xxx',
    @PartnerName = 'Test Partner';

-- Test mark do not send
EXEC [atorfi].[sp_RFI_MarkDoNotSend]
    @JobID = 'J028xxx',
    @PartnerName = 'Test Partner',
    @Notes = 'Test - client prefers phone';

-- Test stop flow
EXEC [atorfi].[sp_RFI_StopFlow]
    @JobID = 'J028xxx',
    @PartnerName = 'Test Partner',
    @Reason = 'Test - client responded';
```

---

## Phase 2 & 3: AI Agent Function Tests

### 2.1 Search Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.1.1 | "Find clients named Smith" | `search_preworkin` | Returns clients with 'Smith' in name | |
| T2.1.2 | "Show me clients due in February" | `search_preworkin` | Returns clients with Feb ATO due date | |
| T2.1.3 | "Clients due in October 2025" | `search_preworkin` | Returns Oct 2025 due dates | |
| T2.1.4 | "Show bookkeeping clients" | `search_preworkin` | Returns bookkeeping clients only | |
| T2.1.5 | "Find client ACME0001" | `search_preworkin` | Returns client by code | |
| T2.1.6 | "List Pre Work In clients" | `search_preworkin` | Returns all Pre Work In clients | |
| T2.1.7 | "Search for Yang" | `search_preworkin` | Returns matching clients | |
| T2.1.8 | "Who is due this month?" | `search_preworkin` | Returns current month due dates | |

### 2.2 Job Details Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.2.1 | "Show details for job J028263" | `get_job_details` | Returns full job info | |
| T2.2.2 | "What's the status of J028263?" | `get_job_details` | Returns job details with RFI status | |
| T2.2.3 | "Get info on job J028263" | `get_job_details` | Returns job details | |
| T2.2.4 | "Tell me about J028263" | `get_job_details` | Returns job details | |
| T2.2.5 | "Job details J028263" | `get_job_details` | Returns job details | |
| T2.2.6 | "Show J999999" (invalid) | `get_job_details` | Returns "No job found" message | |

### 2.3 Initiate RFI Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.3.1 | "Initiate RFI for J028xxx, partner John Smith" | `initiate_rfi` | Success message with workflow ID | |
| T2.3.2 | "Send RFI to J028xxx from Jane Doe" | `initiate_rfi` | Success message | |
| T2.3.3 | "Start RFI for job J028xxx, partner Test User" | `initiate_rfi` | Success message | |
| T2.3.4 | "Initiate RFI J028xxx" (no partner) | `initiate_rfi` | Prompts for partner name | |
| T2.3.5 | "Send RFI for J028xxx with note: Priority client, partner Test" | `initiate_rfi` | Success with notes saved | |
| T2.3.6 | "Initiate RFI for already initiated job" | `initiate_rfi` | Error: already initiated + recommendations | |
| T2.3.7 | "Initiate RFI for J028001, J028002, J028003 from John Smith" | `initiate_rfi` x3 | Calls function 3 times, one per job | |

### 2.4 Mark Do Not Send Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.4.1 | "Mark job J028xxx as do not send, partner John" | `mark_do_not_send` | Success message | |
| T2.4.2 | "Don't send RFI to J028xxx - already in contact" | `mark_do_not_send` | Success with notes | |
| T2.4.3 | "Exclude J028xxx from RFI, client prefers phone" | `mark_do_not_send` | Success with notes | |
| T2.4.4 | "Do not send J028xxx" (no partner) | `mark_do_not_send` | Prompts for partner name | |

### 2.5 Stop RFI Flow Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.5.1 | "Stop RFI for job J028xxx, partner John" | `stop_rfi_flow` | Success message | |
| T2.5.2 | "Cancel reminders for J028xxx - client called" | `stop_rfi_flow` | Success with reason | |
| T2.5.3 | "Stop sending reminders to J028xxx" | `stop_rfi_flow` | Success message | |
| T2.5.4 | "Stop RFI J028xxx - documents uploaded" | `stop_rfi_flow` | Success with reason | |
| T2.5.5 | "Stop RFI for job without active workflow" | `stop_rfi_flow` | Error message | |

### 2.6 Daily Status Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.6.1 | "Show daily status" | `get_daily_status` | Returns status summary with job counts | |
| T2.6.2 | "Give me an overview" | `get_daily_status` | Returns dashboard data | |
| T2.6.3 | "What's the current status?" | `get_daily_status` | Returns summary | |
| T2.6.4 | "Dashboard" | `get_daily_status` | Returns summary | |
| T2.6.5 | "How many jobs are in Pre Work In?" | `get_daily_status` | Returns job counts | |
| T2.6.6 | "Status report" | `get_daily_status` | Returns summary | |

### 2.7 At-Risk Clients Function Tests

| Test ID | Natural Language Input | Expected Function | Expected Behavior | Pass/Fail |
|---------|------------------------|-------------------|-------------------|-----------|
| T2.7.1 | "Show me at-risk clients" | `get_at_risk_clients` | Returns at-risk list | |
| T2.7.2 | "Which clients haven't responded?" | `get_at_risk_clients` | Returns unresponsive clients | |
| T2.7.3 | "Who needs follow-up?" | `get_at_risk_clients` | Returns at-risk list | |
| T2.7.4 | "List unresponsive clients" | `get_at_risk_clients` | Returns at-risk list | |
| T2.7.5 | "Clients not responding to RFI" | `get_at_risk_clients` | Returns at-risk list | |
| T2.7.6 | "Show escalations" | `get_at_risk_clients` | Returns at-risk list | |

---

## Phase 3: Error Handling Tests

### 3.1 Error Response Tests

| Test ID | Scenario | Expected Behavior | Pass/Fail |
|---------|----------|-------------------|-----------|
| T3.1.1 | Initiate RFI for already initiated job | Shows "already initiated" error with recommendation to check job details | |
| T3.1.2 | Initiate RFI for ineligible job | Shows "not eligible" error with state requirements | |
| T3.1.3 | Initiate RFI for job with no email | Shows "no email" error with XPM update recommendation | |
| T3.1.4 | Get details for invalid job ID | Shows "No job found" message | |
| T3.1.5 | Stop RFI for job without workflow | Shows appropriate error | |
| T3.1.6 | SQL connection error | Shows generic error message, no system details exposed | |

### 3.2 Input Validation Tests

| Test ID | Input | Expected Behavior | Pass/Fail |
|---------|-------|-------------------|-----------|
| T3.2.1 | Empty job ID | Prompts for job ID | |
| T3.2.2 | Empty partner name for initiate | Prompts for partner name | |
| T3.2.3 | Invalid date format for search | Handles gracefully or prompts for correct format | |
| T3.2.4 | Very long search term | Handles without error | |
| T3.2.5 | Special characters in search | Handles without SQL injection | |

---

## Integration Tests

### 4.1 End-to-End Workflow Tests

| Test ID | Workflow | Steps | Expected Result | Pass/Fail |
|---------|----------|-------|-----------------|-----------|
| T4.1.1 | Search → View → Initiate | 1. Search for client<br>2. View job details<br>3. Initiate RFI | All steps complete successfully | |
| T4.1.2 | Search → Initiate Multiple | 1. Search clients due in Feb<br>2. Initiate RFI for 3 jobs | 3 separate initiations succeed | |
| T4.1.3 | Initiate → Stop Flow | 1. Initiate RFI<br>2. Stop RFI flow | Workflow created then stopped | |
| T4.1.4 | Search → Mark Do Not Send | 1. Search client<br>2. Mark as do not send | Job excluded from future searches | |
| T4.1.5 | Daily Status → At-Risk → Details | 1. Check daily status<br>2. View at-risk<br>3. Get job details | All queries return correct data | |

### 4.2 Data Integrity Tests

| Test ID | Test Case | Verification | Pass/Fail |
|---------|-----------|--------------|-----------|
| T4.2.1 | RFI initiation creates workflow record | Check RFI_Workflows table | |
| T4.2.2 | RFI initiation creates upload link | Check RFI_ClientUploadLinks table | |
| T4.2.3 | RFI initiation creates communication log | Check RFI_CommunicationLog table | |
| T4.2.4 | Stop flow updates workflow status | Check RFI_Workflows.StoppedFlag = 1 | |
| T4.2.5 | Mark do not send sets flag | Check RFI_Workflows.DoNotSendFlag = 1 | |

---

## Test Execution Checklist

### Pre-Test Setup
- [ ] Database scripts executed successfully
- [ ] All tables created in `atorfi` schema
- [ ] All views returning data
- [ ] All stored procedures created
- [ ] .NET project builds without errors
- [ ] Teams Dev Tools connected
- [ ] Test data populated

### Phase 1 Tests
- [ ] T1.1.x - All tables exist
- [ ] T1.2.x - All views return data
- [ ] T1.3.x - Search procedures work
- [ ] T1.4.x - RFI workflow procedures work

### Phase 2 & 3 Tests
- [ ] T2.1.x - Search function tests
- [ ] T2.2.x - Job details function tests
- [ ] T2.3.x - Initiate RFI function tests
- [ ] T2.4.x - Mark do not send function tests
- [ ] T2.5.x - Stop RFI flow function tests
- [ ] T2.6.x - Daily status function tests
- [ ] T2.7.x - At-risk clients function tests
- [ ] T3.1.x - Error handling tests
- [ ] T3.2.x - Input validation tests

### Integration Tests
- [ ] T4.1.x - End-to-end workflows
- [ ] T4.2.x - Data integrity checks

---

## Test Results Summary

| Phase | Total Tests | Passed | Failed | Blocked |
|-------|-------------|--------|--------|---------|
| Phase 1 - Database | 25 | | | |
| Phase 2 & 3 - Functions | 45 | | | |
| Integration | 10 | | | |
| **Total** | **80** | | | |

---

## Known Issues / Notes

| Issue ID | Description | Severity | Status |
|----------|-------------|----------|--------|
| | | | |

---

## Sign-Off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | | | |
| Developer | | | |
| Reviewer | | | |
