using System.Data;
using System.Text;

using Microsoft.Data.SqlClient;
using Microsoft.Teams.AI.Models.OpenAI;

namespace Preworkinagent.Functions;

/// <summary>
/// Functions for initiating RFI (Request for Information) workflows.
/// Used by the AI agent to start RFI process for jobs.
/// </summary>
public class RFIInitiateFunctions
{
    private readonly OpenAIChatModel _aiModel;
    private readonly IConfiguration _configuration;

    public RFIInitiateFunctions(OpenAIChatModel aiModel, IConfiguration configuration)
    {
        _aiModel = aiModel;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiate RFI workflow for a job.
    /// Creates the workflow record, generates upload link, and queues initial email.
    /// </summary>
    public async Task<string> InitiateRFI(
        string jobId,
        string partnerName,
        string? partnerEmail = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return "Please provide a Job ID to initiate RFI.";
        }

        if (string.IsNullOrWhiteSpace(partnerName))
        {
            return "Please provide your name (Partner Name) to initiate RFI.";
        }

        try
        {
            var result = await ExecuteInitiateRFI(jobId, partnerName, partnerEmail, notes);
            return FormatInitiateResult(result);
        }
        catch (Exception ex)
        {
            return $"Error initiating RFI: {ex.Message}";
        }
    }

    private async Task<InitiateRFIResult> ExecuteInitiateRFI(
        string jobId,
        string partnerName,
        string? partnerEmail,
        string? notes)
    {
        var result = new InitiateRFIResult();
        result.JobID = jobId; // Always set the JobID we're trying to process

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_InitiateJob]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@JobID", jobId);
        command.Parameters.AddWithValue("@PartnerUUID", DBNull.Value);
        command.Parameters.AddWithValue("@PartnerName", partnerName);
        command.Parameters.AddWithValue("@PartnerEmail", string.IsNullOrWhiteSpace(partnerEmail) ? DBNull.Value : partnerEmail);
        command.Parameters.AddWithValue("@PartnerNotes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            result.Status = reader["Status"]?.ToString();
            result.Message = reader["Message"]?.ToString();

            // Only read additional columns if status is Success (error responses don't have these columns)
            if (result.Status == "Success")
            {
                result.RFIWorkflowID = reader["RFIWorkflowID"]?.ToString();
                result.JobID = reader["JobID"]?.ToString();
                result.EmailTo = reader["EmailTo"]?.ToString();
            }
        }

        return result;
    }

    private string FormatInitiateResult(InitiateRFIResult result)
    {
        var sb = new StringBuilder();

        if (result.Status == "Success")
        {
            sb.AppendLine("**RFI Initiated Successfully**\n");
            sb.AppendLine($"- **Job ID:** {result.JobID}");
            sb.AppendLine($"- **Workflow ID:** {result.RFIWorkflowID}");
            sb.AppendLine($"- **Email To:** {result.EmailTo}");
            sb.AppendLine($"\n{result.Message}");
            sb.AppendLine("\nThe initial RFI email has been queued and will be sent shortly.");
        }
        else
        {
            sb.AppendLine("**RFI Initiation Failed**\n");
            sb.AppendLine($"- **Job ID:** {result.JobID}");
            sb.AppendLine($"- **Reason:** {result.Message}");

            // Provide specific recommendations based on the error
            sb.AppendLine("\n**What you can do:**");

            if (result.Message?.Contains("already initiated") == true)
            {
                sb.AppendLine($"- This job already has an active RFI workflow.");
                sb.AppendLine($"- Ask me to *\"Show job details for {result.JobID}\"* to see the current RFI status and reminder history.");
                sb.AppendLine($"- If you need to restart the RFI process, first stop the existing flow with *\"Stop RFI for {result.JobID}\"*.");
            }
            else if (result.Message?.Contains("not found") == true || result.Message?.Contains("not eligible") == true)
            {
                sb.AppendLine($"- The job may not be in 'Pre Work In' state or doesn't exist.");
                sb.AppendLine($"- Ask me to *\"Show job details for {result.JobID}\"* to check the current job state.");
                sb.AppendLine($"- Only jobs in 'CA - Pre Work In - EL' or 'CA - Pre Work In - Non EL' state are eligible for RFI.");
            }
            else if (result.Message?.Contains("email") == true)
            {
                sb.AppendLine($"- The client doesn't have a primary contact email set in XPM.");
                sb.AppendLine($"- Please update the client's contact details in XPM before initiating RFI.");
            }
            else
            {
                sb.AppendLine($"- Ask me to *\"Show job details for {result.JobID}\"* to investigate further.");
            }
        }

        return sb.ToString();
    }

    #region Result Models

    public class InitiateRFIResult
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? RFIWorkflowID { get; set; }
        public string? JobID { get; set; }
        public string? EmailTo { get; set; }
    }

    #endregion
}
