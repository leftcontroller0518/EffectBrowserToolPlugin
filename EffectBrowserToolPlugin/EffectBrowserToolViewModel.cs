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
    
    // 拡張機能用のコレクション
    public ObservableCollection<PresetModel> Presets { get; } = new();
    public ObservableCollection<ComboModel> Combos { get; } = new();
    public ObservableCollection<string> CustomTagNames { get; } = new();
    public ObservableCollection<EffectEntry> CustomTagEffects { get; } = new();
    public ObservableCollection<ActiveEffectViewModel> SelectedItemsEffects { get; } = new();

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
    public bool HasPresets => Presets.Count > 0;

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

    public EffectBrowserToolViewModel()
    {
        Refresh();
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

        if (entry.IsFavorite)
        {
            userData.Favorites.Add(entry.TypeFullName);
        }
        else
        {
            userData.Favorites.Remove(entry.TypeFullName);
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

        // プリセット・コンボの読み込み
        Presets.Clear();
        foreach (var p in userData.Presets)
        {
            Presets.Add(p);
        }
        OnPropertyChanged(nameof(HasPresets));

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
    /// パラメーター付きプリセットを選択中アイテムから新規保存します。
    /// </summary>
    public bool SavePresetFromSelected(string presetName, EffectEntry entry)
    {
        if (string.IsNullOrWhiteSpace(presetName) || entry == null) return false;

        try
        {
            var items = GetSelectedTimelineItems();
            if (items == null || items.Count == 0)
            {
                StatusMessage = "タイムラインアイテムが選択されていません。";
                return false;
            }

            object? targetEffectInstance = null;
            string propertyName = entry.Kind == EffectKind.Video ? "VideoEffects" : "AudioEffects";

            // 選択中のアイテムから該当エフェクトインスタンスを探す
            foreach (var item in items)
            {
                var targetEffectProp = GetCachedProperty(item.GetType(), propertyName)
                    ?? FindFallbackEffectProperty(item.GetType(), propertyName);

                if (targetEffectProp != null && targetEffectProp.GetValue(item) is IEnumerable currentList)
                {
                    foreach (var eff in currentList)
                    {
                        if (eff != null && eff.GetType() == entry.EffectType)
                        {
                            targetEffectInstance = eff;
                            break;
                        }
                    }
                }
                if (targetEffectInstance != null) break;
            }

            if (targetEffectInstance == null)
            {
                StatusMessage = $"選択中アイテムに {entry.DisplayName} が付与されていません。";
                return false;
            }

            // プロパティ値の抽出
            var preset = new PresetModel
            {
                Name = presetName.Trim(),
                TargetEffectTypeFullName = entry.TypeFullName
            };

            var props = entry.EffectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanRead || !p.CanWrite || p.Name == "IsEnabled") continue;

                var val = p.GetValue(targetEffectInstance);
                if (val != null && (p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType.IsEnum || p.PropertyType == typeof(decimal)))
                {
                    preset.Properties.Add(new PresetPropertyModel
                    {
                        Name = p.Name,
                        ValueJson = JsonSerializer.Serialize(val),
                        TypeName = p.PropertyType.AssemblyQualifiedName ?? p.PropertyType.FullName
                    });
                }
            }

            userData.Presets.Add(preset);
            UserDataManager.Save(userData);
            Presets.Add(preset);
            OnPropertyChanged(nameof(HasPresets));

            StatusMessage = $"プリセット「{preset.Name}」を保存しました。";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"プリセット保存失敗: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// パラメーター付きプリセットを選択アイテムに適用します。
    /// </summary>
    public bool ApplyPreset(PresetModel preset)
    {
        if (preset == null) return false;

        try
        {
            var effectType = Type.GetType(preset.TargetEffectTypeFullName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(preset.TargetEffectTypeFullName))
                    .FirstOrDefault(t => t != null);

            if (effectType == null)
            {
                StatusMessage = "エフェクトの型情報が見つかりません。";
                return false;
            }

            var items = GetSelectedTimelineItems();
            if (items == null || items.Count == 0)
            {
                StatusMessage = "選択中のアイテムがありません。";
                return false;
            }

            int appliedCount = 0;
            bool isVideo = typeof(VideoEffectBase).IsAssignableFrom(effectType);
            string propertyName = isVideo ? "VideoEffects" : "AudioEffects";

            foreach (var item in items)
            {
                object? newEffectInstance;
                try
                {
                    newEffectInstance = Activator.CreateInstance(effectType);
                }
                catch
                {
                    continue;
                }

                if (newEffectInstance == null) continue;

                // プリセットのプロパティ値を復元
                foreach (var pModel in preset.Properties)
                {
                    var pInfo = effectType.GetProperty(pModel.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (pInfo != null && pInfo.CanWrite)
                    {
                        var targetType = Type.GetType(pModel.TypeName);
                        if (targetType != null)
                        {
                            var val = JsonSerializer.Deserialize(pModel.ValueJson, targetType);
                            pInfo.SetValue(newEffectInstance, val);
                        }
                    }
                }

                if (ApplyEffectInstanceToItem(item, propertyName, newEffectInstance))
                {
                    appliedCount++;
                }
            }

            if (appliedCount > 0)
            {
                StatusMessage = $"プリセット「{preset.Name}」を適用しました。";
                UpdateActiveEffects();
                return true;
            }

            StatusMessage = "適用に失敗しました。";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"プリセット適用エラー: {ex.Message}";
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
