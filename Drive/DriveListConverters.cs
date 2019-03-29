using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Drive
{
    [ValueConversion(typeof(float), typeof(float))]
    public class DriveUsageToProgressBarValue : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Drive.Atonline.Rest.Drive Drive = value as Drive.Atonline.Rest.Drive;
            if (Drive == null) return 0f;
            return  Drive.Usage_Float * 100;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (float)value / 100;
        }
    }

    [ValueConversion(typeof(object), typeof(LinearGradientBrush))]
    public class DriveUsageToGradient : IValueConverter
    {
        private GradientStop[] getFullGrandientStops()
        {
            GradientStop[] gds = new GradientStop[2];
            gds[0] = new GradientStop();
            gds[0].Color = (Color) ColorConverter.ConvertFromString("#d5203d");
            gds[0].Offset = 0.0;
            gds[1] = new GradientStop();
            gds[1].Color = (Color)ColorConverter.ConvertFromString("#df8a51");
            gds[1].Offset = 1;

            return gds;
        }

        private GradientStop[] getAlmostGrandientStops()
        {
            GradientStop[] gds = new GradientStop[2];
            gds[0] = new GradientStop();
            gds[0].Color = (Color)ColorConverter.ConvertFromString("#fbdf6d");
            gds[0].Offset = 0.0;
            gds[1] = new GradientStop();
            gds[1].Color = (Color)ColorConverter.ConvertFromString("#ed8022");
            gds[1].Offset = 1;

            return gds;
        }

        private GradientStop[] getCompletionGrandientStops()
        {
            GradientStop[] gds = new GradientStop[2];
            gds[0] = new GradientStop();
            gds[0].Color = (Color)ColorConverter.ConvertFromString("#b993d6");
            gds[0].Offset = 0.0;
            gds[1] = new GradientStop();
            gds[1].Color = (Color)ColorConverter.ConvertFromString("#8ca6db");
            gds[1].Offset = 1;

            return gds;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Drive.Atonline.Rest.Drive Drive = value as Drive.Atonline.Rest.Drive;
            if (Drive == null) return null;
            LinearGradientBrush linearGradient = new LinearGradientBrush();
            linearGradient.StartPoint = new Point(0, 0);
            linearGradient.EndPoint = new Point(1, 0);

            GradientStop[] gds;
            if (Drive.Usage_Float < 0.75)
                gds = getCompletionGrandientStops();
            else if (Drive.Usage_Float >= 0.75 && Drive.Usage_Float < 1)
                gds = getAlmostGrandientStops();
            else gds = getFullGrandientStops();

            linearGradient.GradientStops.Add(gds[0]);
            linearGradient.GradientStops.Add(gds[1]);

            return linearGradient;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    [ValueConversion(typeof(string), typeof(string))]
    public class DriveUsageToText : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Drive.Atonline.Rest.Drive Drive = value as Drive.Atonline.Rest.Drive;

            if (Drive == null) return "Wut";
            if(Drive.Plan == "unlimited") return Drive.Root.Size_fmt;


            return $"{Drive.Root.Size_fmt} used on {Drive.Quota_fmt} available";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
