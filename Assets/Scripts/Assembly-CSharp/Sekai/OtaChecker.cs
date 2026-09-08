using System;
using CP;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Sekai
{
    public class OtaChecker : SingletonMonoBehaviour<OtaChecker>
    {
        private int localVersionNumber = 45;
        private string localVersionString = "1.6.7";
        private bool hasChecked = false;
        private bool isDev = false; // 开发模式标志

        protected override void OnInitialize()
        {
            base.OnInitialize();
            Debug.Log("fuck Android");
            Debug.Log("[OtaChecker] OnInitialize called");
            ReadLocalVersion();
        }

        private void ReadLocalVersion()
        {
            try
            {
                TextAsset jsonAsset = Resources.Load<TextAsset>("jsofttool");
                if (jsonAsset != null)
                {
                    var versionData = JsonUtility.FromJson<VersionData>(jsonAsset.text);
                    if (versionData != null)
                    {
                        localVersionNumber = versionData.vernumber;
                        localVersionString = versionData.version;
                        isDev = versionData.isDev;
                        Debug.Log($"[OtaChecker] Local version number: {localVersionNumber}");
                        Debug.Log($"[OtaChecker] Local version string: {localVersionString}");
                        Debug.Log($"[OtaChecker] isDev: {isDev}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[OtaChecker] Config file not found in Resources: jsofttool");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OtaChecker] Failed to read local version: {ex.Message}");
            }
        }

        private string GetAppId()
        {
            // 开发模式优先使用开发appid
            if (isDev)
            {
                Debug.Log("[OtaChecker] Using dev appid: ojskdev");
                return "ojskdev";
            }

            // 根据平台选择appid
            #if UNITY_STANDALONE_WIN
                Debug.Log("[OtaChecker] Platform: Windows, using appid: oj04");
                return "oj04";
            #elif UNITY_ANDROID
                Debug.Log("[OtaChecker] Platform: Android, using appid: oj05");
                return "oj05";
            #else
                Debug.Log("[OtaChecker] Platform: Unsupported, skipping OTA check");
                return null; // 跳过检查
            #endif
        }

        private string GetOtaUrl()
        {
            string appId = GetAppId();
            if (appId == null)
            {
                return null;
            }
            return $"https://ota.jsoftstudio.top/appver?appid={appId}";
        }

        public void CheckForUpdates()
        {
            if (hasChecked)
            {
                Debug.Log("[OtaChecker] Already checked, skipping");
                return;
            }

            // 检查是否需要跳过
            string appId = GetAppId();
            if (appId == null)
            {
                Debug.Log("[OtaChecker] Skipping OTA check for unsupported platform");
                hasChecked = true;
                return;
            }

            hasChecked = true;
            Debug.Log("[OtaChecker] CheckForUpdates started with appid: " + appId);
            CheckForUpdatesAsync().Forget();
        }

        private async UniTaskVoid CheckForUpdatesAsync()
        {
            string otaUrl = GetOtaUrl();
            if (otaUrl == null)
            {
                Debug.Log("[OtaChecker] No OTA URL, skipping check");
                return;
            }

            Debug.Log("[OtaChecker] CheckForUpdatesAsync started, URL: " + otaUrl);
            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get(otaUrl))
                {
                    request.timeout = 10;

                    await request.SendWebRequest();
                    Debug.Log($"[OtaChecker] Request completed, result: {request.result}");

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string jsonText = request.downloadHandler.text;
                        Debug.Log($"[OtaChecker] OTA response: {jsonText}");
                        ProcessOtaResponse(jsonText);
                    }
                    else
                    {
                        Debug.LogWarning($"[OtaChecker] OTA request failed: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OtaChecker] OTA check failed: {ex.Message}");
            }
        }

        private void ProcessOtaResponse(string jsonText)
        {
            Debug.Log("[OtaChecker] ProcessOtaResponse called");
            try
            {
                OtaResponse response = JsonUtility.FromJson<OtaResponse>(jsonText);

                if (response == null)
                {
                    Debug.LogWarning("[OtaChecker] Failed to parse OTA response");
                    return;
                }

                Debug.Log($"[OtaChecker] Cloud version: {response.vernumber}, Local version: {localVersionNumber}");

                if (response.vernumber > localVersionNumber)
                {
                    Debug.Log("[OtaChecker] New version detected, showing dialog");
                    ShowUpdateDialog(response);
                }
                else
                {
                    Debug.Log("[OtaChecker] No new version");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OtaChecker] Failed to process OTA response: {ex.Message}");
            }
        }

        private void ShowUpdateDialog(OtaResponse response)
        {
            Debug.Log("[OtaChecker] ShowUpdateDialog called");
            string versionMessage = $"检测到新版本，请前往群文件更新\n当前版本：{localVersionString}\n云端版本：{response.ver}";

            Debug.Log($"[OtaChecker] ScreenManager.Instance: {ScreenManager.Instance}");
            var dialog = ScreenManager.Instance.ShowDialog<Common1ButtonDialog>(
                DialogType.Common1ButtonDialog,
                DisplayLayerType.Layer_Dialog,
                DialogSize.Large,
                true);

            if (dialog != null)
            {
                dialog.Initialize(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    () => OnVersionDialogClosed(response),
                    DialogSize.Large,
                    true);

                dialog.SetMessageBodyText(versionMessage);
                dialog.Open();
            }
        }

        private void OnVersionDialogClosed(OtaResponse response)
        {
            string notice = DecodeNotice(response.notice);

            var dialog = ScreenManager.Instance.ShowDialog<Common1ButtonDialog>(
                DialogType.Common1ButtonDialog,
                DisplayLayerType.Layer_Dialog,
                DialogSize.Large,
                true);

            if (dialog != null)
            {
                dialog.Initialize(
                    "更新公告",
                    string.Empty,
                    string.Empty,
                    () => { },
                    DialogSize.Large,
                    true);

                dialog.SetMessageBodyText(notice);
                dialog.Open();
            }
        }

        private string DecodeNotice(string notice)
        {
            if (string.IsNullOrEmpty(notice))
                return string.Empty;

            try
            {
                notice = notice.Replace("\\n", "\n");
                notice = notice.Replace("\\\"", "\"");
                notice = DecodeUnicodeEscapes(notice);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OtaChecker] Failed to decode notice: {ex.Message}");
            }

            return notice;
        }

        private string DecodeUnicodeEscapes(string input)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                if (i + 5 < input.Length && input[i] == '\\' && input[i + 1] == 'u')
                {
                    string hex = input.Substring(i + 2, 4);
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                    {
                        sb.Append((char)code);
                        i += 6;
                        continue;
                    }
                }
                sb.Append(input[i]);
                i++;
            }
            return sb.ToString();
        }

        [Serializable]
        private class VersionData
        {
            public int vernumber;
            public string version;
            public bool isDev; // 新增字段
        }

        [Serializable]
        private class OtaResponse
        {
            public string appname;
            public string file;
            public string md5;
            public string notice;
            public string time;
            public string ver;
            public int vernumber;
        }
    }
}
