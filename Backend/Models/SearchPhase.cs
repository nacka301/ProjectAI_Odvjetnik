using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Models;

// ══════════════════════════════════════════════════════════
//  ENUMS & MODELS
// ══════════════════════════════════════════════════════════

public enum SearchPhase
{
    Primary = 0,
    MultiPortal = 1,
    WiderRegion = 2,
    SynonymBlitz = 3,
    SubLocationDrill = 4,
    RegionalFallback = 5,
    NoComparables = 6,
}
