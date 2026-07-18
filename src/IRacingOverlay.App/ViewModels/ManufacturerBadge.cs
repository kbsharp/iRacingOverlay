using IRacingOverlay.Core.Cars;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presentation for a car's manufacturer badge in the standings.
///
/// PLACEHOLDER STAGE: until the real monochrome manufacturer marks are sourced,
/// the badge renders a short brand abbreviation in a tinted chip. That is enough
/// to prove the column's layout, tinting and UI-scale behaviour. When the vector
/// marks land, the badge's content switches from this abbreviation to an Image;
/// this map stays useful as the accessible/tooltip label and as the fallback for
/// any manufacturer without artwork yet.
/// </summary>
internal static class ManufacturerBadge
{
    public static bool Has(Manufacturer make) => make != Manufacturer.Unknown;

    /// <summary>Short, glanceable brand token (2-4 chars) for the placeholder chip.</summary>
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
