using System.Data;
using System.Text;

using Microsoft.Data.SqlClient;
using Microsoft.Teams.AI.Models.OpenAI;

namespace Preworkinagent.Functions;

/// <summary>
/// Functions for retrieving RFI status summaries and at-risk client lists.
/// Used by the AI agent for dashboard queries, daily reports, and escalation reviews.
/// </summary>
public class RFIStatusFunctions
{
    private readonly OpenAIChatModel _aiModel;
    private readonly IConfiguration _configuration;

    public RFIStatusFunctions(OpenAIChatModel aiModel, IConfiguration configuration)
    {
        _aiModel = aiModel;
        _configuration = configuration;
    }

    /// <summary>
    /// Get comprehensive daily status summary including job counts, RFI metrics,
    /// at-risk clients, and approaching deadlines.
    /// </summary>
    public async Task<string> GetDailyStatus()
    {
        try
        {
            var result = await ExecuteGetDailyStatus();
            return FormatDailyStatus(result);
        }
        catch (Exception ex)
        {
            return $"Error retrieving daily status: {ex.Message}";
        }
    }

    /// <summary>
    /// Get list of at-risk clients who haven't responded after 21+ days of RFI initiation.
    /// These clients need manual follow-up (phone call).
    /// </summary>
    public async Task<string> GetAtRiskClients()
    {
        try
        {
            var results = await ExecuteGetAtRiskClients();

            if (results.Count == 0)
            {
                return "**No At-Risk Clients**\n\nGreat news! There are currently no clients who have been unresponsive for more than 21 days.";
            }

            return FormatAtRiskClients(results);
        }
        catch (Exception ex)
        {
            return $"Error retrieving at-risk clients: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute the daily status stored procedure and return the raw result.
    /// This method is public to allow use by card builders.
    /// </summary>
    public async Task<DailyStatusResult> ExecuteGetDailyStatus()
    {
        var result = new DailyStatusResult();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_GetDailyStatus]", connection);
        command.CommandType = CommandType.StoredProcedure;

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        // Result Set 1: Job State Summary (vw_RFIStatusSummary returns columnar format)
        if (await reader.ReadAsync())
        {
            // Read columnar data and convert to rows
            var preWorkInEL = reader["PreWorkInELCount"] as int? ?? 0;
            var preWorkInNonEL = reader["PreWorkInNonELCount"] as int? ?? 0;
            var requested = reader["RequestedCount"] as int? ?? 0;
            var workIn = reader["WorkInCount"] as int? ?? 0;
            var waitOnInfo = reader["WaitOnInfoCount"] as int? ?? 0;
            var waitOnSign = reader["WaitOnSignCount"] as int? ?? 0;
            var readyToLodge = reader["ReadyToLodgeCount"] as int? ?? 0;
            var lodged = reader["LodgedCount"] as int? ?? 0;
            var complete = reader["CompleteCount"] as int? ?? 0;

            // Add as rows for display
            if (preWorkInEL > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Pre Work In (EL)", JobCount = preWorkInEL });
            if (preWorkInNonEL > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Pre Work In (Non-EL)", JobCount = preWorkInNonEL });
            if (requested > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Requested", JobCount = requested });
            if (workIn > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Work In", JobCount = workIn });
            if (waitOnInfo > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Wait on Info", JobCount = waitOnInfo });
            if (waitOnSign > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Wait on Sign", JobCount = waitOnSign });
            if (readyToLodge > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Ready to Lodge", JobCount = readyToLodge });
            if (lodged > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Lodged", JobCount = lodged });
            if (complete > 0)
                result.JobStateSummary.Add(new JobStateCount { SimplifiedJobState = "Complete", JobCount = complete });
        }

        // Result Set 2: RFI Workflow Status
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                result.RFIStatusSummary.Add(new RFIStatusCount
                {
                    RFIStatus = reader["RFIStatus"]?.ToString(),
                    WorkflowCount = reader["WorkflowCount"] as int? ?? 0
                });
            }
        }

        // Result Set 3: At-Risk Count
        if (await reader.NextResultAsync())
        {
            if (await reader.ReadAsync())
            {
                result.AtRiskCount = reader["AtRiskCount"] as int? ?? 0;
            }
        }

        // Result Set 4: Approaching Deadline Count
        if (await reader.NextResultAsync())
        {
            if (await reader.ReadAsync())
            {
                result.ApproachingDeadlineCount = reader["ApproachingDeadlineCount"] as int? ?? 0;
            }
        }

        // Result Set 5: Reminders Due Today
        if (await reader.NextResultAsync())
        {
            if (await reader.ReadAsync())
            {
                result.Reminder1DueCount = reader["Reminder1DueCount"] as int? ?? 0;
                result.Reminder2DueCount = reader["Reminder2DueCount"] as int? ?? 0;
                result.FinalNoticeDueCount = reader["FinalNoticeDueCount"] as int? ?? 0;
            }
        }

        return result;
    }

    private async Task<List<AtRiskClientRecord>> ExecuteGetAtRiskClients()
    {
        var results = new List<AtRiskClientRecord>();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_GetAtRiskClients]", connection);
        command.CommandType = CommandType.StoredProcedure;

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new AtRiskClientRecord
            {
                RFIWorkflowID = reader["RFIWorkflowID"]?.ToString(),
                JobID = reader["JobID"]?.ToString(),
                JobName = reader["JobName"]?.ToString(),
                ClientName = reader["ClientName"]?.ToString(),
                CasualName = reader["CasualName"]?.ToString(),
                GreetingName = reader["GreetingName"]?.ToString(),
                PrimaryContactEmail = reader["PrimaryContactEmail"]?.ToString(),
                PrimaryContactMobile = reader["PrimaryContactMobile"]?.ToString(),
                PartnerName = reader["PartnerName"]?.ToString(),
                ATODueDate = reader["ATODueDate"] as DateTime?,
                DaysSinceInitiated = reader["DaysSinceInitiated"] as int? ?? 0,
                DaysUntilDeadline = reader["DaysUntilDeadline"] as int?,
                InitiatedDate = reader["InitiatedDate"] as DateTime?,
                Reminder1SentDate = reader["Reminder1SentDate"] as DateTime?,
                Reminder2SentDate = reader["Reminder2SentDate"] as DateTime?,
                FinalNoticeSentDate = reader["FinalNoticeSentDate"] as DateTime?
            });
        }

        return results;
    }

    private string FormatDailyStatus(DailyStatusResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"**Daily RFI Status Summary**");
        sb.AppendLine($"*Report Date: {DateTime.Today:dddd, dd MMMM yyyy}*\n");

        // Attention Required Section
        var hasAttentionItems = result.AtRiskCount > 0 ||
                               result.ApproachingDeadlineCount > 0 ||
                               result.FinalNoticeDueCount > 0;

        if (hasAttentionItems)
        {
            sb.AppendLine("## Attention Required");
            if (result.AtRiskCount > 0)
                sb.AppendLine($"- **{result.AtRiskCount}** client(s) unresponsive >21 days - need phone follow-up");
            if (result.ApproachingDeadlineCount > 0)
                sb.AppendLine($"- **{result.ApproachingDeadlineCount}** job(s) due within 7 days with no RFI sent");
            if (result.FinalNoticeDueCount > 0)
                sb.AppendLine($"- **{result.FinalNoticeDueCount}** final notice(s) due today");
            sb.AppendLine();
        }

        // Job State Summary
        sb.AppendLine("## Job States");

        if (result.JobStateSummary.Any())
        {
            sb.AppendLine($"| State | Count |");
            sb.AppendLine($"|-------|-------|");
            foreach (var state in result.JobStateSummary.OrderByDescending(s => s.JobCount))
            {
                sb.AppendLine($"| {state.SimplifiedJobState} | {state.JobCount} |");
            }
        }
        else
        {
            sb.AppendLine("*No job data available*");
        }
        sb.AppendLine();

        // RFI Workflow Status
        if (result.RFIStatusSummary.Any())
        {
            sb.AppendLine("## Active RFI Workflows");
            sb.AppendLine($"| Status | Count |");
            sb.AppendLine($"|--------|-------|");
            foreach (var status in result.RFIStatusSummary.OrderByDescending(s => s.WorkflowCount))
            {
                string statusIcon = status.RFIStatus switch
                {
                    "Requested" => "",
                    "PartiallyReceived" => "",
                    "Stopped" => "",
                    _ => ""
                };
                sb.AppendLine($"| {statusIcon} {status.RFIStatus} | {status.WorkflowCount} |");
            }
            sb.AppendLine();
        }

        // Reminders Due Today
        var totalReminders = result.Reminder1DueCount + result.Reminder2DueCount + result.FinalNoticeDueCount;
        if (totalReminders > 0)
        {
            sb.AppendLine("## Reminders Scheduled Today");
            if (result.Reminder1DueCount > 0)
                sb.AppendLine($"- T+7 Email Reminders: **{result.Reminder1DueCount}**");
            if (result.Reminder2DueCount > 0)
                sb.AppendLine($"- T+14 SMS Reminders: **{result.Reminder2DueCount}**");
            if (result.FinalNoticeDueCount > 0)
                sb.AppendLine($"- T+21 Final Notices: **{result.FinalNoticeDueCount}**");
            sb.AppendLine();
        }

        // Quick Actions
        sb.AppendLine("## Quick Actions");
        sb.AppendLine("- To view at-risk clients, ask: *\"Show me at-risk clients\"*");
        sb.AppendLine("- To search Pre Work In jobs, ask: *\"Find clients due in [month]\"*");
        sb.AppendLine("- To initiate RFI, ask: *\"Send RFI for job [JobID]\"*");

        return sb.ToString();
    }

    private string FormatAtRiskClients(List<AtRiskClientRecord> clients)
    {
        var sb = new StringBuilder();

        sb.AppendLine("**At-Risk Clients - Manual Follow-Up Required**");
        sb.AppendLine($"*{clients.Count} client(s) unresponsive for 21+ days*\n");

        // Group by urgency (days until deadline)
        var critical = clients.Where(c => c.DaysUntilDeadline.HasValue && c.DaysUntilDeadline <= 0).ToList();
        var urgent = clients.Where(c => c.DaysUntilDeadline.HasValue && c.DaysUntilDeadline > 0 && c.DaysUntilDeadline <= 14).ToList();
        var standard = clients.Where(c => !c.DaysUntilDeadline.HasValue || c.DaysUntilDeadline > 14).ToList();

        if (critical.Any())
        {
            sb.AppendLine("## OVERDUE - Immediate Action Required");
            foreach (var client in critical.OrderBy(c => c.DaysUntilDeadline))
            {
                FormatAtRiskClient(sb, client, "");
            }
            sb.AppendLine();
        }

        if (urgent.Any())
        {
            sb.AppendLine("## URGENT - Due Within 14 Days");
            foreach (var client in urgent.OrderBy(c => c.DaysUntilDeadline))
            {
                FormatAtRiskClient(sb, client, "");
            }
            sb.AppendLine();
        }

        if (standard.Any())
        {
            sb.AppendLine("## Standard Priority");
            foreach (var client in standard.OrderBy(c => c.ATODueDate))
            {
                FormatAtRiskClient(sb, client, "");
            }
            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("---");
        sb.AppendLine("**Recommended Actions:**");
        sb.AppendLine("1. Call clients directly using the mobile numbers listed");
        sb.AppendLine("2. After making contact, stop the RFI flow: *\"Stop RFI for job [JobID]\"*");
        sb.AppendLine("3. If client prefers no further contact: *\"Mark job [JobID] as do not send\"*");

        return sb.ToString();
    }

    private void FormatAtRiskClient(StringBuilder sb, AtRiskClientRecord client, string icon)
    {
        sb.AppendLine($"{icon} **{client.ClientName}** ({client.GreetingName ?? client.CasualName})");
        sb.AppendLine($"   - **Job:** {client.JobID}");

        if (client.ATODueDate.HasValue)
        {
            var dueStatus = client.DaysUntilDeadline.HasValue
                ? (client.DaysUntilDeadline <= 0
                    ? $"**{Math.Abs(client.DaysUntilDeadline.Value)} days overdue**"
                    : $"{client.DaysUntilDeadline} days remaining")
                : "Unknown";
            sb.AppendLine($"   - **ATO Due:** {client.ATODueDate.Value:dd MMM yyyy} ({dueStatus})");
        }

        sb.AppendLine($"   - **Days Since RFI:** {client.DaysSinceInitiated} days");

        if (!string.IsNullOrEmpty(client.PrimaryContactMobile))
            sb.AppendLine($"   - **Mobile:** {client.PrimaryContactMobile}");
        if (!string.IsNullOrEmpty(client.PrimaryContactEmail))
            sb.AppendLine($"   - **Email:** {client.PrimaryContactEmail}");
        if (!string.IsNullOrEmpty(client.PartnerName))
            sb.AppendLine($"   - **Partner:** {client.PartnerName}");

        // Reminder history
        var reminders = new List<string>();
        if (client.Reminder1SentDate.HasValue)
            reminders.Add($"T+7 Email ({client.Reminder1SentDate.Value:dd MMM})");
        if (client.Reminder2SentDate.HasValue)
            reminders.Add($"T+14 SMS ({client.Reminder2SentDate.Value:dd MMM})");
        if (client.FinalNoticeSentDate.HasValue)
            reminders.Add($"T+21 Final ({client.FinalNoticeSentDate.Value:dd MMM})");

        if (reminders.Any())
            sb.AppendLine($"   - **Reminders Sent:** {string.Join(", ", reminders)}");

        sb.AppendLine();
    }

    #region Result Models

    public class DailyStatusResult
    {
        public List<JobStateCount> JobStateSummary { get; set; } = new();
        public List<RFIStatusCount> RFIStatusSummary { get; set; } = new();
        public int AtRiskCount { get; set; }
        public int ApproachingDeadlineCount { get; set; }
        public int Reminder1DueCount { get; set; }
        public int Reminder2DueCount { get; set; }
        public int FinalNoticeDueCount { get; set; }
    }

    public class JobStateCount
    {
        public string? SimplifiedJobState { get; set; }
        public int JobCount { get; set; }
    }

    public class RFIStatusCount
    {
        public string? RFIStatus { get; set; }
        public int WorkflowCount { get; set; }
    }

    public class AtRiskClientRecord
    {
        public string? RFIWorkflowID { get; set; }
        public string? JobID { get; set; }
        public string? JobName { get; set; }
        public string? ClientName { get; set; }
        public string? CasualName { get; set; }
        public string? GreetingName { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactMobile { get; set; }
        public string? PartnerName { get; set; }
        public DateTime? ATODueDate { get; set; }
        public int DaysSinceInitiated { get; set; }
        public int? DaysUntilDeadline { get; set; }
        public DateTime? InitiatedDate { get; set; }
        public DateTime? Reminder1SentDate { get; set; }
        public DateTime? Reminder2SentDate { get; set; }
        public DateTime? FinalNoticeSentDate { get; set; }
    }

    #endregion
}
