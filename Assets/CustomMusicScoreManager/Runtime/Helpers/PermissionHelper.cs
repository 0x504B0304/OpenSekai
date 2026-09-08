using System;
using UnityEngine;

namespace CustomMusicScoreManager.Helpers
{
    // Publishing app-owned media via MediaStore needs write permission only on Android 8/9.
    public static class PermissionHelper
    {
        public static string RequiredGalleryWritePermission(int apiLevel) =>
            apiLevel < 29 ? "android.permission.WRITE_EXTERNAL_STORAGE" : null;

        public static bool HasGalleryPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                string permission = RequiredGalleryWritePermission(version.GetStatic<int>("SDK_INT"));
                return permission == null || UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
            }
#else
            return true;
#endif
        }

        public static void RequestGalleryPermission(Action<bool> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (HasGalleryPermission()) { callback?.Invoke(true); return; }
                bool answered = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                Action<bool> finish = granted =>
                {
                    if (answered) return;
                    answered = true;
                    callback?.Invoke(granted);
                };
                callbacks.PermissionGranted += _ => finish(true);
                callbacks.PermissionDenied += _ => finish(false);
                callbacks.PermissionDeniedAndDontAskAgain += _ => finish(false);
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageWrite, callbacks);
            }
            catch (Exception ex) { Debug.LogWarning(ex.Message); callback?.Invoke(false); }
#else
            callback?.Invoke(true);
#endif
        }
    }
}
