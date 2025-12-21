using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.Localization.Tables;

namespace XiaoZhi.Unity
{
    public static class Lang
    {
        private const string Table = "Lang";

        private static StringTable _tableRef;

        public static string Code => LocalizationSettings.SelectedLocale.Identifier.Code;

        public static async UniTask LoadLocale(Locale locale = null)
        {
            locale ??= await LocalizationSettings.SelectedLocaleAsync;
            _tableRef = await LocalizationSettings.StringDatabase.GetTableAsync(Table, locale);
        }

        public static async UniTask SetLocale(Locale locale)
        {
            var current = await LocalizationSettings.SelectedLocaleAsync;
            if (current == locale) return;
            await LoadLocale(locale);
            LocalizationSettings.SelectedLocale = locale;
            LocalizationSettings.StringDatabase.ReleaseTable(Table, current);
        }

        public static string Get(string key, params object[] args)
        {
            var entry = _tableRef.GetEntry(key);
            return entry.GetLocalizedString(args);
        }

        public static LocalizedString GetRef(string key, params KeyValuePair<string, IVariable>[] args)
        {
            var reference = new LocalizedString(Table, key);
            foreach (var arg in args) reference.Add(arg);
            return reference;
        }
    }
}