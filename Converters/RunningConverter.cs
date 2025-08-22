using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;

namespace yoloNet.Converters
{
    public class RunningConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool bit)
            {
                return bit ? "\ue750" : "\ue74f";
            }
            else return "\ue74f";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RunningColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter == null && value is bool bit)
            {
                return bit ? "#578b1e" : "#ef5350";
            }
            else if (parameter != null && value is bool bit2)
            {
                return bit2 ? "#ef5350" : "#578b1e";
            }
            else return "#ef5350";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
