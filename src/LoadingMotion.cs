using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Spacio
{
    internal sealed class LoadingMotion
    {
        private readonly List<Action> stop = new List<Action>();
        private readonly Window window;
        private LoadingMotion(Window window) { this.window = window; }
        private T Find<T>(string name) where T : class { return (T)window.FindName(name); }
        private void Animate(Animatable target, DependencyProperty property, double from, double to, double seconds, bool reverse)
        {
            var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = reverse };
            if (reverse) animation.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            Timeline.SetDesiredFrameRate(animation, 60); target.BeginAnimation(property, animation);
            stop.Add(() => target.BeginAnimation(property, null));
        }
        private void Fade(UIElement target, double from, double to, double seconds)
        {
            target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            stop.Add(() => target.BeginAnimation(UIElement.OpacityProperty, null));
        }
        public static void Attach(Window window)
        {
            var motion = new LoadingMotion(window);
            window.Loaded += delegate { motion.Start(); };
            window.Closed += delegate { foreach (var action in motion.stop) action(); motion.stop.Clear(); };
        }
        private void Start()
        {
            var outer = new RotateTransform(); Find<Canvas>("OuterOrbit").RenderTransform = outer;
            Animate(outer, RotateTransform.AngleProperty, -25, 335, 3.6, false);
            var inner = new RotateTransform(); Find<Canvas>("InnerOrbit").RenderTransform = inner;
            Animate(inner, RotateTransform.AngleProperty, 10, -350, 5.2, false);
            var floating = new TransformGroup(); var tilt = new RotateTransform(); var lift = new TranslateTransform();
            floating.Children.Add(tilt); floating.Children.Add(lift); Find<Grid>("FloatingLogo").RenderTransform = floating;
            Animate(tilt, RotateTransform.AngleProperty, -5, 5, 1.8, true);
            Animate(lift, TranslateTransform.YProperty, -6, 6, 1.8, true);
            var breathe = new ScaleTransform(); Find<Ellipse>("LogoGlow").RenderTransform = breathe;
            Animate(breathe, ScaleTransform.ScaleXProperty, 0.83, 1.1, 1.9, true);
            Animate(breathe, ScaleTransform.ScaleYProperty, 0.83, 1.1, 1.9, true);
            Fade(Find<Ellipse>("LogoGlow"), 0.55, 1, 1.9);
            var auroraA = new TranslateTransform(); Find<Ellipse>("AuroraA").RenderTransform = auroraA;
            Animate(auroraA, TranslateTransform.XProperty, -35, 110, 4.7, true);
            Animate(auroraA, TranslateTransform.YProperty, 0, 60, 3.8, true);
            var auroraB = new TranslateTransform(); Find<Ellipse>("AuroraB").RenderTransform = auroraB;
            Animate(auroraB, TranslateTransform.XProperty, 40, -130, 5.6, true);
            Animate(auroraB, TranslateTransform.YProperty, 10, -70, 4.2, true);
            foreach (string name in new [] { "FlowLineA", "FlowLineB" })
            {
                var drift = new TranslateTransform(); Find<Path>(name).RenderTransform = drift;
                Animate(drift, TranslateTransform.XProperty, -24, 24, name == "FlowLineA" ? 3 : 4, true);
                Animate(drift, TranslateTransform.YProperty, -10, 10, 3.8, true);
            }
            var sweep = new TranslateTransform(); Find<Border>("LoadingSweep").RenderTransform = sweep;
            Animate(sweep, TranslateTransform.XProperty, -150, 620, 1.9, false);
            Fade(Find<Ellipse>("ActivityDot"), 0.3, 1, 0.7);
            var particles = Find<Canvas>("AmbientParticles");
            for (int i = 0; i < 18; i++)
            {
                double size = i % 4 == 0 ? 4 : 2;
                var dot = new Ellipse { Width = size, Height = size, Fill = new SolidColorBrush(Color.FromRgb(109, 219, 154)) };
                Canvas.SetLeft(dot, 34 + ((i * 137) % 610)); Canvas.SetTop(dot, 65 + ((i * 53) % 280));
                var move = new TranslateTransform(); dot.RenderTransform = move; particles.Children.Add(dot);
                Animate(move, TranslateTransform.YProperty, 14, -30 - i % 5 * 5, 2.2 + i % 4 * 0.6, true);
                Animate(move, TranslateTransform.XProperty, -10, 15, 3.1 + i % 3, true);
                Fade(dot, 0.12, i % 4 == 0 ? 0.75 : 0.45, 1.3 + i % 5 * 0.4);
            }
        }
    }
}
