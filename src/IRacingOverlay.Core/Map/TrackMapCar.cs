namespace IRacingOverlay.Core.Map;

/// <summary>
/// One car placed on the track map: where it is round the lap and the little
/// that has to be known to draw it. Position on screen is left to the widget,
/// which owns the canvas size - <see cref="TrackOutline.At"/> turns
/// <see cref="LapDistPct"/> into a point.
/// </summary>
/// <param name="ClassColorHex">iRacing's own class colour, normalised - the same
/// hue the standings and relative already use for this car.</param>
/// <param name="InPits">True while the car is in the pit lane. Its lap fraction
/// still tracks along the lane, so it is drawn on the racing line: the map says
/// where round the circuit a car is, and for a car in the lane that is a rougher
/// statement than for one on track.</param>
public readonly record struct TrackMapCar(
    int CarIdx,
    double LapDistPct,
    string CarNumber,
    string? ClassColorHex,
    bool IsPlayer,
    bool InPits);
