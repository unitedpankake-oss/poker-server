using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BlackjackGame.Models;

namespace BlackjackGame.Views
{
    public partial class CardControl : UserControl
    {
        public static readonly DependencyProperty CardProperty =
            DependencyProperty.Register(nameof(Card), typeof(Card), typeof(CardControl),
                new PropertyMetadata(null, OnCardChanged));

        public Card? Card
        {
            get => (Card?)GetValue(CardProperty);
            set => SetValue(CardProperty, value);
        }

        public CardControl()
        {
            InitializeComponent();
        }

        private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardControl control && e.NewValue is Card card)
            {
                control.UpdateCardDisplay(card);
            }
        }

        private void UpdateCardDisplay(Card card)
        {
            if (card.IsFaceUp)
            {
                CardFace.Visibility = Visibility.Visible;
                CardBack.Visibility = Visibility.Collapsed;

                var color = card.IsRed ? new SolidColorBrush(Color.FromRgb(231, 76, 60)) 
                                        : new SolidColorBrush(Color.FromRgb(44, 62, 80));

                ValueTopLeft.Text = card.DisplayValue;
                ValueTopLeft.Foreground = color;
                SuitTopLeft.Text = card.SuitSymbol;
                SuitTopLeft.Foreground = color;

                SuitCenter.Text = card.SuitSymbol;
                SuitCenter.Foreground = color;

                ValueBottomRight.Text = card.DisplayValue;
                ValueBottomRight.Foreground = color;
                SuitBottomRight.Text = card.SuitSymbol;
                SuitBottomRight.Foreground = color;
            }
            else
            {
                CardFace.Visibility = Visibility.Collapsed;
                CardBack.Visibility = Visibility.Visible;
            }
        }

        public void AnimateDeal(double delay = 0)
        {
            var translateTransform = new TranslateTransform(-200, -100);
            var scaleTransform = new ScaleTransform(0.5, 0.5);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            RenderTransform = transformGroup;
            RenderTransformOrigin = new Point(0.5, 0.5);

            var moveX = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(delay)
            };

            var moveY = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(delay)
            };

            var scaleX = new DoubleAnimation(1, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(delay)
            };

            var scaleY = new DoubleAnimation(1, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(delay)
            };

            translateTransform.BeginAnimation(TranslateTransform.XProperty, moveX);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, moveY);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }

        public void AnimateFlip()
        {
            var scaleTransform = new ScaleTransform(1, 1);
            RenderTransform = scaleTransform;
            RenderTransformOrigin = new Point(0.5, 0.5);

            var shrink = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            var expand = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            shrink.Completed += (s, e) =>
            {
                if (Card != null)
                {
                    Card.IsFaceUp = !Card.IsFaceUp;
                    UpdateCardDisplay(Card);
                }
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, expand);
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        }
    }
}
