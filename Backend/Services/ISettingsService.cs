using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za SettingsService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ISettingsService
{
/// <summary>Fetch user profile by user_id. Returns null if not found or not configured.</summary>
    Task<ProfileResponse?> GetProfileAsync(string userId) ;

/// <summary>Update user profile fields (full_name, phone_number). Creates the row via upsert if missing.</summary>
    Task<ProfileResponse?> UpdateProfileAsync(ProfileUpdateRequest req) ;

/// <summary>Fetch the law firm for a given user (via user_profiles.law_firm_id).</summary>
    Task<LawFirmResponse?> GetFirmAsync(string userId) ;

/// <summary>Update or create the user's law firm and link it to their profile.</summary>
    Task<LawFirmResponse?> UpdateFirmAsync(LawFirmUpdateRequest req) ;

/// <summary>Fetch the subscription for a user's law firm.</summary>
    Task<SubscriptionResponse?> GetSubscriptionAsync(string userId) ;

/// <summary>
    /// Called after Supabase Auth signup: creates a law firm, links profile, and creates a default subscription.
    /// </summary>
    Task<LawFirmResponse?> RegisterFirmAsync(RegisterFirmRequest req) ;
}