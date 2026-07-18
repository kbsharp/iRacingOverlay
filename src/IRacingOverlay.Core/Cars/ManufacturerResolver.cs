namespace IRacingOverlay.Core.Cars;

/// <summary>
/// Derives a car's <see cref="Manufacturer"/> from iRacing's roster strings.
/// The sim gives us no manufacturer field - only a <c>CarPath</c> folder token
/// ("ferrari296gt3", "porsche992rgt3") and a human <c>CarScreenName</c>
/// ("Ferrari 296 GT3") - so we brand-match on distinctive substrings of those.
///
/// The table is intentionally not exhaustive: iRacing adds cars every season,
/// and an unrecognised car resolves to <see cref="Manufacturer.Unknown"/> so the
/// badge column just omits it rather than guessing. Add a token here when a new
/// car ships. Tokens are matched case-insensitively; the first hit wins, so keep
/// each token distinctive enough that it can't appear in another brand's path.
/// </summary>
public static class ManufacturerResolver
{
    // Ordered brand tokens. Brand substrings don't overlap between makes, so
    // first-match-wins is safe regardless of order; a make with several model
    // aliases (Chevrolet, Lamborghini, Mazda) just lists each.
    private static readonly (string Token, Manufacturer Make)[] Rules =
    [
        ("acura", Manufacturer.Acura),
        ("astonmartin", Manufacturer.AstonMartin),
        ("cadillac", Manufacturer.Cadillac),
        ("chevrolet", Manufacturer.Chevrolet),
        ("chevy", Manufacturer.Chevrolet),
        ("corvette", Manufacturer.Chevrolet),
        ("vette", Manufacturer.Chevrolet),
        ("camaro", Manufacturer.Chevrolet),
        ("lamborghini", Manufacturer.Lamborghini),
        ("huracan", Manufacturer.Lamborghini),
        ("mclaren", Manufacturer.McLaren),
        ("mercedes", Manufacturer.Mercedes),
        ("porsche", Manufacturer.Porsche),
        ("ferrari", Manufacturer.Ferrari),
        ("lexus", Manufacturer.Lexus),
        ("ligier", Manufacturer.Ligier),
        ("hyundai", Manufacturer.Hyundai),
        ("dallara", Manufacturer.Dallara),
        ("radical", Manufacturer.Radical),
        ("renault", Manufacturer.Renault),
        ("subaru", Manufacturer.Subaru),
        ("nissan", Manufacturer.Nissan),
        ("toyota", Manufacturer.Toyota),
        ("volkswagen", Manufacturer.Volkswagen),
        ("audi", Manufacturer.Audi),
        ("bmw", Manufacturer.Bmw),
        ("ford", Manufacturer.Ford),
        ("honda", Manufacturer.Honda),
        ("mazda", Manufacturer.Mazda),
        ("mx-5", Manufacturer.Mazda),
        ("mx5", Manufacturer.Mazda),
        ("ruf", Manufacturer.Ruf),
    ];

    /// <summary>
    /// Resolves the manufacturer from the roster's <paramref name="carPath"/>,
    /// falling back to <paramref name="carScreenName"/> if the path yields
    /// nothing. Returns <see cref="Manufacturer.Unknown"/> when neither matches.
    /// </summary>
    public static Manufacturer Resolve(string? carPath, string? carScreenName = null)
    {
        return Match(carPath) is { } fromPath and not Manufacturer.Unknown
            ? fromPath
            : Match(carScreenName);
    }

    private static Manufacturer Match(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Manufacturer.Unknown;
        }

        foreach (var (token, make) in Rules)
        {
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return make;
            }
        }

        return Manufacturer.Unknown;
    }
}
