using System.Text.Json;
using Backend.Models;

namespace Backend.Tests;

/// <summary>
/// Comprehensive tests for the Chronology module:
/// - TimelineEvent model defaults, serialization, and edge cases
/// - ChronologyResult model behavior  
/// - Event sorting logic (DateIso ordinal + Time)
/// - Date range computation
/// - Input validation edge cases
/// </summary>
public class ChronologyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // ══════════════════════════════════════════════════════════
    //  § 1 — TimelineEvent Model Defaults
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void TimelineEvent_DefaultValues_AreCorrect()
    {
        var evt = new TimelineEvent();

        Assert.Equal("", evt.DateIso);
        Assert.Equal("", evt.DisplayDate);
        Assert.Null(evt.Time);
        Assert.Equal("", evt.EventDescription);
        Assert.Equal("", evt.SourceFile);
        Assert.Equal(0, evt.PageNumber);
        Assert.Equal("Medium", evt.Confidence);
    }

    [Fact]
    public void TimelineEvent_AllPropertiesAssignable()
    {
        var evt = new TimelineEvent
        {
            DateIso = "2023-03-12",
            DisplayDate = "12.03.2023.",
            Time = "14:30",
            EventDescription = "Potpisan ugovor o kupoprodaji nekretnine",
            SourceFile = "kupoprodajni_ugovor.pdf",
            PageNumber = 3,
            Confidence = "High"
        };

        Assert.Equal("2023-03-12", evt.DateIso);
        Assert.Equal("12.03.2023.", evt.DisplayDate);
        Assert.Equal("14:30", evt.Time);
        Assert.Equal("Potpisan ugovor o kupoprodaji nekretnine", evt.EventDescription);
        Assert.Equal("kupoprodajni_ugovor.pdf", evt.SourceFile);
        Assert.Equal(3, evt.PageNumber);
        Assert.Equal("High", evt.Confidence);
    }

    [Fact]
    public void TimelineEvent_NullTime_DefaultsToNull()
    {
        var evt = new TimelineEvent { DateIso = "2024-01-15" };
        Assert.Null(evt.Time);
    }

    // ══════════════════════════════════════════════════════════
    //  § 2 — TimelineEvent Serialization (snake_case)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void TimelineEvent_Serialization_UsesSnakeCase()
    {
        var evt = new TimelineEvent
        {
            DateIso = "2023-06-15",
            DisplayDate = "15.06.2023.",
            Time = "10:00",
            EventDescription = "Zaključena javnobilježnička ovjera",
            SourceFile = "ovjera.pdf",
            PageNumber = 1,
            Confidence = "High"
        };

        var json = JsonSerializer.Serialize(evt, JsonOptions);

        Assert.Contains("\"date_iso\"", json);
        Assert.Contains("\"display_date\"", json);
        Assert.Contains("\"time\"", json);
        Assert.Contains("\"event_description\"", json);
        Assert.Contains("\"source_file\"", json);
        Assert.Contains("\"page_number\"", json);
        Assert.Contains("\"confidence\"", json);
    }

    [Fact]
    public void TimelineEvent_Deserialization_CorrectValues()
    {
        var json = """
        {
            "date_iso": "2024-02-20",
            "display_date": "20.02.2024.",
            "time": "09:15",
            "event_description": "Podnesen prijedlog za upis",
            "source_file": "prijedlog.pdf",
            "page_number": 2,
            "confidence": "Low"
        }
        """;

        var evt = JsonSerializer.Deserialize<TimelineEvent>(json, JsonOptions)!;

        Assert.Equal("2024-02-20", evt.DateIso);
        Assert.Equal("20.02.2024.", evt.DisplayDate);
        Assert.Equal("09:15", evt.Time);
        Assert.Equal("Podnesen prijedlog za upis", evt.EventDescription);
        Assert.Equal("prijedlog.pdf", evt.SourceFile);
        Assert.Equal(2, evt.PageNumber);
        Assert.Equal("Low", evt.Confidence);
    }

    [Fact]
    public void TimelineEvent_NullTime_SerializesCorrectly()
    {
        var evt = new TimelineEvent { DateIso = "2023-01-01", Time = null };
        var json = JsonSerializer.Serialize(evt, JsonOptions);
        // null Time should either be omitted or be null
        Assert.DoesNotContain("\"time\":\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  § 3 — ChronologyResult Model
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ChronologyResult_DefaultValues_AreCorrect()
    {
        var result = new ChronologyResult();

        Assert.NotNull(result.Events);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.TotalDocumentsProcessed);
        Assert.Equal(0, result.TotalEventsExtracted);
        Assert.NotNull(result.Errors);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ChronologyResult_FullPopulation()
    {
        var result = new ChronologyResult
        {
            Events =
            [
                new TimelineEvent { DateIso = "2023-01-01", EventDescription = "Događaj 1" },
                new TimelineEvent { DateIso = "2023-06-15", EventDescription = "Događaj 2" },
                new TimelineEvent { DateIso = "2024-03-20", EventDescription = "Događaj 3" }
            ],
            TotalDocumentsProcessed = 5,
            TotalEventsExtracted = 3,
            Errors = ["dokument1.pdf: Nema teksta"]
        };

        Assert.Equal(3, result.Events.Count);
        Assert.Equal(5, result.TotalDocumentsProcessed);
        Assert.Equal(3, result.TotalEventsExtracted);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ChronologyResult_Serialization_SnakeCase()
    {
        var result = new ChronologyResult
        {
            TotalDocumentsProcessed = 3,
            TotalEventsExtracted = 12
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Contains("\"total_documents_processed\"", json);
        Assert.Contains("\"total_events_extracted\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  § 4 — Event Sorting Logic
    //  Controller sorts: DateIso ordinal → Time
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EventSorting_ByDateIso_Ascending()
    {
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2024-03-15", EventDescription = "Treći" },
            new() { DateIso = "2023-01-10", EventDescription = "Prvi" },
            new() { DateIso = "2023-07-22", EventDescription = "Drugi" }
        };

        // Replicate controller sorting logic:
        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal("2023-01-10", events[0].DateIso);
        Assert.Equal("2023-07-22", events[1].DateIso);
        Assert.Equal("2024-03-15", events[2].DateIso);
    }

    [Fact]
    public void EventSorting_SameDate_SortedByTime()
    {
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2023-05-01", Time = "16:00", EventDescription = "Popodne" },
            new() { DateIso = "2023-05-01", Time = "08:30", EventDescription = "Ujutro" },
            new() { DateIso = "2023-05-01", Time = "12:00", EventDescription = "Podne" }
        };

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal("08:30", events[0].Time);
        Assert.Equal("12:00", events[1].Time);
        Assert.Equal("16:00", events[2].Time);
    }

    [Fact]
    public void EventSorting_NullTime_ComesBeforeNonNull()
    {
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2023-05-01", Time = "10:00", EventDescription = "Sa vremenom" },
            new() { DateIso = "2023-05-01", Time = null, EventDescription = "Bez vremena" }
        };

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        // "" < "10:00" → null time event first
        Assert.Equal("Bez vremena", events[0].EventDescription);
        Assert.Equal("Sa vremenom", events[1].EventDescription);
    }

    [Fact]
    public void EventSorting_MixedDatesAndTimes_ComplexScenario()
    {
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2024-01-15", Time = "14:00", EventDescription = "D" },
            new() { DateIso = "2023-03-01", Time = null, EventDescription = "A" },
            new() { DateIso = "2024-01-15", Time = "09:00", EventDescription = "C" },
            new() { DateIso = "2023-09-20", Time = "11:30", EventDescription = "B" },
            new() { DateIso = "2024-01-15", Time = null, EventDescription = "C-rano" }
        };

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal("A", events[0].EventDescription);      // 2023-03-01
        Assert.Equal("B", events[1].EventDescription);      // 2023-09-20
        Assert.Equal("C-rano", events[2].EventDescription); // 2024-01-15, null time (="")
        Assert.Equal("C", events[3].EventDescription);      // 2024-01-15, 09:00
        Assert.Equal("D", events[4].EventDescription);      // 2024-01-15, 14:00
    }

    [Fact]
    public void EventSorting_EmptyList_NoException()
    {
        var events = new List<TimelineEvent>();

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Empty(events);
    }

    [Fact]
    public void EventSorting_SingleEvent_NoException()
    {
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2024-06-01", EventDescription = "Jedini događaj" }
        };

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Single(events);
        Assert.Equal("Jedini događaj", events[0].EventDescription);
    }

    // ══════════════════════════════════════════════════════════
    //  § 5 — Date Range Calculation (from controller)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void DateRange_MultipleEvents_FirstToLast()
    {
        var sorted = new List<TimelineEvent>
        {
            new() { DateIso = "2022-01-01", DisplayDate = "01.01.2022." },
            new() { DateIso = "2023-06-15", DisplayDate = "15.06.2023." },
            new() { DateIso = "2024-12-31", DisplayDate = "31.12.2024." }
        };

        var first = sorted.First().DisplayDate;
        var last = sorted.Last().DisplayDate;
        string dateRange = first == last ? first : $"{first} — {last}";

        Assert.Equal("01.01.2022. — 31.12.2024.", dateRange);
    }

    [Fact]
    public void DateRange_SingleEvent_NoDash()
    {
        var sorted = new List<TimelineEvent>
        {
            new() { DateIso = "2023-05-15", DisplayDate = "15.05.2023." }
        };

        var first = sorted.First().DisplayDate;
        var last = sorted.Last().DisplayDate;
        string dateRange = first == last ? first : $"{first} — {last}";

        Assert.Equal("15.05.2023.", dateRange);
    }

    [Fact]
    public void DateRange_EmptyEvents_EmptyString()
    {
        var sorted = new List<TimelineEvent>();
        string dateRange = "";
        if (sorted.Count > 0)
        {
            var first = sorted.First().DisplayDate;
            var last = sorted.Last().DisplayDate;
            dateRange = first == last ? first : $"{first} — {last}";
        }

        Assert.Equal("", dateRange);
    }

    // ══════════════════════════════════════════════════════════
    //  § 6 — Confidence Levels
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("High")]
    [InlineData("Medium")]
    [InlineData("Low")]
    public void TimelineEvent_ValidConfidenceLevels(string confidence)
    {
        var evt = new TimelineEvent { Confidence = confidence };
        Assert.Equal(confidence, evt.Confidence);
    }

    [Fact]
    public void TimelineEvent_DefaultConfidence_IsMedium()
    {
        var evt = new TimelineEvent();
        Assert.Equal("Medium", evt.Confidence);
    }

    // ══════════════════════════════════════════════════════════
    //  § 7 — Realistic Legal Chronology Scenarios
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_RealEstateTransaction_FullTimeline()
    {
        // Simulate a typical Croatian real estate transaction timeline
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2023-01-15", DisplayDate = "15.01.2023.", Time = "10:00",
                EventDescription = "Potpisan predugovor o kupoprodaji stana u Zagrebu",
                SourceFile = "predugovor.pdf", PageNumber = 1, Confidence = "High" },

            new() { DateIso = "2023-01-15", DisplayDate = "15.01.2023.", Time = "10:30",
                EventDescription = "Plaćena kapara u iznosu od 10% kupoprodajne cijene",
                SourceFile = "predugovor.pdf", PageNumber = 4, Confidence = "High" },

            new() { DateIso = "2023-02-01", DisplayDate = "01.02.2023.", Time = null,
                EventDescription = "Pribavljen ZK izvadak — nekretnina bez tereta",
                SourceFile = "zk_izvadak.pdf", PageNumber = 1, Confidence = "High" },

            new() { DateIso = "2023-02-15", DisplayDate = "15.02.2023.", Time = "14:00",
                EventDescription = "Potpisan glavni kupoprodajni ugovor kod javnog bilježnika",
                SourceFile = "ugovor.pdf", PageNumber = 1, Confidence = "High" },

            new() { DateIso = "2023-02-15", DisplayDate = "15.02.2023.", Time = "14:30",
                EventDescription = "Javnobilježnička ovjera clausulae intabulandi",
                SourceFile = "ugovor.pdf", PageNumber = 8, Confidence = "High" },

            new() { DateIso = "2023-02-20", DisplayDate = "20.02.2023.", Time = null,
                EventDescription = "Podnesen prijedlog za uknjižbu prava vlasništva",
                SourceFile = "prijedlog_upis.pdf", PageNumber = 1, Confidence = "Medium" },

            new() { DateIso = "2023-03-01", DisplayDate = "01.03.2023.", Time = null,
                EventDescription = "Predaja nekretnine u posjed kupcu — primopredajni zapisnik",
                SourceFile = "zapisnik.pdf", PageNumber = 1, Confidence = "High" },

            new() { DateIso = "2023-03-15", DisplayDate = "15.03.2023.", Time = null,
                EventDescription = "Uknjižba prava vlasništva u zemljišnu knjigu — Z-12345/2023",
                SourceFile = "rjesenje_zk.pdf", PageNumber = 1, Confidence = "High" }
        };

        // Sort chronologically
        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal(8, events.Count);
        Assert.Equal("2023-01-15", events[0].DateIso);
        Assert.Equal("10:00", events[0].Time);
        Assert.Equal("2023-03-15", events[7].DateIso);

        // Events on same day 2023-01-15 sorted by time
        Assert.Equal("10:00", events[0].Time);
        Assert.Equal("10:30", events[1].Time);

        // Events on same day 2023-02-15 sorted by time
        Assert.Equal("14:00", events[3].Time);
        Assert.Equal("14:30", events[4].Time);

        // Date range
        var first = events.First().DisplayDate;
        var last = events.Last().DisplayDate;
        string dateRange = $"{first} — {last}";
        Assert.Equal("15.01.2023. — 15.03.2023.", dateRange);
    }

    [Fact]
    public void Scenario_CourtProceedings_WithInferredDates()
    {
        // A court case timeline with mixed confidence
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "2022-06-01", DisplayDate = "01.06.2022.",
                EventDescription = "Podnesena tužba radi raskida ugovora",
                SourceFile = "tuzba.pdf", Confidence = "High" },

            new() { DateIso = "2022-09-15", DisplayDate = "15.09.2022.",
                EventDescription = "Održano pripremno ročište",
                SourceFile = "zapisnik_rociste1.pdf", Confidence = "High" },

            new() { DateIso = "2022-11-01", DisplayDate = "studeni 2022.",
                EventDescription = "Izvedeni dokazi — svjedoci saslusani",
                SourceFile = "zapisnik_rociste2.pdf", Confidence = "Medium" },

            new() { DateIso = "2023-02-01", DisplayDate = "početak 2023.",
                EventDescription = "Vještačenje nekretnine",
                SourceFile = "vjestacenje.pdf", Confidence = "Low" },

            new() { DateIso = "2023-06-20", DisplayDate = "20.06.2023.",
                EventDescription = "Presuda — tužbeni zahtjev usvojen",
                SourceFile = "presuda.pdf", Confidence = "High" }
        };

        // Sort
        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal(5, events.Count);
        Assert.Equal("2022-06-01", events[0].DateIso);
        Assert.Equal("2023-06-20", events[4].DateIso);

        // Verify mixed confidence
        Assert.Equal("High", events[0].Confidence);
        Assert.Equal("Medium", events[2].Confidence);
        Assert.Equal("Low", events[3].Confidence);
    }

    [Fact]
    public void Scenario_MultiDocument_MergedTimeline()
    {
        // Events from 3 different source files, merged and sorted
        var fromUgovor = new List<TimelineEvent>
        {
            new() { DateIso = "2023-03-01", EventDescription = "Ugovor potpisan",
                SourceFile = "ugovor.pdf", Confidence = "High" },
            new() { DateIso = "2023-04-15", EventDescription = "Rok za plaćanje istekao",
                SourceFile = "ugovor.pdf", Confidence = "High" }
        };

        var fromZk = new List<TimelineEvent>
        {
            new() { DateIso = "2023-02-20", EventDescription = "ZK izvadak pribavljen",
                SourceFile = "zk_izvadak.pdf", Confidence = "High" },
            new() { DateIso = "2023-05-10", EventDescription = "Upis vlasništva izvršen",
                SourceFile = "zk_izvadak.pdf", Confidence = "High" }
        };

        var fromDopis = new List<TimelineEvent>
        {
            new() { DateIso = "2023-03-15", EventDescription = "Opomena poslana prodavatelju",
                SourceFile = "dopis.pdf", Confidence = "Medium" }
        };

        // Merge all events (as controller does)
        var allEvents = new List<TimelineEvent>();
        allEvents.AddRange(fromUgovor);
        allEvents.AddRange(fromZk);
        allEvents.AddRange(fromDopis);

        // Sort
        allEvents.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal(5, allEvents.Count);
        Assert.Equal("zk_izvadak.pdf", allEvents[0].SourceFile); // 2023-02-20
        Assert.Equal("ugovor.pdf", allEvents[1].SourceFile);     // 2023-03-01
        Assert.Equal("dopis.pdf", allEvents[2].SourceFile);      // 2023-03-15
        Assert.Equal("ugovor.pdf", allEvents[3].SourceFile);     // 2023-04-15
        Assert.Equal("zk_izvadak.pdf", allEvents[4].SourceFile); // 2023-05-10
    }

    // ══════════════════════════════════════════════════════════
    //  § 8 — Edge Cases: Unknown/Sentinel Dates
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EventSorting_SentinelDate_9999_GoesToEnd()
    {
        // Service uses "9999-99-99" as fallback when no date found
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "9999-99-99", EventDescription = "Nepoznat datum" },
            new() { DateIso = "2023-05-01", EventDescription = "Poznati datum" }
        };

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        Assert.Equal("Poznati datum", events[0].EventDescription);
        Assert.Equal("Nepoznat datum", events[1].EventDescription);
    }

    [Fact]
    public void EventSorting_AllSentinelDates_StableSort()
    {
        var events = new List<TimelineEvent>
        {
            new() { DateIso = "9999-99-99", EventDescription = "A" },
            new() { DateIso = "9999-99-99", EventDescription = "B" },
            new() { DateIso = "9999-99-99", Time = "10:00", EventDescription = "C" }
        };

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        // "" < "" stays stable, "10:00" goes last among same-date
        Assert.Equal("C", events[2].EventDescription);
    }

    // ══════════════════════════════════════════════════════════
    //  § 9 — ChronologyResult with Errors
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ChronologyResult_WithErrors_PreservesAllData()
    {
        var result = new ChronologyResult
        {
            Events = [new TimelineEvent { DateIso = "2023-01-01", EventDescription = "OK" }],
            TotalDocumentsProcessed = 3,
            TotalEventsExtracted = 1,
            Errors =
            [
                "dokument1.pdf: Nije moguće pročitati PDF",
                "dokument2.pdf: Nema teksta (OCR?)"
            ]
        };

        Assert.Single(result.Events);
        Assert.Equal(3, result.TotalDocumentsProcessed);
        Assert.Equal(1, result.TotalEventsExtracted);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("OCR", result.Errors[1]);
    }

    [Fact]
    public void ChronologyResult_Serialization_Roundtrip()
    {
        var original = new ChronologyResult
        {
            Events =
            [
                new TimelineEvent
                {
                    DateIso = "2023-07-04",
                    DisplayDate = "04.07.2023.",
                    Time = "11:00",
                    EventDescription = "Ročište",
                    SourceFile = "zapisnik.pdf",
                    PageNumber = 1,
                    Confidence = "High"
                }
            ],
            TotalDocumentsProcessed = 1,
            TotalEventsExtracted = 1,
            Errors = []
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ChronologyResult>(json, JsonOptions)!;

        Assert.Single(deserialized.Events);
        Assert.Equal("2023-07-04", deserialized.Events[0].DateIso);
        Assert.Equal("04.07.2023.", deserialized.Events[0].DisplayDate);
        Assert.Equal("11:00", deserialized.Events[0].Time);
        Assert.Equal("Ročište", deserialized.Events[0].EventDescription);
        Assert.Equal("zapisnik.pdf", deserialized.Events[0].SourceFile);
        Assert.Equal(1, deserialized.Events[0].PageNumber);
        Assert.Equal("High", deserialized.Events[0].Confidence);
        Assert.Equal(1, deserialized.TotalDocumentsProcessed);
        Assert.Empty(deserialized.Errors);
    }

    // ══════════════════════════════════════════════════════════
    //  § 10 — Large Timeline Sorting (Performance)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EventSorting_LargeTimeline_100Events()
    {
        var rng = new Random(42); // deterministic seed
        var events = Enumerable.Range(0, 100).Select(i =>
        {
            var date = new DateOnly(2020 + rng.Next(5), rng.Next(1, 13), rng.Next(1, 28));
            return new TimelineEvent
            {
                DateIso = date.ToString("yyyy-MM-dd"),
                DisplayDate = date.ToString("dd.MM.yyyy."),
                Time = rng.Next(2) == 0 ? null : $"{rng.Next(24):D2}:{rng.Next(60):D2}",
                EventDescription = $"Događaj {i}",
                SourceFile = $"doc_{i % 10}.pdf"
            };
        }).ToList();

        events.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        // Verify ascending order
        for (int i = 1; i < events.Count; i++)
        {
            int cmp = string.Compare(events[i - 1].DateIso, events[i].DateIso, StringComparison.Ordinal);
            if (cmp == 0)
                cmp = string.Compare(events[i - 1].Time ?? "", events[i].Time ?? "", StringComparison.Ordinal);
            Assert.True(cmp <= 0, $"Events at index {i - 1} and {i} are out of order");
        }
    }
}
