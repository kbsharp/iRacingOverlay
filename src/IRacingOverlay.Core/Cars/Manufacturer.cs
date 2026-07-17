namespace IRacingOverlay.Core.Cars;

/// <summary>
/// A car manufacturer we can show a badge for. iRacing exposes no manufacturer
/// field on the roster (only <c>CarPath</c>/<c>CarScreenName</c>), so this is
/// derived from those strings by <see cref="ManufacturerResolver"/>. The enum
/// name doubles as the stable key an asset lookup uses (e.g. a badge file), so
/// renaming a member is a breaking change for the badge set.
/// </summary>
public enum Manufacturer
{
    /// <summary>No manufacturer could be derived - the caller shows no badge.</summary>
    Unknown,
    Acura,
    AstonMartin,
    Audi,
    Bmw,
    Cadillac,
    Chevrolet,
    Dallara,
    Ferrari,
    Ford,
    Honda,
    Hyundai,
    Lamborghini,
    Lexus,
    Ligier,
    Mazda,
    McLaren,
    Mercedes,
    Nissan,
    Porsche,
    Radical,
    Renault,
    Ruf,
    Subaru,
    Toyota,
    Volkswagen,
}
