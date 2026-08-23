namespace Backend.Tests;

/// <summary>
/// Serialises the test classes that set the ADMIN_EMAIL environment variable.
///
/// AdminIdentity reads ADMIN_EMAIL from the process environment, which is global
/// state — and xUnit runs test classes in parallel by default. Two classes each
/// setting it to their own address is a race: whichever assigns last decides who
/// counts as the admin for both, and the loser fails intermittently on a
/// perfectly good middleware.
///
/// Any future test class that pokes at ADMIN_EMAIL belongs in this collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AdminEmailEnvCollection
{
    public const string Name = "ADMIN_EMAIL environment";
}
