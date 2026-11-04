using BiliCopilot.UI.Toolkits;
using Richasy.WinUIKernel.Share.Base;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls;

public sealed partial class IncrementVerticalScrollControl : LayoutUserControlBase
{
    private bool _checkPending;
    private bool _isActive;
    private int _loadVersion;
    private int _budgetSettleVersion;
    private Task _budgetSettleTask = Task.CompletedTask;
    private readonly TimeProvider _timeProvider;

    public static readonly DependencyProperty ElementProperty =
        DependencyProperty.Register(nameof(Element), typeof(object), typeof(IncrementVerticalScrollControl), new PropertyMetadata(default));

    public static readonly DependencyProperty LoadMoreCommandProperty =
        DependencyProperty.Register(nameof(LoadMoreCommand), typeof(ICommand), typeof(IncrementVerticalScrollControl), new PropertyMetadata(default));

    /// <summary>
    /// 列表里真实条目的数量；绑定后启用「按视口槽位预算加载」，未绑定（-1）时保持旧的滚动余量判据.
    /// </summary>
    public static readonly DependencyProperty ItemCountProperty =
        DependencyProperty.Register(nameof(ItemCount), typeof(int), typeof(IncrementVerticalScrollControl), new PropertyMetadata(-1));

    /// <summary>
    /// 是否保留「滚动余量不足」这条兜底判据.
    /// </summary>
    /// <remarks>
    /// 页面用骨架屏撑起尚未加载的区域时，extent 会一直等于视口高度，
    /// 余量判据将永远成立；此时必须关掉兜底，只按「真实条目数是否够预算」决定是否加载。
    /// </remarks>
    public static readonly DependencyProperty ExtentFallbackEnabledProperty =
        DependencyProperty.Register(nameof(ExtentFallbackEnabled), typeof(bool), typeof(IncrementVerticalScrollControl), new PropertyMetadata(true));

    public IncrementVerticalScrollControl()
        : this(TimeProvider.System)
    {
    }

    internal IncrementVerticalScrollControl(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        InitializeComponent();
        // The NuGet base unregisters its lifecycle handlers on the first unload.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 视口槽位预算发生变化，参数为新的槽位数.
    /// </summary>
    public event Action<int> BudgetChanged;

    public object Element
    {
        get => (object)GetValue(ElementProperty);
        set => SetValue(ElementProperty, value);
    }

    public ICommand LoadMoreCommand
    {
        get => (ICommand)GetValue(LoadMoreCommandProperty);
        set => SetValue(LoadMoreCommandProperty, value);
    }

    /// <summary>
    /// 列表里真实条目的数量；-1 表示页面没有声明，此时只按滚动余量判断.
    /// </summary>
    public int ItemCount
    {
        get => GetValue(ItemCountProperty) is int value ? value : -1;
        set => SetValue(ItemCountProperty, value);
    }

    /// <summary>
    /// 是否保留滚动余量兜底判据.
    /// </summary>
    public bool ExtentFallbackEnabled
    {
        get => GetValue(ExtentFallbackEnabledProperty) is bool value ? value : true;
        set => SetValue(ExtentFallbackEnabledProperty, value);
    }

    /// <summary>
    /// 最近一次算出的槽位预算；-1 表示还没有可用的布局尺寸.
    /// </summary>
    public int CurrentBudget { get; private set; } = -1;

    /// <summary>
    /// 按当前视口重新计算预算；预算变化时通知观察者.
    /// </summary>
    private int RefreshBudget()
    {
        if (View is null)
        {
            return CurrentBudget;
        }

        var width = View.ActualWidth;
        var height = View.ActualHeight;
        if (!(width > 0) || !(height > 0))
        {
            return CurrentBudget;
        }

        var budget = ScrollSlotBudget.Compute(width, height, SlotLayout.Video);
        if (budget == CurrentBudget)
        {
            return CurrentBudget;
        }

        CurrentBudget = budget;
        BudgetChanged?.Invoke(budget);
        return CurrentBudget;
    }

    /// <summary>
    /// 列表更新后检查是否需要继续加载.
    /// </summary>
    /// <remarks>
    /// 启用槽位预算的页面只看「真实条目数够不够」，不依赖布局结果，
    /// 因此可以立即检查：一页落地就接着请求下一页，首屏不必为每页多等一次布局稳定。
    /// 只按滚动余量判断的旧页面仍然需要等布局，否则会读到尚未刷新的 extent。
    /// </remarks>
    public async Task DelayCheckItemsAsync()
    {
        if (!_isActive)
        {
            return;
        }

        if (ItemCount >= 0)
        {
            CheckItemsCount();
            return;
        }

        var version = _loadVersion;
        await Task.Delay(TimeSpan.FromMilliseconds(500), _timeProvider);
        if (version == _loadVersion)
        {
            CheckItemsCount();
        }
    }

    public void ResetScrollPosition()
    {
        if (View is null)
        {
            return;
        }

        View.ScrollTo(0, 0);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        View.ViewChanged += OnViewChanged;
        View.SizeChanged += OnViewSizeChanged;
        ScheduleBudgetSettle();
        CheckItemsCount();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _loadVersion++;
        // 卸载后不再接受尺寸变化引起的预算重算。
        _budgetSettleVersion++;
        // Keep the pending slot until its callback drains, even across a reload.
        View.ViewChanged -= OnViewChanged;
        View.SizeChanged -= OnViewSizeChanged;
    }

    private void OnViewChanged(ScrollView sender, object e)
    {
        CheckItemsCount();
    }

    private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 100)
        {
            CheckItemsCount();
            ScheduleBudgetSettle();
        }
    }

    /// <summary>
    /// 拖动窗口时尺寸每帧都在变，骨架数量等观察者只应在尺寸稳定后收到一次通知.
    /// </summary>
    private void ScheduleBudgetSettle()
    {
        var version = ++_budgetSettleVersion;
        _budgetSettleTask = SettleBudgetAsync(version);
    }

    /// <summary>
    /// 最近一次尺寸稳定后的预算重算任务，供测试等待.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "VSTHRD003:Avoid awaiting foreign Tasks",
        Justification = "测试需要等待页面自身的预算重算完成，任务生命周期由控件持有。")]
    internal Task BudgetSettleTask => _budgetSettleTask;

    private async Task SettleBudgetAsync(int version)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(120), _timeProvider);
        if (version != _budgetSettleVersion || !_isActive)
        {
            return;
        }

        RefreshBudget();
    }

    private void CheckItemsCount()
    {
        if (!_isActive || _checkPending)
        {
            return;
        }

        _checkPending = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            // Reentrant layout can request one follow-up; always read the current view.
            _checkPending = false;
            if (_isActive && NeedsMoreItems())
            {
                ExecuteLoadMore();
            }
        }))
        {
            _checkPending = false;
        }
    }

    private bool NeedsMoreItems()
    {
        var nearEnd = View.ExtentHeight - View.ViewportHeight - View.VerticalOffset <= 800;
        if (ItemCount < 0)
        {
            // 没有声明条目数的页面保持旧行为：只看滚动余量。
            return nearEnd;
        }

        if (!(View.ActualWidth > 0) || !(View.ActualHeight > 0))
        {
            // 首帧布局还没完成，此时预算不可信，先按余量判断。
            return nearEnd;
        }

        RefreshBudget();
        var underBudget = ItemCount < CurrentBudget;
        return ExtentFallbackEnabled ? nearEnd || underBudget : underBudget;
    }

    private void ExecuteLoadMore()
    {
        var command = LoadMoreCommand;
        if (command?.CanExecute(default) == true)
        {
            command.Execute(default);
        }
    }
}