using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EffectBrowserToolPlugin;

/// <summary>
/// エフェクトの種類（映像 / 音声）
/// </summary>
public enum EffectKind
{
    Video,
    Audio
}

/// <summary>
/// 一覧に表示するエフェクト1件分のデータモデルです。
/// </summary>
public sealed class EffectEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public string DisplayName { get; }
    public string OriginalName { get; }
    public string Category { get; }
    public string OriginalCategory { get; }
    public string Keywords { get; }
    public string TypeFullName { get; }
    public Type EffectType { get; }
    public EffectKind Kind { get; }

    private bool isFavorite;
    public bool IsFavorite
    {
        get => isFavorite;
        set
        {
            if (isFavorite != value)
            {
                isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FavoriteStarIcon));
                OnPropertyChanged(nameof(FavoriteStarColor));
            }
        }
    }

    private string customKeywords = string.Empty;
    public string CustomKeywords
    {
        get => customKeywords;
        set
        {
            if (customKeywords != value)
            {
                customKeywords = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCustomKeywords));
            }
        }
    }

    public bool HasCustomKeywords => !string.IsNullOrWhiteSpace(CustomKeywords);

    private int applyCount;
    public int ApplyCount
    {
        get => applyCount;
        set
        {
            if (applyCount != value)
            {
                applyCount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// エフェクト種別の表示用テキスト
    /// </summary>
    public string KindText => Kind == EffectKind.Video ? "映像" : "音声";

    /// <summary>
    /// サブテキスト（型名/クラス名）
    /// </summary>
    public string SubText => TypeFullName;

    /// <summary>
    /// お気に入りスターアイコン ("★" または "☆")
    /// </summary>
    public string FavoriteStarIcon => IsFavorite ? "★" : "☆";

    /// <summary>
    /// スターアイコンの表示色 ("#FFD700" / "#808080")
    /// </summary>
    public string FavoriteStarColor => IsFavorite ? "#FFD700" : "#808080";

    /// <summary>
    /// ビジュアル説明ツールチップ用テキスト
    /// </summary>
    public string ToolTipDescription => $"{DisplayName} ({OriginalName})\nカテゴリ: {Category}\n型: {TypeFullName}\n使用回数: {ApplyCount}\n検索ワード: {CustomKeywords}";

    public EffectEntry(
        string displayName,
        string originalName,
        string category,
        string originalCategory,
        string typeFullName,
        Type effectType,
        EffectKind kind,
        bool isFavorite = false,
        string customKeywords = "",
        int applyCount = 0,
        string keywords = "")
    {
        DisplayName = displayName;
        OriginalName = originalName;
        Category = category;
        OriginalCategory = originalCategory;
        TypeFullName = typeFullName;
        EffectType = effectType;
        Kind = kind;
        IsFavorite = isFavorite;
        CustomKeywords = customKeywords;
        ApplyCount = applyCount;
        Keywords = keywords ?? string.Empty;
    }
}

/// <summary>
/// カテゴリ名と、そのカテゴリに属するエフェクトの一覧です。
/// </summary>
public sealed class EffectCategoryGroup
{
    public string CategoryName { get; }

    public System.Collections.Generic.IReadOnlyList<EffectEntry> Effects { get; }

    /// <summary>
    /// 見出しに表示する文字列（例: "アニメーション (12)"）。
    /// </summary>
    public string HeaderText => $"{CategoryName} ({Effects.Count})";

    public EffectCategoryGroup(string categoryName, System.Collections.Generic.IReadOnlyList<EffectEntry> effects)
    {
        CategoryName = categoryName;
        Effects = effects;
    }
}
