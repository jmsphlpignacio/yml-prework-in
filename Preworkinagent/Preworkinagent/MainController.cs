using Microsoft.Teams.AI;
using Microsoft.Teams.AI.Messages;
using Microsoft.Teams.AI.Models.OpenAI;
using Microsoft.Teams.AI.Prompts;
using Microsoft.Teams.AI.Templates;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Activities;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Activities;
using Microsoft.Teams.Apps.Annotations;
using Microsoft.Teams.Cards;
using Preworkinagent.Functions;
using Preworkinagent.Cards;

namespace Preworkinagent;

[TeamsController("main")]
public class MainController
{
    private readonly OpenAIChatModel _aiModel;
    private readonly IConfiguration _configuration;
    private readonly PreWorkInSearchFunctions _preWorkInSearchFunctions;
    private readonly JobDetailsFunctions _jobDetailsFunctions;
    private readonly RFIInitiateFunctions _rfiInitiateFunctions;
    private readonly RFIManagementFunctions _rfiManagementFunctions;
    private readonly RFIStatusFunctions _rfiStatusFunctions;
    private readonly CardActionHandler _cardActionHandler;

    public MainController(
        OpenAIChatModel aiModel,
        IConfiguration configuration,
        PreWorkInSearchFunctions preWorkInSearchFunctions,
        JobDetailsFunctions jobDetailsFunctions,
        RFIInitiateFunctions rfiInitiateFunctions,
        RFIManagementFunctions rfiManagementFunctions,
        RFIStatusFunctions rfiStatusFunctions,
        CardActionHandler cardActionHandler)
    {
        _aiModel = aiModel;
        _configuration = configuration;
        _preWorkInSearchFunctions = preWorkInSearchFunctions;
        _jobDetailsFunctions = jobDetailsFunctions;
        _rfiInitiateFunctions = rfiInitiateFunctions;
        _rfiManagementFunctions = rfiManagementFunctions;
        _rfiStatusFunctions = rfiStatusFunctions;
        _cardActionHandler = cardActionHandler;
    }

    [Message]
    public async Task OnMessage([Context] MessageActivity activity, [Context] IContext.Client client, IContext<MessageActivity> context)
    {
        await HandleQuery(_aiModel, activity, client, context);
    }

    /// <summary>
    /// Handle user queries - LLM determines which function to call
    /// </summary>
    private async Task HandleQuery(OpenAIChatModel model, MessageActivity activity, IContext.Client client, IContext<MessageActivity> context)
    {
        var userText = activity.Text?.ToLowerInvariant() ?? "";
        var conversationId = context.Activity.Conversation.Id;

        // Get or create conversation memory
        var messages = ConversationMemory.GetOrCreate(conversationId);

        // Check for clear/reset commands
        if (userText == "clear" || userText == "reset" || userText == "start over" || userText == "new conversation")
        {
            ConversationMemory.Clear(conversationId);
            await client.Send("Conversation history cleared. How can I help you?");
            return;
        }

        // Check if user is asking for daily status report card
        if (userText.Contains("daily status card") ||
            userText.Contains("status report card") ||
            userText.Contains("show status card") ||
            userText.Contains("daily report card") ||
            (userText.Contains("status") && userText.Contains("card")))
        {
            await ShowDailyStatusReportCard(client);
            return;
        }

        // Check if user is asking to review jobs (show card instead of text)
        if ((userText.Contains("review") && userText.Contains("job")) ||
            userText.Contains("show card") ||
            userText.Contains("review card"))
        {
            // Extract all job IDs from the message
            var jobIdMatches = System.Text.RegularExpressions.Regex.Matches(
                activity.Text ?? "",
                @"J\d{6}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (jobIdMatches.Count > 1)
            {
                // Multiple jobs - show Multi-Job Review Card
                var jobIds = jobIdMatches.Select(m => m.Value.ToUpper()).Distinct().ToList();
                await ShowMultiJobReviewCard(jobIds, client);
                return;
            }
            else if (jobIdMatches.Count == 1)
            {
                // Single job - show Individual Job Review Card
                await ShowJobReviewCard(jobIdMatches[0].Value, client);
                return;
            }
        }

        var prompt = new OpenAIChatPrompt(model, new ChatPromptOptions
        {
            Instructions = new StringTemplate(@"You are the YML Pre Work In Assistant, an AI agent that helps YML Partners manage RFI (Request for Information) workflows.

## Your Role
You help Partners with:
1. **Search Pre Work In clients** - Find clients in 'Pre Work In' state by various criteria
2. **Get job details** - View comprehensive information about a specific job
3. **Initiate RFI** - Start RFI workflow for jobs
4. **Manage RFI workflows** - Stop active flows or mark jobs as do not send
5. **View status reports** - Daily summaries and at-risk client lists

## Key Terminology
- **RFI**: Request for Information - the workflow to collect year-end documents from clients
- **Pre Work In**: Job state indicating work has not commenced; eligible for RFI
- **Client Group**: A Billing Client and all related entities sharing the same Primary Contact
- **ATO Due Date**: Australian Taxation Office lodgement deadline for the client
- **At-Risk Client**: Client who hasn't responded to RFI for 21+ days

## Available Functions

### search_preworkin
Use when user asks to find, search, or list clients in Pre Work In state.
Parameters:
- **searchTerm**: Client name or code for partial matching (e.g., ""Smith"", ""ACME0001"")
- **month**: Convert month names to numbers (January=1, February=2, ..., December=12)
- **year**: Use current year if not specified
- **startDate/endDate**: For date range queries, use YYYY-MM-DD format
- **bookkeepingOnly**: Set to true when user asks for bookkeeping clients

Examples:
- ""Find clients named Smith"" → searchTerm: ""Smith""
- ""Clients due in October"" → month: 10
- ""Show bookkeeping clients"" → bookkeepingOnly: true

### get_job_details
Use when user asks for details, information, or status about a specific job.
Parameters:
- **jobId**: The Job ID to get details for (e.g., ""J012345"")

Examples:
- ""Show me details for job J012345"" → jobId: ""J012345""
- ""What's the status of J067890?"" → jobId: ""J067890""

### initiate_rfi
Use when user wants to start RFI for a job. Call this function ONCE PER JOB.
Parameters:
- **jobId** (required): The Job ID to initiate RFI for
- **partnerName** (required): Name of the partner initiating the RFI
- **partnerEmail** (optional): Email of the partner
- **notes** (optional): Any notes about this client

Examples:
- ""Initiate RFI for job J012345, partner John Smith"" → jobId: ""J012345"", partnerName: ""John Smith""
- ""Send RFI to J067890 from Jane Doe with note: Priority client"" → jobId: ""J067890"", partnerName: ""Jane Doe"", notes: ""Priority client""

IMPORTANT: For multiple jobs, call initiate_rfi SEPARATELY for EACH job ID.
Example: ""Initiate RFI for jobs J028001, J028002, J028003 from John Smith"" → Call initiate_rfi 3 times:
  1. jobId: ""J028001"", partnerName: ""John Smith""
  2. jobId: ""J028002"", partnerName: ""John Smith""
  3. jobId: ""J028003"", partnerName: ""John Smith""

### mark_do_not_send
Use when user wants to exclude a job from RFI - client should NOT receive automated emails.
Parameters:
- **jobId** (required): The Job ID to exclude
- **partnerName** (required): Name of the partner making this decision
- **partnerEmail** (optional): Email of the partner
- **notes** (optional): Reason for exclusion (recommended)

Examples:
- ""Mark job J012345 as do not send, partner John Smith"" → jobId: ""J012345"", partnerName: ""John Smith""
- ""Don't send RFI to J067890, already in contact - Jane Doe"" → jobId: ""J067890"", partnerName: ""Jane Doe"", notes: ""already in contact""
- ""Exclude J028111 from RFI - client prefers phone"" → jobId: ""J028111"", notes: ""client prefers phone""

### stop_rfi_flow
Use when user wants to stop an ACTIVE RFI workflow - cancels pending reminders.
Use this when client has responded (called in, sending docs) and no more reminders needed.
Parameters:
- **jobId** (required): The Job ID whose RFI flow should stop
- **partnerName** (required): Name of the partner stopping the flow
- **partnerEmail** (optional): Email of the partner
- **reason** (optional): Why the flow is being stopped (recommended)

Examples:
- ""Stop RFI for job J012345, partner John Smith"" → jobId: ""J012345"", partnerName: ""John Smith""
- ""Cancel reminders for J067890 - client called and will send docs"" → jobId: ""J067890"", reason: ""client called and will send docs""
- ""Stop sending reminders to J028111, they've uploaded documents"" → jobId: ""J028111"", reason: ""documents uploaded""

### get_daily_status
Use when user asks for a status summary, daily report, dashboard, or overview.
No parameters required.

Examples:
- ""Show me the daily status""
- ""Give me an overview""
- ""What's the current status?""
- ""Dashboard""
- ""How many jobs are in Pre Work In?""

### get_at_risk_clients
Use when user asks about at-risk clients, unresponsive clients, or clients needing follow-up.
No parameters required.

Examples:
- ""Show me at-risk clients""
- ""Which clients haven't responded?""
- ""Who needs follow-up?""
- ""List unresponsive clients""
- ""Clients not responding to RFI""

IMPORTANT: You must CALL the appropriate function and return its results. Never just return the extracted parameters.")
        });

        // Register the search_preworkin function
        prompt.Function(
            "search_preworkin",
            "Search for clients in Pre Work In state eligible for RFI. Returns formatted results grouped by Primary Contact with client details, due dates, and RFI status.",
            _preWorkInSearchFunctions.SearchPreWorkIn
        );

        // Register the get_job_details function
        prompt.Function(
            "get_job_details",
            "Get comprehensive details for a specific job including RFI workflow status, communication history, and uploaded documents.",
            _jobDetailsFunctions.GetJobDetails
        );

        // Register the initiate_rfi function
        prompt.Function(
            "initiate_rfi",
            "Initiate RFI workflow for a job. Creates workflow record, generates upload link, and queues initial email to the client. For multiple jobs, call this function separately for each job.",
            _rfiInitiateFunctions.InitiateRFI
        );

        // Register the mark_do_not_send function
        prompt.Function(
            "mark_do_not_send",
            "Mark a job as Do Not Send - excludes it from RFI workflow. Use when a client should not receive automated RFI emails (e.g., already in contact, prefers phone calls).",
            _rfiManagementFunctions.MarkDoNotSend
        );

        // Register the stop_rfi_flow function
        prompt.Function(
            "stop_rfi_flow",
            "Stop an active RFI workflow - cancels all pending reminders. Use when client has responded (called in, sending docs) and no more automated communications are needed.",
            _rfiManagementFunctions.StopRFIFlow
        );

        // Register the get_daily_status function
        prompt.Function(
            "get_daily_status",
            "Get comprehensive daily status summary including job state counts, RFI workflow metrics, at-risk clients count, approaching deadlines, and reminders due today.",
            _rfiStatusFunctions.GetDailyStatus
        );

        // Register the get_at_risk_clients function
        prompt.Function(
            "get_at_risk_clients",
            "Get list of at-risk clients who haven't responded after 21+ days of RFI initiation. These clients need manual phone follow-up. Returns contact details, reminder history, and days until ATO deadline.",
            _rfiStatusFunctions.GetAtRiskClients
        );

        // Send with conversation history for context
        var options = new IChatPrompt<OpenAI.Chat.ChatCompletionOptions>.RequestOptions
        {
            Messages = messages
        };

        var result = await prompt.Send(activity.Text ?? string.Empty, options);
        if (result.Content != null)
        {
            var message = new MessageActivity
            {
                Text = result.Content,
            }.AddAIGenerated();
            await context.Send(message);

            // Update conversation history
            messages.Add(UserMessage.Text(activity.Text ?? string.Empty));
            messages.Add(new ModelMessage<string>(result.Content));
        }
        else
        {
            await client.Send("No results found or an error occurred while processing your query.");
        }
    }

    /// <summary>
    /// Show the Individual Job Review Card for a specific job
    /// </summary>
    private async Task ShowJobReviewCard(string jobId, IContext.Client client)
    {
        try
        {
            // Fetch real job details from database
            var jobResult = await _jobDetailsFunctions.GetJobDetailsResult(jobId);

            if (jobResult.JobDetails == null)
            {
                await client.Send($"No job found with ID: {jobId}");
                return;
            }

            // Create card data from the real job details
            var cardData = IndividualJobReviewCardBuilder.CreateFromJobDetails(
                jobResult,
                currentIndex: 1,
                totalContacts: 1,
                allJobIds: jobId
            );

            var card = IndividualJobReviewCardBuilder.BuildCard(cardData);
            await client.Send(card);
        }
        catch (Exception ex)
        {
            await client.Send($"Error loading review card: {ex.Message}");
        }
    }

    /// <summary>
    /// Show the Daily Status Report Card (BR-6.1.1)
    /// </summary>
    private async Task ShowDailyStatusReportCard(IContext.Client client)
    {
        try
        {
            // Fetch real status data from database
            var statusResult = await _rfiStatusFunctions.ExecuteGetDailyStatus();

            // Create card data from the status result
            var cardData = DailyStatusReportCardBuilder.CreateFromDailyStatus(statusResult);

            var card = DailyStatusReportCardBuilder.BuildCard(cardData);
            await client.Send(card);
        }
        catch (Exception ex)
        {
            // On error, show a sample card with error message
            await client.Send($"Error loading status report: {ex.Message}");

            // Optionally show sample card for demo purposes
            var sampleCard = DailyStatusReportCardBuilder.BuildSampleCard();
            await client.Send(sampleCard);
        }
    }

    /// <summary>
    /// Show the Multi-Job Review Card for multiple jobs
    /// </summary>
    private async Task ShowMultiJobReviewCard(List<string> jobIds, IContext.Client client)
    {
        try
        {
            // Fetch job details for all jobs
            var jobResults = new List<Functions.JobDetailsFunctions.JobDetailsResult>();

            foreach (var jobId in jobIds)
            {
                var jobResult = await _jobDetailsFunctions.GetJobDetailsResult(jobId);
                if (jobResult.JobDetails != null)
                {
                    jobResults.Add(jobResult);
                }
            }

            if (jobResults.Count == 0)
            {
                await client.Send($"No valid jobs found for IDs: {string.Join(", ", jobIds)}");
                return;
            }

            // Report any jobs that weren't found
            var foundJobIds = jobResults.Select(r => r.JobDetails?.JobID).ToList();
            var notFoundJobIds = jobIds.Where(id => !foundJobIds.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();

            if (notFoundJobIds.Count > 0)
            {
                await client.Send($"Note: Jobs not found: {string.Join(", ", notFoundJobIds)}");
            }

            // Create and send the Multi-Job Review Card
            var cardData = MultiJobReviewCardBuilder.CreateFromJobDetailsList(jobResults);
            var card = MultiJobReviewCardBuilder.BuildCard(cardData);
            await client.Send(card);
        }
        catch (Exception ex)
        {
            await client.Send($"Error loading multi-job review card: {ex.Message}");

            // Show sample card for demo purposes
            var sampleCard = MultiJobReviewCardBuilder.BuildSampleCard();
            await client.Send(sampleCard);
        }
    }
}
