using System.Globalization;

namespace SBSBeautySpa.Mobile.Converters
{
    // CONVERTER 1: BoolToColorConverter
    //
    // Takes a true/false value and returns one of two colors.
    // Used to change how a selected/unselected item looks.
    //
    // How to use it in XAML:
    //   BackgroundColor="{Binding IsSelected, Converter={StaticResource BoolToColor}, ConverterParameter='Navy|White'}"
    //
    // The parameter is "TrueColor|FalseColor" — separated by a pipe (|).
    // You can use hex codes ("#1B2A4A") or common names (White, Black, Transparent).
    //
    // Used on SelectDateTimeView for time slots, and intended for other
    // selectable grids too.
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Is the value true?
            var isTrue = value is bool b && b;

            // Read the two colors from the parameter (split by |).
            // Default is "Black|Transparent" if no parameter is given.
            var parts = (parameter as string ?? "Black|Transparent").Split('|');

            // Pick the first color if true, second if false.
            var chosen = isTrue ? parts[0] : parts[1];

            // Convert the color text into an actual Color object.
            return ParseColor(chosen);
        }

        // This converter is one-way only (UI → ViewModel is not supported).
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        // Turns a color name or hex code into a Color object.
        private static Color ParseColor(string value) => value.Trim().ToLowerInvariant() switch
        {
            "white" => Colors.White,
            "black" => Colors.Black,
            "transparent" => Colors.Transparent,
            _ => Color.FromArgb(value.Trim()),  // Anything else is treated as hex
        };
    }

    // CONVERTER 2: InvertedBoolConverter
    //
    // Takes a true/false value and flips it.
    // true → false, false → true
    //
    // How to use it in XAML:
    //   IsVisible="{Binding IsBusy, Converter={StaticResource InvertedBool}}"
    //
    // Example: Show the list when loading is DONE (not busy).
    public class InvertedBoolConverter : IValueConverter
    {
        // Convert: flip the bool when displaying.
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            !(value is bool b && b);

        // ConvertBack: flip the bool when saving.
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            !(value is bool b && b);
    }

    // CONVERTER 3: IsNotNullConverter
    //
    // Returns true if the value exists (is not null).
    // Returns false if the value is null.
    //
    // How to use it in XAML:
    //   IsVisible="{Binding ErrorMessage, Converter={StaticResource IsNotNull}}"
    //
    // Example: Show the error label only when there's actually an error.
    public class IsNotNullConverter : IValueConverter
    {
        // Convert: is the value not null?
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is not null;

        // This converter is one-way only (UI → ViewModel is not supported).
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}