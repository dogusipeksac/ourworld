using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Gridroam.Core
{
    /// <summary>
    /// Firebase Remote Config parameter keys — identical schema to Kube 2026 & Daily Hydrate.
    /// Supports Maintenance mode, Force Update, and Soft Update independently for Android & iOS.
    /// </summary>
    public static class UpdateConfigKeys
    {
        public const string MaintenanceAndroidEnabled = "maintenance_android_enabled";
        public const string MaintenanceIosEnabled = "maintenance_ios_enabled";

        public const string AndroidLatest = "update_android_latest_version_code";
        public const string AndroidMin = "update_android_min_required_version_code";
        public const string AndroidForce = "update_android_force_update";
        public const string AndroidStoreUrl = "update_android_store_url";

        public const string IosLatest = "update_ios_latest_version_code";
        public const string IosMin = "update_ios_min_required_version_code";
        public const string IosForce = "update_ios_force_update";
        public const string IosStoreUrl = "update_ios_store_url";

        public const string TitleTr = "update_title_tr";
        public const string TitleEn = "update_title_en";
        public const string MessageTr = "update_message_tr";
        public const string MessageEn = "update_message_en";
        public const string ConfirmTr = "update_confirm_tr";
        public const string ConfirmEn = "update_confirm_en";
        public const string CancelTr = "update_cancel_tr";
        public const string CancelEn = "update_cancel_en";

        public const string MaintTitleTr = "maintenance_title_tr";
        public const string MaintTitleEn = "maintenance_title_en";
        public const string MaintMessageTr = "maintenance_message_tr";
        public const string MaintMessageEn = "maintenance_message_en";

        public const string DefaultAndroidStoreUrl = "https://play.google.com/store/apps/details?id=com.dogusipeksac.ourworld";
        public const string DefaultIosStoreUrl = "https://apps.apple.com/search?term=OurWorld";
    }

    public sealed class UpdateInfo
    {
        public int LatestVersionCode;
        public int MinRequiredVersionCode;
        public string Title;
        public string Description;
        public bool ForceUpdate;
        public bool IsMaintenance;
        public string ConfirmButtonText;
        public string CancelButtonText;
        public string AndroidStoreUrl;
        public string IosStoreUrl;

        public string StoreUrl
        {
            get
            {
#if UNITY_IOS
                if (!string.IsNullOrEmpty(IosStoreUrl)) return IosStoreUrl;
                return UpdateConfigKeys.DefaultIosStoreUrl;
#else
                if (!string.IsNullOrEmpty(AndroidStoreUrl)) return AndroidStoreUrl;
                return UpdateConfigKeys.DefaultAndroidStoreUrl;
#endif
            }
        }
    }

    /// <summary>
    /// Evaluates Remote Config entries into Maintenance / Force Update / Soft Update / None decisions.
    /// Identical decision tree to Kube 2026's UpdateRuntime.
    /// </summary>
    public static class UpdateDecisionRuntime
    {
        private static bool _maintenanceAndroid;
        private static bool _maintenanceIos;
        private static long _androidLatest;
        private static long _androidMin;
        private static bool _androidForce;
        private static string _androidStoreUrl = string.Empty;
        private static long _iosLatest;
        private static long _iosMin;
        private static bool _iosForce;
        private static string _iosStoreUrl = string.Empty;

        private static string _titleTr = string.Empty;
        private static string _titleEn = string.Empty;
        private static string _messageTr = string.Empty;
        private static string _messageEn = string.Empty;
        private static string _confirmTr = string.Empty;
        private static string _confirmEn = string.Empty;
        private static string _cancelTr = string.Empty;
        private static string _cancelEn = string.Empty;
        private static string _maintTitleTr = string.Empty;
        private static string _maintTitleEn = string.Empty;
        private static string _maintMessageTr = string.Empty;
        private static string _maintMessageEn = string.Empty;

        public static void ApplyEntries(Dictionary<string, string> entries)
        {
            if (entries == null)
            {
                Clear();
                return;
            }

            _maintenanceAndroid = GetBool(entries, UpdateConfigKeys.MaintenanceAndroidEnabled);
            _maintenanceIos = GetBool(entries, UpdateConfigKeys.MaintenanceIosEnabled);

            _androidLatest = GetLong(entries, UpdateConfigKeys.AndroidLatest);
            _androidMin = GetLong(entries, UpdateConfigKeys.AndroidMin);
            _androidForce = GetBool(entries, UpdateConfigKeys.AndroidForce);
            _androidStoreUrl = GetStr(entries, UpdateConfigKeys.AndroidStoreUrl);

            _iosLatest = GetLong(entries, UpdateConfigKeys.IosLatest);
            _iosMin = GetLong(entries, UpdateConfigKeys.IosMin);
            _iosForce = GetBool(entries, UpdateConfigKeys.IosForce);
            _iosStoreUrl = GetStr(entries, UpdateConfigKeys.IosStoreUrl);

            _titleTr = GetStr(entries, UpdateConfigKeys.TitleTr);
            _titleEn = GetStr(entries, UpdateConfigKeys.TitleEn);
            _messageTr = GetStr(entries, UpdateConfigKeys.MessageTr);
            _messageEn = GetStr(entries, UpdateConfigKeys.MessageEn);
            _confirmTr = GetStr(entries, UpdateConfigKeys.ConfirmTr);
            _confirmEn = GetStr(entries, UpdateConfigKeys.ConfirmEn);
            _cancelTr = GetStr(entries, UpdateConfigKeys.CancelTr);
            _cancelEn = GetStr(entries, UpdateConfigKeys.CancelEn);

            _maintTitleTr = GetStr(entries, UpdateConfigKeys.MaintTitleTr);
            _maintTitleEn = GetStr(entries, UpdateConfigKeys.MaintTitleEn);
            _maintMessageTr = GetStr(entries, UpdateConfigKeys.MaintMessageTr);
            _maintMessageEn = GetStr(entries, UpdateConfigKeys.MaintMessageEn);
        }

        public static void Clear()
        {
            _maintenanceAndroid = _maintenanceIos = _androidForce = _iosForce = false;
            _androidLatest = _androidMin = _iosLatest = _iosMin = 0;
            _androidStoreUrl = _iosStoreUrl = string.Empty;
            _titleTr = _titleEn = _messageTr = _messageEn = string.Empty;
            _confirmTr = _confirmEn = _cancelTr = _cancelEn = string.Empty;
            _maintTitleTr = _maintTitleEn = _maintMessageTr = _maintMessageEn = string.Empty;
        }

        public static UpdateInfo BuildUpdateInfo(int currentVersionCode, string languageCode)
        {
            bool isTr = string.IsNullOrEmpty(languageCode) ||
                        languageCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

            if (PlatformMaintenance)
            {
                return new UpdateInfo
                {
                    LatestVersionCode = -1,
                    MinRequiredVersionCode = -1,
                    Title = Pick(isTr, _maintTitleTr, _maintTitleEn)
                            ?? (isTr ? "Bakım çalışması" : "Maintenance"),
                    Description = Pick(isTr, _maintMessageTr, _maintMessageEn)
                                  ?? (isTr
                                      ? "OurWorld şu an güncelleniyor. Lütfen kısa süre sonra tekrar deneyin."
                                      : "OurWorld is temporarily under maintenance. Please try again in a moment."),
                    ForceUpdate = true,
                    IsMaintenance = true,
                    ConfirmButtonText = string.Empty,
                    CancelButtonText = null,
                    AndroidStoreUrl = ResolveAndroidStore(),
                    IosStoreUrl = ResolveIosStore()
                };
            }

            long latest = PlatformLatest;
            long minRequired = PlatformMin;
            bool forceFlag = PlatformForce;

            if (latest <= 0 || latest <= currentVersionCode)
            {
                return null;
            }

            bool isForce = forceFlag && currentVersionCode < minRequired;

            return new UpdateInfo
            {
                LatestVersionCode = (int)latest,
                MinRequiredVersionCode = (int)minRequired,
                Title = Pick(isTr, _titleTr, _titleEn)
                        ?? (isTr ? "Yeni sürüm hazır" : "Update available"),
                Description = Pick(isTr, _messageTr, _messageEn)
                              ?? (isTr
                                  ? "OurWorld için yeni bir sürüm yayınlandı. Oyuncuların istediği yenilikler için hemen güncelle!"
                                  : "A new version of OurWorld is available. Update now for the latest community features!"),
                ForceUpdate = isForce,
                IsMaintenance = false,
                ConfirmButtonText = Pick(isTr, _confirmTr, _confirmEn)
                                    ?? (isTr ? "Güncelle" : "Update"),
                CancelButtonText = isForce
                    ? null
                    : (Pick(isTr, _cancelTr, _cancelEn) ?? (isTr ? "Sonra" : "Later")),
                AndroidStoreUrl = ResolveAndroidStore(),
                IosStoreUrl = ResolveIosStore()
            };
        }

        private static bool UseIosRemoteKeys =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#elif UNITY_ANDROID && !UNITY_EDITOR
            false;
#elif UNITY_IOS
            true;
#else
            false;
#endif

        private static bool PlatformMaintenance => UseIosRemoteKeys ? _maintenanceIos : _maintenanceAndroid;
        private static long PlatformLatest => UseIosRemoteKeys ? _iosLatest : _androidLatest;
        private static long PlatformMin => UseIosRemoteKeys ? _iosMin : _androidMin;
        private static bool PlatformForce => UseIosRemoteKeys ? _iosForce : _androidForce;

        private static string ResolveAndroidStore() =>
            !string.IsNullOrEmpty(_androidStoreUrl)
                ? _androidStoreUrl
                : $"https://play.google.com/store/apps/details?id={Application.identifier}";

        private static string ResolveIosStore() =>
            !string.IsNullOrEmpty(_iosStoreUrl) ? _iosStoreUrl : UpdateConfigKeys.DefaultIosStoreUrl;

        private static string Pick(bool preferTr, string tr, string en) =>
            preferTr ? (NonEmpty(tr) ?? NonEmpty(en)) : (NonEmpty(en) ?? NonEmpty(tr));

        private static string NonEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string GetStr(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out string v) && v != null ? v.Trim() : string.Empty;

        private static bool GetBool(Dictionary<string, string> d, string key)
        {
            if (!d.TryGetValue(key, out string v) || string.IsNullOrWhiteSpace(v)) return false;
            string s = v.Trim().ToLowerInvariant();
            return s == "true" || s == "1" || s == "yes";
        }

        private static long GetLong(Dictionary<string, string> d, string key)
        {
            if (!d.TryGetValue(key, out string v) || string.IsNullOrWhiteSpace(v)) return 0;
            return long.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : 0;
        }
    }
}
