using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Collections.Specialized;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Resources.Localization;

namespace EffectBrowserToolPlugin;

/// <summary>
/// エフェクト一覧ツールのViewModelです。
/// 映像・音声・お気に入り・使用履歴の管理、検索、選択アイテムへの適用機能を提供します。
/// </summary>
public sealed class EffectBrowserToolViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // 各種コレクション
    public ObservableCollection<EffectCategoryGroup> VideoEffectGroups { get; } = new();
    public ObservableCollection<EffectCategoryGroup> AudioEffectGroups { get; } = new();
    public ObservableCollection<EffectEntry> FavoriteEffects { get; } = new();
    public ObservableCollection<EffectEntry> RecentEffects { get; } = new();
    public ObservableCollection<EffectEntry> SearchResults { get; } = new();
    
    // 拡張機能用のコレクション (プリセット機能は廃止)
    public ObservableCollection<ComboModel> Combos { get; } = new();
    public ObservableCollection<string> CustomTagNames { get; } = new();
    public ObservableCollection<EffectEntry> CustomTagEffects { get; } = new();
    public ObservableCollection<ActiveEffectViewModel> SelectedItemsEffects { get; } = new();

    public enum ActiveViewDisplayMode
    {
        Split,
        EditorOnly,
        ListOnly
    }

    private ActiveViewDisplayMode activeTabDisplayMode = ActiveViewDisplayMode.Split;
    public ActiveViewDisplayMode ActiveTabDisplayMode
    {
        get => activeTabDisplayMode;
        set
        {
            if (activeTabDisplayMode != value)
            {
                activeTabDisplayMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSplitMode));
                OnPropertyChanged(nameof(IsEditorOnlyMode));
                OnPropertyChanged(nameof(IsListOnlyMode));
                OnPropertyChanged(nameof(IsListVisible));
                OnPropertyChanged(nameof(IsEditorVisible));
            }
        }
    }

    public bool IsSplitMode => activeTabDisplayMode == ActiveViewDisplayMode.Split;
    public bool IsEditorOnlyMode => activeTabDisplayMode == ActiveViewDisplayMode.EditorOnly;
    public bool IsListOnlyMode => activeTabDisplayMode == ActiveViewDisplayMode.ListOnly;

    public bool IsListVisible => activeTabDisplayMode != ActiveViewDisplayMode.EditorOnly;
    public bool IsEditorVisible => activeTabDisplayMode != ActiveViewDisplayMode.ListOnly;

    private readonly List<EffectEntry> allEffects = new();
    private UserDataModel userData = new();
    
    private string selectedCustomTag = string.Empty;
    public string SelectedCustomTag
    {
        get => selectedCustomTag;
        set
        {
            if (selectedCustomTag != value)
            {
                selectedCustomTag = value;
                OnPropertyChanged();
                UpdateCustomTagEffects();
            }
        }
    }

    // リフレクション探索の高速キャッシュ
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> NamedPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> AddMethodCache = new();

    private int videoEffectCount;
    public int VideoEffectCount
    {
        get => videoEffectCount;
        private set { videoEffectCount = value; OnPropertyChanged(); }
    }

    private int audioEffectCount;
    public int AudioEffectCount
    {
        get => audioEffectCount;
        private set { audioEffectCount = value; OnPropertyChanged(); }
    }

    private int favoriteEffectCount;
    public int FavoriteEffectCount
    {
        get => favoriteEffectCount;
        private set { favoriteEffectCount = value; OnPropertyChanged(); }
    }

    private int recentEffectCount;
    public int RecentEffectCount
    {
        get => recentEffectCount;
        private set { recentEffectCount = value; OnPropertyChanged(); }
    }

    private int searchResultCount;
    public int SearchResultCount
    {
        get => searchResultCount;
        private set { searchResultCount = value; OnPropertyChanged(); }
    }

    private string searchQuery = string.Empty;
    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            if (searchQuery != value)
            {
                searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchQuery));
                PerformSearch();
            }
        }
    }

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    public bool HasSearchResults => SearchResults.Count > 0;
    public bool HasFavoriteEffects => FavoriteEffects.Count > 0;
    public bool HasRecentEffects => RecentEffects.Count > 0;

    private int selectedTabIndex;
    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set
        {
            if (selectedTabIndex != value)
            {
                selectedTabIndex = value;
                OnPropertyChanged();
                if (selectedTabIndex == 5) // 「適用中管理」タブ
                {
                    UpdateActiveEffects();
                }
            }
        }
    }

    private string statusMessage = string.Empty;
    public string StatusMessage
    {
        get => statusMessage;
        private set { statusMessage = value; OnPropertyChanged(); }
    }

    private IList? _hostFavoritesCollection;
    private INotifyCollectionChanged? _hostFavoritesNotifier;
    private bool _isSyncingFavorites = false;

    public EffectBrowserToolViewModel()
    {
        Refresh();
        SetupFavoriteSync();
    }

    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    /// <summary>
    /// エフェクトの適用回数をインクリメントします。
    /// </summary>
    private void IncrementApplyCount(string typeFullName)
    {
        if (userData.ApplyCounts.ContainsKey(typeFullName))
        {
            userData.ApplyCounts[typeFullName]++;
        }
        else
        {
            userData.ApplyCounts[typeFullName] = 1;
        }

        var entry = allEffects.FirstOrDefault(e => e.TypeFullName.Equals(typeFullName, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            entry.ApplyCount = userData.ApplyCounts[typeFullName];
        }
    }

    /// <summary>
    /// お気に入り登録状態をトグル切り替えします。
    /// </summary>
    public void ToggleFavorite(EffectEntry entry)
    {
        if (entry == null) return;

        entry.IsFavorite = !entry.IsFavorite;

        try
        {
            _isSyncingFavorites = true;
            if (entry.IsFavorite)
            {
                userData.Favorites.Add(entry.TypeFullName);
                // ホスト側が文字列リストなら追加を試みる
                if (_hostFavoritesCollection is IList hostList && hostList.IsFixedSize == false)
                {
                    bool exists = false;
                    foreach (var it in hostList)
                    {
                        if (it is string s && string.Equals(s, entry.TypeFullName, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                    }
                    if (!exists)
                    {
                        try { hostList.Add(entry.TypeFullName); } catch { }
                    }
                }
            }
            else
            {
                userData.Favorites.Remove(entry.TypeFullName);
                if (_hostFavoritesCollection is IList hostList)
                {
                    for (int i = hostList.Count - 1; i >= 0; i--)
                    {
                        if (hostList[i] is string s && string.Equals(s, entry.TypeFullName, StringComparison.OrdinalIgnoreCase))
                        {
                            try { hostList.RemoveAt(i); } catch { }
                        }
                    }
                }
            }
        }
        finally
        {
            _isSyncingFavorites = false;
        }

        UserDataManager.Save(userData);
        UpdateFavoriteCollection();
    }

    /// <summary>
    /// 任意のカスタム検索キーワードを設定します。
    /// </summary>
    public void SetCustomKeywords(EffectEntry entry, string keywords)
    {
        if (entry == null) return;

        var cleaned = keywords?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(cleaned))
        {
            userData.CustomKeywords.Remove(entry.TypeFullName);
        }
        else
        {
            userData.CustomKeywords[entry.TypeFullName] = cleaned;
        }

        entry.CustomKeywords = cleaned;
        UserDataManager.Save(userData);
        StatusMessage = $"{entry.DisplayName} の検索ワードを設定しました。";
        PerformSearch();
    }

    /// <summary>
    /// エフェクト一覧およびユーザーデータを再取得します。
    /// </summary>
    public void Refresh()
    {
        userData = UserDataManager.Load();
        allEffects.Clear();

        var (videoGroups, videoTotal, audioGroups, audioTotal, allEntries) = CollectAllEffects(userData.Favorites, userData.CustomKeywords, userData.ApplyCounts);

        allEffects.AddRange(allEntries);

        VideoEffectGroups.Clear();
        foreach (var group in videoGroups)
        {
            VideoEffectGroups.Add(group);
        }
        VideoEffectCount = videoTotal;

        AudioEffectGroups.Clear();
        foreach (var group in audioGroups)
        {
            AudioEffectGroups.Add(group);
        }
        AudioEffectCount = audioTotal;

        // コンボの読み込み
        Combos.Clear();
        foreach (var c in userData.Combos)
        {
            Combos.Add(c);
        }

        // タグの読み込み
        CustomTagNames.Clear();
        foreach (var tagName in userData.CustomTags.Keys)
        {
            CustomTagNames.Add(tagName);
        }
        if (CustomTagNames.Count > 0 && string.IsNullOrEmpty(SelectedCustomTag))
        {
            SelectedCustomTag = CustomTagNames[0];
        }

        UpdateFavoriteCollection();
        UpdateRecentCollection();
        PerformSearch();
        UpdateActiveEffects();
    }

    private void UpdateFavoriteCollection()
    {
        FavoriteEffects.Clear();
        var favs = allEffects.Where(e => e.IsFavorite).OrderBy(e => e.DisplayName);
        foreach (var f in favs)
        {
            FavoriteEffects.Add(f);
        }
        FavoriteEffectCount = FavoriteEffects.Count;
        OnPropertyChanged(nameof(HasFavoriteEffects));
    }

    private void UpdateRecentCollection()
    {
        RecentEffects.Clear();
        var typeToEntry = allEffects.GroupBy(e => e.TypeFullName, StringComparer.OrdinalIgnoreCase)
                                   .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var typeName in userData.RecentHistory)
        {
            if (typeToEntry.TryGetValue(typeName, out var entry))
            {
                RecentEffects.Add(entry);
            }
        }
        RecentEffectCount = RecentEffects.Count;
        OnPropertyChanged(nameof(HasRecentEffects));
    }

    private void UpdateCustomTagEffects()
    {
        CustomTagEffects.Clear();
        if (string.IsNullOrEmpty(SelectedCustomTag)) return;

        if (userData.CustomTags.TryGetValue(SelectedCustomTag, out var list))
        {
            var typeToEntry = allEffects.GroupBy(e => e.TypeFullName, StringComparer.OrdinalIgnoreCase)
                                       .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var typeName in list)
            {
                if (typeToEntry.TryGetValue(typeName, out var entry))
                {
                    CustomTagEffects.Add(entry);
                }
            }
        }
    }

    /// <summary>
    /// 新規のカスタムタグ（フォルダ）を作成し、エフェクトを登録します。
    /// </summary>
    public void AddEffectToTag(string tagName, EffectEntry entry)
    {
        if (string.IsNullOrWhiteSpace(tagName) || entry == null) return;

        var name = tagName.Trim();
        if (!userData.CustomTags.TryGetValue(name, out var list))
        {
            list = new List<string>();
            userData.CustomTags[name] = list;
        }

        if (!list.Contains(entry.TypeFullName))
        {
            list.Add(entry.TypeFullName);
            UserDataManager.Save(userData);
            Refresh();
            StatusMessage = $"{entry.DisplayName} をフォルダ「{name}」に追加しました。";
        }
    }

    /// <summary>
    /// カスタムタグ内のエフェクトを移動する（並び替え）
    /// </summary>
    public void MoveEffectInTag(string tagName, int fromIndex, int toIndex)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;
        if (!userData.CustomTags.TryGetValue(tagName, out var list)) return;
        if (fromIndex < 0 || fromIndex >= list.Count) return;
        if (toIndex < 0) toIndex = 0;
        if (toIndex > list.Count) toIndex = list.Count;
        if (fromIndex == toIndex || fromIndex + 1 == toIndex) return; // no-op

        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        // adjust target index if removing earlier in list
        if (toIndex > fromIndex) toIndex--;
        list.Insert(toIndex, item);
        UserDataManager.Save(userData);
        UpdateCustomTagEffects();
        StatusMessage = $"フォルダ内のエフェクトを移動しました。";
    }

    /// <summary>
    /// カスタムタグからエフェクトを削除する
    /// </summary>
    public void RemoveEffectFromTag(string tagName, EffectEntry entry)
    {
        if (string.IsNullOrWhiteSpace(tagName) || entry == null) return;
        if (!userData.CustomTags.TryGetValue(tagName, out var list)) return;
        if (list.Remove(entry.TypeFullName))
        {
            UserDataManager.Save(userData);
            UpdateCustomTagEffects();
            StatusMessage = $"{entry.DisplayName} をフォルダ「{tagName}」から削除しました。";
        }
    }

    /// <summary>
    /// カスタムタグ（フォルダ）自体を削除します。
    /// </summary>
    public void RemoveCustomTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;
        if (userData.CustomTags.Remove(tagName))
        {
            UserDataManager.Save(userData);
            Refresh();
            StatusMessage = $"フォルダ「{tagName}」を削除しました。";
        }
    }

    /// <summary>
    /// 選択中のエフェクトをタイムライン上の選択中アイテムに適用します。
    /// </summary>
    public bool ApplyEffect(EffectEntry entry)
    {
        if (entry == null || entry.EffectType == null) return false;

        try
        {
            var items = GetSelectedTimelineItems();
            if (items == null || items.Count == 0)
            {
                StatusMessage = "選択中のアイテム（対応タイプ）がありません。";
                return false;
            }

            int appliedCount = 0;
            string propertyName = entry.Kind == EffectKind.Video ? "VideoEffects" : "AudioEffects";

            foreach (var item in items)
            {
                object? newEffectInstance;
                try
                {
                    newEffectInstance = Activator.CreateInstance(entry.EffectType);
                }
                catch
                {
                    continue;
                }

                if (newEffectInstance == null) continue;

                if (ApplyEffectInstanceToItem(item, propertyName, newEffectInstance))
                {
                    appliedCount++;
                }
            }

            if (appliedCount > 0)
            {
                StatusMessage = $"{entry.DisplayName} を {appliedCount} 件のアイテムに適用しました。";

                // 履歴の更新・保存
                userData.RecentHistory.Remove(entry.TypeFullName);
                userData.RecentHistory.Insert(0, entry.TypeFullName);
                if (userData.RecentHistory.Count > 20)
                {
                    userData.RecentHistory = userData.RecentHistory.Take(20).ToList();
                }

                IncrementApplyCount(entry.TypeFullName);
                UserDataManager.Save(userData);
                UpdateRecentCollection();
                UpdateActiveEffects();

                return true;
            }

            StatusMessage = "適用に失敗しました（対応していないアイテムです）。";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"適用エラー: {ex.Message}";
            return false;
        }
    }



    /// <summary>
    /// 新しい「エフェクトコンボ（組み合わせ）」を保存します。
    /// </summary>
    public void SaveCombo(string comboName, List<EffectEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(comboName) || entries == null || entries.Count == 0) return;

        var combo = new ComboModel
        {
            Name = comboName.Trim(),
            EffectTypeFullNames = entries.Select(e => e.TypeFullName).ToList()
        };

        userData.Combos.Add(combo);
        UserDataManager.Save(userData);
        Combos.Add(combo);
        StatusMessage = $"コンボ「{combo.Name}」を保存しました。";
    }

    /// <summary>
    /// エフェクトコンボを適用します。
    /// </summary>
    public bool ApplyCombo(ComboModel combo)
    {
        if (combo == null || combo.EffectTypeFullNames.Count == 0) return false;

        int successCount = 0;
        foreach (var typeName in combo.EffectTypeFullNames)
        {
            var entry = allEffects.FirstOrDefault(e => e.TypeFullName.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                if (ApplyEffect(entry))
                {
                    successCount++;
                }
            }
        }

        if (successCount > 0)
        {
            StatusMessage = $"コンボ「{combo.Name}」のエフェクト（{successCount}個）を適用しました。";
            return true;
        }
        return false;
    }

    /// <summary>
    /// 現在選択中のアイテムに付与されているアクティブエフェクト一覧を更新します。
    /// </summary>
    public void UpdateActiveEffects()
    {
        SelectedItemsEffects.Clear();
        try
        {
            var items = GetSelectedTimelineItems();
            if (items == null) return;

            foreach (var item in items)
            {
                var itemType = item.GetType();
                
                // 映像エフェクトの抽出
                var videoProp = GetCachedProperty(itemType, "VideoEffects") ?? FindFallbackEffectProperty(itemType, "VideoEffects");
                if (videoProp != null && videoProp.GetValue(item) is IEnumerable videoList)
                {
                    foreach (var eff in videoList)
                    {
                        if (eff != null)
                        {
                            SelectedItemsEffects.Add(new ActiveEffectViewModel(item, eff, videoProp, UpdateActiveEffects));
                        }
                    }
                }

                // 音声エフェクトの抽出
                var audioProp = GetCachedProperty(itemType, "AudioEffects") ?? FindFallbackEffectProperty(itemType, "AudioEffects");
                if (audioProp != null && audioProp.GetValue(item) is IEnumerable audioList)
                {
                    foreach (var eff in audioList)
                    {
                        if (eff != null)
                        {
                            SelectedItemsEffects.Add(new ActiveEffectViewModel(item, eff, audioProp, UpdateActiveEffects));
                        }
                    }
                }
            }
        }
        catch
        {
            // エラー時はリストクリアのまま
        }

        NotifyHostItemEditorUpdate();
    }

    /// <summary>
    /// エフェクトインスタンスを実アイテムのプロパティに挿入します。
    /// </summary>
    private static bool ApplyEffectInstanceToItem(object item, string propertyName, object newEffectInstance)
    {
        var targetEffectProp = GetCachedProperty(item.GetType(), propertyName)
            ?? FindFallbackEffectProperty(item.GetType(), propertyName);

        if (targetEffectProp != null)
        {
            var currentList = targetEffectProp.GetValue(item);
            if (currentList != null)
            {
                var addMethod = GetCachedAddMethod(currentList.GetType(), newEffectInstance.GetType());
                if (addMethod != null)
                {
                    var newList = addMethod.Invoke(currentList, new[] { newEffectInstance });
                    targetEffectProp.SetValue(item, newList);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// タイムラインで選択中の実アイテム（IItemのリスト）を取得します。
    /// </summary>
    private static List<object> GetSelectedTimelineItems()
    {
        var list = new List<object>();
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow == null) return list;

        var mainVm = mainWindow.DataContext;
        if (mainVm == null) return list;

        var timelineVmProp = GetCachedProperty(mainVm.GetType(), "ActiveTimelineViewModel");
        var timelineVm = timelineVmProp?.GetValue(mainVm);
        if (timelineVm == null) return list;

        var itemsProp = GetCachedProperty(timelineVm.GetType(), "Items");
        if (itemsProp?.GetValue(timelineVm) is not IEnumerable timelineItems) return list;

        foreach (var itemVm in timelineItems)
        {
            if (itemVm == null) continue;

            var isSelectedProp = GetCachedProperty(itemVm.GetType(), "IsSelected");
            if (isSelectedProp?.GetValue(itemVm) is not true) continue;

            var itemProp = GetCachedProperty(itemVm.GetType(), "Item");
            var item = itemProp?.GetValue(itemVm);
            if (item != null)
            {
                list.Add(item);
            }
        }
        return list;
    }

    private static bool _isNotifyingHostItemEditor = false;

    /// <summary>
    /// YMM4本体の ItemEditorViewModel を取得します。
    /// </summary>
    public static object? ResolveItemEditorViewModel()
    {
        try
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow?.DataContext == null) return null;

            var mainVm = mainWindow.DataContext;
            var mainVmType = mainVm.GetType();

            // 1. itemEditorAreaViewModel フィールドから取得
            var field = mainVmType.GetField("itemEditorAreaViewModel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var areaVm = field.GetValue(mainVm);
                if (areaVm != null)
                {
                    var vmProp = areaVm.GetType().GetProperty("ViewModel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var vm = vmProp?.GetValue(areaVm);
                    if (vm != null) return vm;
                }
            }

            // 2. AnchorableAreaViewModels から探す
            var anchorProp = mainVmType.GetProperty("AnchorableAreaViewModels", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (anchorProp?.GetValue(mainVm) is IEnumerable anchorables)
            {
                foreach (var item in anchorables)
                {
                    if (item == null) continue;
                    var vmProp = item.GetType().GetProperty("ViewModel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var vm = vmProp?.GetValue(item);
                    if (vm != null && (vm.GetType().Name == "ItemEditorViewModel" || vm.GetType().FullName?.EndsWith("ItemEditorViewModel") == true))
                    {
                        return vm;
                    }
                }
            }
        }
        catch
        {
            // エラー時はnull
        }

        return null;
    }

    /// <summary>
    /// YMM4本体のアイテム設定画面（ItemEditorViewModel）の表示内容を強制更新します。
    /// </summary>
    public static void NotifyHostItemEditorUpdate()
    {
        if (_isNotifyingHostItemEditor) return;

        try
        {
            _isNotifyingHostItemEditor = true;
            var vm = ResolveItemEditorViewModel();
            if (vm != null)
            {
                var updateMethod = vm.GetType().GetMethod("UpdateSelectItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                updateMethod?.Invoke(vm, null);
            }
        }
        catch
        {
        }
        finally
        {
            _isNotifyingHostItemEditor = false;
        }
    }

    private static PropertyInfo? GetCachedProperty(Type type, string propertyName)
    {
        return NamedPropertyCache.GetOrAdd((type, propertyName), key =>
            key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }

    private static PropertyInfo? FindFallbackEffectProperty(Type type, string propertyName)
    {
        return NamedPropertyCache.GetOrAdd((type, "Fallback_" + propertyName), key =>
        {
            foreach (var prop in key.Item1.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (prop.Name.EndsWith(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return prop;
                }
            }
            return null;
        });
    }

    #region Favorite sync with host (best-effort)

    private void SetupFavoriteSync()
    {
        try
        {            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null) return;
            var root = mainWindow.DataContext;
            if (root == null) return;
            // try find an IList<string> or list-like property named *Favorite*
            var candidate = FindStringListInObject(root);
            if (candidate != null)
            {                _hostFavoritesCollection = candidate;
                if (candidate is INotifyCollectionChanged notifier)
                {                    _hostFavoritesNotifier = notifier;                    _hostFavoritesNotifier.CollectionChanged += OnHostFavoritesChanged;                }
                // merge both sides (union)                SyncFromHostToLocal();            }        }        catch { }    }

    private IList? FindStringListInObject(object obj)
    {        // check the object itself        if (obj is IList list)        {            // if list contains strings or appears typed as string list, accept it            foreach (var it in list)            {                if (it == null) continue;                if (it is string) return list;                break; // not string elements            }        }        // inspect properties (shallow)        var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);        foreach (var p in props)        {            try            {                var val = p.GetValue(obj);                if (val == null) continue;                if (val is IList l)                {                    bool likelyStringList = false;                    if (l.Count > 0)                    {                        foreach (var it in l)                        {                            if (it is string) { likelyStringList = true; break; }                            // if item is object with TypeFullName property, treat as not string list                            var t = it.GetType().GetProperty("TypeFullName");                            if (t != null) { likelyStringList = false; break; }                        }                    }                    else                    {                        // empty list - infer from generic arg if any                        var ifaces = l.GetType().GetInterfaces();                        foreach (var iface in ifaces)                        {                            if (iface.IsGenericType && iface.GetGenericArguments()[0] == typeof(string)) { likelyStringList = true; break; }                        }                    }                    if (likelyStringList) return l;                }            }            catch { }        }        return null;    }

    private void OnHostFavoritesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {        if (_isSyncingFavorites) return;        try        {            _isSyncingFavorites = true;            // Recompute local favorites from host list contents (string-based)            SyncFromHostToLocal();        }        finally        {            _isSyncingFavorites = false;        }    }

    private void SyncFromHostToLocal()
    {        if (_hostFavoritesCollection == null) return;        var hostSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);        foreach (var it in _hostFavoritesCollection)        {            if (it is string s) hostSet.Add(s);        }        // union: keep both sides' items        var union = new HashSet<string>(userData.Favorites, StringComparer.OrdinalIgnoreCase);        union.UnionWith(hostSet);        userData.Favorites = new HashSet<string>(union, StringComparer.OrdinalIgnoreCase);
        // update in-memory entries' IsFavorite flags
        foreach (var entry in allEffects)
        {
            entry.IsFavorite = union.Contains(entry.TypeFullName);
        }        // ensure host has union as well (if modifiable)        if (_hostFavoritesCollection is IList hostList && hostList.IsFixedSize == false)        {            // remove strings not in union            for (int i = hostList.Count - 1; i >= 0; i--)            {                if (hostList[i] is string s)                {                    if (!union.Contains(s))                    {                        try { hostList.RemoveAt(i); } catch { }                    }                }            }            // add missing ones            foreach (var s in union)            {                bool found = false;                foreach (var it in hostList)                {                    if (it is string ss && string.Equals(ss, s, StringComparison.OrdinalIgnoreCase)) { found = true; break; }                }                if (!found)                {                    try { hostList.Add(s); } catch { }                }            }        }        UserDataManager.Save(userData);        UpdateFavoriteCollection();    }

    #endregion

    private static MethodInfo? GetCachedAddMethod(Type listType, Type itemType)
    {
        return AddMethodCache.GetOrAdd(listType, type =>
            type.GetMethod("Add", new[] { itemType })
            ?? type.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1));
    }

    /// <summary>
    /// 検索処理。カスタムキーワード（検索エイリアス）も部分一致の対象に含めます。
    /// </summary>
    private void PerformSearch()
    {
        SearchResults.Clear();

        var query = SearchQuery?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            foreach (var item in allEffects)
            {
                SearchResults.Add(item);
            }
        }
        else
        {
            var filtered = allEffects.Where(e =>
                e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.OriginalName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.TypeFullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.CustomKeywords.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filtered)
            {
                SearchResults.Add(item);
            }
        }

        SearchResultCount = SearchResults.Count;
        OnPropertyChanged(nameof(HasSearchResults));
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.FullName;
        if (string.IsNullOrEmpty(name)) return true;

        return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("UIAutomation", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("SharpDX", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("DirectX", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Accessibility", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 映像・音声エフェクトを一括収集します（お気に入り・カスタムワード・適用回数マッピング付き）。
    /// </summary>
    private static (List<EffectCategoryGroup> VideoGroups, int VideoTotal, List<EffectCategoryGroup> AudioGroups, int AudioTotal, List<EffectEntry> AllEntries) CollectAllEffects(
        HashSet<string> favoriteSet,
        Dictionary<string, string> customKeywordsMap,
        Dictionary<string, int> applyCountsMap)
    {
        var videoCategorized = new List<(string Category, EffectEntry Entry)>();
        var audioCategorized = new List<(string Category, EffectEntry Entry)>();
        var foundVideoTypes = new HashSet<Type>();
        var foundAudioTypes = new HashSet<Type>();
        var allEntries = new List<EffectEntry>();

        var videoBaseType = typeof(VideoEffectBase);
        var audioBaseType = typeof(AudioEffectBase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (IsSystemAssembly(assembly)) continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type is null || type.IsAbstract || !type.IsClass) continue;

                bool isVideo = videoBaseType.IsAssignableFrom(type);
                bool isAudio = audioBaseType.IsAssignableFrom(type);

                if (!isVideo && !isAudio) continue;

                if (isVideo && foundVideoTypes.Add(type))
                {
                    ProcessEffectType<VideoEffectAttribute>(type, EffectKind.Video, favoriteSet, customKeywordsMap, applyCountsMap, videoCategorized, allEntries);
                }

                if (isAudio && foundAudioTypes.Add(type))
                {
                    ProcessEffectType<AudioEffectAttribute>(type, EffectKind.Audio, favoriteSet, customKeywordsMap, applyCountsMap, audioCategorized, allEntries);
                }
            }
        }

        var videoGroups = BuildCategoryGroups(videoCategorized);
        var audioGroups = BuildCategoryGroups(audioCategorized);

        return (videoGroups, foundVideoTypes.Count, audioGroups, foundAudioTypes.Count, allEntries);
    }

    private static void ProcessEffectType<TAttr>(
        Type type,
        EffectKind kind,
        HashSet<string> favoriteSet,
        Dictionary<string, string> customKeywordsMap,
        Dictionary<string, int> applyCountsMap,
        List<(string Category, EffectEntry Entry)> categorizedList,
        List<EffectEntry> allEntries)
        where TAttr : Attribute
    {
        var (originalName, originalCategories, keywords) = ReadEffectMetadata<TAttr>(type);
        var displayName = EffectLocalizer.LocalizeEffectName(originalName);
        var typeFullName = type.FullName ?? type.Name;
        
        bool isFavorite = favoriteSet.Contains(typeFullName);
        customKeywordsMap.TryGetValue(typeFullName, out var customKeywords);
        applyCountsMap.TryGetValue(typeFullName, out var applyCount);

        foreach (var rawCategory in originalCategories)
        {
            var category = EffectLocalizer.LocalizeCategory(rawCategory);
            var entry = new EffectEntry(
                displayName: displayName,
                originalName: originalName,
                category: category,
                originalCategory: rawCategory,
                typeFullName: typeFullName,
                effectType: type,
                kind: kind,
                isFavorite: isFavorite,
                customKeywords: customKeywords ?? string.Empty,
                applyCount: applyCount,
                keywords: keywords);

            categorizedList.Add((category, entry));
            if (!allEntries.Contains(entry))
            {
                allEntries.Add(entry);
            }
        }
    }

    private static List<EffectCategoryGroup> BuildCategoryGroups(List<(string Category, EffectEntry Entry)> categorized)
    {
        return categorized
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EffectCategoryGroup(
                g.Key,
                g.Select(x => x.Entry)
                 .Distinct()
                 .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                 .ToList()))
            .ToList();
    }

    private static (string Name, string[] Categories, string Keywords) ReadEffectMetadata<TAttr>(Type type)
        where TAttr : Attribute
    {
        var name = type.Name;
        var categories = Array.Empty<string>();
        var keywords = Array.Empty<string>();

        var attribute = type.GetCustomAttribute(typeof(TAttr));
        if (attribute is VideoEffectAttribute videoAttr)
        {
            name = videoAttr.GetName();
            categories = videoAttr.GetCategories();
            keywords = videoAttr.GetKeywords();
        }
        else if (attribute is AudioEffectAttribute audioAttr)
        {
            name = audioAttr.GetName();
            categories = audioAttr.GetCategories();
            keywords = audioAttr.GetKeywords();
        }
        else if (attribute is not null)
        {
            var attrType = attribute.GetType();

            if (attrType.GetMethod("GetName")?.Invoke(attribute, null) is string localizedName
                && !string.IsNullOrWhiteSpace(localizedName))
            {
                name = localizedName;
            }
            else if (attrType.GetProperty("Name")?.GetValue(attribute) is string attrName
                && !string.IsNullOrWhiteSpace(attrName))
            {
                name = attrName;
            }

            if (attrType.GetMethod("GetCategories")?.Invoke(attribute, null) is IEnumerable<string> localizedCategories)
            {
                categories = localizedCategories.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
            }
            else if (attrType.GetProperty("Categories")?.GetValue(attribute) is IEnumerable<string> attrCategories)
            {
                categories = attrCategories.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
            }

            if (attrType.GetMethod("GetKeywords")?.Invoke(attribute, null) is IEnumerable<string> localizedKeywords)
            {
                keywords = localizedKeywords.Where(k => !string.IsNullOrWhiteSpace(k)).ToArray();
            }
        }

        if (categories.Length == 0)
        {
            categories = new[] { Texts.EffectEtcGroupName };
        }

        var keywordText = string.Join(" ", keywords.Select(EffectLocalizer.Localize).Where(k => !string.IsNullOrWhiteSpace(k)));
        return (name, categories, keywordText);
    }
}
