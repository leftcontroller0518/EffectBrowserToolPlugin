using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace EffectBrowserToolPlugin
{
    public static class Strings
    {
        private static readonly ResourceManager _rm = new ResourceManager("EffectBrowserToolPlugin.Resources.Strings", Assembly.GetExecutingAssembly());

        private static string Get(string key, params object[] args)
        {
            try
            {
                // ここでは CurrentUICulture をそのまま使い、.resx に基づくサテライトアセンブリで解決させる。
                var culture = CultureInfo.CurrentUICulture ?? CultureInfo.InvariantCulture;
                var s = _rm.GetString(key, culture) ?? _rm.GetString(key, CultureInfo.InvariantCulture) ?? string.Empty;
                if (args != null && args.Length > 0) return string.Format(s, args);
                return s;
            }
            catch
            {
                return key;
            }
        }

        public static string ToolWindowTitle => Get(nameof(ToolWindowTitle));
        public static string SetSearchKeywords => Get(nameof(SetSearchKeywords));
        public static string AddToFolder => Get(nameof(AddToFolder));
        public static string ToggleFavorite => Get(nameof(ToggleFavorite));
        public static string Apply => Get(nameof(Apply));
        public static string FolderDeleteTooltip => Get(nameof(FolderDeleteTooltip));
        public static string NoFavoritesText => Get(nameof(NoFavoritesText));
        public static string Favorites => Get(nameof(Favorites));
        public static string TabFolder => Get(nameof(TabFolder));
        public static string TabActive => Get(nameof(TabActive));
        public static string TabSearch => Get(nameof(TabSearch));
        public static string SearchPlaceholder => Get(nameof(SearchPlaceholder));
        public static string NoRecentText => Get(nameof(NoRecentText));
        public static string GetActiveEffectsButton => Get(nameof(GetActiveEffectsButton));
        public static string AddToFolderDialogTitle => Get(nameof(AddToFolderDialogTitle));
        public static string AddToFolderPrompt => Get(nameof(AddToFolderPrompt));
        public static string DefaultFolderName => Get(nameof(DefaultFolderName));
        public static string DeleteFolderConfirmTitle => Get(nameof(DeleteFolderConfirmTitle));
        public static string DeleteFolderConfirmMessage(string tag) => Get(nameof(DeleteFolderConfirmMessage), tag);
        public static string DeleteFolderButton => Get(nameof(DeleteFolderButton));
        public static string FolderDeletedMessage(string tag) => Get(nameof(FolderDeletedMessage), tag);
        public static string AddedToFolderMessage(string entry, string tag) => Get(nameof(AddedToFolderMessage), entry, tag);
        public static string MovedInFolderMessage => Get(nameof(MovedInFolderMessage));
        public static string RemovedFromFolderMessage(string entry, string tag) => Get(nameof(RemovedFromFolderMessage), entry, tag);
        public static string SearchWordSetMessage(string entry) => Get(nameof(SearchWordSetMessage), entry);
        public static string NoSelectedItemsMessage => Get(nameof(NoSelectedItemsMessage));
        public static string AppliedToItemsMessage(string entry, int count) => Get(nameof(AppliedToItemsMessage), entry, count);
        public static string ApplyFailedUnsupported => Get(nameof(ApplyFailedUnsupported));
        public static string ApplyError(string msg) => Get(nameof(ApplyError), msg);
        public static string ComboSavedMessage(string name) => Get(nameof(ComboSavedMessage), name);
        public static string ComboAppliedMessage(string name, int count) => Get(nameof(ComboAppliedMessage), name, count);
        public static string DeleteActiveEffectTooltip => Get(nameof(DeleteActiveEffectTooltip));
        public static string SearchKeywordsTitle => Get(nameof(SearchKeywordsTitle));
        public static string SearchKeywordsPromptFormat(string entry) => Get(nameof(SearchKeywordsPromptFormat), entry);
        public static string ToolTipDescriptionFormat(string display, string orig, string category, string type, int count, string keywords) => Get(nameof(ToolTipDescriptionFormat), display, orig, category, type, count, keywords);
        public static string KindVideo => Get(nameof(KindVideo));
        public static string KindAudio => Get(nameof(KindAudio));
        public static string HeaderEffectsList => Get(nameof(HeaderEffectsList));
        public static string RefreshButton => Get(nameof(RefreshButton));
        public static string TabHistory => Get(nameof(TabHistory));
        public static string EffectEtcGroupName => Get(nameof(EffectEtcGroupName));
    }
}