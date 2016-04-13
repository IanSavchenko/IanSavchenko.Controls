using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using IanSavchenko.Controls.Tools;

namespace IanSavchenko.Controls
{
    [TemplatePart(Name = ItemsControlPartName, Type = typeof(ItemsControl))]
    [TemplatePart(Name = ScrollViewerPartName, Type = typeof(ScrollViewer))]
    [TemplatePart(Name = InactiveStateItemPartName, Type = typeof(ListSelectorItem))]
    public sealed class ListSelector : Control
    {
        private const string ItemsControlPartName = "PART_ItemsControl";
        private const string ScrollViewerPartName = "PART_ScrollViewer";
        private const string InactiveStateItemPartName = "PART_InactiveStateItem";

        private const int ShowHideAnimationDurationMs = 200;

        /// <summary>
        /// Global reference to active selector. Helps to handle behavior that only one selector is active.
        /// </summary>
        private static ListSelector ActiveSelector = null;

        public static readonly DependencyProperty ItemHeightProperty = 
            DependencyProperty.Register("ItemHeight", typeof(double), typeof(ListSelector), new PropertyMetadata(130d, OnItemHeightChanged));
        
        public static readonly DependencyProperty ItemWidthProperty = 
            DependencyProperty.Register("ItemWidth", typeof(double), typeof(ListSelector), new PropertyMetadata(130d, OnItemWidthChanged));

        public static readonly DependencyProperty ItemMarginProperty = 
            DependencyProperty.Register("ItemMargin", typeof(Thickness), typeof(ListSelector), new PropertyMetadata(default(Thickness)));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IList), typeof(ListSelector), new PropertyMetadata(null, OnItemsSourceChanged));

        public static DependencyProperty ItemTemplateProperty =
           DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(ListSelector), new PropertyMetadata(null, OnItemTemplateChangedCallback));

        public static DependencyProperty SelectedIndexProperty = 
            DependencyProperty.Register("SelectedIndex", typeof(int), typeof(ListSelector), new PropertyMetadata(-1, OnSelectedIndexChanged));

        public static DependencyProperty IsActiveProperty = 
            DependencyProperty.Register("IsActive", typeof(bool), typeof(ListSelector), new PropertyMetadata(false, OnIsActiveChanged));

        private readonly ScheduleInvoker _scheduleInvoker;
        private readonly List<ListSelectorItem> _items = new List<ListSelectorItem>();

        private ItemsControl _itemsControlPart;
        private ScrollViewer _scrollViewerPart;
        private ListSelectorItem _inactiveStateItemPart;

        private Thickness _itemsControlMargin;

        private int _highlightIndex = -1;
        private double _latestVerticalScrollOffset;

        private volatile bool _snappingPerformed;

        private Storyboard _hideItemsStoryboard;
        private DoubleAnimation _hideItemsAnimation;

        private Storyboard _showItemsStoryboard;
        private DoubleAnimation _showItemsAnimation;

        private double _prevChangeViewCallOffset = -1;
        private double _prevChangeViewCallCurrentOffset = -1;
        private bool _active;

        public ListSelector()
        {
            this.DefaultStyleKey = typeof(ListSelector);
            this.SizeChanged += OnSizeChanged;
            this.Loaded += OnLoaded;

            _scheduleInvoker = new ScheduleInvoker(Dispatcher);
        }

        public double ItemWidth
        {
            get { return (double)this.GetValue(ItemWidthProperty); }
            set { this.SetValue(ItemWidthProperty, value); }
        }

        public double ItemHeight
        {
            get { return (double) this.GetValue(ItemHeightProperty); }
            set { this.SetValue(ItemHeightProperty, value);}
        }

        public Thickness ItemMargin
        {
            get { return (Thickness) this.GetValue(ItemMarginProperty); }
            set { this.SetValue(ItemMarginProperty, value); }
        }

        public IList ItemsSource
        {
            get { return (IList)this.GetValue(ItemsSourceProperty); }
            set { this.SetValue(ItemsSourceProperty, value); }
        }

        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set
            {
                if (SelectedIndex != value)
                    SetValue(SelectedIndexProperty, value);
            }
        }

        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }

        private double ScrollOffsetPerItem
        {
            get
            {
                if (_items.Count == 0)
                    return 0;

                return (_scrollViewerPart.ExtentHeight - _itemsControlMargin.Top - _itemsControlMargin.Bottom) / _items.Count;
            }
        }
        
        protected override void OnApplyTemplate()
        {
            // Part that contains all items
            _itemsControlPart = GetTemplateChild(ItemsControlPartName) as ItemsControl;
            // Part that scrolls items
            _scrollViewerPart = GetTemplateChild(ScrollViewerPartName) as ScrollViewer;
            // Placeholder that displays selected item, when ListSelector not active
            _inactiveStateItemPart = GetTemplateChild(InactiveStateItemPartName) as ListSelectorItem;

            _scrollViewerPart.ViewChanged += ScrollViewerPartOnViewChanged;
            _scrollViewerPart.ViewChanging += ScrollViewerPartOnViewChanging;
            _scrollViewerPart.IsTapEnabled = true;
            _scrollViewerPart.Tapped += ScrollViewerPartOnTapped;

            UpdateItemsControlItems();
            UpdateGeometricalParams();
            CreateAnimations();
            base.OnApplyTemplate();
        }

        private static void OnItemHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // ToDo: handle run-time changes
        }

        private static void OnItemWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // ToDo: handle run-time changes
        }

        private static void OnItemTemplateChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            // ToDo: handle run-time changes
        }

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var newValue = (bool)e.NewValue;
            var target = (ListSelector)d;
            
            Debug.WriteLine($"IsActive {newValue}" + DateTime.Now.ToString("O"));

            if (newValue)
            {
                target.SetActive(true);

                if (ActiveSelector == target)
                    return;

                var prevActiveSelector = ActiveSelector;
                ActiveSelector = target;

                if (prevActiveSelector != null)
                    prevActiveSelector.IsActive = false;
            }
            else
            {
                target.SetActive(false);

                if (ActiveSelector == target)
                    ActiveSelector = null;
            }
        }

        private static async void OnSelectedIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var obj = (ListSelector)dependencyObject;
            var newIndex = (int)dependencyPropertyChangedEventArgs.NewValue;
            await obj.SelectItem(newIndex).ConfigureAwait(true);
        }

        private static async void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var listSelector = (ListSelector)d;

            listSelector.UpdateItemsControlItems();
            await listSelector.SelectItem(listSelector.SelectedIndex).ConfigureAwait(true);
        }

        private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            await SelectItem(SelectedIndex).ConfigureAwait(true);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            UpdateGeometricalParams();
        }

        private void UpdateGeometricalParams()
        {
            if (_itemsControlPart == null)
                return;

            // Getting height of ListSelector
            var height = ActualHeight;

            // calculating margin for ItemsControl part
            var topMargin = (height / 2) - ItemMargin.Top - (ItemHeight / 2);
            var bottomMargin = (height / 2) - ItemMargin.Bottom - (ItemHeight / 2);
            _itemsControlMargin = new Thickness(0, topMargin, 0, bottomMargin);

            // making first and last elements centered
            _itemsControlPart.Margin = _itemsControlMargin;
        }
        
        private int GetItemIndexForScrollOffset(double scrollOffset)
        {
            var firstItemOffset = _itemsControlMargin.Top;
            var offsetToCenter = ActualHeight / 2;
            var currentItemOffset = scrollOffset + offsetToCenter - firstItemOffset;
            var currentItemPos = currentItemOffset / ScrollOffsetPerItem;
            var index = (int)currentItemPos;
            return index;
        }
        
        private async void UpdateItemsControlItems()
        {
            if (_itemsControlPart == null)
                return;
            
            _items.Clear();

            if (ItemsSource != null)
            {
                int index = 0;
                foreach (var item in ItemsSource)
                {
                    var selectorItem = new ListSelectorItem()
                    {
                        Width = ItemWidth,
                        Height = ItemHeight,
                        Margin = ItemMargin,
                        ItemContent = item,
                        ItemIndex = index++,
                    };

                    selectorItem.ItemTemplate = ItemTemplate;
                    _items.Add(selectorItem);
                }
            }

            // Maybe don't need this reset here?
            _itemsControlPart.ItemsSource = null;
            _itemsControlPart.ItemsSource = _items;

            if (_items.Count > 0 && SelectedIndex == -1)
                await SelectItem(0).ConfigureAwait(true);
        }

        private void CreateAnimations()
        {
            _showItemsStoryboard = new Storyboard();
            _hideItemsStoryboard = new Storyboard();
            
            _hideItemsAnimation = new DoubleAnimation();
            _hideItemsAnimation.To = 0;
            _showItemsAnimation = new DoubleAnimation();
            _showItemsAnimation.To = 1;

            _showItemsStoryboard.Children.Add(_showItemsAnimation);
            _hideItemsStoryboard.Children.Add(_hideItemsAnimation);

            Storyboard.SetTarget(_showItemsStoryboard, _scrollViewerPart);
            Storyboard.SetTarget(_hideItemsStoryboard, _scrollViewerPart);
            Storyboard.SetTargetProperty(_showItemsStoryboard, "Opacity");
            Storyboard.SetTargetProperty(_hideItemsStoryboard, "Opacity");

            _showItemsStoryboard.Completed += ShowItemsStoryboardOnCompleted;
        }

        private async void ScrollViewerPartOnTapped(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            Debug.WriteLine("Tapped " + DateTime.Now.ToString("O"));
            if (_snappingPerformed)
                return;

            if (!IsActive)
            {
                IsActive = true;
                return;
            }

            var tapPosition = tappedRoutedEventArgs.GetPosition(_itemsControlPart);
            var scrollOffset = tapPosition.Y - (ItemHeight / 2);

            // tapped outside 
            if (scrollOffset < 0)
                return;
            
            var itemIndex = GetItemIndexForScrollOffset(scrollOffset);

            // tapped outside
            if (itemIndex >= _items.Count)
                return;

            if (itemIndex == SelectedIndex)
            {
                if (IsActive)
                    IsActive = false;
                
                return;
            }
            
            CancelSnappingCheck();
            await SelectItem(itemIndex).ConfigureAwait(true);
        }

        /// <summary>
        /// Occurs before every small scrolling step
        /// </summary>
        private void ScrollViewerPartOnViewChanging(object sender, ScrollViewerViewChangingEventArgs scrollViewerViewChangingEventArgs)
        {
            Debug.WriteLine("ScrollViewerPartOnViewChanging IsInertial: " + scrollViewerViewChangingEventArgs.IsInertial + " " + scrollViewerViewChangingEventArgs.NextView.VerticalOffset + " " + DateTime.Now.ToString("O"));
            _latestVerticalScrollOffset = scrollViewerViewChangingEventArgs.NextView.VerticalOffset;

            if (_snappingPerformed)
            {
                RescheduleSnappingCheck();
                return;
            }

            if (!_active && !scrollViewerViewChangingEventArgs.IsInertial)
            {
                _inactiveStateItemPart.Visibility = Visibility.Collapsed; // hiding immediately 
                IsActive = true;
            }

            // While scrolling, no items should be selected
            RemoveHighlight();
            CancelSnappingCheck();
        }

        /// <summary>
        /// Occurs after every small scrolling step
        /// </summary>
        private async void ScrollViewerPartOnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            Debug.WriteLine("ScrollViewerPartOnViewChanged IsIntermediate: " + e.IsIntermediate + " " + DateTime.Now.ToString("O"));
            if (e.IsIntermediate)
                return;

            // Coming here only when scroll finalized
            if (_snappingPerformed)
                FinishSnapping();
            
            // making sure we have scrolled to item
            await SelectItem(GetItemIndexForScrollOffset(_latestVerticalScrollOffset)).ConfigureAwait(true);
        }
        
        private async Task SelectItem(int index)
        {
            if (_itemsControlPart == null)
                return;

            // HACK to make all animations work ok
            await Task.Delay(1).ConfigureAwait(true);

            HighlightItem(index);
            SnapScrollToItem(index);
            SelectedIndex = index;
        }

        private void HighlightItem(int index)
        {
            if (_highlightIndex == index)
                return;

            RemoveHighlight();

            if (index >= _items.Count)
                return;

            _highlightIndex = index;
            if (_highlightIndex == -1)
                return;
            
            _items[_highlightIndex].IsSelected = true;

            UpdateInactiveStateItem();
        }

        private void RemoveHighlight()
        {
            if (_highlightIndex == -1)
                return;

            if (_highlightIndex >= _items.Count)
                return;
            
            _items[_highlightIndex].IsSelected = false;

            _highlightIndex = -1;
        }

        private void UpdateInactiveStateItem()
        {
            // some hack to make placeholder update content
            _inactiveStateItemPart.ItemTemplate = null;
            _inactiveStateItemPart.ItemContent = _items[_highlightIndex].ItemContent;
            _inactiveStateItemPart.ItemTemplate = ItemTemplate;
        }
        
        private void SnapScrollToItem(int index)
        {
            if (index == -1)
                return;
            
            var offset = Math.Round(index * ScrollOffsetPerItem, MidpointRounding.ToEven);
            if (offset == Math.Round(_latestVerticalScrollOffset, MidpointRounding.ToEven))
            {
                FinishSnapping();

                // Finalizing no active state only after snapping finished
                if (_active == false)
                    SetActive(_active);

                return;
            }

            //if (_snappingPerformed)
            //    return;
            
            StartSnapping();
            ScrollToOffset(offset, _latestVerticalScrollOffset);
            RescheduleSnappingCheck();
        }

        private void RescheduleSnappingCheck()
        {
            _scheduleInvoker.Schedule(TimeSpan.FromMilliseconds(300), CheckSnappingFinished);
        }

        private void CancelSnappingCheck()
        {
            _scheduleInvoker.Stop();
        }

        /// <summary>
        /// Fail-safe mechanism to make sure we always are snapped
        /// </summary>
        private async void CheckSnappingFinished()
        {
            if (_snappingPerformed == false)
                return;

            await SelectItem(GetItemIndexForScrollOffset(_latestVerticalScrollOffset)).ConfigureAwait(true);
        }

        private void StartSnapping()
        {
            _snappingPerformed = true;
            _scrollViewerPart.IsHitTestVisible = false;
        }

        private void FinishSnapping()
        {
            _snappingPerformed = false;
            _scrollViewerPart.IsHitTestVisible = true;
        }
        
        private void ScrollToOffset(double newOffset, double currentOffset)
        {
            // Sometimes ScrollViewer.ChangeView(...) doesn't work if you call it simultaneously with same params.
            // In such case using obsolete ScrollToVerticalOffset
            // This happens very rarely, only when you are tapping 2 items like crazy
            if (_prevChangeViewCallOffset == newOffset && currentOffset == _prevChangeViewCallCurrentOffset)
            {
                _scrollViewerPart.ScrollToVerticalOffset(newOffset);
            }
            else
            {
                _scrollViewerPart.ChangeView(0, newOffset, null);
                _prevChangeViewCallOffset = newOffset;
                _prevChangeViewCallCurrentOffset = currentOffset;
            }
        }

        private void SetActive(bool active)
        {
            _active = active;

            // if has no highlight or snapping, not running animation
            if (_highlightIndex == -1 || _snappingPerformed)
                return;

            if (active)
                ShowScrollViewer();
            else
                HideScrollViewer();

            // Showing immediately when not active
            if (!_active)
                _inactiveStateItemPart.Visibility = Visibility.Visible;

            UpdateInactiveStateItem();
        }

        private void ShowScrollViewer()
        {
            var currentOpacity = _scrollViewerPart.Opacity;
            
            _hideItemsStoryboard.Stop();

            lock (_showItemsStoryboard)
            {
                if (_showItemsStoryboard.GetCurrentState() != ClockState.Stopped)
                    return;

                _showItemsAnimation.From = currentOpacity; // To = 1
                _showItemsAnimation.Duration =
                    new Duration(TimeSpan.FromMilliseconds(ShowHideAnimationDurationMs*Math.Abs(1 - currentOpacity)));

                _showItemsStoryboard.Begin();
            }
        }

        private void HideScrollViewer()
        {
            var currentOpacity = _scrollViewerPart.Opacity;

            _showItemsStoryboard.Stop();

            lock (_hideItemsStoryboard)
            {
                if (_hideItemsStoryboard.GetCurrentState() != ClockState.Stopped)
                    return;

                _hideItemsAnimation.From = currentOpacity; // To = 0
                _hideItemsAnimation.Duration =
                    new Duration(TimeSpan.FromMilliseconds(ShowHideAnimationDurationMs*Math.Abs(currentOpacity)));

                _hideItemsStoryboard.Begin();
            }
        }

        private void ShowItemsStoryboardOnCompleted(object sender, object o)
        {
            // only when we showing animation completed, hiding placeholder
            if (IsActive)
                _inactiveStateItemPart.Visibility = Visibility.Collapsed;
        }
    }
}
