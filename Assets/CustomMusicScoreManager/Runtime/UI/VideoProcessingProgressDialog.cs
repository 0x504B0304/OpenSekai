using Sekai.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.CustomMusicScoreManager
{
	/// <summary>
	/// 视频后处理进度对话框
	/// 显示视频后处理的实时进度和当前步骤
	/// </summary>
	public class VideoProcessingProgressDialog : DialogBase
	{
		#region UI Components

		[Header("Progress UI")]
		[SerializeField]
		private Slider progressSlider;

		[SerializeField]
		private CustomTextMesh progressPercentageText;

		[SerializeField]
		private CustomText progressPercentageTextAlt;

		[SerializeField]
		private CustomTextMesh currentStepText;

		[SerializeField]
		private CustomText currentStepTextAlt;

		#endregion

		#region Private Fields

		private ICustomText progressTextInterface;
		private ICustomText stepTextInterface;

		#endregion

		#region Properties

		/// <summary>
		/// 当前进度 (0.0 - 1.0)
		/// </summary>
		public float Progress
		{
			get => progressSlider != null ? progressSlider.value : 0f;
			set => SetProgress(value);
		}

		/// <summary>
		/// 当前步骤描述
		/// </summary>
		public string CurrentStep
		{
			get => GetStepText();
			set => SetStepText(value);
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// 初始化对话框
		/// </summary>
		public override void Initialize(DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true)
		{
			// 进度对话框不允许外部关闭，处理进行中不应被关闭
			base.Initialize(dialogSize, false);

			InitializeTextInterfaces();
			SetProgress(0f);
			SetStepText("准备处理...");
		}

		/// <summary>
		/// 设置进度值
		/// </summary>
		/// <param name="progress">进度值 (0.0 - 1.0)</param>
		public void SetProgress(float progress)
		{
			progress = Mathf.Clamp01(progress);

			if (progressSlider != null)
			{
				progressSlider.value = progress;
			}

			SetProgressText(progress);
		}

		/// <summary>
		/// 设置当前步骤文本
		/// </summary>
		/// <param name="stepText">步骤描述文本</param>
		public void SetStepText(string stepText)
		{
			InitializeTextInterfaces();

			if (stepTextInterface == null)
			{
				return;
			}

			stepTextInterface.UseWordingKey = false;
			stepTextInterface.SetText(stepText ?? string.Empty);
		}

		/// <summary>
		/// 更新进度和步骤（从VideoPostProcessor的事件调用）
		/// </summary>
		/// <param name="progress">进度值 (0.0 - 1.0)</param>
		/// <param name="stepText">步骤描述文本</param>
		public void UpdateProgress(float progress, string stepText)
		{
			SetProgress(progress);
			SetStepText(stepText);
		}

		#endregion

		#region Protected Methods

		protected override void AwakeProcess()
		{
			base.AwakeProcess();
			InitializeTextInterfaces();
		}

		protected override void OnHardwareBackKeyProcess()
		{
			// 进度对话框不支持返回键关闭
			// 处理完成后会自动关闭
		}

		protected override void OnCloseExternal()
		{
			// 进度对话框不支持外部点击关闭
			// 处理完成后会自动关闭
		}

		#endregion

		#region Private Methods

		private void InitializeTextInterfaces()
		{
			if (progressTextInterface == null)
			{
				progressTextInterface = progressPercentageText != null
					? (ICustomText)progressPercentageText
					: progressPercentageTextAlt;
			}

			if (stepTextInterface == null)
			{
				stepTextInterface = currentStepText != null
					? (ICustomText)currentStepText
					: currentStepTextAlt;
			}
		}

		private void SetProgressText(float progress)
		{
			InitializeTextInterfaces();

			if (progressTextInterface == null)
			{
				return;
			}

			int percentage = Mathf.RoundToInt(progress * 100);
			progressTextInterface.UseWordingKey = false;
			progressTextInterface.SetText($"{percentage}%");
		}

		private string GetStepText()
		{
			InitializeTextInterfaces();

			if (stepTextInterface == null)
			{
				return string.Empty;
			}

			return stepTextInterface.Text;
		}

		#endregion
	}
}