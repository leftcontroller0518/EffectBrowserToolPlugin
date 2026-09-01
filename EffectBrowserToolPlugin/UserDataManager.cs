using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EffectBrowserToolPlugin;

/// <summary>
/// プリセットのパラメータ値モデル
/// </summary>
public sealed class PresetPropertyModel
{
    public string Name { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
}

/// <summary>
/// パラメーター付きカスタムプリセットモデル
/// </summary>
public sealed class PresetModel
{
    public string PresetId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string TargetEffectTypeFullName { get; set; } = string.Empty;
    public List<PresetPropertyModel> Properties { get; set; } = new();
}

/// <summary>
/// エフェクトの組み合わせ（コンボ）モデル
/// </summary>
public sealed class ComboModel
{
    public string ComboId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<string> EffectTypeFullNames { get; set; } = new();
}

/// <summary>
/// ユーザーデータ（お気に入りリスト・使用履歴・カスタム設定）のデータモデル
/// </summary>
public sealed class UserDataModel
{
    public HashSet<string> Favorites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> RecentHistory { get; set; } = new();
    
    // 拡張機能データ
    public Dictionary<string, string> CustomKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase); // TypeFullName -> Keywords CSV
    public List<PresetModel> Presets { get; set; } = new();
    public List<ComboModel> Combos { get; set; } = new();
    public Dictionary<string, List<string>> CustomTags { get; set; } = new(StringComparer.OrdinalIgnoreCase); // TagName -> List of TypeFullName
    public Dictionary<string, int> ApplyCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase); // TypeFullName -> Counts
}

/// <summary>
/// お気に入りおよびユーザーの各種カスタム設定のローカル保存・読み込みを行うマネージャー
/// </summary>
public static class UserDataManager
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YMM4EffectBrowserPlugin");

    private static readonly string FilePath = Path.Combine(FolderPath, "user_data.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static UserDataModel Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var model = JsonSerializer.Deserialize<UserDataModel>(json, JsonOptions);
                if (model != null)
                {
                    // Dictionary等の初期化保証
                    model.CustomKeywords ??= new(StringComparer.OrdinalIgnoreCase);
                    model.Presets ??= new();
                    model.Combos ??= new();
                    model.CustomTags ??= new(StringComparer.OrdinalIgnoreCase);
                    model.ApplyCounts ??= new(StringComparer.OrdinalIgnoreCase);
                    return model;
                }
            }
        }
        catch
        {
            // 読み込み失敗時
        }

        return new UserDataModel();
    }

    public static void Save(UserDataModel model)
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            var json = JsonSerializer.Serialize(model, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 保存失敗時
        }
    }
}
