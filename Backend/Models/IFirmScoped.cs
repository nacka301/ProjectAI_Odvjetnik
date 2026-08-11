namespace Backend.Models;

/// <summary>
/// Implemented by every record that gets persisted into a firm-scoped table.
///
/// Since migration 016 those tables carry
/// <c>WITH CHECK (law_firm_id = public.user_firm_id())</c>, so an INSERT without
/// <see cref="LawFirmId"/> is rejected by Postgres rather than silently stored
/// under the wrong owner. Making it an interface means the requirement shows up
/// in the type — a new record type that forgets it will not compile against
/// <c>ControllerBaseExtensions.ScopeToFirm</c>.
///
/// <see cref="LawFirmId"/> is nullable only so that deserialisation of an
/// incoming request body does not have to supply it. It is always overwritten
/// server-side from the verified token; a value sent by the client is ignored.
/// </summary>
public interface IFirmScoped
{
    string? LawFirmId { get; set; }
}
