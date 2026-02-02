using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;

namespace Preworkinagent.Cards;

/// <summary>
/// Builds the Individual Job Review Adaptive Card for BR-3.1.2.
/// Displays detailed job information for Partner review during individual job review flow.
/// </summary>
public class IndividualJobReviewCardBuilder
{
    /// <summary>
    /// Data model for the Individual Job Review Card
    /// </summary>
    public class JobReviewCardData
    {
        // Billing Entity Information
        public string BillingEntityName { get; set; } = string.Empty;
        public string BillingEntityEmail { get; set; } = string.Empty;
        public string BillingEntityPhone { get; set; } = "Not available";

        // Job Summary
        public int JobCount { get; set; } = 1;
        public string EarliestDueDate { get; set; } = "N/A";
        public string DueDateColor { get; set; } = "Default";

        // Client Group
        public string ClientGroupName { get; set; } = string.Empty;
        public string GroupReference { get; set; } = string.Empty;
        public string ClientGroupUUID { get; set; } = string.Empty;

        // Job Info
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;

        // Billing Client (Primary)
        public string BillingClientName { get; set; } = string.Empty;
        public string BillingClientDueDate { get; set; } = "N/A";

        // Related Clients (if any)
        public List<RelatedClientInfo> RelatedClients { get; set; } = new();

        // Prior Year History
        public string PriorYearStartDate { get; set; } = "N/A";
        public string PriorYearInvoiceDate { get; set; } = "N/A";

        // Staff
        public string PartnerName { get; set; } = "Not assigned";
        public string? ManagerName { get; set; }

        // Flags
        public bool IsBookkeepingClient { get; set; }

        // Navigation
        public int CurrentIndex { get; set; } = 1;
        public int TotalContacts { get; set; } = 1;
        public string AllJobIds { get; set; } = string.Empty;
    }

    public class RelatedClientInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DueDate { get; set; } = "N/A";
        public string JobId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Builds the Adaptive Card from the provided data using Microsoft.Teams.Cards builder
    /// </summary>
    public static AdaptiveCard BuildCard(JobReviewCardData data)
    {
        var bodyElements = new List<CardElement>();

        // 1. Header Section - Job ID and Primary Contact
        bodyElements.Add(BuildHeaderSection(data));

        // 2. Client Group Section - All clients in the job
        bodyElements.AddRange(BuildClientGroupSection(data));

        // 3. Due Date Summary
        bodyElements.Add(BuildDueDateSection(data));

        // 4. Billing Entity Information
        bodyElements.Add(BuildBillingEntitySection(data));

        // 5. Staff Assignment
        bodyElements.Add(BuildStaffSection(data));

        // 6. Prior Year History Section
        bodyElements.Add(BuildPriorYearSection(data));

        // 7. Bookkeeping Client Badge (conditional)
        if (data.IsBookkeepingClient)
        {
            bodyElements.Add(BuildBookkeepingBadge());
        }

        // 8. Notes Input Section
        bodyElements.AddRange(BuildNotesSection());

        return new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Version = new Microsoft.Teams.Cards.Version("1.5"),
            Body = bodyElements,
            Actions = BuildActions(data)
        };
    }

    private static Container BuildHeaderSection(JobReviewCardData data)
    {
        return new Container
        {
            Style = ContainerStyle.Emphasis,
            Bleed = true,
            Items = new List<CardElement>
            {
                new ColumnSet
                {
                    Columns = new List<Column>
                    {
                        new Column
                        {
                            Width = new Union<string, float>("auto"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("\ud83d\udcbc")
                                {
                                    Size = TextSize.Large
                                }
                            }
                        },
                        new Column
                        {
                            Width = new Union<string, float>("stretch"),
                            Items = new List<CardElement>
                            {
                                new TextBlock($"Job: {data.JobId}")
                                {
                                    Weight = TextWeight.Bolder,
                                    Size = TextSize.Large,
                                    Wrap = true
                                },
                                new TextBlock(data.JobName)
                                {
                                    Spacing = Spacing.None,
                                    Wrap = true
                                },
                                new TextBlock($"{data.JobCount} client(s) in this job")
                                {
                                    Spacing = Spacing.None,
                                    IsSubtle = true,
                                    Size = TextSize.Small,
                                    Wrap = true
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static List<CardElement> BuildClientGroupSection(JobReviewCardData data)
    {
        var elements = new List<CardElement>
        {
            // Client Group Header
            new Container
            {
                Separator = true,
                Spacing = Spacing.Medium,
                Items = new List<CardElement>
                {
                    new TextBlock($"\ud83d\udcc1 Client Group: {data.ClientGroupName}")
                    {
                        Weight = TextWeight.Bolder,
                        Size = TextSize.Medium
                    }
                }
            },
            // Billing Client (Primary) - marked with star
            new ColumnSet
            {
                Spacing = Spacing.Small,
                Columns = new List<Column>
                {
                    new Column
                    {
                        Width = new Union<string, float>("auto"),
                        Items = new List<CardElement>
                        {
                            new TextBlock("\u2605")
                            {
                                Color = TextColor.Warning
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("stretch"),
                        Items = new List<CardElement>
                        {
                            new TextBlock($"{data.BillingClientName} ({data.GroupReference})")
                            {
                                Weight = TextWeight.Bolder
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("auto"),
                        Items = new List<CardElement>
                        {
                            new TextBlock(data.BillingClientDueDate != "N/A" ? $"Due: {data.BillingClientDueDate}" : "")
                            {
                                IsSubtle = true,
                                Size = TextSize.Small
                            }
                        }
                    }
                }
            }
        };

        // Add related clients if any
        if (data.RelatedClients.Count > 0)
        {
            foreach (var client in data.RelatedClients)
            {
                elements.Add(new ColumnSet
                {
                    Spacing = Spacing.Small,
                    Columns = new List<Column>
                    {
                        new Column
                        {
                            Width = new Union<string, float>("auto"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("\u2022")
                                {
                                    IsSubtle = true
                                }
                            }
                        },
                        new Column
                        {
                            Width = new Union<string, float>("stretch"),
                            Items = new List<CardElement>
                            {
                                new TextBlock(client.Name)
                                {
                                    IsSubtle = true
                                }
                            }
                        },
                        new Column
                        {
                            Width = new Union<string, float>("auto"),
                            Items = new List<CardElement>
                            {
                                new TextBlock(client.DueDate != "N/A" ? $"Due: {client.DueDate}" : "")
                                {
                                    IsSubtle = true,
                                    Size = TextSize.Small
                                }
                            }
                        }
                    }
                });
            }
        }

        return elements;
    }

    private static Container BuildDueDateSection(JobReviewCardData data)
    {
        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new ColumnSet
                {
                    Columns = new List<Column>
                    {
                        new Column
                        {
                            Width = new Union<string, float>("auto"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("\ud83d\udcc5")
                                {
                                    Size = TextSize.Medium
                                }
                            }
                        },
                        new Column
                        {
                            Width = new Union<string, float>("stretch"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("Earliest Due Date")
                                {
                                    Size = TextSize.Small,
                                    IsSubtle = true
                                },
                                new TextBlock(data.EarliestDueDate)
                                {
                                    Weight = TextWeight.Bolder,
                                    Color = GetTextColor(data.DueDateColor),
                                    Spacing = Spacing.None
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static Container BuildBillingEntitySection(JobReviewCardData data)
    {
        var facts = new List<Fact>
        {
            new Fact("Name:", data.BillingEntityName),
            new Fact("Email:", data.BillingEntityEmail)
        };

        // Add phone if available
        if (!string.IsNullOrEmpty(data.BillingEntityPhone) && data.BillingEntityPhone != "Not available")
        {
            facts.Add(new Fact("Phone:", data.BillingEntityPhone));
        }

        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("\ud83c\udfe2 Billing Entity")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Small
                },
                new FactSet
                {
                    Facts = facts
                }
            }
        };
    }

    private static Container BuildStaffSection(JobReviewCardData data)
    {
        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("\ud83d\udc54 Staff Assignment")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Small
                },
                new FactSet
                {
                    Facts = new List<Fact>
                    {
                        new Fact("Partner:", data.PartnerName),
                        new Fact("Manager:", data.ManagerName ?? data.PartnerName)
                    }
                }
            }
        };
    }

    private static Container BuildPriorYearSection(JobReviewCardData data)
    {
        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("\ud83d\udccc Prior Year History")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Small
                },
                new ColumnSet
                {
                    Columns = new List<Column>
                    {
                        new Column
                        {
                            Width = new Union<string, float>("stretch"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("Work started:")
                                {
                                    Size = TextSize.Small,
                                    IsSubtle = true
                                },
                                new TextBlock(data.PriorYearStartDate)
                                {
                                    Spacing = Spacing.None
                                }
                            }
                        },
                        new Column
                        {
                            Width = new Union<string, float>("stretch"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("Invoiced:")
                                {
                                    Size = TextSize.Small,
                                    IsSubtle = true
                                },
                                new TextBlock(data.PriorYearInvoiceDate)
                                {
                                    Spacing = Spacing.None
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static Container BuildBookkeepingBadge()
    {
        return new Container
        {
            Items = new List<CardElement>
            {
                new TextBlock("\ud83d\udcd2 Bookkeeping Client")
                {
                    Color = TextColor.Accent,
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Small
                }
            }
        };
    }

    private static List<CardElement> BuildNotesSection()
    {
        return new List<CardElement>
        {
            new Container
            {
                Separator = true,
                Spacing = Spacing.Medium,
                Items = new List<CardElement>
                {
                    new TextBlock("\ud83d\udcdd Add note to email (optional):")
                    {
                        Size = TextSize.Small,
                        Weight = TextWeight.Bolder
                    }
                }
            },
            new TextInput
            {
                Id = "partnerNotes",
                Placeholder = "Enter any notes for the client email...",
                IsMultiline = true,
                MaxLength = 500
            }
        };
    }

    private static List<Microsoft.Teams.Cards.Action> BuildActions(JobReviewCardData data)
    {
        return new List<Microsoft.Teams.Cards.Action>
        {
            new ExecuteAction
            {
                Title = "\u2705 Send RFI",
                Style = ActionStyle.Positive,
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "send_rfi" },
                        { "jobId", data.JobId },
                        { "clientGroupUUID", data.ClientGroupUUID },
                        { "billingEntityEmail", data.BillingEntityEmail },
                        { "currentIndex", data.CurrentIndex },
                        { "totalContacts", data.TotalContacts },
                        { "allJobIds", data.AllJobIds }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            },
            new ExecuteAction
            {
                Title = "\u23ed\ufe0f Skip",
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "skip" },
                        { "jobId", data.JobId },
                        { "currentIndex", data.CurrentIndex },
                        { "totalContacts", data.TotalContacts },
                        { "allJobIds", data.AllJobIds }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            },
            new ExecuteAction
            {
                Title = "\ud83d\udeab Do Not Send",
                Style = ActionStyle.Destructive,
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "do_not_send" },
                        { "jobId", data.JobId },
                        { "clientGroupUUID", data.ClientGroupUUID },
                        { "billingEntityEmail", data.BillingEntityEmail },
                        { "currentIndex", data.CurrentIndex },
                        { "totalContacts", data.TotalContacts },
                        { "allJobIds", data.AllJobIds }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            }
        };
    }

    /// <summary>
    /// Helper method to convert color string to TextColor enum
    /// </summary>
    private static TextColor GetTextColor(string colorName)
    {
        return colorName switch
        {
            "Attention" => TextColor.Attention,
            "Warning" => TextColor.Warning,
            "Good" => TextColor.Good,
            "Accent" => TextColor.Accent,
            "Light" => TextColor.Light,
            "Dark" => TextColor.Dark,
            _ => TextColor.Default
        };
    }

    /// <summary>
    /// Helper method to determine due date color based on days until deadline
    /// </summary>
    public static string GetDueDateColor(DateTime? dueDate)
    {
        if (!dueDate.HasValue) return "Default";

        var daysUntil = (dueDate.Value - DateTime.Today).Days;

        return daysUntil switch
        {
            < 0 => "Attention",   // Overdue - Red
            <= 7 => "Warning",    // Within 7 days - Yellow/Orange
            <= 21 => "Default",   // Within 21 days - Default
            _ => "Good"           // More than 21 days - Green
        };
    }

    /// <summary>
    /// Helper method to format date for display
    /// </summary>
    public static string FormatDate(DateTime? date)
    {
        return date.HasValue ? date.Value.ToString("dd MMM yyyy") : "N/A";
    }

    /// <summary>
    /// Creates card data from JobDetailsFunctions.JobDetailsResult
    /// </summary>
    public static JobReviewCardData CreateFromJobDetails(
        Functions.JobDetailsFunctions.JobDetailsResult jobResult,
        int currentIndex = 1,
        int totalContacts = 1,
        string allJobIds = "")
    {
        var job = jobResult.JobDetails;
        if (job == null) return new JobReviewCardData();

        // Get the first client (billing client) for primary info
        var primaryClient = jobResult.Clients.FirstOrDefault();

        // Find the earliest ATO due date across all clients
        var earliestDueDate = jobResult.Clients
            .Where(c => c.ATODueDate.HasValue)
            .OrderBy(c => c.ATODueDate)
            .FirstOrDefault()?.ATODueDate;

        // Build related clients list (excluding the primary/billing client)
        var relatedClients = jobResult.Clients
            .Skip(1)  // Skip first client (primary)
            .Select(c => new RelatedClientInfo
            {
                Name = c.ClientName ?? "Unknown",
                DueDate = FormatDate(c.ATODueDate),
                JobId = job.JobID ?? ""
            })
            .ToList();

        return new JobReviewCardData
        {
            // Billing Entity Information (from job-level data)
            BillingEntityName = job.BillingEntityName ?? "Unknown",
            BillingEntityEmail = job.BillingEntityEmail ?? "No email",
            BillingEntityPhone = job.BillingEntityPhone ?? "Not available",

            // Job Summary
            JobCount = jobResult.Clients.Count,
            EarliestDueDate = FormatDate(earliestDueDate),
            DueDateColor = GetDueDateColor(earliestDueDate),

            // Client Group
            ClientGroupName = job.ClientGroupName ?? primaryClient?.ClientName ?? "Unknown Group",
            GroupReference = job.ClientGroupReference ?? primaryClient?.ClientCode ?? "",
            ClientGroupUUID = job.ClientGroupUUID ?? "",

            // Job Info
            JobId = job.JobID ?? "",
            JobName = job.JobName ?? "",

            // Billing Client (Primary in client list)
            BillingClientName = primaryClient?.ClientName ?? "Unknown",
            BillingClientDueDate = FormatDate(primaryClient?.ATODueDate),

            // Related Clients
            RelatedClients = relatedClients,

            // Prior Year History
            PriorYearStartDate = FormatDate(job.JobStartDate),
            PriorYearInvoiceDate = "N/A", // Will need to be populated from historical data

            // Staff
            PartnerName = job.PartnerName ?? "Not assigned",
            ManagerName = job.ManagerName,

            // Flags
            IsBookkeepingClient = primaryClient?.IsBookkeepingClient ?? false,

            // Navigation
            CurrentIndex = currentIndex,
            TotalContacts = totalContacts,
            AllJobIds = string.IsNullOrEmpty(allJobIds) ? job.JobID ?? "" : allJobIds
        };
    }

    /// <summary>
    /// Build a confirmation card showing the action result
    /// </summary>
    public static AdaptiveCard BuildConfirmationCard(CardActionResult result)
    {
        var statusIcon = result.Success ? "\u2705" : "\u274c";

        var bodyElements = new List<CardElement>
        {
            new Container
            {
                Style = result.Success ? ContainerStyle.Good : ContainerStyle.Attention,
                Items = new List<CardElement>
                {
                    new TextBlock($"{statusIcon} {result.ActionTaken}")
                    {
                        Weight = TextWeight.Bolder,
                        Size = TextSize.Large
                    },
                    new TextBlock(result.Message)
                    {
                        Wrap = true
                    }
                }
            }
        };

        // Add progress info if reviewing multiple
        if (result.TotalContacts > 1)
        {
            bodyElements.Add(new TextBlock($"Progress: {result.CurrentIndex} of {result.TotalContacts} contacts reviewed")
            {
                Size = TextSize.Small,
                IsSubtle = true,
                Spacing = Spacing.Medium
            });
        }

        var actions = new List<Microsoft.Teams.Cards.Action>();

        // Add "Next" action if more contacts to review
        if (result.HasNextCard)
        {
            actions.Add(new ExecuteAction
            {
                Title = "\u27a1\ufe0f Review Next",
                Style = ActionStyle.Positive,
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "review_next" },
                        { "nextIndex", result.NextIndex },
                        { "totalContacts", result.TotalContacts },
                        { "allJobIds", result.AllJobIds }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            });
            actions.Add(new ExecuteAction
            {
                Title = "Done",
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "review_complete" }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            });
        }

        return new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Version = new Microsoft.Teams.Cards.Version("1.5"),
            Body = bodyElements,
            Actions = actions.Count > 0 ? actions : null
        };
    }

    /// <summary>
    /// Build a summary card showing all actions taken during review session
    /// </summary>
    public static AdaptiveCard BuildReviewSummaryCard(List<CardActionResult> results)
    {
        var sentCount = results.Count(r => r.ActionTaken == "RFI Sent" && r.Success);
        var skippedCount = results.Count(r => r.ActionTaken == "Skipped");
        var doNotSendCount = results.Count(r => r.ActionTaken == "Do Not Send" && r.Success);
        var errorCount = results.Count(r => !r.Success);

        var bodyElements = new List<CardElement>
        {
            new TextBlock("\ud83d\udcca Review Session Complete")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Large
            },
            new FactSet
            {
                Facts = new List<Fact>
                {
                    new Fact("\ud83d\udce4 RFI Sent:", sentCount.ToString()),
                    new Fact("\u23ed\ufe0f Skipped:", skippedCount.ToString()),
                    new Fact("\ud83d\udeab Do Not Send:", doNotSendCount.ToString())
                }
            }
        };

        if (errorCount > 0)
        {
            bodyElements.Add(new TextBlock($"\u26a0\ufe0f {errorCount} action(s) failed")
            {
                Color = TextColor.Attention,
                Spacing = Spacing.Medium
            });
        }

        return new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Version = new Microsoft.Teams.Cards.Version("1.5"),
            Body = bodyElements
        };
    }
}

/// <summary>
/// Result of a card action
/// </summary>
public class CardActionResult
{
    public bool Success { get; set; }
    public string Action { get; set; } = "";
    public string ActionTaken { get; set; } = "";
    public string Message { get; set; } = "";
    public string JobId { get; set; } = "";
    public int CurrentIndex { get; set; }
    public int TotalContacts { get; set; }
    public string AllJobIds { get; set; } = "";
    public bool HasNextCard { get; set; }
    public int NextIndex { get; set; }
}
