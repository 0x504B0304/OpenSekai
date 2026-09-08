using UnityEngine;
using System;

namespace CustomMusicScoreManager.Helpers
{
    /// <summary>
    /// Android权限帮助类，封装权限检查和申请逻辑
    /// </summary>
    public static class PermissionHelper
    {
        private static Action<bool> _permissionCallback;

        // Android权限常量
        private const string READ_MEDIA_IMAGES = "android.permission.READ_MEDIA_IMAGES";
        private const string WRITE_EXTERNAL_STORAGE = "android.permission.WRITE_EXTERNAL_STORAGE";

        // Android API级别
        private const int API_LEVEL_33 = 33; // Android 13

        /// <summary>
        /// 检查是否拥有相册权限
        /// Android 13+ (API 33+) 检查 READ_MEDIA_IMAGES
        /// Android 12及以下 (API < 33) 检查 WRITE_EXTERNAL_STORAGE
        /// </summary>
        /// <returns>是否拥有相册权限</returns>
        public static bool HasGalleryPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // 获取当前API级别
                int apiLevel = GetAndroidApiLevel();
                Debug.Log($"[PermissionHelper] Current Android API Level: {apiLevel}");

                string permission;
                if (apiLevel >= API_LEVEL_33)
                {
                    // Android 13+ 使用 READ_MEDIA_IMAGES
                    permission = READ_MEDIA_IMAGES;
                    Debug.Log($"[PermissionHelper] Checking READ_MEDIA_IMAGES permission for Android {apiLevel}");
                }
                else
                {
                    // Android 12及以下使用 WRITE_EXTERNAL_STORAGE
                    permission = WRITE_EXTERNAL_STORAGE;
                    Debug.Log($"[PermissionHelper] Checking WRITE_EXTERNAL_STORAGE permission for Android {apiLevel}");
                }

                // 检查权限状态
                bool hasPermission = CheckPermission(permission);
                Debug.Log($"[PermissionHelper] Permission {permission} status: {hasPermission}");
                return hasPermission;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PermissionHelper] Error checking gallery permission: {e.Message}");
                return false;
            }
#elif UNITY_EDITOR
            // 编辑器模式下默认有权限
            Debug.Log("[PermissionHelper] Editor mode - permission granted by default");
            return true;
#else
            return true;
#endif
        }

        /// <summary>
        /// 申请相册权限
        /// </summary>
        /// <param name="callback">权限结果回调，true表示获得权限，false表示被拒绝</param>
        public static void RequestGalleryPermission(Action<bool> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _permissionCallback = callback;

                // 如果已经有权限，直接回调
                if (HasGalleryPermission())
                {
                    Debug.Log("[PermissionHelper] Gallery permission already granted");
                    _permissionCallback?.Invoke(true);
                    return;
                }

                // 获取需要申请的权限
                int apiLevel = GetAndroidApiLevel();
                string permission = apiLevel >= API_LEVEL_33 ? READ_MEDIA_IMAGES : WRITE_EXTERNAL_STORAGE;

                Debug.Log($"[PermissionHelper] Requesting permission: {permission}");

                // 使用反射调用Unity的Permission.RequestUserPermission，避免编译错误
                // 因为UnityEngine.Permission类只在Android/iOS平台存在，Windows编译时会报错
                Type permissionType = Type.GetType("UnityEngine.Permission, UnityEngine.CoreModule");
                if (permissionType != null)
                {
                    var requestMethod = permissionType.GetMethod("RequestUserPermission", new Type[] { typeof(string) });
                    if (requestMethod != null)
                    {
                        requestMethod.Invoke(null, new object[] { permission });
                    }
                    else
                    {
                        Debug.LogError("[PermissionHelper] Failed to find RequestUserPermission method");
                    }
                }
                else
                {
                    Debug.LogWarning("[PermissionHelper] UnityEngine.Permission type not found, using fallback method");
                    // 使用AndroidJavaClass作为备用方案
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        currentActivity.Call("requestPermissions", new string[] { permission }, 0);
                    }
                }

                // 注册权限结果检查
                RegisterPermissionCheck(permission);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PermissionHelper] Error requesting gallery permission: {e.Message}");
                _permissionCallback?.Invoke(false);
            }
#elif UNITY_EDITOR
            // 编辑器模式下直接返回成功
            Debug.Log("[PermissionHelper] Editor mode - permission request granted by default");
            callback?.Invoke(true);
#else
            callback?.Invoke(true);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 获取Android API级别
        /// </summary>
        private static int GetAndroidApiLevel()
        {
            using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return versionClass.GetStatic<int>("SDK_INT");
            }
        }

        /// <summary>
        /// 检查指定权限是否已授权
        /// </summary>
        private static bool CheckPermission(string permission)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                int result = currentActivity.Call<int>("checkSelfPermission", permission);
                // PackageManager.PERMISSION_GRANTED = 0
                return result == 0;
            }
        }

        /// <summary>
        /// 注册权限检查，等待Unity权限对话框结果
        /// </summary>
        private static void RegisterPermissionCheck(string permission)
        {
            // 由于Unity的Permission.RequestUserPermission是异步的，
            // 我们需要在后续帧检查权限是否被授予
            // 这里使用一个简单的方法：创建一个MonoBehaviour来协程检查
            var checker = PermissionCallbackHelper.Create();
            checker.StartPermissionCheck(permission, OnPermissionResult);
        }

        /// <summary>
        /// 权限结果处理
        /// </summary>
        private static void OnPermissionResult(bool granted)
        {
            Debug.Log($"[PermissionHelper] Permission result: {(granted ? "Granted" : "Denied")}");
            _permissionCallback?.Invoke(granted);
            _permissionCallback = null;
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// 权限回调帮助类，用于处理异步权限请求结果
    /// </summary>
    internal class PermissionCallbackHelper : MonoBehaviour
    {
        private string _permission;
        private Action<bool> _callback;
        private float _timeout = 30f; // 30秒超时
        private float _elapsed = 0f;
        private bool _isChecking = false;

        public static PermissionCallbackHelper Create()
        {
            var go = new GameObject("PermissionCallbackHelper");
            DontDestroyOnLoad(go);
            return go.AddComponent<PermissionCallbackHelper>();
        }

        public void StartPermissionCheck(string permission, Action<bool> callback)
        {
            _permission = permission;
            _callback = callback;
            _isChecking = true;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (!_isChecking)
                return;

            _elapsed += Time.deltaTime;

            // 检查权限是否已被授予
            if (HasPermission())
            {
                Debug.Log("[PermissionCallbackHelper] Permission granted");
                Complete(true);
                return;
            }

            // 检查是否超时
            if (_elapsed >= _timeout)
            {
                Debug.LogWarning("[PermissionCallbackHelper] Permission check timeout");
                Complete(false);
            }
        }

        private bool HasPermission()
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                int result = currentActivity.Call<int>("checkSelfPermission", _permission);
                return result == 0;
            }
        }

        private void Complete(bool granted)
        {
            _isChecking = false;
            _callback?.Invoke(granted);
            Destroy(gameObject);
        }
    }
#endif
}