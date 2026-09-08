using Sekai;
using Sekai.CustomMusicScoreManager;
using Sekai.UI;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager.Editor
{
	/// <summary>
	/// VideoProcessingProgressDialog预制体生成器
	/// 在Unity编辑器菜单中自动创建预制体
	/// </summary>
	public static class VideoProcessingProgressDialogEditor
	{
		private const string PREFAB_PATH = "Assets/CustomMusicScoreManager/Resources/CustomMusicScoreManager/UI/VideoProcessingProgressDialog.prefab";
		private const string MENU_PATH = "Tools/CustomMusicScoreManager/Create VideoProcessingProgressDialog Prefab";

		[MenuItem(MENU_PATH)]
		public static void CreatePrefab()
		{
			// 创建目录结构
			string directory = "Assets/CustomMusicScoreManager/Resources/CustomMusicScoreManager/UI";
			if (!AssetDatabase.IsValidFolder(directory))
			{
				string[] folders = directory.Split('/');
				string currentPath = folders[0];
				for (int i = 1; i < folders.Length; i++)
				{
					string targetPath = currentPath + "/" + folders[i];
					if (!AssetDatabase.IsValidFolder(targetPath))
					{
						AssetDatabase.CreateFolder(currentPath, folders[i]);
					}
					currentPath = targetPath;
				}
			}

			// 检查是否已存在预制体
			GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (existingPrefab != null)
			{
				Debug.Log($"[VideoProcessingProgressDialogEditor] 预制体已存在: {PREFAB_PATH}");
				EditorUtility.DisplayDialog("提示", "预制体已存在，无需重复创建。\n路径: " + PREFAB_PATH, "确定");
				return;
			}

			// 创建对话框根对象
			GameObject dialogRoot = new GameObject("VideoProcessingProgressDialog");
			RectTransform rootRect = dialogRoot.AddComponent<RectTransform>();
			rootRect.sizeDelta = new Vector2(800, 600);

			// 添加CanvasGroup（DialogBase需要）
			CanvasGroup canvasGroup = dialogRoot.AddComponent<CanvasGroup>();
			canvasGroup.interactable = true;
			canvasGroup.blocksRaycasts = true;

			// 添加VideoProcessingProgressDialog组件
			VideoProcessingProgressDialog dialog = dialogRoot.AddComponent<VideoProcessingProgressDialog>();

			// 创建背景遮罩（UIPartsDialogFillCover）
			GameObject fillCover = CreateFillCover(dialogRoot.transform);
			dialogRoot.AddComponent<DialogSetting>();

			// 创建窗口对象（windowObject）
			GameObject windowObject = CreateWindowObject(dialogRoot.transform);

			// 创建Header
			DialogHeader header = CreateDialogHeader(windowObject.transform);

			// 创建进度UI元素
			CreateProgressUI(windowObject.transform, dialog);

			// 设置DialogBase的字段（使用反射，因为这些是private/protected字段）
			SetPrivateField(dialog, "header", header);
			SetPrivateField(dialog, "windowObject", windowObject);
			SetPrivateField(dialog, "needBackgroundCover", true);
			SetPrivateField(dialog, "closeButton", null); // 进度对话框不需要关闭按钮
			SetPrivateField(dialog, "openSE", DialogBase.OpenSE.SubWindowOpen);
			SetPrivateField(dialog, "closeBehavior", DialogBase.CloseBehavior.Destroy);

			// 保存为预制体
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(dialogRoot, PREFAB_PATH);

			// 清理场景中的临时对象
			Object.DestroyImmediate(dialogRoot);

			// 刷新资源数据库
			AssetDatabase.Refresh();

			Debug.Log($"[VideoProcessingProgressDialogEditor] 预制体已创建: {PREFAB_PATH}");
			EditorUtility.DisplayDialog("成功", "VideoProcessingProgressDialog预制体已创建成功！\n路径: " + PREFAB_PATH, "确定");

			// 选中创建的预制体
			Selection.activeObject = prefab;
		}

		private static GameObject CreateFillCover(Transform parent)
		{
			GameObject fillCover = new GameObject("UIPartsDialogFillCover");
			fillCover.transform.SetParent(parent, false);
			fillCover.transform.SetAsFirstSibling();

			RectTransform rect = fillCover.AddComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.sizeDelta = Vector2.zero;
			rect.localPosition = Vector3.zero;

			// 添加Image作为背景
			Image image = fillCover.AddComponent<Image>();
			image.color = new Color(0, 0, 0, 0.3f); // 半透明黑色背景
			image.raycastTarget = true;

			// 添加UIPartsFillCover组件
			UIPartsFillCover fillCoverComponent = fillCover.AddComponent<UIPartsFillCover>();

			return fillCover;
		}

		private static GameObject CreateWindowObject(Transform parent)
		{
			GameObject windowObject = new GameObject("Window");
			windowObject.transform.SetParent(parent, false);

			RectTransform rect = windowObject.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(600, 400);
			rect.localPosition = Vector3.zero;

			// 添加背景Image
			Image image = windowObject.AddComponent<Image>();
			image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f); // 深灰色背景

			return windowObject;
		}

		private static DialogHeader CreateDialogHeader(Transform parent)
		{
			GameObject headerObject = new GameObject("DialogHeader");
			headerObject.transform.SetParent(parent, false);

			RectTransform rect = headerObject.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0, 1);
			rect.anchorMax = new Vector2(1, 1);
			rect.pivot = new Vector2(0.5f, 1);
			rect.sizeDelta = new Vector2(0, 60);
			rect.localPosition = new Vector3(0, -30, 0);

			// 添加背景Image
			Image image = headerObject.AddComponent<Image>();
			image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

			// 创建标题文本
			GameObject titleObject = new GameObject("Title");
			titleObject.transform.SetParent(headerObject.transform, false);

			RectTransform titleRect = titleObject.AddComponent<RectTransform>();
			titleRect.anchorMin = Vector2.zero;
			titleRect.anchorMax = Vector2.one;
			titleRect.sizeDelta = Vector2.zero;
			titleRect.localPosition = Vector3.zero;

			// 添加CustomText组件
			CustomText titleText = titleObject.AddComponent<CustomText>();
			titleText.SetText("视频生成中");
			titleText.fontSize = 24;
			titleText.alignment = TextAnchor.MiddleCenter;
			titleText.color = Color.white;

			// 创建DialogHeader组件
			DialogHeader header = headerObject.AddComponent<DialogHeader>();
			SetPrivateField(header, "title", titleText);

			return header;
		}

		private static void CreateProgressUI(Transform parent, VideoProcessingProgressDialog dialog)
		{
			// 创建内容区域
			GameObject contentArea = new GameObject("ContentArea");
			contentArea.transform.SetParent(parent, false);

			RectTransform contentRect = contentArea.AddComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.pivot = new Vector2(0.5f, 0.5f);
			contentRect.sizeDelta = new Vector2(-40, -100); // 左右留20px边距，上下留50px边距（60px header + 40px padding）
			contentRect.localPosition = new Vector3(0, -20, 0);

			// 创建进度百分比文本
			GameObject percentageObject = new GameObject("ProgressPercentage");
			percentageObject.transform.SetParent(contentArea.transform, false);

			RectTransform percentageRect = percentageObject.AddComponent<RectTransform>();
			percentageRect.anchorMin = new Vector2(0.5f, 1);
			percentageRect.anchorMax = new Vector2(0.5f, 1);
			percentageRect.pivot = new Vector2(0.5f, 1);
			percentageRect.sizeDelta = new Vector2(200, 40);
			percentageRect.localPosition = new Vector3(0, -20, 0);

			CustomText percentageText = percentageObject.AddComponent<CustomText>();
			percentageText.SetText("0%");
			percentageText.fontSize = 32;
			percentageText.alignment = TextAnchor.MiddleCenter;
			percentageText.color = Color.white;

			// 创建进度条背景
			GameObject sliderBackground = new GameObject("SliderBackground");
			sliderBackground.transform.SetParent(contentArea.transform, false);

			RectTransform sliderBgRect = sliderBackground.AddComponent<RectTransform>();
			sliderBgRect.anchorMin = new Vector2(0.5f, 0.5f);
			sliderBgRect.anchorMax = new Vector2(0.5f, 0.5f);
			sliderBgRect.pivot = new Vector2(0.5f, 0.5f);
			sliderBgRect.sizeDelta = new Vector2(480, 20);
			sliderBgRect.localPosition = new Vector3(0, -40, 0);

			Image sliderBgImage = sliderBackground.AddComponent<Image>();
			sliderBgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

			// 创建进度条填充区域
			GameObject sliderFillArea = new GameObject("Fill Area");
			sliderFillArea.transform.SetParent(sliderBackground.transform, false);

			RectTransform fillAreaRect = sliderFillArea.AddComponent<RectTransform>();
			fillAreaRect.anchorMin = Vector2.zero;
			fillAreaRect.anchorMax = Vector2.one;
			fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
			fillAreaRect.sizeDelta = Vector2.zero;
			fillAreaRect.localPosition = Vector3.zero;

			GameObject sliderFill = new GameObject("Fill");
			sliderFill.transform.SetParent(sliderFillArea.transform, false);

			RectTransform fillRect = sliderFill.AddComponent<RectTransform>();
			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = new Vector2(1, 1);
			fillRect.pivot = new Vector2(0.5f, 0.5f);
			fillRect.sizeDelta = Vector2.zero;
			fillRect.localPosition = Vector3.zero;

			Image fillImage = sliderFill.AddComponent<Image>();
			fillImage.color = new Color(0.3f, 0.7f, 0.3f, 1f); // 绿色进度条

			// 创建Slider组件
			Slider slider = sliderBackground.AddComponent<Slider>();
			slider.targetGraphic = fillImage;
			slider.direction = Slider.Direction.LeftToRight;
			slider.minValue = 0f;
			slider.maxValue = 1f;
			slider.value = 0f;
			slider.interactable = false; // 进度条不可交互

			// 设置Slider的fillRect
			SerializedObject sliderSO = new SerializedObject(slider);
			sliderSO.FindProperty("m_FillRect").objectReferenceValue = fillRect;
			sliderSO.ApplyModifiedProperties();

			// 创建当前步骤文本
			GameObject stepObject = new GameObject("CurrentStep");
			stepObject.transform.SetParent(contentArea.transform, false);

			RectTransform stepRect = stepObject.AddComponent<RectTransform>();
			stepRect.anchorMin = new Vector2(0.5f, 0);
			stepRect.anchorMax = new Vector2(0.5f, 0);
			stepRect.pivot = new Vector2(0.5f, 0);
			stepRect.sizeDelta = new Vector2(480, 60);
			stepRect.localPosition = new Vector3(0, 40, 0);

			CustomText stepText = stepObject.AddComponent<CustomText>();
			stepText.SetText("准备处理...");
			stepText.fontSize = 18;
			stepText.alignment = TextAnchor.MiddleCenter;
			stepText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

			// 设置VideoProcessingProgressDialog的字段
			SetPrivateField(dialog, "progressSlider", slider);
			SetPrivateField(dialog, "progressPercentageTextAlt", percentageText);
			SetPrivateField(dialog, "currentStepTextAlt", stepText);
		}

		private static void SetPrivateField(object obj, string fieldName, object value)
		{
			System.Reflection.FieldInfo field = obj.GetType().GetField(fieldName,
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.Public);

			if (field != null)
			{
				field.SetValue(obj, value);
			}
			else
			{
				Debug.LogWarning($"[VideoProcessingProgressDialogEditor] 无法找到字段: {fieldName} in {obj.GetType().Name}");
			}
		}
	}
}