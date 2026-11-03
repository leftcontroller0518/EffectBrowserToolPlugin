using System;
using System.Reflection;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffectBrowserToolPlugin;

/// <summary>
/// タイムラインで選択中のアイテムに現在適用されているエフェクトを管理するViewModelです。
/// </summary>
public sealed class ActiveEffectViewModel
{
    private readonly object item;
    private readonly object effectInstance;
    private readonly PropertyInfo effectsProperty;
    private readonly Action onUpdated;

    public string DisplayName { get; }
    public string TypeFullName { get; }
    
    private bool isEnabled;
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled != value)
            {
                isEnabled = value;
                SetEnabledState(value);
            }
        }
    }

    public ActiveEffectViewModel(object item, object effectInstance, PropertyInfo effectsProperty, Action onUpdated)
    {
        this.item = item;
        this.effectInstance = effectInstance;
        this.effectsProperty = effectsProperty;
        this.onUpdated = onUpdated;

        TypeFullName = effectInstance.GetType().FullName ?? effectInstance.GetType().Name;
        DisplayName = ResolveDisplayName(effectInstance);

        // 現在のIsEnabled状態をリフレクションで取得
        var isEnabledProp = effectInstance.GetType().GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance);
        isEnabled = isEnabledProp?.GetValue(effectInstance) is not false;
    }

    private static string ResolveDisplayName(object effectInstance)
    {
        if (effectInstance is IVideoEffect videoEffect && !string.IsNullOrWhiteSpace(videoEffect.Label))
        {
            return videoEffect.Label;
        }

        if (effectInstance is IAudioEffect audioEffect && !string.IsNullOrWhiteSpace(audioEffect.Label))
        {
            return audioEffect.Label;
        }

        var type = effectInstance.GetType();
        var videoAttr = type.GetCustomAttribute<VideoEffectAttribute>();
        if (videoAttr is not null)
        {
            return EffectLocalizer.LocalizeEffectName(videoAttr.GetName());
        }

        var audioAttr = type.GetCustomAttribute<AudioEffectAttribute>();
        if (audioAttr is not null)
        {
            return EffectLocalizer.LocalizeEffectName(audioAttr.GetName());
        }

        return EffectLocalizer.LocalizeEffectName(type.Name);
    }

    /// <summary>
    /// エフェクトの有効/無効状態を更新します。
    /// </summary>
    private void SetEnabledState(bool enabled)
    {
        try
        {
            var isEnabledProp = effectInstance.GetType().GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance);
            if (isEnabledProp != null && isEnabledProp.CanWrite)
            {
                isEnabledProp.SetValue(effectInstance, enabled);
                // YMM4のタイムライン変更通知を促すため、リスト全体を再代入
                var currentList = effectsProperty.GetValue(item);
                if (currentList != null)
                {
                    effectsProperty.SetValue(item, currentList);
                }
                onUpdated?.Invoke();
            }
        }
        catch
        {
            // エラー時
        }
    }

    /// <summary>
    /// このエフェクトをアイテムから削除します。
    /// </summary>
    public void Delete()
    {
        try
        {
            var currentList = effectsProperty.GetValue(item);
            if (currentList != null)
            {
                var removeMethod = currentList.GetType().GetMethod("Remove", new[] { effectInstance.GetType() })
                    ?? currentList.GetType().GetMethods().FirstOrDefault(m => m.Name == "Remove" && m.GetParameters().Length == 1);

                if (removeMethod != null)
                {
                    var newList = removeMethod.Invoke(currentList, new[] { effectInstance });
                    effectsProperty.SetValue(item, newList);
                    onUpdated?.Invoke();
                }
            }
        }
        catch
        {
            // エラー時
        }
    }
}
