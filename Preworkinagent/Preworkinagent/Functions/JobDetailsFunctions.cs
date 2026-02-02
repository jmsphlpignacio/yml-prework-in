using System.Data;
using System.Text;

using Microsoft.Data.SqlClient;
using Microsoft.Teams.AI.Models.OpenAI;

namespace Preworkinagent.Functions;

/// <summary>
/// Functions for retrieving detailed job information.
/// Used by the AI agent to get comprehensive job details including
/// RFI workflow status, communication history, and uploaded documents.
/// </summary>
public class JobDetailsFunctions
{
    private readonly OpenAIChatModel _aiModel;
    private readonly IConfiguration _configuration;

    public JobDetailsFunctions(OpenAIChatModel aiModel, IConfiguration configuration)
    {
        _aiModel = aiModel;
        _configuration = configuration;
    }

    /// <summary>
    /// Get detailed information about a specific job including RFI workflow status,
    /// communication history, and uploaded documents.
    /// </summary>
    public async Task<string> GetJobDetails(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return "Please provide a Job ID to get details.";
        }

        try
        {
            var result = await GetJobDetailsResult(jobId);

            if (result.JobDetails == null)
            {
                return $"No job found with ID: {jobId}";
            }

            return FormatJobDetails(result);
        }
        catch (Exception ex)
        {
            return $"Error retrieving job details: {ex.Message}";
        }
    }

    /// <summary>
    /// Get job details as a structured result object.
    /// Used by the card builder to populate Adaptive Cards with real data.
    /// </summary>
    public async Task<JobDetailsResult> GetJobDetailsResult(string jobId)
    {
        var result = new JobDetailsResult();

        using var connection = new SqlConnection(_configuration["SqlConnectionString"]);
        using var command = new SqlCommand("[atorfi].[sp_RFI_GetJobDetails]", connection);
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@JobID", jobId);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        // Result Set 1: Job Details with multiple clients
        // Each row represents the same job but with different client information
        bool jobDetailsPopulated = false;
        while (await reader.ReadAsync())
        {
            // Populate job-level details only once (from the first row)
            if (!jobDetailsPopulated)
            {
                result.JobDetails = new JobDetailRecord
                {
                    JobUUID = reader["JobUUID"]?.ToString(),
                    JobID = reader["JobID"]?.ToString(),
                    JobName = reader["JobName"]?.ToString(),
                    JobState = reader["JobState"]?.ToString(),
                    SimplifiedJobState = reader["SimplifiedJobState"]?.ToString(),
                    JobWebURL = reader["JobWebURL"]?.ToString(),
                    TaxYear = reader["TaxYear"] as int?,
                    JobStartDate = reader["JobStartDate"] as DateTime?,
                    JobDueDate = reader["JobDueDate"] as DateTime?,
                    // Staff
                    PartnerUUID = reader["PartnerUUID"]?.ToString(),
                    PartnerName = reader["PartnerName"]?.ToString(),
                    ManagerUUID = reader["ManagerUUID"]?.ToString(),
                    ManagerName = reader["ManagerName"]?.ToString(),
                    // Client Group (shared)
                    ClientGroupUUID = reader["ClientGroupUUID"]?.ToString(),
                    ClientGroupName = reader["ClientGroupName"]?.ToString(),
                    ClientGroupReference = reader["ClientGroupReference"]?.ToString(),
                    // Billing
                    BillingEntity = reader["BillingEntity"]?.ToString(),
                    BillingEntityUUID = reader["BillingEntityUUID"]?.ToString(),
                    BillingEntityName = reader["BillingEntityName"]?.ToString(),
                    BillingEntityEmail = reader["BillingEntityEmail"]?.ToString(),
                    BillingEntityPhone = reader["BillingEntityPhone"]?.ToString(),
                    // RFI Workflow fields (job-level)
                    RFIWorkflowID = reader["RFIWorkflowID"]?.ToString(),
                    RFIStatus = reader["RFIStatus"]?.ToString(),
                    RFIInitiatedDate = reader["RFIInitiatedDate"] as DateTime?,
                    InitiatedByPartnerName = reader["InitiatedByPartnerName"]?.ToString(),
                    RFIPartnerNotes = reader["RFIPartnerNotes"]?.ToString(),
                    Reminder1SentDate = reader["Reminder1SentDate"] as DateTime?,
                    Reminder2SentDate = reader["Reminder2SentDate"] as DateTime?,
                    FinalNoticeSentDate = reader["FinalNoticeSentDate"] as DateTime?,
                    FirstDocumentUploadDate = reader["FirstDocumentUploadDate"] as DateTime?,
                    DoNotSendFlag = reader["DoNotSendFlag"] as bool? ?? false,
                    StoppedFlag = reader["StoppedFlag"] as bool? ?? false
                };
                jobDetailsPopulated = true;
            }

            // Add each client from every row
            result.Clients.Add(new ClientRecord
            {
                ClientUUID = reader["ClientUUID"]?.ToString(),
                ClientCode = reader["ClientCode"]?.ToString(),
                ClientName = reader["ClientName"]?.ToString(),
                CasualName = reader["CasualName"]?.ToString(),
                GreetingName = reader["GreetingName"]?.ToString(),
                ClientEmail = reader["ClientEmail"]?.ToString(),
                ClientPhone = reader["ClientPhone"]?.ToString(),
                TFN = reader["TFN"]?.ToString(),
                IsBookkeepingClient = reader["IsBookkeepingClient"] as bool? ?? false,
                BillingClientUUID = reader["BillingClientUUID"]?.ToString(),
                // Contact Info
                PrimaryContactName = reader["PrimaryContactName"]?.ToString(),
                PrimaryContactEmail = reader["PrimaryContactEmail"]?.ToString(),
                PrimaryContactMobile = reader["PrimaryContactMobile"]?.ToString(),
                CCContactEmail = reader["CCContactEmail"]?.ToString(),
                // ATO Info (client-specific)
                ATODueDate = reader["ATODueDate"] as DateTime?,
                ATOLodgementCode = reader["ATOLodgementCode"]?.ToString(),
                ATO2025Status = reader["ATO2025Status"]?.ToString(),
                ATOClientType = reader["ATOClientType"]?.ToString()
            });
        }

        // Result Set 2: Communication History
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                result.CommunicationHistory.Add(new CommunicationRecord
                {
                    CommunicationType = reader["CommunicationType"]?.ToString(),
                    CommunicationStage = reader["CommunicationStage"]?.ToString(),
                    ToAddress = reader["ToAddress"]?.ToString(),
                    Subject = reader["Subject"]?.ToString(),
                    SendStatus = reader["SendStatus"]?.ToString(),
                    SentDate = reader["SentDate"] as DateTime?,
                    FailureReason = reader["FailureReason"]?.ToString()
                });
            }
        }

        // Result Set 3: Uploaded Documents
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                result.UploadedDocuments.Add(new DocumentRecord
                {
                    DocumentID = reader["DocumentID"]?.ToString(),
                    OriginalFileName = reader["OriginalFileName"]?.ToString(),
                    FileType = reader["FileType"]?.ToString(),
                    FileSize = reader["FileSize"] as long? ?? 0,
                    DocumentCategory = reader["DocumentCategory"]?.ToString(),
                    UploadedDate = reader["UploadedDate"] as DateTime?,
                    FiledToFYI = reader["FiledToFYI"] as bool? ?? false
                });
            }
        }

        return result;
    }

    private string FormatJobDetails(JobDetailsResult result)
    {
        var sb = new StringBuilder();
        var job = result.JobDetails!;

        // Header
        sb.AppendLine($"**Job Details: {job.JobID}**\n");

        // Job Information
        sb.AppendLine("## Job Information");
        sb.AppendLine($"- **Job Name:** {job.JobName}");
        sb.AppendLine($"- **State:** {job.SimplifiedJobState} ({job.JobState})");
        sb.AppendLine($"- **Tax Year:** {job.TaxYear}");
        if (!string.IsNullOrEmpty(job.JobWebURL))
            sb.AppendLine($"- **XPM Link:** {job.JobWebURL}");
        sb.AppendLine();

        // Client Group Information (shared across all clients)
        if (!string.IsNullOrEmpty(job.ClientGroupName))
        {
            sb.AppendLine("## Client Group");
            sb.AppendLine($"- **Group:** {job.ClientGroupName}");
            if (!string.IsNullOrEmpty(job.ClientGroupReference))
                sb.AppendLine($"- **Reference:** {job.ClientGroupReference}");
            sb.AppendLine();
        }

        // Client Information - now supports multiple clients
        sb.AppendLine("## Clients in Job");
        sb.AppendLine($"*{result.Clients.Count} client(s) associated with this job*\n");

        for (int i = 0; i < result.Clients.Count; i++)
        {
            var client = result.Clients[i];
            sb.AppendLine($"### Client {i + 1}: {client.ClientName} ({client.ClientCode})");

            if (!string.IsNullOrEmpty(client.CasualName))
                sb.AppendLine($"- **Casual Name:** {client.CasualName}");
            if (!string.IsNullOrEmpty(client.ATOClientType))
                sb.AppendLine($"- **Client Type:** {client.ATOClientType}");
            if (client.IsBookkeepingClient)
                sb.AppendLine($"- **Bookkeeping Client:** Yes");

            // Contact Info
            sb.AppendLine($"- **Primary Contact:** {client.PrimaryContactName ?? "Not set"}");
            sb.AppendLine($"- **Email:** {client.PrimaryContactEmail ?? client.ClientEmail ?? "Not set"}");
            if (!string.IsNullOrEmpty(client.PrimaryContactMobile) || !string.IsNullOrEmpty(client.ClientPhone))
                sb.AppendLine($"- **Mobile:** {client.PrimaryContactMobile ?? client.ClientPhone}");
            if (!string.IsNullOrEmpty(client.CCContactEmail))
                sb.AppendLine($"- **CC Email:** {client.CCContactEmail}");

            // ATO Info (client-specific)
            if (client.ATODueDate.HasValue)
            {
                var daysUntil = (client.ATODueDate.Value - DateTime.Today).Days;
                var dueStatus = daysUntil < 0 ? $"**{Math.Abs(daysUntil)} days overdue**" : $"{daysUntil} days remaining";
                sb.AppendLine($"- **ATO Due Date:** {client.ATODueDate.Value:dd MMM yyyy} ({dueStatus})");
            }
            if (!string.IsNullOrEmpty(client.ATOLodgementCode))
                sb.AppendLine($"- **Lodgement Code:** {client.ATOLodgementCode}");
            if (!string.IsNullOrEmpty(client.ATO2025Status))
                sb.AppendLine($"- **ATO Status:** {client.ATO2025Status}");

            sb.AppendLine();
        }

        // Staff Assignment
        sb.AppendLine("## Staff Assignment");
        sb.AppendLine($"- **Partner:** {job.PartnerName ?? "Not assigned"}");
        if (!string.IsNullOrEmpty(job.ManagerName))
            sb.AppendLine($"- **Manager:** {job.ManagerName}");
        sb.AppendLine();

        // RFI Workflow Status
        sb.AppendLine("## RFI Workflow Status");
        if (string.IsNullOrEmpty(job.RFIStatus))
        {
            sb.AppendLine("- **Status:** Not Started");
        }
        else
        {
            string statusIcon = job.RFIStatus switch
            {
                "Requested" => "",
                "PartiallyReceived" => "",
                "Completed" => "",
                "Stopped" => "",
                "DoNotSend" => "",
                _ => ""
            };
            sb.AppendLine($"- **Status:** {statusIcon} {job.RFIStatus}");

            if (job.RFIInitiatedDate.HasValue)
            {
                var daysSince = (DateTime.Today - job.RFIInitiatedDate.Value).Days;
                sb.AppendLine($"- **Initiated:** {job.RFIInitiatedDate.Value:dd MMM yyyy} ({daysSince} days ago)");
            }
            if (!string.IsNullOrEmpty(job.InitiatedByPartnerName))
                sb.AppendLine($"- **Initiated By:** {job.InitiatedByPartnerName}");
            if (!string.IsNullOrEmpty(job.RFIPartnerNotes))
                sb.AppendLine($"- **Notes:** {job.RFIPartnerNotes}");

            // Reminder Status
            sb.AppendLine("\n**Reminders:**");
            sb.AppendLine($"- T+7 Email: {(job.Reminder1SentDate.HasValue ? $"Sent {job.Reminder1SentDate.Value:dd MMM yyyy}" : "Not sent")}");
            sb.AppendLine($"- T+14 SMS: {(job.Reminder2SentDate.HasValue ? $"Sent {job.Reminder2SentDate.Value:dd MMM yyyy}" : "Not sent")}");
            sb.AppendLine($"- T+21 Final: {(job.FinalNoticeSentDate.HasValue ? $"Sent {job.FinalNoticeSentDate.Value:dd MMM yyyy}" : "Not sent")}");

            if (job.FirstDocumentUploadDate.HasValue)
                sb.AppendLine($"\n**First Document Received:** {job.FirstDocumentUploadDate.Value:dd MMM yyyy}");

            if (job.DoNotSendFlag)
                sb.AppendLine("\n **Marked as Do Not Send**");
            if (job.StoppedFlag)
                sb.AppendLine("\n **RFI Flow Stopped**");
        }
        sb.AppendLine();

        // Communication History
        if (result.CommunicationHistory.Count > 0)
        {
            sb.AppendLine("## Communication History");
            sb.AppendLine($"*{result.CommunicationHistory.Count} communication(s)*\n");

            foreach (var comm in result.CommunicationHistory)
            {
                string statusIcon = comm.SendStatus switch
                {
                    "Sent" => "",
                    "Pending" => "",
                    "Failed" => "",
                    "Bounced" => "",
                    "Cancelled" => "",
                    _ => ""
                };
                sb.AppendLine($"- {statusIcon} **{comm.CommunicationStage}** ({comm.CommunicationType})");
                sb.AppendLine($"  - To: {comm.ToAddress}");
                if (comm.SentDate.HasValue)
                    sb.AppendLine($"  - Sent: {comm.SentDate.Value:dd MMM yyyy HH:mm}");
                sb.AppendLine($"  - Status: {comm.SendStatus}");
                if (!string.IsNullOrEmpty(comm.FailureReason))
                    sb.AppendLine($"  - Error: {comm.FailureReason}");
            }
            sb.AppendLine();
        }

        // Uploaded Documents
        if (result.UploadedDocuments.Count > 0)
        {
            sb.AppendLine("## Uploaded Documents");
            sb.AppendLine($"*{result.UploadedDocuments.Count} document(s) received*\n");

            foreach (var doc in result.UploadedDocuments)
            {
                var fyiStatus = doc.FiledToFYI ? " Filed to FYI" : " Pending FYI";
                var fileSize = FormatFileSize(doc.FileSize);
                sb.AppendLine($"- **{doc.OriginalFileName}**");
                sb.AppendLine($"  - Type: {doc.FileType} | Size: {fileSize}");
                if (!string.IsNullOrEmpty(doc.DocumentCategory))
                    sb.AppendLine($"  - Category: {doc.DocumentCategory}");
                if (doc.UploadedDate.HasValue)
                    sb.AppendLine($"  - Uploaded: {doc.UploadedDate.Value:dd MMM yyyy HH:mm}");
                sb.AppendLine($"  - {fyiStatus}");
            }
        }

        return sb.ToString();
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #region Result Models

    public class JobDetailsResult
    {
        public JobDetailRecord? JobDetails { get; set; }
        public List<ClientRecord> Clients { get; set; } = new();
        public List<CommunicationRecord> CommunicationHistory { get; set; } = new();
        public List<DocumentRecord> UploadedDocuments { get; set; } = new();
    }

    public class JobDetailRecord
    {
        // Job Info
        public string? JobUUID { get; set; }
        public string? JobID { get; set; }
        public string? JobName { get; set; }
        public string? JobState { get; set; }
        public string? SimplifiedJobState { get; set; }
        public string? JobWebURL { get; set; }
        public int? TaxYear { get; set; }
        public DateTime? JobStartDate { get; set; }
        public DateTime? JobDueDate { get; set; }

        // Staff
        public string? PartnerUUID { get; set; }
        public string? PartnerName { get; set; }
        public string? ManagerUUID { get; set; }
        public string? ManagerName { get; set; }

        // Client Group (shared across all clients in job)
        public string? ClientGroupUUID { get; set; }
        public string? ClientGroupName { get; set; }
        public string? ClientGroupReference { get; set; }

        // Billing
        public string? BillingEntity { get; set; }
        public string? BillingEntityUUID { get; set; }
        public string? BillingEntityName { get; set; }
        public string? BillingEntityEmail { get; set; }
        public string? BillingEntityPhone { get; set; }

        // RFI Workflow (job-level)
        public string? RFIWorkflowID { get; set; }
        public string? RFIStatus { get; set; }
        public DateTime? RFIInitiatedDate { get; set; }
        public string? InitiatedByPartnerName { get; set; }
        public string? RFIPartnerNotes { get; set; }
        public DateTime? Reminder1SentDate { get; set; }
        public DateTime? Reminder2SentDate { get; set; }
        public DateTime? FinalNoticeSentDate { get; set; }
        public DateTime? FirstDocumentUploadDate { get; set; }
        public bool DoNotSendFlag { get; set; }
        public bool StoppedFlag { get; set; }
    }

    public class ClientRecord
    {
        // Client Info
        public string? ClientUUID { get; set; }
        public string? ClientCode { get; set; }
        public string? ClientName { get; set; }
        public string? CasualName { get; set; }
        public string? GreetingName { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }
        public string? TFN { get; set; }
        public bool IsBookkeepingClient { get; set; }
        public string? BillingClientUUID { get; set; }

        // Contact Info
        public string? PrimaryContactName { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactMobile { get; set; }
        public string? CCContactEmail { get; set; }

        // ATO Info (client-specific)
        public DateTime? ATODueDate { get; set; }
        public string? ATOLodgementCode { get; set; }
        public string? ATO2025Status { get; set; }
        public string? ATOClientType { get; set; }
    }

    public class CommunicationRecord
    {
        public string? CommunicationType { get; set; }
        public string? CommunicationStage { get; set; }
        public string? ToAddress { get; set; }
        public string? Subject { get; set; }
        public string? SendStatus { get; set; }
        public DateTime? SentDate { get; set; }
        public string? FailureReason { get; set; }
    }

    public class DocumentRecord
    {
        public string? DocumentID { get; set; }
        public string? OriginalFileName { get; set; }
        public string? FileType { get; set; }
        public long FileSize { get; set; }
        public string? DocumentCategory { get; set; }
        public DateTime? UploadedDate { get; set; }
        public bool FiledToFYI { get; set; }
    }

    #endregion
}
