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

    public EffectBrowserToolView()
    {
        InitializeComponent();
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
                "検索ワード",
                $"{entry.DisplayName} の検索ワードを入力してください（カンマ区切り）:",
                entry.CustomKeywords);

            if (keywords != null)
            {
                viewModel.SetCustomKeywords(entry, keywords);
            }
        }
    }

    // 右クリック：パラメータ付きプリセットとして保存
    private void SaveAsPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            var parentWindow = Window.GetWindow(this);
            var presetName = InputDialog.ShowDialog(
                parentWindow,
                "プリセットの保存",
                "プリセット名を入力してください:",
                $"{entry.DisplayName} のプリセット");

            if (!string.IsNullOrWhiteSpace(presetName))
            {
                viewModel.SavePresetFromSelected(presetName, entry);
            }
        }
    }

    // 右クリック：独自タグフォルダに追加
    private void AddToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is EffectEntry entry && DataContext is EffectBrowserToolViewModel viewModel)
        {
            var parentWindow = Window.GetWindow(this);
            var tagName = InputDialog.ShowDialog(
                parentWindow,
                "フォルダに追加",
                "追加先のフォルダ名を入力してください:",
                "お気に入り");

            if (!string.IsNullOrWhiteSpace(tagName))
            {
                viewModel.AddEffectToTag(tagName, entry);
            }
        }
    }

    // プリセット適用ボタン
    private void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PresetModel preset && DataContext is EffectBrowserToolViewModel viewModel)
        {
            viewModel.ApplyPreset(preset);
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
        }
    }


}
