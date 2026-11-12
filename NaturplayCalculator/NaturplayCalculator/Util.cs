using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace NaturplayCalculator
{
    public class Thickness
    {
        public double Top;
        public double Bottom;
        public double Left;
        public double Right;

        public Thickness() { }

        public Thickness(double value)
        {
            Top = value;
            Bottom = value;
            Left = value;
            Right = value;
        }

        public Thickness(double lr, double tb)
        {
            Top = tb;
            Bottom = tb;
            Left = lr;
            Right = lr;
        }

        public Thickness(double l, double t, double r)
        {
            Top = t;
            Bottom = t;
            Left = l;
            Right = r;
        }

        public Thickness(double l, double t, double r, double b)
        {
            Top = t;
            Bottom = b;
            Left = l;
            Right = r;
        }

        public static implicit operator Microsoft.UI.Xaml.Thickness (Thickness t)
        {
            return new Microsoft.UI.Xaml.Thickness()
            {
                Top = t.Top,
                Bottom = t.Bottom,
                Left = t.Left,
                Right = t.Right,
            };
        }
    }

    public class GridLength
    {
        public double Value;
        public GridUnitType GridUnitType;

        public GridLength(double value, GridUnitType gridUnitType)
        {
            Value = value;
            GridUnitType = gridUnitType;
        }

        public static implicit operator Microsoft.UI.Xaml.GridLength(GridLength t)
        {
            return new Microsoft.UI.Xaml.GridLength(t.Value, t.GridUnitType);
        }
    }
}
