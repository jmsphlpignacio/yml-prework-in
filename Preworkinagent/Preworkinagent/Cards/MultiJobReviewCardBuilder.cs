using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;

namespace Preworkinagent.Cards;

/// <summary>
/// Builds the Multi-Job Review Adaptive Card for reviewing and initiating RFI on multiple jobs at once.
/// Triggered by prompts like "review card J022755 J022756 J022757".
/// </summary>
public class MultiJobReviewCardBuilder
{
    /// <summary>
    /// Data model for the Multi-Job Review Card
    /// </summary>
    public class MultiJobReviewCardData
    {
        // Summary
        public int JobCount => Jobs.Count;
        public string EarliestDueDate { get; set; } = "N/A";
        public string DueDateColor { get; set; } = "Default";

        // Jobs List
        public List<JobInfo> Jobs { get; set; } = new();

        // All Job IDs (comma-separated for action data)
        public string AllJobIds => string.Join(",", Jobs.Select(j => j.JobId));

        // Partner Info (for RFI initiation)
        public string PartnerName { get; set; } = string.Empty;
        public string? PartnerEmail { get; set; }
    }

    public class JobInfo
    {
        public string JobId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientGroupName { get; set; } = string.Empty;
        public string ClientGroupReference { get; set; } = string.Empty;
        public string DueDate { get; set; } = "N/A";
        public string DueDateColor { get; set; } = "Default";
        public string BillingEntityEmail { get; set; } = string.Empty;
        public string JobState { get; set; } = string.Empty;
        public bool IsBookkeepingClient { get; set; }
        public DateTime? ATODueDate { get; set; }
    }

    /// <summary>
    /// Builds the Adaptive Card from the provided data using Microsoft.Teams.Cards builder
    /// </summary>
    public static AdaptiveCard BuildCard(MultiJobReviewCardData data)
    {
        var bodyElements = new List<CardElement>();

        // 1. Header Section
        bodyElements.Add(BuildHeaderSection(data));

        // 2. Summary Section (earliest due date)
        bodyElements.Add(BuildSummarySection(data));

        // 3. Jobs List Section
        bodyElements.AddRange(BuildJobsListSection(data));

        // 4. Notes Input Section
        bodyElements.AddRange(BuildNotesSection());

        // 5. Exclusion Selection (if multiple jobs)
        if (data.JobCount > 1)
        {
            bodyElements.Add(BuildExclusionSection(data));
        }

        return new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Version = new Microsoft.Teams.Cards.Version("1.5"),
            Body = bodyElements,
            Actions = BuildActions(data)
        };
    }

    private static Container BuildHeaderSection(MultiJobReviewCardData data)
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
                                new TextBlock("\ud83d\udcdd")
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
                                new TextBlock("Multi-Job RFI Review")
                                {
                                    Weight = TextWeight.Bolder,
                                    Size = TextSize.Large,
                                    Wrap = true
                                },
                                new TextBlock($"{data.JobCount} job(s) selected for review")
                                {
                                    Spacing = Spacing.None,
                                    IsSubtle = true,
                                    Wrap = true
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static Container BuildSummarySection(MultiJobReviewCardData data)
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
                            Width = new Union<string, float>("stretch"),
                            Items = new List<CardElement>
                            {
                                new TextBlock("\ud83d\udcc5 Earliest Due:")
                                {
                                    Size = TextSize.Small,
                                    IsSubtle = true
                                }
                            }
                        },
                        new Column
                        {
                            Width = new Union<string, float>("auto"),
                            Items = new List<CardElement>
                            {
                                new TextBlock(data.EarliestDueDate)
                                {
                                    Weight = TextWeight.Bolder,
                                    Color = GetTextColor(data.DueDateColor)
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static List<CardElement> BuildJobsListSection(MultiJobReviewCardData data)
    {
        var elements = new List<CardElement>
        {
            // Section header
            new Container
            {
                Separator = true,
                Spacing = Spacing.Medium,
                Items = new List<CardElement>
                {
                    new TextBlock("\ud83d\udccb JOBS TO REVIEW")
                    {
                        Weight = TextWeight.Bolder,
                        Size = TextSize.Medium
                    },
                    // Column headers
                    new ColumnSet
                    {
                        Columns = new List<Column>
                        {
                            new Column
                            {
                                Width = new Union<string, float>("80px"),
                                Items = new List<CardElement>
                                {
                                    new TextBlock("Job ID")
                                    {
                                        Weight = TextWeight.Bolder,
                                        Size = TextSize.Small
                                    }
                                }
                            },
                            new Column
                            {
                                Width = new Union<string, float>("stretch"),
                                Items = new List<CardElement>
                                {
                                    new TextBlock("Client / Group")
                                    {
                                        Weight = TextWeight.Bolder,
                                        Size = TextSize.Small
                                    }
                                }
                            },
                            new Column
                            {
                                Width = new Union<string, float>("90px"),
                                Items = new List<CardElement>
                                {
                                    new TextBlock("Due Date")
                                    {
                                        Weight = TextWeight.Bolder,
                                        Size = TextSize.Small,
                                        HorizontalAlignment = HorizontalAlignment.Right
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Add each job row
        foreach (var job in data.Jobs)
        {
            var bookkeepingBadge = job.IsBookkeepingClient ? " \ud83d\udcd2" : "";

            elements.Add(new ColumnSet
            {
                Columns = new List<Column>
                {
                    new Column
                    {
                        Width = new Union<string, float>("80px"),
                        Items = new List<CardElement>
                        {
                            new TextBlock(job.JobId)
                            {
                                Size = TextSize.Small,
                                Color = TextColor.Accent
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("stretch"),
                        Items = new List<CardElement>
                        {
                            new TextBlock($"{job.ClientName}{bookkeepingBadge}")
                            {
                                Size = TextSize.Small,
                                Wrap = true
                            },
                            new TextBlock($"{job.ClientGroupName} ({job.ClientGroupReference})")
                            {
                                Size = TextSize.Small,
                                IsSubtle = true,
                                Spacing = Spacing.None,
                                Wrap = true
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("90px"),
                        Items = new List<CardElement>
                        {
                            new TextBlock(job.DueDate)
                            {
                                Size = TextSize.Small,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Color = GetTextColor(job.DueDateColor)
                            }
                        }
                    }
                }
            });
        }

        return elements;
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
                    new TextBlock("\ud83d\udcdd Add note to all emails (optional):")
                    {
                        Size = TextSize.Small,
                        Weight = TextWeight.Bolder
                    }
                }
            },
            new TextInput
            {
                Id = "partnerNotes",
                Placeholder = "Enter any notes to include in all RFI emails...",
                IsMultiline = true,
                MaxLength = 500
            }
        };
    }

    private static Container BuildExclusionSection(MultiJobReviewCardData data)
    {
        var choices = data.Jobs.Select(j => new Choice
        {
            Title = $"{j.JobId} - {j.ClientName}",
            Value = j.JobId
        }).ToList();

        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("\u274c Select jobs to EXCLUDE (optional):")
                {
                    Size = TextSize.Small,
                    Weight = TextWeight.Bolder
                },
                new ChoiceSetInput
                {
                    Id = "excludedJobs",
                    IsMultiSelect = true,
                    Choices = choices
                }
            }
        };
    }

    private static List<Microsoft.Teams.Cards.Action> BuildActions(MultiJobReviewCardData data)
    {
        return new List<Microsoft.Teams.Cards.Action>
        {
            new ExecuteAction
            {
                Title = $"\u2705 Send RFI to All ({data.JobCount})",
                Style = ActionStyle.Positive,
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "send_rfi_multi" },
                        { "allJobIds", data.AllJobIds },
                        { "jobCount", data.JobCount },
                        { "partnerName", data.PartnerName },
                        { "partnerEmail", data.PartnerEmail }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            },
            new ExecuteAction
            {
                Title = "\ud83d\udd0d Review One by One",
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "review_individual_multi" },
                        { "allJobIds", data.AllJobIds },
                        { "jobCount", data.JobCount },
                        { "currentIndex", 1 }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            },
            new ExecuteAction
            {
                Title = "\u274c Cancel",
                Style = ActionStyle.Destructive,
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "cancel_multi_review" }
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
    /// Creates card data from a list of JobDetailsFunctions.JobDetailsResult
    /// </summary>
    public static MultiJobReviewCardData CreateFromJobDetailsList(
        List<Functions.JobDetailsFunctions.JobDetailsResult> jobResults,
        string partnerName = "",
        string? partnerEmail = null)
    {
        var cardData = new MultiJobReviewCardData
        {
            PartnerName = partnerName,
            PartnerEmail = partnerEmail
        };

        DateTime? earliestDue = null;

        foreach (var jobResult in jobResults)
        {
            var job = jobResult.JobDetails;
            if (job == null) continue;

            var primaryClient = jobResult.Clients.FirstOrDefault();

            // Find earliest ATO due date for this job
            var jobEarliestDue = jobResult.Clients
                .Where(c => c.ATODueDate.HasValue)
                .OrderBy(c => c.ATODueDate)
                .FirstOrDefault()?.ATODueDate;

            // Track overall earliest due
            if (jobEarliestDue.HasValue && (!earliestDue.HasValue || jobEarliestDue < earliestDue))
            {
                earliestDue = jobEarliestDue;
            }

            cardData.Jobs.Add(new JobInfo
            {
                JobId = job.JobID ?? "",
                ClientName = job.BillingEntityName ?? primaryClient?.ClientName ?? "Unknown",
                ClientGroupName = job.ClientGroupName ?? "No Group",
                ClientGroupReference = job.ClientGroupReference ?? "",
                DueDate = FormatDate(jobEarliestDue),
                DueDateColor = GetDueDateColor(jobEarliestDue),
                BillingEntityEmail = job.BillingEntityEmail ?? "",
                JobState = job.JobState ?? "",
                IsBookkeepingClient = primaryClient?.IsBookkeepingClient ?? false,
                ATODueDate = jobEarliestDue
            });
        }

        // Sort jobs by due date
        cardData.Jobs = cardData.Jobs
            .OrderBy(j => j.ATODueDate ?? DateTime.MaxValue)
            .ToList();

        // Set earliest due date for summary
        cardData.EarliestDueDate = FormatDate(earliestDue);
        cardData.DueDateColor = GetDueDateColor(earliestDue);

        return cardData;
    }

    /// <summary>
    /// Build a sample card for testing/preview purposes
    /// </summary>
    public static AdaptiveCard BuildSampleCard()
    {
        var sampleData = new MultiJobReviewCardData
        {
            PartnerName = "John Smith",
            PartnerEmail = "john.smith@yml.com.au",
            EarliestDueDate = "15 Feb 2026",
            DueDateColor = "Warning",
            Jobs = new List<JobInfo>
            {
                new()
                {
                    JobId = "J022755",
                    ClientName = "Smith Family Trust",
                    ClientGroupName = "Smith Holdings",
                    ClientGroupReference = "SMIT0001",
                    DueDate = "15 Feb 2026",
                    DueDateColor = "Warning",
                    IsBookkeepingClient = true
                },
                new()
                {
                    JobId = "J022756",
                    ClientName = "John Smith",
                    ClientGroupName = "Smith Holdings",
                    ClientGroupReference = "SMIT0001",
                    DueDate = "15 Feb 2026",
                    DueDateColor = "Warning",
                    IsBookkeepingClient = false
                },
                new()
                {
                    JobId = "J022757",
                    ClientName = "Smith Investments Pty Ltd",
                    ClientGroupName = "Smith Holdings",
                    ClientGroupReference = "SMIT0001",
                    DueDate = "28 Feb 2026",
                    DueDateColor = "Default",
                    IsBookkeepingClient = false
                }
            }
        };

        return BuildCard(sampleData);
    }

    /// <summary>
    /// Build confirmation card after sending RFI to multiple jobs
    /// </summary>
    public static AdaptiveCard BuildMultiRFIConfirmationCard(
        int totalJobs,
        int successCount,
        int failedCount,
        List<string> failedJobIds,
        List<string> excludedJobIds)
    {
        var bodyElements = new List<CardElement>();

        var allSuccessful = failedCount == 0;
        var statusIcon = allSuccessful ? "\u2705" : "\u26a0\ufe0f";
        var statusStyle = allSuccessful ? ContainerStyle.Good : ContainerStyle.Attention;

        // Header
        bodyElements.Add(new Container
        {
            Style = statusStyle,
            Bleed = true,
            Items = new List<CardElement>
            {
                new TextBlock($"{statusIcon} Multi-Job RFI {(allSuccessful ? "Completed" : "Completed with Errors")}")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Large
                }
            }
        });

        // Summary
        var facts = new List<Fact>
        {
            new Fact("\ud83d\udce4 RFI Sent:", successCount.ToString()),
        };

        if (excludedJobIds.Count > 0)
        {
            facts.Add(new Fact("\u23ed\ufe0f Excluded:", excludedJobIds.Count.ToString()));
        }

        if (failedCount > 0)
        {
            facts.Add(new Fact("\u274c Failed:", failedCount.ToString()));
        }

        bodyElements.Add(new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("Summary")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Medium
                },
                new FactSet
                {
                    Facts = facts
                }
            }
        });

        // Failed jobs details (if any)
        if (failedJobIds.Count > 0)
        {
            bodyElements.Add(new Container
            {
                Separator = true,
                Spacing = Spacing.Medium,
                Items = new List<CardElement>
                {
                    new TextBlock("\u274c Failed Jobs:")
                    {
                        Weight = TextWeight.Bolder,
                        Size = TextSize.Small,
                        Color = TextColor.Attention
                    },
                    new TextBlock(string.Join(", ", failedJobIds))
                    {
                        Size = TextSize.Small,
                        Wrap = true
                    }
                }
            });
        }

        // Excluded jobs details (if any)
        if (excludedJobIds.Count > 0)
        {
            bodyElements.Add(new Container
            {
                Separator = true,
                Spacing = Spacing.Medium,
                Items = new List<CardElement>
                {
                    new TextBlock("\u23ed\ufe0f Excluded Jobs:")
                    {
                        Weight = TextWeight.Bolder,
                        Size = TextSize.Small
                    },
                    new TextBlock(string.Join(", ", excludedJobIds))
                    {
                        Size = TextSize.Small,
                        IsSubtle = true,
                        Wrap = true
                    }
                }
            });
        }

        // Footer message
        bodyElements.Add(new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("Reminder sequence activated: T+7 email, T+14 SMS, T+21 final notice")
                {
                    Size = TextSize.Small,
                    IsSubtle = true,
                    Wrap = true
                }
            }
        });

        return new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Version = new Microsoft.Teams.Cards.Version("1.5"),
            Body = bodyElements
        };
    }
}
