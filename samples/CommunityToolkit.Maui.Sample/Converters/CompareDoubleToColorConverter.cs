using CommunityToolkit.Maui.Converters;

namespace CommunityToolkit.Maui.Sample.Converters;

/// <summary>
/// Compares a double value against the ComparingValue property
/// and returns a <see cref="Color"/> based on the comparison.
/// </summary>
[AcceptEmptyServiceProvider]
public sealed partial class CompareDoubleToColorConverter : CompareConverter<double, Color>
{
}