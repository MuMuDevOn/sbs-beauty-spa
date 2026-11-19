using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    // One client's page for admins: their details + private staff notes.
    // Uses the existing IClientProfileService (no new service).
    public partial class AdminClientDetailViewModel : BaseViewModel
    {
        private readonly IClientProfileService _profiles;

        public AdminClientDetailViewModel(IClientProfileService profiles)
        {
            _profiles = profiles;
            Title = "Client";
        }

        public ObservableCollection<ClientNote> Notes { get; } = new();

        [ObservableProperty]
        private ClientSummary? client;

        [ObservableProperty]
        private string newNote = string.Empty;

        [ObservableProperty]
        private string? infoMessage;

        [ObservableProperty]
        private bool isEmpty = true;

        // Called by the page when the admin taps a client in the list.
        public void SetClient(ClientSummary summary)
        {
            Client = summary;
            Notes.Clear();
            IsEmpty = true;
            InfoMessage = null;
            ErrorMessage = null;
            NewNote = string.Empty;
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || Client is null) return;

            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await FetchNotesAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load notes: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddNoteAsync()
        {
            if (IsBusy || Client is null) return;

            var text = NewNote?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                ErrorMessage = "Type a note first.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            try
            {
                await _profiles.AddClientNoteAsync(Client.Uid, text);
                NewNote = string.Empty;
                await FetchNotesAsync();
                InfoMessage = "Note saved.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't save the note: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Loads the notes (newest first). No busy check, so callers can use it freely.
        private async Task FetchNotesAsync()
        {
            if (Client is null) return;

            var notes = await _profiles.ListClientNotesAsync(Client.Uid);

            Notes.Clear();
            foreach (var n in notes.OrderByDescending(n => n.CreatedAt ?? DateTime.MinValue))
                Notes.Add(n);

            IsEmpty = Notes.Count == 0;
        }
    }
}
