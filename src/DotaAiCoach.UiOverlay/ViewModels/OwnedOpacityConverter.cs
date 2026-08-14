using System.Globalization;
using System.Windows.Data;

namespace DotaAiCoach.UiOverlay.ViewModels;

/// <summary>Dims an already-owned item's icon (see ItemPlanDisplayEntry.IsOwned) instead of hiding it, so the full planned build stays visible per explicit request.</summary>
public sealed class OwnedOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.45 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
