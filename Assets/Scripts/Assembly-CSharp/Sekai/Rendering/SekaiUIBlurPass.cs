using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiUIBlurPass : ScriptableRenderPass
	{
		private static readonly int SamplingDistance = Shader.PropertyToID("_SamplingDistance");
		private static readonly int BlurResolutionParams = Shader.PropertyToID("_BlurResolutionParams");

		private readonly ProfilingSampler m_ProfilingSampler;
		private readonly Material m_UIEffectMaterial;

		public SekaiUIBlurPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiUIBlurPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
			var shader = Shader.Find("Hidden/CP/PostEffect/UIEffect");
			if (shader == null)
			{
				shader = Shader.Find("Sekai/UI/UIGaussianBlur");
			}

			if (shader != null)
			{
				m_UIEffectMaterial = CoreUtils.CreateEngineMaterial(shader);
			}
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var sourceHandle = SekaiUIBuffer.CaptureColorTexHandle;
			var destinationHandle = SekaiUIBuffer.UIBlurTexHandle;
			var tempHandle = SekaiUIBuffer.BlurTempHandle;
			if (sourceHandle == null || destinationHandle == null || tempHandle == null || m_UIEffectMaterial == null)
			{
				return;
			}

			var source = renderGraph.ImportTexture(sourceHandle);
			var destination = renderGraph.ImportTexture(destinationHandle);
			var temp = renderGraph.ImportTexture(tempHandle);
			if (!source.IsValid() || !destination.IsValid() || !temp.IsValid())
			{
				return;
			}

			var descriptor = frameData.Get<UniversalCameraData>().cameraTargetDescriptor;
			m_UIEffectMaterial.SetFloat(SamplingDistance, SekaiUIEffectSettings.Blur.BlurSamplingDistance);
			m_UIEffectMaterial.SetVector(
				BlurResolutionParams,
				new Vector4(1f / descriptor.width, 1f / descriptor.height, 0f, 0f));

			var horizontal = new RenderGraphUtils.BlitMaterialParameters(source, temp, m_UIEffectMaterial, 0);
			renderGraph.AddBlitPass(horizontal, m_ProfilingSampler.name + " Horizontal");
			var vertical = new RenderGraphUtils.BlitMaterialParameters(temp, destination, m_UIEffectMaterial, 1);
			renderGraph.AddBlitPass(vertical, m_ProfilingSampler.name + " Vertical");
		}

		public void Cleanup()
		{
			CoreUtils.Destroy(m_UIEffectMaterial);
		}
	}
}
