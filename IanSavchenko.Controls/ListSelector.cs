using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using IanSavchenko.Controls.Tools;

// The Templated Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234235

namespace IanSavchenko.Controls
{
    [TemplatePart(Name = ItemsControlPartName, Type = typeof(ItemsControl))]
    [TemplatePart(Name = ScrollViewerPartName, Type = typeof(ScrollViewer))]
    public sealed class ListSelector : Control
    {
        private const string ItemsControlPartName = "PART_ItemsControl";
        private const string ScrollViewerPartName = "PART_ScrollViewer";

        public static readonly DependencyProperty ItemHeightProperty = 
            DependencyProperty.Register("ItemHeight", typeof(double), typeof(ListSelector), new PropertyMetadata(100d, OnItemHeightChanged));
        
        public static readonly DependencyProperty ItemWidthProperty = 
            DependencyProperty.Register("ItemWidth", typeof(double), typeof(ListSelector), new PropertyMetadata(100d, OnItemWidthChanged));

        public static readonly DependencyProperty ItemMarginProperty = 
            DependencyProperty.Register("ItemMargin", typeof(Thickness), typeof(ListSelector), new PropertyMetadata(default(Thickness)));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IList), typeof(ListSelector), new PropertyMetadata(null, OnItemsSourceChanged));

        public static DependencyProperty ItemTemplateProperty =
           DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(ListSelector), new PropertyMetadata(null, OnItemTemplateChangedCallback));

        public static DependencyProperty SelectedIndexProperty = 
            DependencyProperty.Register("SelectedIndex", typeof(int), typeof(ListSelector), new PropertyMetadata(-1, OnSelectedIndexChanged));

        private readonly ScheduleInvoker _scheduleInvoker;
        private readonly List<ListSelectorItem> _items = new List<ListSelectorItem>();

        private ItemsControl _itemsControlPart;
        private ScrollViewer _scrollViewerPart;

        private Thickness _itemsControlMargin;

        private int _highlightIndex = -1;
        private double _latestVerticalScrollOffset;

        private volatile bool _snappingPerformed;

        public ListSelector()
        {
            this.DefaultStyleKey = typeof(ListSelector);
            this.SizeChanged += OnSizeChanged;
            this.Loaded += OnLoaded;

            _scheduleInvoker = new ScheduleInvoker(Dispatcher);
        }

        private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            await SelectItem(SelectedIndex).ConfigureAwait(true);
        }

        protected override void OnApplyTemplate()
        {
            _itemsControlPart = GetTemplateChild(ItemsControlPartName) as ItemsControl;
            _scrollViewerPart = GetTemplateChild(ScrollViewerPartName) as ScrollViewer;

            _scrollViewerPart.ViewChanged += ScrollViewerPartOnViewChanged;
            _scrollViewerPart.ViewChanging += ScrollViewerPartOnViewChanging;
            _scrollViewerPart.IsTapEnabled = true;
            _scrollViewerPart.Tapped += ScrollViewerPartOnTapped;

            UpdateItemsControlItems();
            UpdateGeometricalParams();
            base.OnApplyTemplate();
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

        private double ScrollOffsetPerItem
        {
            get
            {
                if (_items.Count == 0)
                    return 0;

                return (_scrollViewerPart.ExtentHeight - _itemsControlMargin.Top - _itemsControlMargin.Bottom) / _items.Count;
            }
        }

        private static void OnItemHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        private static void OnItemWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        private static void OnItemTemplateChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
        }

        private static async void OnSelectedIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var obj = (ListSelector)dependencyObject;
            var newIndex = (int)dependencyPropertyChangedEventArgs.NewValue;
            await obj.SelectItem(newIndex).ConfigureAwait(true);
        }

        private static async void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var listSelector = d as ListSelector;
            if (listSelector == null)
                return;
            
            listSelector.UpdateItemsControlItems();
            await listSelector.SelectItem(listSelector.SelectedIndex).ConfigureAwait(true);
        }
        
        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            UpdateGeometricalParams();
        }

        private void UpdateGeometricalParams()
        {
            if (_itemsControlPart == null)
                return;

            // making first and last elements centered 
            var topMargin = (ActualHeight / 2) - ItemMargin.Top - (ItemHeight / 2);
            var bottomMargin = (ActualHeight / 2) - ItemMargin.Bottom - (ItemHeight / 2);
            _itemsControlMargin = new Thickness(0, topMargin, 0, bottomMargin);
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
        
        private void UpdateItemsControlItems()
        {
            if (_itemsControlPart == null)
                return;

            int index = 0;

            _items.Clear();
            if (ItemsSource != null)
            {
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

            _itemsControlPart.ItemsSource = null;
            _itemsControlPart.ItemsSource = _items;
        }

        private async void ScrollViewerPartOnTapped(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            if (_snappingPerformed)
                return;

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
                return;
            
            CancelSnappingCheck();
            await SelectItem(itemIndex).ConfigureAwait(true);
        }

        private void ScrollViewerPartOnViewChanging(object sender, ScrollViewerViewChangingEventArgs scrollViewerViewChangingEventArgs)
        {
            _latestVerticalScrollOffset = scrollViewerViewChangingEventArgs.NextView.VerticalOffset;

            if (_snappingPerformed)
            {
                RescheduleSnappingCheck();
                return;
            }

            // While scrolling, no items should be selected
            RemoveHighlight();
            CancelSnappingCheck();
        }

        private async void ScrollViewerPartOnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
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

        private void SnapScrollToItem(int index)
        {
            if (index == -1)
                return;
            
            var offset = Math.Round(index * ScrollOffsetPerItem, MidpointRounding.ToEven);
            if (offset == Math.Round(_latestVerticalScrollOffset, MidpointRounding.ToEven))
            {
                FinishSnapping();
                return;
            }

            if (_snappingPerformed)
                return;
            
            StartSnapping();
            ScrollToOffset(offset, _latestVerticalScrollOffset);
            RescheduleSnappingCheck();
        }

        private void RescheduleSnappingCheck()
        {
            _scheduleInvoker.Schedule(TimeSpan.FromMilliseconds(500), CheckSnappingFinished);
        }

        private void CancelSnappingCheck()
        {
            _scheduleInvoker.Stop();
        }

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
        
        private double _prevChangeViewCallOffset = -1;
        private double _prevChangeViewCallCurrentOffset = -1;

        private void ScrollToOffset(double newOffset, double currentOffset)
        {
            if (_prevChangeViewCallOffset == newOffset && currentOffset == _prevChangeViewCallCurrentOffset)
            {
                // Debug.WriteLine("OLD WAY");
                _scrollViewerPart.ScrollToVerticalOffset(newOffset);
            }
            else
            {
                // Debug.WriteLine("NEW WAY");
                _scrollViewerPart.ChangeView(0, newOffset, null);
                _prevChangeViewCallOffset = newOffset;
                _prevChangeViewCallCurrentOffset = currentOffset;
            }

        }
    }
}
