using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;

namespace IanSavchenko.Controls
{
    /// <summary>
    /// Flyout to containt ListSelectors
    /// </summary>
    [ContentProperty(Name = "Content")]
    public class ListSelectorFlyout : PickerFlyoutBase
    {
        public static DependencyProperty ContentProperty =
           DependencyProperty.Register("Content", typeof(object), typeof(ListPickerFlyout), new PropertyMetadata(null));

        public static DependencyProperty ConfirmationButtonsVisibleProperty =
            DependencyProperty.Register("ConfirmationButtonsVisible", typeof(bool), typeof(ListPickerFlyout), new PropertyMetadata(false));

        public ListSelectorFlyout()
        {
            Placement = FlyoutPlacementMode.Full;
        }

        public event EventHandler Confirmed;

        public object Content
        {
            get { return (UIElement)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public bool ConfirmationButtonsVisible
        {
            get { return (bool)GetValue(ConfirmationButtonsVisibleProperty); }
            set { SetValue(ConfirmationButtonsVisibleProperty, value); }
        }
        
        protected override Control CreatePresenter()
        {
            var presenter = new FlyoutPresenter()
            {
                Content = Content,
                IsTabStop = true,
                TabNavigation = KeyboardNavigationMode.Cycle,
                Height = Windows.UI.Xaml.Window.Current.Bounds.Height
            };

            ScrollViewer.SetVerticalScrollBarVisibility(presenter, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollMode(presenter, ScrollMode.Disabled);

            return presenter;
        }

        protected override bool ShouldShowConfirmationButtons()
        {
            return ConfirmationButtonsVisible;
        }

        protected override void OnConfirmed()
        {
            Confirmed?.Invoke(this, EventArgs.Empty);
        }
    }
}
