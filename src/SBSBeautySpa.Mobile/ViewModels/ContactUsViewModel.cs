using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class ContactUsViewModel : BaseViewModel
    {
        private readonly IContactMessagesService _messages;

        public ContactUsViewModel(IContactMessagesService messages)
        {
            _messages = messages;
            Title = "Contact Us";
        }

        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string email = string.Empty;
        [ObservableProperty] private string? phone;
        [ObservableProperty] private string? subject;
        [ObservableProperty] private string message = string.Empty;
        [ObservableProperty] private bool messageSent;

        [RelayCommand]
        private async Task SendAsync()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your name and email.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Message))
            {
                ErrorMessage = "Please describe how we can help.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _messages.SubmitContactMessageAsync(Name.Trim(), Email.Trim(), Phone, Subject, Message.Trim());
                MessageSent = true;

                // Clear the form
                Name = string.Empty;
                Email = string.Empty;
                Phone = null;
                Subject = null;
                Message = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't send your message. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}