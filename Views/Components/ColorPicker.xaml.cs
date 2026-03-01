using System;
using System.Windows;
using System.Windows.Controls;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using ColorConverter = System.Windows.Media.ColorConverter;
using UserControl = System.Windows.Controls.UserControl;

namespace SwitchBlade.Views.Components
{
    public partial class ColorPicker : UserControl
    {
        private bool _isInternalUpdate;

        public ColorPicker()
        {
            InitializeComponent();
        }

        // --- R Property ---
        public static readonly DependencyProperty RProperty =
            DependencyProperty.Register("R", typeof(double), typeof(ColorPicker),
                new PropertyMetadata(0.0, OnColorComponentChanged));

        public double R
        {
            get => (double)GetValue(RProperty);
            set => SetValue(RProperty, value);
        }

        // --- G Property ---
        public static readonly DependencyProperty GProperty =
            DependencyProperty.Register("G", typeof(double), typeof(ColorPicker),
                new PropertyMetadata(0.0, OnColorComponentChanged));

        public double G
        {
            get => (double)GetValue(GProperty);
            set => SetValue(GProperty, value);
        }

        // --- B Property ---
        public static readonly DependencyProperty BProperty =
            DependencyProperty.Register("B", typeof(double), typeof(ColorPicker),
                new PropertyMetadata(0.0, OnColorComponentChanged));

        public double B
        {
            get => (double)GetValue(BProperty);
            set => SetValue(BProperty, value);
        }

        // --- A Property ---
        public static readonly DependencyProperty AProperty =
            DependencyProperty.Register("A", typeof(double), typeof(ColorPicker),
                new PropertyMetadata(255.0, OnColorComponentChanged));

        public double A
        {
            get => (double)GetValue(AProperty);
            set => SetValue(AProperty, value);
        }

        // --- HexColor Property ---
        public static readonly DependencyProperty HexColorProperty =
            DependencyProperty.Register("HexColor", typeof(string), typeof(ColorPicker),
                new PropertyMetadata("#FFFFFFFF", OnHexColorChanged));

        public string HexColor
        {
            get => (string)GetValue(HexColorProperty);
            set => SetValue(HexColorProperty, value);
        }

        // --- SelectedColorProperty (TwoWay binding to VM) ---
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(string), typeof(ColorPicker),
                new FrameworkPropertyMetadata("#FFFFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        public string SelectedColor
        {
            get => (string)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        // --- PreviewColor Property ---
        public static readonly DependencyProperty PreviewColorProperty =
            DependencyProperty.Register("PreviewColor", typeof(Color), typeof(ColorPicker),
                new PropertyMetadata(Colors.White));

        public Color PreviewColor
        {
            get => (Color)GetValue(PreviewColorProperty);
            private set => SetValue(PreviewColorProperty, value);
        }

        private static void OnColorComponentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pc = (ColorPicker)d;
            if (pc._isInternalUpdate) return;

            pc._isInternalUpdate = true;
            try
            {
                var color = Color.FromArgb((byte)pc.A, (byte)pc.R, (byte)pc.G, (byte)pc.B);
                string hex = color.ToString();
                pc.HexColor = hex;
                pc.SelectedColor = hex;
                pc.PreviewColor = color;
            }
            finally
            {
                pc._isInternalUpdate = false;
            }
        }

        private static void OnHexColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pc = (ColorPicker)d;
            if (pc._isInternalUpdate) return;

            pc._isInternalUpdate = true;
            try
            {
                string hex = (string)e.NewValue;
                if (TryParseColor(hex, out Color color))
                {
                    pc.A = color.A;
                    pc.R = color.R;
                    pc.G = color.G;
                    pc.B = color.B;
                    pc.SelectedColor = hex;
                    pc.PreviewColor = color;
                }
            }
            finally
            {
                pc._isInternalUpdate = false;
            }
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pc = (ColorPicker)d;
            if (pc._isInternalUpdate) return;

            pc._isInternalUpdate = true;
            try
            {
                string hex = (string)e.NewValue;
                if (TryParseColor(hex, out Color color))
                {
                    pc.A = color.A;
                    pc.R = color.R;
                    pc.G = color.G;
                    pc.B = color.B;
                    pc.HexColor = hex;
                    pc.PreviewColor = color;
                }
            }
            finally
            {
                pc._isInternalUpdate = false;
            }
        }

        private static bool TryParseColor(string hex, out Color color)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    color = Colors.White;
                    return false;
                }
                color = (Color)ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch
            {
                color = Colors.White;
                return false;
            }
        }
    }
}
