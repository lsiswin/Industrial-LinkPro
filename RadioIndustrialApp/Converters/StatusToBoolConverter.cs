using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RadioIndustrialApp.Converters;

public class StatusToBoolConverter:IValueConverter
{
    /// <summary>
    /// 将绑定值与参数进行比较，相等则返回 true
    /// </summary>
    /// <param name="value">绑定的 Status 字符串</param>
    /// <param name="targetType">目标类型 (bool)</param>
    /// <param name="parameter">在 XAML 中传入的对比值，如 "ERROR"</param>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // 比较绑定的状态字符串与参数是否一致（忽略大小写）
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}