# YML Pre Work In Agent - Implementation Plan

## Overview

This document outlines the step-by-step implementation plan for building the complete Pre Work In AI Agent system, including database schema, AI tools, Adaptive Cards, and automated workflows.

**Last Updated:** 2026-01-27

---

## Progress Summary

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1 | **COMPLETED** | Database Schema Design |
| Phase 2 | **COMPLETED** | C# Data Layer Implementation |
| Phase 3 | **COMPLETED** | AI Agent Tools Implementation |
| Phase 4 | **NEXT** | Adaptive Cards Implementation |
| Phase 5 | Pending | Communication Services |
| Phase 6 | Pending | Document Upload Portal |
| Phase 7 | Pending | Testing and Deployment |

---

## Data Architecture (Finalized)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           EXISTING DATA SOURCES                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐    ┌──────────────────┐    ┌────────────────────────┐ │
│  │   ATO Report     │    │   XPM Report     │    │   SyncHub XPM Tables   │ │
│  │ [dbo].[ATO Report]│   │   [dbo].[XPM]    │    │ [xeropracticemanager_  │ │
│  │                  │    │                  │    │  xpm_8963_2].*         │ │
│  │ • TFN           │───▶│ • TFN            │───▶│                        │ │
│  │ • Due_Date      │    │ • Client_Code    │    │ • ClientDetails        │ │
│  │ • Status        │    │ • Client_Name    │    │ • JobDetails           │ │
│  │ • Lodgment_Code │    │ • Casual_Name    │    │ • ClientGroupDetails   │ │
│  │                  │    │ • Email, Phone   │    │ • Contact              │ │
│  │ (Manual CSV)    │    │ (Manual CSV)     │    │ • Client (bridge)      │ │
│  └──────────────────┘    └──────────────────┘    │ (Auto-synced daily)   │ │
│                                                   └────────────────────────┘ │
│                                                                              │
│                         JOIN KEY CHAIN:                                      │
│          ATO.TFN → XPM.Client_TaxNumber → XPM.Client_Client_Code            │
│                  → ClientDetails.ClientCode                                  │
│                                                                              │
│  WHY XPM REPORT IS NEEDED:                                                  │
│  1. SyncHub TFN is encrypted - cannot join directly to ATO                  │
│  2. ATO client names are inconsistent - need XPM as bridge                  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Job States in XPM (Actual Values)

| XPM State | Category | RFI Eligible |
|-----------|----------|--------------|
| `CA - Pre Work In - EL` | Pre Work In | **YES** |
| `CA - Pre Work In - Non EL` | Pre Work In | **YES** |
| `CA - Requested` | Requested | No |
| `CA - Work In` | Work In | No |
| `CA - Wait on Info Not Started` | Wait on Info | No |
| `CA - Wait on Info In Progress` | Wait on Info | No |
| `CA - Wait on Sign` | Wait on Sign | No |
| `CA - Ready to lodge` | Ready to Lodge | No |
| `CA - Lodged` | Lodged | No |
| `CA - Lodged by Client` | Lodged | No |
| `CA - Lodged via portal` | Lodged | No |
| `CA - Complete` | Complete | No |

---

## Phase 1: Database Schema Design ✅ COMPLETED

### Deliverables

All SQL scripts are located in `/Database/`:

| File | Description | Status |
|------|-------------|--------|
| `00_README.md` | Documentation | ✅ Created |
| `01_Schema_Tables.sql` | 8 RFI workflow tables | ✅ Created |
| `02_Views.sql` | 10 views joining all sources | ✅ Created |
| `03_StoredProcedures.sql` | 13 stored procedures | ✅ Created |
| `TEST_PLAN.md` | Comprehensive test plan (80 test cases) | ✅ Created |
| `TEST_CHAT_PROMPTS.md` | Chat prompts for testing AI agent | ✅ Created |

### New Tables Created

| Table | Purpose |
|-------|---------|
| `RFI_Workflows` | Track RFI status per job |
| `RFI_CommunicationLog` | Email/SMS audit trail |
| `RFI_ClientUploadLinks` | Unique upload URLs |
| `RFI_DocumentUploads` | Uploaded file tracking |
| `RFI_PartnerEscalations` | T+21 escalation queue |
| `RFI_DailySnapshots` | Daily reporting snapshots |
| `RFI_AuditLog` | Complete audit trail |
| `RFI_Configuration` | System settings |

### Key Views Created

| View | Purpose |
|------|---------|
| `vw_ClientMaster` | Joins SyncHub + XPM Report + ATO Report |
| `vw_ClientWithPrimaryContact` | Clients with Primary/CC contacts |
| `vw_JobMaster` | Jobs with full client & ATO data |
| `vw_RFIEligibleJobs` | Pre Work In jobs not excluded |
| `vw_RFIJobsGroupedByContact` | For bulk RFI operations |
| `vw_RFIActiveWorkflows` | Active workflows + reminder status |
| `vw_AtRiskClients` | >21 days no response |
| `vw_RFIStatusSummary` | Job state counts |
| `vw_ClientGroupEntities` | All entities in a group |

### Key Stored Procedures Created

| Procedure | Purpose | AI Tool |
|-----------|---------|---------|
| `sp_RFI_SearchByClient` | Search by name/code | SearchJobsTool |
| `sp_RFI_SearchByDueDate` | Search by date range | SearchJobsTool |
| `sp_RFI_SearchByMonth` | Search by month | SearchJobsTool |
| `sp_RFI_SearchBookkeepingClients` | Search bookkeeping | SearchJobsTool |
| `sp_RFI_GetJobDetails` | Get full job info | GetJobDetailsTool |
| `sp_RFI_InitiateJob` | Initiate RFI for a job | initiate_rfi |
| `sp_RFI_MarkDoNotSend` | Exclude job from RFI | MarkDoNotSendTool |
| `sp_RFI_StopFlow` | Stop active RFI | StopRFIFlowTool |
| `sp_RFI_GetDailyStatus` | Get status summary | GetStatusSummaryTool |
| `sp_RFI_GetAtRiskClients` | Get at-risk list | GetAtRiskClientsTool |
| `sp_RFI_RecordDocumentUpload` | Record upload | DocumentUploadAPI |
| `sp_RFI_GetPendingReminders` | Get reminders due | SchedulerService |
| `sp_RFI_CreateDailySnapshot` | Create daily snapshot | SchedulerService |

### Installation Instructions

```bash
# Run in SQL Server Management Studio in this order:
1. 01_Schema_Tables.sql      # Creates tables and indexes
2. 02_Views.sql              # Creates views
3. 03_StoredProcedures.sql   # Creates procedures
```

---

## Phase 2 & 3: AI Agent Functions Implementation ✅ COMPLETED

### Architecture Overview

We implemented a **Function Classes** pattern where each function class:
- Handles its own SQL connections directly (no separate service layer)
- Contains result models as inner classes
- Formats output as markdown strings for Teams display
- Is registered as a singleton in DI and wired to the AI prompt

### Project Structure (Actual Implementation)

```
/Preworkinagent
  /Functions
    - PreWorkInSearchFunctions.cs    ✅ Search clients by name, date, month, bookkeeping
    - JobDetailsFunctions.cs          ✅ Get comprehensive job details
    - RFIInitiateFunctions.cs         ✅ Initiate RFI workflow (single job per call)
    - RFIManagementFunctions.cs       ✅ Stop RFI flow, Mark as Do Not Send
    - RFIStatusFunctions.cs           ✅ Daily status summary, At-risk clients
  - MainController.cs                 ✅ Wires functions to AI prompt
  - Program.cs                        ✅ Registers all services
```

### Function Class Pattern

Each function class follows this consistent pattern:

```csharp
// Example: /Functions/PreWorkInSearchFunctions.cs
namespace Preworkinagent.Functions;

public class PreWorkInSearchFunctions
{
    private readonly OpenAIChatModel _aiModel;
    private readonly IConfiguration _configuration;

    public PreWorkInSearchFunctions(OpenAIChatModel aiModel, IConfiguration configuration)
    {
        _aiModel = aiModel;
        _configuration = configuration;
    }

    /// <summary>
    /// Main function called by AI agent
    /// </summary>
    public async Task<string> SearchPreWorkIn(
        string? searchTerm = null,
        string? startDate = null,
        string? endDate = null,
        int? month = null,
        int? year = null,
        bool bookkeepingOnly = false,
        bool includeAlreadyRequested = false)
    {
        // 1. Validate parameters
        // 2. Call appropriate stored procedure
        // 3. Format results as markdown string
    }

    // Private SQL execution methods
    private async Task<List<PreWorkInClientResult>> ExecuteSearchByClient(...)
    {
        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_SearchByClient]", connection);
        command.CommandType = CommandType.StoredProcedure;
        // ... execute and read results
    }

    // Private formatting methods
    private string FormatSearchResults(List<PreWorkInClientResult> results) { ... }

    // Inner result model classes
    public class PreWorkInClientResult
    {
        public string? JobID { get; set; }
        public string? ClientName { get; set; }
        // ... other properties
    }
}
```

### Implemented Function Classes

| Class | Methods | Stored Procedures Used |
|-------|---------|------------------------|
| `PreWorkInSearchFunctions` | `SearchPreWorkIn()` | `sp_RFI_SearchByClient`, `sp_RFI_SearchByDueDate`, `sp_RFI_SearchByMonth`, `sp_RFI_SearchBookkeepingClients` |
| `JobDetailsFunctions` | `GetJobDetails()` | `sp_RFI_GetJobDetails` |
| `RFIInitiateFunctions` | `InitiateRFI()` | `sp_RFI_InitiateJob` |
| `RFIManagementFunctions` | `MarkDoNotSend()`, `StopRFIFlow()` | `sp_RFI_MarkDoNotSend`, `sp_RFI_StopFlow` |
| `RFIStatusFunctions` | `GetDailyStatus()`, `GetAtRiskClients()` | `sp_RFI_GetDailyStatus`, `sp_RFI_GetAtRiskClients` |

### Service Registration (Program.cs)

```csharp
// Program.cs
builder.AddTeams();
builder.AddTeamsDevTools();

// Register AI Agent Function Classes
builder.Services.AddSingleton<Preworkinagent.Functions.PreWorkInSearchFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.JobDetailsFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.RFIInitiateFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.RFIManagementFunctions>();
builder.Services.AddSingleton<Preworkinagent.Functions.RFIStatusFunctions>();

builder.Services.AddSingleton<MainController>();
```

### Function Wiring (MainController.cs)

```csharp
[TeamsController("main")]
public class MainController
{
    private readonly OpenAIChatModel _aiModel;
    private readonly PreWorkInSearchFunctions _preWorkInSearchFunctions;
    private readonly JobDetailsFunctions _jobDetailsFunctions;
    private readonly RFIInitiateFunctions _rfiInitiateFunctions;
    private readonly RFIManagementFunctions _rfiManagementFunctions;
    private readonly RFIStatusFunctions _rfiStatusFunctions;

    // Constructor injection for all function classes...

    [Message]
    public async Task OnMessage([Context] MessageActivity activity, ...)
    {
        var prompt = new OpenAIChatPrompt(model, new ChatPromptOptions
        {
            Instructions = new StringTemplate(@"You are the YML Pre Work In Assistant...")
        });

        // Register functions with the AI prompt
        prompt.Function("search_preworkin", "Search for clients...", _preWorkInSearchFunctions.SearchPreWorkIn);
        prompt.Function("get_job_details", "Get job details...", _jobDetailsFunctions.GetJobDetails);
        prompt.Function("initiate_rfi", "Initiate RFI (call once per job)...", _rfiInitiateFunctions.InitiateRFI);
        prompt.Function("mark_do_not_send", "Exclude job...", _rfiManagementFunctions.MarkDoNotSend);
        prompt.Function("stop_rfi_flow", "Stop active RFI...", _rfiManagementFunctions.StopRFIFlow);
        prompt.Function("get_daily_status", "Get status...", _rfiStatusFunctions.GetDailyStatus);
        prompt.Function("get_at_risk_clients", "Get at-risk...", _rfiStatusFunctions.GetAtRiskClients);

        var result = await prompt.Send(activity.Text);
        // ... send response
    }
}
```

### AI Agent Capabilities (Completed)

| Capability | Natural Language Examples |
|------------|---------------------------|
| **Search Clients** | "Find clients named Smith", "Clients due in October", "Show bookkeeping clients" |
| **View Job Details** | "Show me details for job J012345", "What's the status of J067890?" |
| **Initiate RFI** | "Send RFI for job J012345, partner John Smith", "Initiate RFI for J028001, J028002, J028003 from Jane Doe" (calls function 3 times) |
| **Stop RFI Flow** | "Stop RFI for job J012345", "Cancel reminders for J067890 - client called" |
| **Mark Do Not Send** | "Mark job J012345 as do not send", "Don't send RFI to J067890, already in contact" |
| **Daily Status** | "Show me the daily status", "Dashboard", "Give me an overview" |
| **At-Risk Clients** | "Show me at-risk clients", "Which clients haven't responded?", "Who needs follow-up?" |

**Note:** For multiple jobs, the AI calls `initiate_rfi` separately for each job ID to avoid timeout issues.

---

## Phase 4: Adaptive Cards Implementation

### Step 4.1: Query Results Card

```json
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.4",
  "body": [
    {
      "type": "TextBlock",
      "text": "RFI-Eligible Jobs",
      "weight": "Bolder",
      "size": "Large"
    },
    {
      "type": "TextBlock",
      "text": "${totalJobs} jobs across ${totalContacts} contacts",
      "spacing": "None"
    },
    {
      "type": "Container",
      "$data": "${contacts}",
      "items": [
        {
          "type": "TextBlock",
          "text": "👤 ${greetingName} (${email})",
          "weight": "Bolder"
        },
        {
          "type": "FactSet",
          "$data": "${jobs}",
          "facts": [
            { "title": "${clientName}", "value": "${jobState} | ${groupRef} | ${dueDate}" }
          ]
        }
      ]
    }
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "🔍 Review Individually",
      "data": { "action": "review_individual", "jobIds": "${allJobIds}" }
    }
  ]
}
```

### Step 4.2: Card Action Handler

```csharp
[CardAction]
public async Task OnCardAction([Context] CardActionActivity activity, [Context] IContext.Client client)
{
    var data = activity.Value as JObject;
    var action = data["action"]?.ToString();

    switch (action)
    {
        case "review_individual":
            await HandleIndividualReview(data, client);
            break;
        case "send_rfi":
            // Single job RFI - calls InitiateRFI
            await HandleSendRFI(data, client);
            break;
        case "do_not_send":
            await HandleDoNotSend(data, client);
            break;
    }
}
```

**Note:** Bulk send removed - use natural language to initiate RFI for multiple jobs (AI calls single initiation for each job).

---

## Phase 5: Communication Services

### Step 5.1: Email Service Interface

```csharp
public interface IEmailService
{
    Task<bool> SendInitialRFIEmailAsync(RFIWorkflow workflow, List<ClientUploadLink> uploadLinks);
    Task<bool> SendReminder1EmailAsync(RFIWorkflow workflow);
    Task<bool> SendFinalNoticeEmailAsync(RFIWorkflow workflow);
}
```

### Step 5.2: SMS Service Interface

```csharp
public interface ISmsService
{
    Task<bool> SendReminder2SmsAsync(RFIWorkflow workflow);
}
```

### Step 5.3: Background Scheduler

```csharp
// Runs daily to process reminders
public class ReminderSchedulerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingReminders();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessPendingReminders()
    {
        var reminders = await _db.GetPendingRemindersAsync();

        foreach (var reminder in reminders.Where(r => r.ReminderType == "Reminder1"))
            await _emailService.SendReminder1EmailAsync(reminder);

        foreach (var reminder in reminders.Where(r => r.ReminderType == "Reminder2"))
            await _smsService.SendReminder2SmsAsync(reminder);

        foreach (var reminder in reminders.Where(r => r.ReminderType == "FinalNotice"))
        {
            await _emailService.SendFinalNoticeEmailAsync(reminder);
            await _teamsService.SendPartnerAlertAsync(reminder);
        }
    }
}
```

---

## Phase 6: Document Upload Portal

### Step 6.1: Upload API Endpoint

```csharp
[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    [HttpPost("{token}")]
    public async Task<IActionResult> UploadDocument(
        string token,
        IFormFile file,
        [FromForm] string category = null)
    {
        // Validate token
        var uploadLink = await _db.ValidateUploadTokenAsync(token);
        if (uploadLink == null)
            return NotFound("Invalid or expired upload link");

        // Upload to Azure Blob
        var blobUrl = await _blobService.UploadAsync(file, uploadLink.ClientCode);

        // Record in database
        await _db.RecordDocumentUploadAsync(new DocumentUpload
        {
            UploadLinkID = uploadLink.UploadLinkID,
            OriginalFileName = file.FileName,
            // ... other properties
        });

        return Ok(new { success = true, message = "Document uploaded successfully" });
    }
}
```

---

## Phase 7: Testing and Deployment

### Step 7.1: Test Documentation

| Document | Description | Location |
|----------|-------------|----------|
| `TEST_PLAN.md` | Comprehensive test plan with 80 test cases | `/TEST_PLAN.md` |
| `TEST_CHAT_PROMPTS.md` | Copy-paste chat prompts for Teams testing | `/TEST_CHAT_PROMPTS.md` |

### Step 7.2: Test Categories

| Category | Tests | Description |
|----------|-------|-------------|
| Database Schema | 25 | Tables, views, stored procedures |
| AI Functions | 45 | All 7 agent functions with variations |
| Error Handling | 6 | Error messages and recommendations |
| Input Validation | 5 | Edge cases and security |
| Integration | 10 | End-to-end workflows |

### Step 7.3: Quick Smoke Test (10 prompts)

1. `Show daily status`
2. `Find clients due in February`
3. `Show details for job J028xxx`
4. `Initiate RFI for J028xxx, partner Test User`
5. `Show details for job J028xxx` *(verify RFI initiated)*
6. `Stop RFI for J028xxx - testing, partner Test User`
7. `Show me at-risk clients`
8. `Show bookkeeping clients`
9. `Mark job J028yyy as do not send, partner Test User`
10. `Show daily status` *(verify counts updated)*

### Step 7.4: Test Checklist

**Phase 1-3 (Completed)**
- [ ] All 8 database tables exist
- [ ] All 10 views return correct data
- [ ] All 13 stored procedures execute correctly
- [ ] Search function works (by name, month, date, bookkeeping)
- [ ] Job details function returns full info
- [ ] Initiate RFI creates workflow correctly
- [ ] Mark do not send sets flag correctly
- [ ] Stop RFI flow updates status correctly
- [ ] Daily status returns job counts
- [ ] At-risk clients returns correct list
- [ ] Error messages are helpful with recommendations

**Phase 4-6 (Pending)**
- [ ] Adaptive cards render correctly
- [ ] Card actions work
- [ ] Email sending works
- [ ] SMS sending works
- [ ] Document upload works
- [ ] Daily reports generate correctly

### Step 7.5: Deployment Steps

1. Run database scripts on production SQL Server
2. Deploy API to Azure App Service
3. Configure Teams app manifest
4. Deploy to Teams admin center
5. Test with pilot users

---

## Next Steps (Phase 4)

1. **Create Cards folder** - `/Preworkinagent/Cards/`
2. **Implement Adaptive Card templates** - JSON templates for each card type
3. **Add Card Action Handler** - Handle button clicks from cards
4. **Integrate with existing functions** - Wire cards to function outputs
5. **Test card rendering** - Verify cards display correctly in Teams
