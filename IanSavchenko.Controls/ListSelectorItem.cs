using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace IanSavchenko.Controls
{
    public sealed class ListSelectorItem : Control
    {
        public static DependencyProperty ItemContentProperty =
            DependencyProperty.Register("ItemContent", typeof(object), typeof(ListSelectorItem), new PropertyMetadata(null));

        public static DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(ListSelectorItem), new PropertyMetadata(null));

        public static DependencyProperty IsSelectedProperty = 
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(ListSelectorItem), new PropertyMetadata(false, IsSelectedPropertyChanged));
        
        public ListSelectorItem()
        {
            this.DefaultStyleKey = typeof(ListSelectorItem);
            this.Loaded += (sender, args) => UpdateStates(true);
        }

        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public object ItemContent
        {
            get { return GetValue(ItemContentProperty); }
            set { SetValue(ItemContentProperty, value);}
        }

        public bool IsSelected
        {
            get { return (bool) GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public int ItemIndex { get; set; }
        
        private static void IsSelectedPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var obj = (ListSelectorItem)dependencyObject;
            obj.UpdateStates(true);
        }

        private void UpdateStates(bool useTransitions)
        {
            if (IsSelected)
                VisualStateManager.GoToState(this, "Selected", useTransitions);
            else
                VisualStateManager.GoToState(this, "Normal", useTransitions);
        }
    }
}
