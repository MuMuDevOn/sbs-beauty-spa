using Microsoft.Maui.Handlers;
using Microsoft.Maui.Storage;

namespace SBSBeautySpa.Mobile;

// Windows only (this file lives in Platforms\Windows).
// On Windows the logo sometimes came out blank, so every <Image Source="logo.png">
// is loaded from a plain app file instead (Resources\Raw\sbs_logo.png).
// Register() is called from MauiProgram.CreateMauiApp, after Windows is ready.
internal static class WindowsLogoFix
{
    internal static void Register()
    {
        ImageHandler.Mapper.PrependToMapping("Source", (handler, view) =>
        {
            if (view is Microsoft.Maui.Controls.Image img
                && img.Source is FileImageSource fs
                && fs.File == "logo.png")
            {
                img.Source = ImageSource.FromStream(
                    ct => FileSystem.OpenAppPackageFileAsync("sbs_logo.png"));
            }
        });
    }
}
