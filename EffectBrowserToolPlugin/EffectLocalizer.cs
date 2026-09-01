using System;
using System.Collections.Concurrent;
using System.Reflection;
using YukkuriMovieMaker.Resources.Localization;

namespace EffectBrowserToolPlugin;

/// <summary>
/// エフェクト名・カテゴリ名を、YMM4本体と同じ Texts リソース経由で日本語化するヘルパーです。
/// 本体の VideoEffectAttribute.GetName() は "YMM4Key_" プレフィックスを Texts のプロパティ名として解決します。
/// GetCategories() は ResourceType が無い場合にキー文字列をそのまま返すため、こちらでも同じ解決を行います。
/// </summary>
public static class EffectLocalizer
{
    private const string Ymm4KeyPrefix = "YMM4Key_";
    private static readonly ConcurrentDictionary<string, PropertyInfo?> TextsPropertyCache = new(StringComparer.Ordinal);

    /// <summary>
    /// カテゴリ名をYMM4の表示言語で返します。
    /// </summary>
    public static string LocalizeCategory(string? rawCategory)
    {
        var localized = Localize(rawCategory);
        return string.IsNullOrWhiteSpace(localized) ? Texts.EffectEtcGroupName : localized;
    }

    /// <summary>
    /// エフェクト名をYMM4の表示言語で返します。
    /// </summary>
    public static string LocalizeEffectName(string? rawName)
    {
        var localized = Localize(rawName);
        return string.IsNullOrWhiteSpace(localized) ? "名前なし" : localized;
    }

    /// <summary>
    /// リソースキーまたは既に解決済みの文字列を、可能なら Texts から日本語（現在の言語）にします。
    /// </summary>
    public static string Localize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();

        if (trimmed.StartsWith(Ymm4KeyPrefix, StringComparison.Ordinal))
        {
            var fromKey = TryGetTextsValue(trimmed.Substring(Ymm4KeyPrefix.Length));
            if (!string.IsNullOrEmpty(fromKey))
            {
                return fromKey;
            }
        }

        var fromPropertyName = TryGetTextsValue(trimmed);
        if (!string.IsNullOrEmpty(fromPropertyName))
        {
            return fromPropertyName;
        }

        return trimmed;
    }

    private static string? TryGetTextsValue(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return null;
        }

        var property = TextsPropertyCache.GetOrAdd(
            propertyName,
            name => typeof(Texts).GetProperty(name, BindingFlags.Public | BindingFlags.Static));

        try
        {
            return property?.GetValue(null)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
