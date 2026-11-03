using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EffectBrowserToolPlugin;

/// <summary>
/// bool値をVisibilityに変換するコンバーター（逆方向: true→Collapsed, false→Visible）。
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// EffectBrowserToolView.xaml の相互作用ロジック。
/// DataContextはYMM4側がEffectBrowserToolViewModelを生成してセットします。
/// </summary>
public partial class EffectBrowserToolView : UserControl
{
    public static readonly BooleanToVisibilityConverter BooleanToVisibilityConverter = new();
    public static readonly InverseBooleanToVisibilityConverter InverseBooleanToVisibilityConverter = new();

    private Point _dragStartPoint;
    private bool _isDragging;

    public EffectBrowserToolView()
    {
        InitializeComponent();
        Loaded += UserControl_Loaded;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t) return t;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void FolderItems_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void FolderItems_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { _isDragging = false; return; }
        var pos = e.GetPosition(null);
        var diff = pos - _dragStartPoint;
        if (!_isDragging && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _isDragging = true;
            // determine dragged item
            if (sender is ItemsControl itemsControl)
            {
                var element = e.OriginalSource as DependencyObject;
                ContentPresenter? container = FindAncestor<ContentPresenter>(element);
                if (container == null)
                {
                    var border = FindAncestor<Border>(element);
                    if (border != null)
                    {
                        container = FindAncestor<ContentPresenter>(border);
                    }
                }

                // fallback: use InputHitTest
                if (container == null)
                {
                    var item = itemsControl.InputHitTest(e.GetPosition(itemsControl)) as DependencyObject;
                    container = FindAncestor<ContentPresenter>(item);
                }

                if (container != null)
                {
                    var entry = container.Content as EffectEntry;
                    if (entry != null)
                    {
                        var data = new DataObject("EffectEntry", entry);
                        DragDrop.DoDragDrop(itemsControl, data, DragDropEffects.Move);
                    }
                }
            }
        }
    }

    private void FolderItems_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("EffectEntry"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void FolderItems_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("EffectEntry")) return;
        var entry = e.Data.GetData("EffectEntry") as EffectEntry;
        if (entry == null) return;

        if (!(DataContext is EffectBrowserToolViewModel vm)) return;
        if (sender is ItemsControl itemsControl)
        {
            // find target index
            var point = e.GetPosition(itemsControl);
            int idx = 0;
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var ic = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (ic == null) continue;
                var bounds = new Rect(ic.TranslatePoint(new Point(0,0), itemsControl), new Size(ic.ActualWidth, ic.ActualHeight));
                if (point.Y < bounds.Top + bounds.Height / 2)
                {
                    idx = i;
                    break;
                }
                idx = i + 1;
            }

            // compute fromIndex
            int fromIndex = vm.CustomTagEffects.IndexOf(entry);
            if (fromIndex >= 0)
            {
                vm.MoveEffectInTag(vm.SelectedCustomTag, fromIndex, idx);
                e.Handled = true;
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel viewModel)
        {
            viewModel.Refresh();
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel viewModel)
        {
            viewModel.ClearSearch();
        }
    }

    private void ToggleFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        EffectEntry? entry = null;
        if (sender is Button button)
        {
            entry = button.Tag as EffectEntry;
        }
        else if (sender is MenuItem menuItem)
        {
            entry = menuItem.Tag as EffectEntry;
        }

        if (entry != null && DataContext is EffectBrowserToolViewModel viewModel)
        {
            viewModel.ToggleFavorite(entry);
            e.Handled = true;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            viewModel.ApplyEffect(entry);
            e.Handled = true;
        }
    }

    private void EffectItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            if (e.ClickCount == 2)
            {
                viewModel.ApplyEffect(entry);
                e.Handled = true;
            }
        }
    }

    // 右クリック：カスタム検索ワードの設定
    private void SetCustomKeywords_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            var parentWindow = Window.GetWindow(this);
            var keywords = InputDialog.ShowDialog(
                parentWindow,
                            Texts.SearchKeywordsTitle,
                            string.Format(Texts.SearchKeywordsPromptFormat, entry.DisplayName),
                entry.CustomKeywords);

            if (keywords != null)
            {
                viewModel.SetCustomKeywords(entry, keywords);
            }
        }
    }

    // プリセット機能は廃止されたため、関連UIハンドラを削除しました。

    // 右クリック：独自タグフォルダに追加
    private void AddToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            var parentWindow = Window.GetWindow(this);
            var tagName = InputDialog.ShowDialog(
                parentWindow,
                            Texts.AddToFolderDialogTitle,
                            Texts.AddToFolderPrompt,
                            Texts.DefaultFolderName);

            if (!string.IsNullOrWhiteSpace(tagName))
            {
                viewModel.AddEffectToTag(tagName, entry);
            }
        }
    }

    // フォルダ削除ボタン
    private void DeleteCustomTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel viewModel)
        {
            var tag = viewModel.SelectedCustomTag;
            if (!string.IsNullOrEmpty(tag))
            {
                var wnd = Window.GetWindow(this);
                var ok = MessageBox.Show(wnd, string.Format(Texts.DeleteFolderConfirmMessage, tag), Texts.DeleteFolderConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ok == MessageBoxResult.Yes)
                {
                    viewModel.RemoveCustomTag(tag);
                }
            }
        }
    }

    // 適用中エフェクト管理：削除ボタン
    private void DeleteActiveEffectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ActiveEffectViewModel activeEffect)
        {
            activeEffect.Delete();
        }
    }

    // 適用中エフェクト管理：更新ボタン
    private void GetActiveEffectsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel viewModel)
        {
            viewModel.UpdateActiveEffects();
            EnsureItemEditorInitialized();
        }
    }

    private bool _isItemEditorInitialized = false;

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureItemEditorInitialized();
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl tc && tc.SelectedIndex == 5)
        {
            EnsureItemEditorInitialized();
        }
    }

    public void EnsureItemEditorInitialized()
    {
        if (_isItemEditorInitialized && itemEditorHost.Content != null) return;

        try
        {
            var itemEditorVm = EffectBrowserToolViewModel.ResolveItemEditorViewModel();
            if (itemEditorVm != null)
            {
                var editorView = new YukkuriMovieMaker.Views.ItemEditorView
                {
                    DataContext = itemEditorVm
                };
                itemEditorHost.Content = editorView;
                _isItemEditorInitialized = true;
            }
            else
            {
                itemEditorHost.Content = new TextBlock
                {
                    Text = Texts.NoItemEditorFound,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10)
                };
            }
        }
        catch (Exception ex)
        {
            itemEditorHost.Content = new TextBlock
            {
                Text = $"{Texts.NoItemEditorFound}\n({ex.Message})",
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };
        }
    }

    private void DisplayModeSplit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel vm)
        {
            vm.ActiveTabDisplayMode = EffectBrowserToolViewModel.ActiveViewDisplayMode.Split;
            EnsureItemEditorInitialized();
        }
    }

    private void DisplayModeEditorOnly_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel vm)
        {
            vm.ActiveTabDisplayMode = EffectBrowserToolViewModel.ActiveViewDisplayMode.EditorOnly;
            EnsureItemEditorInitialized();
        }
    }

    private void DisplayModeListOnly_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EffectBrowserToolViewModel vm)
        {
            vm.ActiveTabDisplayMode = EffectBrowserToolViewModel.ActiveViewDisplayMode.ListOnly;
        }
    }

    // フォルダ内エフェクト：フォルダからの削除ボタン
    private void DeleteFromFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            if (!string.IsNullOrEmpty(viewModel.SelectedCustomTag))
            {
                viewModel.RemoveEffectFromTag(viewModel.SelectedCustomTag, entry);
            }
        }
    }

}
