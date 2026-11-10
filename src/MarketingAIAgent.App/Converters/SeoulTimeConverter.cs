using System.Globalization;
using System.Windows.Data;
namespace MarketingAIAgent.App.Converters;
public sealed class SeoulTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is DateTime utc
        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul")).ToString("yyyy-MM-dd HH:mm:ss 'KST'", CultureInfo.InvariantCulture)
        : "—";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
