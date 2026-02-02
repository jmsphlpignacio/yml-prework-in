using System.Data;
using System.Text;

using Microsoft.Data.SqlClient;
using Microsoft.Teams.AI.Models.OpenAI;

namespace Preworkinagent.Functions;

/// <summary>
/// Functions for managing RFI workflows - stopping flows and marking jobs as do not send.
/// Used by the AI agent when partners need to exclude jobs or stop active RFI processes.
/// </summary>
public class RFIManagementFunctions
{
    private readonly OpenAIChatModel _aiModel;
    private readonly IConfiguration _configuration;

    public RFIManagementFunctions(OpenAIChatModel aiModel, IConfiguration configuration)
    {
        _aiModel = aiModel;
        _configuration = configuration;
    }

    /// <summary>
    /// Mark a job as "Do Not Send" - excludes it from RFI workflow.
    /// Use when a client should not receive automated RFI emails (e.g., already in contact, prefers phone).
    /// </summary>
    public async Task<string> MarkDoNotSend(
        string jobId,
        string partnerName,
        string? partnerEmail = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return "Please provide a Job ID to mark as Do Not Send.";
        }

        if (string.IsNullOrWhiteSpace(partnerName))
        {
            return "Please provide your name (Partner Name) to mark this job.";
        }

        try
        {
            var result = await ExecuteMarkDoNotSend(jobId, partnerName, partnerEmail, notes);
            return FormatMarkDoNotSendResult(result, jobId);
        }
        catch (Exception ex)
        {
            return $"Error marking job as Do Not Send: {ex.Message}";
        }
    }

    /// <summary>
    /// Stop an active RFI workflow - cancels pending reminders and marks workflow as stopped.
    /// Use when a client has responded (e.g., called in, sending docs) and no more reminders needed.
    /// </summary>
    public async Task<string> StopRFIFlow(
        string jobId,
        string partnerName,
        string? partnerEmail = null,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return "Please provide a Job ID to stop the RFI flow.";
        }

        if (string.IsNullOrWhiteSpace(partnerName))
        {
            return "Please provide your name (Partner Name) to stop this RFI flow.";
        }

        try
        {
            var result = await ExecuteStopRFIFlow(jobId, partnerName, partnerEmail, reason);
            return FormatStopFlowResult(result, jobId);
        }
        catch (Exception ex)
        {
            return $"Error stopping RFI flow: {ex.Message}";
        }
    }

    private async Task<ManagementResult> ExecuteMarkDoNotSend(
        string jobId,
        string partnerName,
        string? partnerEmail,
        string? notes)
    {
        var result = new ManagementResult();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_MarkDoNotSend]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@JobID", jobId);
        command.Parameters.AddWithValue("@PartnerName", partnerName);
        command.Parameters.AddWithValue("@PartnerEmail", string.IsNullOrWhiteSpace(partnerEmail) ? DBNull.Value : partnerEmail);
        command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            result.Status = reader["Status"]?.ToString();
            result.Message = reader["Message"]?.ToString();
            result.RFIWorkflowID = reader["RFIWorkflowID"]?.ToString();
        }

        return result;
    }

    private async Task<ManagementResult> ExecuteStopRFIFlow(
        string jobId,
        string partnerName,
        string? partnerEmail,
        string? reason)
    {
        var result = new ManagementResult();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_StopFlow]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@JobID", jobId);
        command.Parameters.AddWithValue("@PartnerName", partnerName);
        command.Parameters.AddWithValue("@PartnerEmail", string.IsNullOrWhiteSpace(partnerEmail) ? DBNull.Value : partnerEmail);
        command.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            result.Status = reader["Status"]?.ToString();
            result.Message = reader["Message"]?.ToString();
            result.RFIWorkflowID = reader["RFIWorkflowID"]?.ToString();
        }

        return result;
    }

    private string FormatMarkDoNotSendResult(ManagementResult result, string jobId)
    {
        var sb = new StringBuilder();

        if (result.Status == "Success")
        {
            sb.AppendLine("**Job Marked as Do Not Send**\n");
            sb.AppendLine($"- **Job ID:** {jobId}");
            sb.AppendLine($"- **Workflow ID:** {result.RFIWorkflowID}");
            sb.AppendLine($"\n{result.Message}");
            sb.AppendLine("\nThis job will no longer appear in RFI-eligible lists and will not receive automated emails.");
        }
        else
        {
            sb.AppendLine("**Failed to Mark as Do Not Send**\n");
            sb.AppendLine($"- **Job ID:** {jobId}");
            sb.AppendLine($"- **Error:** {result.Message}");
        }

        return sb.ToString();
    }

    private string FormatStopFlowResult(ManagementResult result, string jobId)
    {
        var sb = new StringBuilder();

        if (result.Status == "Success")
        {
            sb.AppendLine("**RFI Flow Stopped Successfully**\n");
            sb.AppendLine($"- **Job ID:** {jobId}");
            sb.AppendLine($"- **Workflow ID:** {result.RFIWorkflowID}");
            sb.AppendLine($"\n{result.Message}");
            sb.AppendLine("\nAll pending reminders have been cancelled. No further automated communications will be sent for this job.");
        }
        else
        {
            sb.AppendLine("**Failed to Stop RFI Flow**\n");
            sb.AppendLine($"- **Job ID:** {jobId}");
            sb.AppendLine($"- **Error:** {result.Message}");
        }

        return sb.ToString();
    }

    #region Result Models

    public class ManagementResult
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? RFIWorkflowID { get; set; }
    }

    #endregion
}
