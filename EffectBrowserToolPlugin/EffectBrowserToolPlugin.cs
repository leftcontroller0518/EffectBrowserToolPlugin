using System;
using YukkuriMovieMaker.Plugin;

namespace EffectBrowserToolPlugin;

/// <summary>
/// YMM4の「ツール」メニューから呼び出せる、映像エフェクト・音声エフェクトの一覧ウィンドウを追加するプラグインです。
/// IToolPluginの最小構成（Name / ViewModelType / ViewType）のみで動作します。
/// </summary>
public class EffectBrowserToolPlugin : IToolPlugin
{
    /// <summary>
    /// ツールメニューに表示される名前です。
    /// </summary>
    public string Name => "エフェクト一覧";

    /// <summary>
    /// このツールのViewModelの型です。YMM4側でインスタンス化され、
    /// Viewのコンテキスト（DataContext）として渡されます。
    /// </summary>
    public Type ViewModelType => typeof(EffectBrowserToolViewModel);

    /// <summary>
    /// このツールのViewの型です。Windowではなく、UserControlである必要があります。
    /// </summary>
    public Type ViewType => typeof(EffectBrowserToolView);
}
