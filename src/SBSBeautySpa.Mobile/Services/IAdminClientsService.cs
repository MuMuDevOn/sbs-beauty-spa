namespace SBSBeautySpa.Mobile.Services
{
    public record ClientSummary(
        string Uid,
        string Name,
        string Email,
        string PhoneNumber,
        bool IsActive,
        string CreatedAtIso
    );

    public record ClientNoteEntry(string Id, string Note, string? CreatedAtIso);

    public record ClientDetail(
        ClientSummary Client,
        List<Models.Booking> Bookings,
        List<ClientNoteEntry> Notes
    );

    public interface IAdminClientsService
    {
        Task<List<ClientSummary>> ListClientsAsync(string? search = null, CancellationToken ct = default);
        Task<ClientDetail> GetClientDetailsAsync(string clientId, CancellationToken ct = default);
        Task SetClientDisabledAsync(string clientId, bool disabled, CancellationToken ct = default);
    }
}