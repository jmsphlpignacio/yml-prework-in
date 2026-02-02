using Microsoft.Teams.Cards;
using Preworkinagent.Functions;

namespace Preworkinagent.Cards;

/// <summary>
/// Handles Adaptive Card action submissions from Teams.
/// Processes actions from Individual Job Review Card and Daily Status Report Card.
/// </summary>
public class CardActionHandler
{
    private readonly RFIInitiateFunctions _rfiInitiateFunctions;
    private readonly RFIManagementFunctions _rfiManagementFunctions;
    private readonly JobDetailsFunctions _jobDetailsFunctions;
    private readonly RFIStatusFunctions _rfiStatusFunctions;
    private readonly PreWorkInSearchFunctions _preWorkInSearchFunctions;
    private readonly IConfiguration _configuration;

    public CardActionHandler(
        RFIInitiateFunctions rfiInitiateFunctions,
        RFIManagementFunctions rfiManagementFunctions,
        JobDetailsFunctions jobDetailsFunctions,
        RFIStatusFunctions rfiStatusFunctions,
        PreWorkInSearchFunctions preWorkInSearchFunctions,
        IConfiguration configuration)
    {
        _rfiInitiateFunctions = rfiInitiateFunctions;
        _rfiManagementFunctions = rfiManagementFunctions;
        _jobDetailsFunctions = jobDetailsFunctions;
        _rfiStatusFunctions = rfiStatusFunctions;
        _preWorkInSearchFunctions = preWorkInSearchFunctions;
        _configuration = configuration;
    }

    /// <summary>
    /// Process card action and return appropriate response
    /// </summary>
    public async Task<CardActionResult> HandleActionAsync(
        Dictionary<string, object?> actionData,
        string partnerName,
        string? partnerEmail = null)
    {
        var action = actionData.GetValueOrDefault("action")?.ToString() ?? "";
        var jobId = actionData.GetValueOrDefault("jobId")?.ToString() ?? "";
        var currentIndex = Convert.ToInt32(actionData.GetValueOrDefault("currentIndex") ?? 1);
        var totalContacts = Convert.ToInt32(actionData.GetValueOrDefault("totalContacts") ?? 1);
        var allJobIds = actionData.GetValueOrDefault("allJobIds")?.ToString() ?? "";
        var partnerNotes = actionData.GetValueOrDefault("partnerNotes")?.ToString();

        var result = new CardActionResult
        {
            Action = action,
            JobId = jobId,
            CurrentIndex = currentIndex,
            TotalContacts = totalContacts,
            AllJobIds = allJobIds
        };

        try
        {
            switch (action)
            {
                // Individual Job Review Card Actions
                case "send_rfi":
                    result = await HandleSendRFI(jobId, partnerName, partnerEmail, partnerNotes, result);
                    break;

                case "skip":
                    result = HandleSkip(result);
                    break;

                case "do_not_send":
                    result = await HandleDoNotSend(jobId, partnerName, partnerEmail, partnerNotes, result);
                    break;

                case "review_next":
                    result = HandleReviewNext(result);
                    break;

                case "review_complete":
                    result = HandleReviewComplete(result);
                    break;

                // Daily Status Report Card Actions
                case "view_preworkin_jobs":
                    return await HandleViewPreWorkInJobs();

                case "view_at_risk_clients":
                    return await HandleViewAtRiskClients();

                // Multi-Job Review Card Actions
                case "send_rfi_multi":
                    return await HandleSendRFIMulti(actionData, partnerName, partnerEmail);

                case "review_individual_multi":
                    return HandleReviewIndividualMulti(actionData);

                case "cancel_multi_review":
                    return HandleCancelMultiReview();

                default:
                    result.Success = false;
                    result.Message = $"Unknown action: {action}";
                    break;
            }

            // Check if we need to show next card
            if (result.Success && currentIndex < totalContacts)
            {
                result.HasNextCard = true;
                result.NextIndex = currentIndex + 1;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error processing action: {ex.Message}";
        }

        return result;
    }

    private async Task<CardActionResult> HandleSendRFI(
        string jobId,
        string partnerName,
        string? partnerEmail,
        string? notes,
        CardActionResult result)
    {
        var initiateResult = await _rfiInitiateFunctions.InitiateRFI(
            jobId: jobId,
            partnerName: partnerName,
            partnerEmail: partnerEmail,
            notes: notes
        );

        result.Success = !initiateResult.Contains("Error") && !initiateResult.Contains("failed");
        result.Message = result.Success
            ? $"RFI initiated for job {jobId}"
            : initiateResult;
        result.ActionTaken = "RFI Sent";

        return result;
    }

    private CardActionResult HandleSkip(CardActionResult result)
    {
        result.Success = true;
        result.Message = $"Skipped job {result.JobId}";
        result.ActionTaken = "Skipped";
        return result;
    }

    private async Task<CardActionResult> HandleDoNotSend(
        string jobId,
        string partnerName,
        string? partnerEmail,
        string? notes,
        CardActionResult result)
    {
        var doNotSendResult = await _rfiManagementFunctions.MarkDoNotSend(
            jobId: jobId,
            partnerName: partnerName,
            partnerEmail: partnerEmail,
            notes: notes ?? "Marked during individual review"
        );

        result.Success = !doNotSendResult.Contains("Error") && !doNotSendResult.Contains("failed");
        result.Message = result.Success
            ? $"Job {jobId} marked as Do Not Send"
            : doNotSendResult;
        result.ActionTaken = "Do Not Send";

        return result;
    }

    private CardActionResult HandleReviewNext(CardActionResult result)
    {
        result.Success = true;
        result.Message = "Advancing to next contact...";
        result.ActionTaken = "Review Next";
        result.HasNextCard = true;
        return result;
    }

    private CardActionResult HandleReviewComplete(CardActionResult result)
    {
        result.Success = true;
        result.Message = "Review session completed";
        result.ActionTaken = "Review Complete";
        result.HasNextCard = false;
        return result;
    }

    #region Daily Status Report Card Actions

    /// <summary>
    /// Handle View Pre Work In Jobs action from Daily Status Report Card
    /// </summary>
    private async Task<CardActionResult> HandleViewPreWorkInJobs()
    {
        var result = new CardActionResult
        {
            Action = "view_preworkin_jobs",
            ActionTaken = "View Pre Work In Jobs"
        };

        try
        {
            // Search for all Pre Work In jobs
            var searchResult = await _preWorkInSearchFunctions.SearchPreWorkIn();

            result.Success = true;
            result.Message = searchResult;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error retrieving Pre Work In jobs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Handle View At-Risk Clients action from Daily Status Report Card
    /// </summary>
    private async Task<CardActionResult> HandleViewAtRiskClients()
    {
        var result = new CardActionResult
        {
            Action = "view_at_risk_clients",
            ActionTaken = "View At-Risk Clients"
        };

        try
        {
            var atRiskResult = await _rfiStatusFunctions.GetAtRiskClients();

            result.Success = true;
            result.Message = atRiskResult;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error retrieving at-risk clients: {ex.Message}";
        }

        return result;
    }

    #endregion

    #region Multi-Job Review Card Actions

    /// <summary>
    /// Handle Send RFI to multiple jobs action from Multi-Job Review Card
    /// </summary>
    private async Task<CardActionResult> HandleSendRFIMulti(
        Dictionary<string, object?> actionData,
        string partnerName,
        string? partnerEmail)
    {
        var result = new CardActionResult
        {
            Action = "send_rfi_multi",
            ActionTaken = "Send RFI Multi"
        };

        var allJobIds = actionData.GetValueOrDefault("allJobIds")?.ToString() ?? "";
        var partnerNotes = actionData.GetValueOrDefault("partnerNotes")?.ToString();
        var excludedJobsStr = actionData.GetValueOrDefault("excludedJobs")?.ToString() ?? "";

        // Parse excluded jobs (comma-separated from multi-select)
        var excludedJobs = string.IsNullOrEmpty(excludedJobsStr)
            ? new List<string>()
            : excludedJobsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

        // Parse all job IDs
        var jobIds = allJobIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(id => !excludedJobs.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (jobIds.Count == 0)
        {
            result.Success = false;
            result.Message = "No jobs selected for RFI (all jobs were excluded)";
            return result;
        }

        var successCount = 0;
        var failedCount = 0;
        var failedJobIds = new List<string>();

        // Send RFI for each job
        foreach (var jobId in jobIds)
        {
            try
            {
                var initiateResult = await _rfiInitiateFunctions.InitiateRFI(
                    jobId: jobId,
                    partnerName: partnerName,
                    partnerEmail: partnerEmail,
                    notes: partnerNotes
                );

                if (!initiateResult.Contains("Error") && !initiateResult.Contains("failed"))
                {
                    successCount++;
                }
                else
                {
                    failedCount++;
                    failedJobIds.Add(jobId);
                }
            }
            catch
            {
                failedCount++;
                failedJobIds.Add(jobId);
            }
        }

        result.Success = successCount > 0;
        result.Message = failedCount == 0
            ? $"Successfully sent RFI for {successCount} job(s)"
            : $"Sent RFI for {successCount} job(s). Failed: {failedCount} ({string.Join(", ", failedJobIds)})";

        // Store additional data for confirmation card
        result.AllJobIds = allJobIds;
        result.TotalContacts = jobIds.Count + excludedJobs.Count;
        result.CurrentIndex = successCount;

        return result;
    }

    /// <summary>
    /// Handle Review Individual action from Multi-Job Review Card
    /// Switches to individual review mode, starting with the first job
    /// </summary>
    private CardActionResult HandleReviewIndividualMulti(Dictionary<string, object?> actionData)
    {
        var allJobIds = actionData.GetValueOrDefault("allJobIds")?.ToString() ?? "";
        var jobCount = Convert.ToInt32(actionData.GetValueOrDefault("jobCount") ?? 0);

        return new CardActionResult
        {
            Success = true,
            Action = "review_individual_multi",
            ActionTaken = "Review Individual Multi",
            Message = "Switching to individual review mode...",
            AllJobIds = allJobIds,
            TotalContacts = jobCount,
            CurrentIndex = 1,
            HasNextCard = true,
            NextIndex = 1
        };
    }

    /// <summary>
    /// Handle Cancel action from Multi-Job Review Card
    /// </summary>
    private CardActionResult HandleCancelMultiReview()
    {
        return new CardActionResult
        {
            Success = true,
            Action = "cancel_multi_review",
            ActionTaken = "Cancelled",
            Message = "Multi-job review cancelled",
            HasNextCard = false
        };
    }

    /// <summary>
    /// Build confirmation card for multi-job RFI send
    /// </summary>
    public AdaptiveCard BuildMultiRFIConfirmationCard(
        int totalJobs,
        int successCount,
        int failedCount,
        List<string> failedJobIds,
        List<string> excludedJobIds)
    {
        return MultiJobReviewCardBuilder.BuildMultiRFIConfirmationCard(
            totalJobs, successCount, failedCount, failedJobIds, excludedJobIds);
    }

    #endregion

    #region Daily Status Report Card Builder

    /// <summary>
    /// Build and return the Daily Status Report Card
    /// </summary>
    public async Task<AdaptiveCard> BuildDailyStatusReportCardAsync()
    {
        try
        {
            // Get current status data
            var statusResult = await GetDailyStatusDataAsync();

            // Build the card
            var cardData = DailyStatusReportCardBuilder.CreateFromDailyStatus(statusResult);
            return DailyStatusReportCardBuilder.BuildCard(cardData);
        }
        catch
        {
            // Return a sample card if data retrieval fails
            return DailyStatusReportCardBuilder.BuildSampleCard();
        }
    }

    /// <summary>
    /// Get daily status data from the database
    /// </summary>
    private async Task<RFIStatusFunctions.DailyStatusResult> GetDailyStatusDataAsync()
    {
        return await _rfiStatusFunctions.ExecuteGetDailyStatus();
    }

    #endregion

    /// <summary>
    /// Get the next job's review card data
    /// </summary>
    public async Task<IndividualJobReviewCardBuilder.JobReviewCardData?> GetNextCardDataAsync(
        string allJobIds,
        int nextIndex,
        int totalContacts)
    {
        var jobIdList = allJobIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (nextIndex > jobIdList.Length) return null;

        var nextJobId = jobIdList[nextIndex - 1].Trim();
        var jobDetails = await _jobDetailsFunctions.GetJobDetails(nextJobId);

        // Note: In a real implementation, you would parse the job details
        // and create the card data. For now, we return a placeholder.
        // This would need integration with the actual database query.

        return new IndividualJobReviewCardBuilder.JobReviewCardData
        {
            JobId = nextJobId,
            CurrentIndex = nextIndex,
            TotalContacts = totalContacts,
            AllJobIds = allJobIds
        };
    }
}
