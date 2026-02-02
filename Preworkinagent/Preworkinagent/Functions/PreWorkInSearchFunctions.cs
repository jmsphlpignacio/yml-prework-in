using System.Data;
using System.Text;

using Microsoft.Data.SqlClient;
using Microsoft.Teams.AI.Models.OpenAI;

namespace Preworkinagent.Functions;

/// <summary>
/// Functions for searching clients/jobs in Pre Work In state that are eligible for RFI.
/// Used by the AI agent to find clients based on various criteria.
/// </summary>
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
    /// Search for clients in Pre Work In state eligible for RFI with flexible filtering options.
    /// Called by the AI when user asks to find or search for clients/jobs.
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
        try
        {
            List<PreWorkInClientResult> results;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Search by client name/code
                results = await ExecuteSearchByClient(searchTerm, includeAlreadyRequested);
            }
            else if (month.HasValue)
            {
                // Search by month
                results = await ExecuteSearchByMonth(month.Value, year, includeAlreadyRequested);
            }
            else if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
            {
                // Search by date range
                var start = DateTime.Parse(startDate);
                var end = DateTime.Parse(endDate);
                results = await ExecuteSearchByDueDate(start, end, includeAlreadyRequested);
            }
            else if (bookkeepingOnly)
            {
                // Search bookkeeping clients
                DateTime? start = string.IsNullOrWhiteSpace(startDate) ? null : DateTime.Parse(startDate);
                DateTime? end = string.IsNullOrWhiteSpace(endDate) ? null : DateTime.Parse(endDate);
                results = await ExecuteSearchBookkeepingClients(start, end, includeAlreadyRequested);
            }
            else
            {
                return "Please provide search criteria: a client name/code, date range, month, or set bookkeepingOnly to true.";
            }

            if (results.Count == 0)
            {
                return "No clients found matching your criteria.";
            }

            return FormatSearchResults(results);
        }
        catch (Exception ex)
        {
            return $"Error executing search: {ex.Message}";
        }
    }

    private async Task<List<PreWorkInClientResult>> ExecuteSearchByClient(string searchTerm, bool includeAlreadyRequested)
    {
        var results = new List<PreWorkInClientResult>();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_SearchByClient]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@SearchTerm", searchTerm);
        command.Parameters.AddWithValue("@IncludeAlreadyRequested", includeAlreadyRequested);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        results = await ReadClientResults(reader);

        return results;
    }

    private async Task<List<PreWorkInClientResult>> ExecuteSearchByDueDate(DateTime startDate, DateTime endDate, bool includeAlreadyRequested)
    {
        var results = new List<PreWorkInClientResult>();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_SearchByDueDate]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@StartDate", startDate);
        command.Parameters.AddWithValue("@EndDate", endDate);
        command.Parameters.AddWithValue("@IncludeAlreadyRequested", includeAlreadyRequested);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        results = await ReadClientResults(reader);

        return results;
    }

    private async Task<List<PreWorkInClientResult>> ExecuteSearchByMonth(int month, int? year, bool includeAlreadyRequested)
    {
        var results = new List<PreWorkInClientResult>();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_SearchByMonth]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@Month", month);
        if (year.HasValue)
            command.Parameters.AddWithValue("@Year", year.Value);
        else
            command.Parameters.AddWithValue("@Year", DBNull.Value);
        command.Parameters.AddWithValue("@IncludeAlreadyRequested", includeAlreadyRequested);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        results = await ReadClientResults(reader);

        return results;
    }

    private async Task<List<PreWorkInClientResult>> ExecuteSearchBookkeepingClients(DateTime? startDate, DateTime? endDate, bool includeAlreadyRequested)
    {
        var results = new List<PreWorkInClientResult>();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_SearchBookkeepingClients]", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@DueDateStart", startDate.HasValue ? startDate.Value : DBNull.Value);
        command.Parameters.AddWithValue("@DueDateEnd", endDate.HasValue ? endDate.Value : DBNull.Value);
        command.Parameters.AddWithValue("@IncludeAlreadyRequested", includeAlreadyRequested);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        results = await ReadClientResults(reader);

        return results;
    }

    private async Task<List<PreWorkInClientResult>> ReadClientResults(SqlDataReader reader)
    {
        var results = new List<PreWorkInClientResult>();

        while (await reader.ReadAsync())
        {
            results.Add(new PreWorkInClientResult
            {
                JobID = reader["JobID"]?.ToString(),
                JobName = reader["JobName"]?.ToString(),
                JobState = reader["JobState"]?.ToString(),
                SimplifiedJobState = reader["SimplifiedJobState"]?.ToString(),
                JobWebURL = reader["JobWebURL"]?.ToString(),
                TaxYear = reader["TaxYear"] as int?,
                ClientUUID = reader["ClientUUID"]?.ToString(),
                ClientCode = reader["ClientCode"]?.ToString(),
                ClientName = reader["ClientName"]?.ToString(),
                CasualName = reader["CasualName"]?.ToString(),
                GreetingName = reader["GreetingName"]?.ToString(),
                IsBookkeepingClient = reader["IsBookkeepingClient"] as bool? ?? false,
                ClientGroupName = reader["ClientGroupName"]?.ToString(),
                ClientGroupReference = reader["ClientGroupReference"]?.ToString(),
                PrimaryContactName = reader["PrimaryContactName"]?.ToString(),
                PrimaryContactEmail = reader["PrimaryContactEmail"]?.ToString(),
                PrimaryContactMobile = reader["PrimaryContactMobile"]?.ToString(),
                CCContactEmail = reader["CCContactEmail"]?.ToString(),
                PartnerName = reader["PartnerName"]?.ToString(),
                ATODueDate = reader["ATODueDate"] as DateTime?,
                ATOLodgementCode = reader["ATOLodgementCode"]?.ToString(),
                RFIStatus = reader["RFIStatus"]?.ToString(),
                RFIInitiatedDate = reader["RFIInitiatedDate"] as DateTime?,
                DaysUntilATODeadline = reader["DaysUntilATODeadline"] as int?
            });
        }

        return results;
    }

    private string FormatSearchResults(List<PreWorkInClientResult> results)
    {
        var sb = new StringBuilder();

        // Summary section
        sb.AppendLine($"**Pre Work In Search Results**");
        sb.AppendLine($"Found **{results.Count}** client(s) eligible for RFI\n");

        // Statistics
        var notStartedCount = results.Count(r => string.IsNullOrEmpty(r.RFIStatus));
        var requestedCount = results.Count(r => r.RFIStatus == "Requested");
        var overdueCount = results.Count(r => r.ATODueDate.HasValue && r.ATODueDate.Value < DateTime.Today);
        var upcomingCount = results.Count(r => r.ATODueDate.HasValue &&
            r.ATODueDate.Value >= DateTime.Today &&
            r.ATODueDate.Value <= DateTime.Today.AddDays(30));

        if (overdueCount > 0 || notStartedCount > 0 || upcomingCount > 0)
        {
            sb.AppendLine("**Quick Stats:**");
            if (overdueCount > 0)
                sb.AppendLine($"- Overdue: {overdueCount}");
            if (notStartedCount > 0)
                sb.AppendLine($"- RFI Not Started: {notStartedCount}");
            if (requestedCount > 0)
                sb.AppendLine($"- RFI Requested: {requestedCount}");
            if (upcomingCount > 0)
                sb.AppendLine($"- Due within 30 days: {upcomingCount}");
            sb.AppendLine();
        }

        // Group results by Primary Contact Email
        var groupedResults = results
            .Where(r => !string.IsNullOrEmpty(r.PrimaryContactEmail))
            .GroupBy(r => r.PrimaryContactEmail)
            .OrderBy(g => g.Min(r => r.ATODueDate) ?? DateTime.MaxValue);

        foreach (var group in groupedResults)
        {
            var firstClient = group.First();
            sb.AppendLine($"**{firstClient.PrimaryContactName ?? "Unknown Contact"}**");
            sb.AppendLine($"Email: {group.Key}");
            if (!string.IsNullOrEmpty(firstClient.PrimaryContactMobile))
                sb.AppendLine($"Mobile: {firstClient.PrimaryContactMobile}");

            var earliestDue = group.Min(r => r.ATODueDate);
            if (earliestDue.HasValue)
            {
                var daysUntil = (earliestDue.Value - DateTime.Today).Days;
                sb.AppendLine($"Earliest Due: {earliestDue.Value:dd MMM yyyy} ({(daysUntil < 0 ? $"{Math.Abs(daysUntil)} days overdue" : $"{daysUntil} days")})");
            }
            sb.AppendLine();

            // List clients for this contact
            foreach (var client in group.OrderBy(r => r.ATODueDate ?? DateTime.MaxValue))
            {
                string statusIcon = GetStatusIcon(client);
                sb.AppendLine($"{statusIcon} **{client.ClientName}** ({client.ClientCode})");

                if (client.ATODueDate.HasValue)
                    sb.AppendLine($"   Due: {client.ATODueDate.Value:dd MMM yyyy}");

                sb.AppendLine($"   Job: {client.JobID} | State: {client.SimplifiedJobState}");

                if (!string.IsNullOrEmpty(client.RFIStatus))
                    sb.AppendLine($"   RFI Status: {client.RFIStatus}");
                else
                    sb.AppendLine($"   RFI Status: Not Started");

                if (client.IsBookkeepingClient)
                    sb.AppendLine($"   Bookkeeping Client");

                sb.AppendLine();
            }

            sb.AppendLine("---");
        }

        // Handle clients without contact email
        var noEmailClients = results.Where(r => string.IsNullOrEmpty(r.PrimaryContactEmail)).ToList();
        if (noEmailClients.Any())
        {
            sb.AppendLine("\n**Clients Missing Contact Email:**");
            foreach (var client in noEmailClients.Take(5))
            {
                sb.AppendLine($"- {client.ClientName} ({client.ClientCode}) - Job {client.JobID}");
            }
            if (noEmailClients.Count > 5)
                sb.AppendLine($"*... and {noEmailClients.Count - 5} more*");
        }

        return sb.ToString();
    }

    private string GetStatusIcon(PreWorkInClientResult client)
    {
        if (client.ATODueDate.HasValue && client.ATODueDate.Value < DateTime.Today)
            return ""; // Overdue

        if (string.IsNullOrEmpty(client.RFIStatus))
            return ""; // RFI Not Started

        if (client.RFIStatus == "Requested")
            return ""; // RFI Sent

        return ""; // Default
    }

    /// <summary>
    /// Result model for clients in Pre Work In state from stored procedures
    /// </summary>
    public class PreWorkInClientResult
    {
        public string? JobID { get; set; }
        public string? JobName { get; set; }
        public string? JobState { get; set; }
        public string? SimplifiedJobState { get; set; }
        public string? JobWebURL { get; set; }
        public int? TaxYear { get; set; }
        public string? ClientUUID { get; set; }
        public string? ClientCode { get; set; }
        public string? ClientName { get; set; }
        public string? CasualName { get; set; }
        public string? GreetingName { get; set; }
        public bool IsBookkeepingClient { get; set; }
        public string? ClientGroupName { get; set; }
        public string? ClientGroupReference { get; set; }
        public string? PrimaryContactName { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactMobile { get; set; }
        public string? CCContactEmail { get; set; }
        public string? PartnerName { get; set; }
        public DateTime? ATODueDate { get; set; }
        public string? ATOLodgementCode { get; set; }
        public string? RFIStatus { get; set; }
        public DateTime? RFIInitiatedDate { get; set; }
        public int? DaysSinceRFIInitiated { get; set; }
        public int? DaysUntilATODeadline { get; set; }
    }
}
