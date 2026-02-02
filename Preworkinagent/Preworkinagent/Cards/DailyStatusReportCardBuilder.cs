using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;

namespace Preworkinagent.Cards;

/// <summary>
/// Builds the Daily Status Report Adaptive Card for BR-6.1.1.
/// Displays job state counts, attention items, and reminders due for the daily report.
/// </summary>
public class DailyStatusReportCardBuilder
{
    /// <summary>
    /// Data model for the Daily Status Report Card
    /// </summary>
    public class DailyStatusReportCardData
    {
        // Report Header
        public string ReportDate { get; set; } = DateTime.Today.ToString("dddd, dd MMMM yyyy");
        public string ReportTime { get; set; } = DateTime.Now.ToString("h:mm tt");

        // Attention Required Section
        public int AtRiskCount { get; set; }
        public int ApproachingDeadlineCount { get; set; }
        public int EmailBounceCount { get; set; }

        // Job State Summary
        public List<JobStateSummaryItem> JobStateSummary { get; set; } = new();

        // Reminders Due Today
        public int Reminder1DueCount { get; set; }
        public int Reminder2DueCount { get; set; }
        public int FinalNoticeDueCount { get; set; }

        // Computed Properties
        public bool HasAttentionItems => AtRiskCount > 0 || ApproachingDeadlineCount > 0 || EmailBounceCount > 0;
        public int TotalRemindersDue => Reminder1DueCount + Reminder2DueCount + FinalNoticeDueCount;
        public int TotalJobs => JobStateSummary.Sum(s => s.Count);
    }

    public class JobStateSummaryItem
    {
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
        public int? PreviousCount { get; set; }

        // Computed change values
        public int Change => PreviousCount.HasValue ? Count - PreviousCount.Value : 0;
        public string ChangeDisplay => Change == 0 ? "-" : (Change > 0 ? $"+{Change}" : Change.ToString());
        public string ChangeColor => Change > 0 ? "Good" : (Change < 0 ? "Attention" : "Default");
    }

    /// <summary>
    /// Builds the Adaptive Card from the provided data using Microsoft.Teams.Cards builder
    /// </summary>
    public static AdaptiveCard BuildCard(DailyStatusReportCardData data)
    {
        var bodyElements = new List<CardElement>();

        // 1. Header Section
        bodyElements.Add(BuildHeaderSection(data));

        // 2. Attention Required Section (conditional)
        if (data.HasAttentionItems)
        {
            bodyElements.Add(BuildAttentionSection(data));
        }

        // 3. Job Status Summary Section
        bodyElements.Add(BuildJobStatusSection(data));

        // 4. Reminders Due Section (conditional)
        if (data.TotalRemindersDue > 0)
        {
            bodyElements.Add(BuildRemindersSection(data));
        }

        // 5. Footer with Total Jobs
        bodyElements.Add(BuildFooterSection(data));

        return new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Version = new Microsoft.Teams.Cards.Version("1.5"),
            Body = bodyElements,
            Actions = BuildActions()
        };
    }

    private static Container BuildHeaderSection(DailyStatusReportCardData data)
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
                                new TextBlock("\ud83d\udcca")
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
                                new TextBlock("Daily RFI Status Report")
                                {
                                    Weight = TextWeight.Bolder,
                                    Size = TextSize.Large,
                                    Wrap = true
                                },
                                new TextBlock($"{data.ReportDate} - {data.ReportTime} AEST")
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

    private static Container BuildAttentionSection(DailyStatusReportCardData data)
    {
        var facts = new List<Fact>();

        if (data.AtRiskCount > 0)
        {
            facts.Add(new Fact("\ud83d\udd34 At Risk (>21 days)", data.AtRiskCount.ToString()));
        }

        if (data.ApproachingDeadlineCount > 0)
        {
            facts.Add(new Fact("\ud83d\udfe1 Approaching Deadline (7 days)", data.ApproachingDeadlineCount.ToString()));
        }

        if (data.EmailBounceCount > 0)
        {
            facts.Add(new Fact("\ud83d\udce7 Email Bounces", data.EmailBounceCount.ToString()));
        }

        return new Container
        {
            Style = ContainerStyle.Attention,
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("\u26a0\ufe0f ATTENTION REQUIRED")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Medium,
                    Color = TextColor.Attention
                },
                new FactSet
                {
                    Facts = facts
                }
            }
        };
    }

    private static Container BuildJobStatusSection(DailyStatusReportCardData data)
    {
        var items = new List<CardElement>
        {
            new TextBlock("\ud83d\udccb JOB STATUS SUMMARY")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Medium
            },
            // Header row
            new ColumnSet
            {
                Columns = new List<Column>
                {
                    new Column
                    {
                        Width = new Union<string, float>("stretch"),
                        Items = new List<CardElement>
                        {
                            new TextBlock("Status")
                            {
                                Weight = TextWeight.Bolder,
                                Size = TextSize.Small
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("60px"),
                        Items = new List<CardElement>
                        {
                            new TextBlock("Count")
                            {
                                Weight = TextWeight.Bolder,
                                Size = TextSize.Small,
                                HorizontalAlignment = HorizontalAlignment.Right
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("60px"),
                        Items = new List<CardElement>
                        {
                            new TextBlock("Change")
                            {
                                Weight = TextWeight.Bolder,
                                Size = TextSize.Small,
                                HorizontalAlignment = HorizontalAlignment.Right
                            }
                        }
                    }
                }
            }
        };

        // Add each job state row
        foreach (var state in data.JobStateSummary)
        {
            items.Add(new ColumnSet
            {
                Columns = new List<Column>
                {
                    new Column
                    {
                        Width = new Union<string, float>("stretch"),
                        Items = new List<CardElement>
                        {
                            new TextBlock(GetStatusIcon(state.StatusName) + " " + state.StatusName)
                            {
                                Size = TextSize.Small
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("60px"),
                        Items = new List<CardElement>
                        {
                            new TextBlock(state.Count.ToString())
                            {
                                Size = TextSize.Small,
                                Weight = TextWeight.Bolder,
                                HorizontalAlignment = HorizontalAlignment.Right
                            }
                        }
                    },
                    new Column
                    {
                        Width = new Union<string, float>("60px"),
                        Items = new List<CardElement>
                        {
                            new TextBlock(state.ChangeDisplay)
                            {
                                Size = TextSize.Small,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Color = GetTextColor(state.ChangeColor)
                            }
                        }
                    }
                }
            });
        }

        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = items
        };
    }

    private static Container BuildRemindersSection(DailyStatusReportCardData data)
    {
        var facts = new List<Fact>();

        if (data.Reminder1DueCount > 0)
        {
            facts.Add(new Fact("\ud83d\udce7 T+7 Email Reminders", data.Reminder1DueCount.ToString()));
        }

        if (data.Reminder2DueCount > 0)
        {
            facts.Add(new Fact("\ud83d\udcf1 T+14 SMS Reminders", data.Reminder2DueCount.ToString()));
        }

        if (data.FinalNoticeDueCount > 0)
        {
            facts.Add(new Fact("\ud83d\udea8 T+21 Final Notices", data.FinalNoticeDueCount.ToString()));
        }

        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock("\ud83d\udd14 REMINDERS SCHEDULED TODAY")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Medium
                },
                new FactSet
                {
                    Facts = facts
                }
            }
        };
    }

    private static Container BuildFooterSection(DailyStatusReportCardData data)
    {
        return new Container
        {
            Separator = true,
            Spacing = Spacing.Medium,
            Items = new List<CardElement>
            {
                new TextBlock($"Total Jobs: {data.TotalJobs}")
                {
                    Size = TextSize.Small,
                    IsSubtle = true,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
    }

    private static List<Microsoft.Teams.Cards.Action> BuildActions()
    {
        return new List<Microsoft.Teams.Cards.Action>
        {
            new ExecuteAction
            {
                Title = "\ud83d\udcdd View Pre Work In Jobs",
                Style = ActionStyle.Positive,
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "view_preworkin_jobs" }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            },
            new ExecuteAction
            {
                Title = "\u26a0\ufe0f View At-Risk Clients",
                Data = new Union<string, SubmitActionData>(new SubmitActionData
                {
                    NonSchemaProperties = new Dictionary<string, object?>
                    {
                        { "action", "view_at_risk_clients" }
                    }
                }),
                AssociatedInputs = AssociatedInputs.Auto
            }
        };
    }

    /// <summary>
    /// Helper method to get status icon based on job state name
    /// </summary>
    private static string GetStatusIcon(string statusName)
    {
        return statusName switch
        {
            "Pre Work In" or "Pre Work In (EL)" or "Pre Work In (Non-EL)" => "\ud83d\udce5",
            "Requested" => "\ud83d\udce4",
            "Work In" => "\ud83d\udcdd",
            "Wait on Info" => "\u23f3",
            "Wait on Sign" => "\u270d\ufe0f",
            "Ready to Lodge" or "For Lodgement" => "\ud83d\udce6",
            "Lodged" => "\u2705",
            "Complete" => "\ud83c\udfc6",
            "Cancelled" => "\u274c",
            _ => "\u2022"
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
    /// Creates card data from RFIStatusFunctions.DailyStatusResult
    /// </summary>
    public static DailyStatusReportCardData CreateFromDailyStatus(
        Functions.RFIStatusFunctions.DailyStatusResult statusResult,
        Functions.RFIStatusFunctions.DailyStatusResult? previousDayResult = null)
    {
        var cardData = new DailyStatusReportCardData
        {
            ReportDate = DateTime.Today.ToString("dddd, dd MMMM yyyy"),
            ReportTime = DateTime.Now.ToString("h:mm tt"),
            AtRiskCount = statusResult.AtRiskCount,
            ApproachingDeadlineCount = statusResult.ApproachingDeadlineCount,
            EmailBounceCount = 0, // Placeholder - implement email bounce tracking if available
            Reminder1DueCount = statusResult.Reminder1DueCount,
            Reminder2DueCount = statusResult.Reminder2DueCount,
            FinalNoticeDueCount = statusResult.FinalNoticeDueCount
        };

        // Build job state summary with optional previous day comparison
        foreach (var state in statusResult.JobStateSummary)
        {
            var previousCount = previousDayResult?.JobStateSummary
                .FirstOrDefault(s => s.SimplifiedJobState == state.SimplifiedJobState)?.JobCount;

            cardData.JobStateSummary.Add(new JobStateSummaryItem
            {
                StatusName = state.SimplifiedJobState ?? "Unknown",
                Count = state.JobCount,
                PreviousCount = previousCount
            });
        }

        // Ensure standard states are present even if count is 0
        var standardStates = new[]
        {
            "Pre Work In (EL)",
            "Pre Work In (Non-EL)",
            "Requested",
            "Work In",
            "Wait on Info",
            "Wait on Sign",
            "Ready to Lodge",
            "Lodged",
            "Complete"
        };

        foreach (var stateName in standardStates)
        {
            if (!cardData.JobStateSummary.Any(s => s.StatusName == stateName))
            {
                var previousCount = previousDayResult?.JobStateSummary
                    .FirstOrDefault(s => s.SimplifiedJobState == stateName)?.JobCount;

                cardData.JobStateSummary.Add(new JobStateSummaryItem
                {
                    StatusName = stateName,
                    Count = 0,
                    PreviousCount = previousCount
                });
            }
        }

        // Sort by workflow order
        var stateOrder = new Dictionary<string, int>
        {
            { "Pre Work In (EL)", 1 },
            { "Pre Work In (Non-EL)", 2 },
            { "Requested", 3 },
            { "Work In", 4 },
            { "Wait on Info", 5 },
            { "Wait on Sign", 6 },
            { "Ready to Lodge", 7 },
            { "Lodged", 8 },
            { "Complete", 9 }
        };

        cardData.JobStateSummary = cardData.JobStateSummary
            .OrderBy(s => stateOrder.GetValueOrDefault(s.StatusName, 100))
            .ToList();

        return cardData;
    }

    /// <summary>
    /// Build a sample card for testing/preview purposes
    /// </summary>
    public static AdaptiveCard BuildSampleCard()
    {
        var sampleData = new DailyStatusReportCardData
        {
            ReportDate = DateTime.Today.ToString("dddd, dd MMMM yyyy"),
            ReportTime = DateTime.Now.ToString("h:mm tt"),
            AtRiskCount = 5,
            ApproachingDeadlineCount = 12,
            EmailBounceCount = 2,
            Reminder1DueCount = 8,
            Reminder2DueCount = 3,
            FinalNoticeDueCount = 2,
            JobStateSummary = new List<JobStateSummaryItem>
            {
                new() { StatusName = "Pre Work In (EL)", Count = 245, PreviousCount = 250 },
                new() { StatusName = "Pre Work In (Non-EL)", Count = 120, PreviousCount = 118 },
                new() { StatusName = "Requested", Count = 89, PreviousCount = 85 },
                new() { StatusName = "Work In", Count = 156, PreviousCount = 152 },
                new() { StatusName = "Wait on Info", Count = 34, PreviousCount = 36 },
                new() { StatusName = "Wait on Sign", Count = 28, PreviousCount = 25 },
                new() { StatusName = "Ready to Lodge", Count = 45, PreviousCount = 42 },
                new() { StatusName = "Lodged", Count = 312, PreviousCount = 305 },
                new() { StatusName = "Complete", Count = 1250, PreviousCount = 1240 }
            }
        };

        return BuildCard(sampleData);
    }
}
