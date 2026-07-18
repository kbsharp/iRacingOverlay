using IRacingOverlay.App.Cars;
using IRacingOverlay.Core.Cars;
using Geometry = System.Windows.Media.Geometry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presentation for a car's manufacturer badge in the standings.
///
/// Most makes render as a monochrome vector mark (see <see cref="ManufacturerMarks"/>),
/// tinted by the caller so it sits in the panel material rather than adding a
/// colour. The handful with no CC0 artwork upstream (Dallara, Ligier, Mercedes,
/// Radical, Ruf) fall back to the short brand abbreviation below, so those rows
/// still identify the car instead of showing an empty cell.
/// </summary>
internal static class ManufacturerBadge
{
    public static bool Has(Manufacturer make) => make != Manufacturer.Unknown;

    /// <summary>The vector mark for a make, or null when it falls back to text.</summary>
    public static Geometry? Mark(Manufacturer make) => ManufacturerMarks.For(make);

    /// <summary>
    /// Short brand token, shown when a make has no vector mark. Also the natural
    /// accessible/tooltip label for the marks that do render.
    /// </summary>
    public static string Abbrev(Manufacturer make) => make switch
    {
        Manufacturer.Acura => "ACU",
        Manufacturer.AstonMartin => "AM",
        Manufacturer.Audi => "AUDI",
        Manufacturer.Bmw => "BMW",
        Manufacturer.Cadillac => "CAD",
        Manufacturer.Chevrolet => "CHV",
        Manufacturer.Dallara => "DAL",
        Manufacturer.Ferrari => "FER",
        Manufacturer.Ford => "FORD",
        Manufacturer.Honda => "HON",
        Manufacturer.Hyundai => "HYU",
        Manufacturer.Lamborghini => "LAM",
        Manufacturer.Lexus => "LEX",
        Manufacturer.Ligier => "LIG",
        Manufacturer.Mazda => "MAZ",
        Manufacturer.McLaren => "MCL",
        Manufacturer.Mercedes => "MER",
        Manufacturer.Nissan => "NIS",
        Manufacturer.Porsche => "POR",
        Manufacturer.Radical => "RAD",
        Manufacturer.Renault => "REN",
        Manufacturer.Ruf => "RUF",
        Manufacturer.Subaru => "SUB",
        Manufacturer.Toyota => "TOY",
        Manufacturer.Volkswagen => "VW",
        _ => string.Empty,
    };
}
