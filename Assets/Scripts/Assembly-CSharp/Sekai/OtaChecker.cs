using System;
using CP;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Sekai
{
    public class OtaChecker : SingletonMonoBehaviour<OtaChecker>
    {
        private const string OtaUrl = "https://ota.jsoftstudio.top/appver?appid=oj03";

        private int localVersionNumber = 45;
        private string localVersionString = "1.6.7";
        private bool hasChecked = false;

        protected override void OnInitialize()
        {
            base.OnInitialize();
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
                        Debug.Log($"[OtaChecker] Local version number: {localVersionNumber}");
                        Debug.Log($"[OtaChecker] Local version string: {localVersionString}");
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

        public void CheckForUpdates()
        {
            if (hasChecked)
            {
                Debug.Log("[OtaChecker] Already checked, skipping");
                return;
            }
            hasChecked = true;
            Debug.Log("[OtaChecker] CheckForUpdates started");
            CheckForUpdatesAsync().Forget();
        }

        private async UniTaskVoid CheckForUpdatesAsync()
        {
            Debug.Log("[OtaChecker] CheckForUpdatesAsync started");
            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get(OtaUrl))
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
