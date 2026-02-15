// ═══════════════════════════════════════════
//  SHARED TYPES — Wizard & ResultView
// ═══════════════════════════════════════════

export type PropertyType = "Apartment" | "House" | "Land";
export type PropertyCondition = "New" | "Renovated" | "Good" | "Original" | "NeedsWork";
export type HouseFloors = "OneStory" | "TwoStory" | "MultiStory";
export type LandUse = "Construction" | "Agricultural" | "Mixed";
export type Orientation = "Unknown" | "North" | "NorthEast" | "East" | "SouthEast" | "South" | "SouthWest" | "West" | "NorthWest";
export type ViewType = "Unknown" | "Street" | "Courtyard" | "Park" | "City" | "Sea" | "Panoramic";
export type ViewState = "INPUT" | "PROCESSING" | "RESULT";
export type ResultTab = "overview" | "comparables";

export interface LocationInfo {
  city: string;
  district: string;
}

export interface ApartmentData {
  area_m2: number;
  room_count: number;
  floor: string;
  total_floors: number;
  condition: PropertyCondition;
  year_built?: number;
  distance_to_sea_m?: number;
  orientation: Orientation;
  view_type: ViewType;
  has_elevator: boolean;
  has_parking: boolean;
  has_balcony: boolean;
  sea_view: boolean;
}

export interface HouseData {
  living_area_m2: number;
  land_area_m2: number;
  floors: HouseFloors;
  condition: PropertyCondition;
  year_built?: number;
  distance_to_sea_m?: number;
  orientation: Orientation;
  view_type: ViewType;
  has_garage: boolean;
  has_pool: boolean;
  first_row_to_sea: boolean;
  has_auxiliary_object: boolean;
}

export interface LandData {
  land_area_m2: number;
  land_use: LandUse;
  distance_to_sea_m?: number;
  has_access_road: boolean;
  has_infrastructure: boolean;
  first_row_to_sea: boolean;
  is_legalized: boolean;
}

export interface ValidationError {
  field: string;
  message: string;
}

export interface DraftResponse {
  valid: boolean;
  errors?: ValidationError[];
  warnings?: ValidationError[];
  description: string;
  ready_for_valuation: boolean;
}

export interface StreamStage {
  id: string;
  label: string;
  status: "pending" | "active" | "done" | "skipped" | "error";
  detail?: string;
  error?: string;
}

export interface SelectedComparable {
  title: string;
  price: string;
  location: string;
  url: string;
  reason_for_selection: string;
}

export interface ValueDriver {
  factor: string;
  impact: string;
  adjustment_percent: number;
}

export interface UsedComparable {
  title: string;
  url: string;
  price_per_m2: number;
  similarity_score: number;
  is_outlier: boolean;
  outlier_reason?: string;
  total_price?: number;
  area_m2?: number;
  rooms?: number;
  floor?: string;
  year_built?: number;
  condition?: string;
  energy_class?: string;
  heating?: string;
  location?: string;
  has_garage?: boolean;
  has_parking?: boolean;
  // V29: Per-comparable correction
  correction_percent?: number;
  correction_reason?: string;
}

export interface ValuationAdjustment {
  factor: string;
  description: string;
  percent: number;
}

export interface ComparisonTableRow {
  index: number;
  title: string;
  url: string;
  area_m2: number;
  total_price: number;
  raw_price_per_m2: number;
  correction_percent: number;
  adjusted_price_per_m2: number;
  similarity_score: number;
  deep_dive_status?: string;
  correction_reason?: string;
}

export interface PriceRangeEstimate {
  conservative_price_per_m2: number;
  realistic_price_per_m2: number;
  optimistic_price_per_m2: number;
  conservative_total: number;
  realistic_total: number;
  optimistic_total: number;
}

export interface ValuationResult {
  target_property_summary: string;
  selected_comparables: SelectedComparable[];
  estimated_value_eur: number;
  value_drivers: ValueDriver[];
  valuation_reasoning: string;
  confidence_level?: string;
  calculated_price_per_m2?: number;
  after_discount_per_m2?: number;  // V27: After gap, before adjustments
  total_listings_found?: number;
  usable_price_points?: number;
  outliers_removed?: number;
  realization_discount_percent?: number;
  math_confidence_score?: number;
  math_confidence_level?: string;
  math_confidence_explanation?: string;
  market_average_asking?: number;
  std_dev_per_m2?: number;
  coefficient_of_variation?: number;
  total_adjustment_percent?: number;
  realization_gap_reasoning?: string;
  used_comparables?: UsedComparable[];
  adjustments?: ValuationAdjustment[];
  pipeline?: string[];
  outlier_log?: string[];
  outlier_method?: string;
  confidence_score?: number;
  confidence_explanation?: string;
  confidence_color?: string;
  price_source?: string;
  region_name?: string;
  note?: string;
  search_phase_used?: string;
  sherlock_insight?: string;
  comparison_table?: ComparisonTableRow[];
  price_range?: PriceRangeEstimate;
  benchmark_summary?: string;
}
