using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiDrawUIPass : ScriptableRenderPass
	{
		private static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
		private static readonly int s_ScaleBiasRtPropID = Shader.PropertyToID("_ScaleBiasRt");
		private static readonly int s_ScreenParamsPropID = Shader.PropertyToID("_ScreenParams");

		private readonly ProfilingSampler m_ProfilingSampler;
		private ShaderTagId m_ShaderTagId = new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
		private FilteringSettings m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, ~0);
		private RenderStateBlock m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
		private bool m_IsCameraRenderTarget;

		private sealed class PassData
		{
			public RendererListHandle RendererList;
			public Vector4 ScaleBiasRt;
			public Vector4 ScreenParams;
		}

		public SekaiDrawUIPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiDrawUIPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
		}

		public void Setup(
			LayerMask layerMask,
			StencilState stencilState,
			int stencilReference,
			bool isCameraRenderTarget,
			RenderQueueRange renderQueueRange)
		{
			m_ShaderTagId = new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
			m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
			m_IsCameraRenderTarget = isCameraRenderTarget;
			m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
			if (stencilState.enabled)
			{
				m_RenderStateBlock.stencilReference = stencilReference;
				m_RenderStateBlock.mask = RenderStateMask.Stencil;
				m_RenderStateBlock.stencilState = stencilState;
			}
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var resourceData = frameData.Get<UniversalResourceData>();
			var renderingData = frameData.Get<UniversalRenderingData>();
			var cameraData = frameData.Get<UniversalCameraData>();
			var lightData = frameData.Get<UniversalLightData>();
			var colorTarget = m_IsCameraRenderTarget
				? resourceData.activeColorTexture
				: ImportTexture(renderGraph, SekaiUIBuffer.CaptureColorTexHandle);
			var depthTarget = m_IsCameraRenderTarget
				? resourceData.activeDepthTexture
				: ImportTexture(renderGraph, SekaiUIBuffer.CaptureDepthTexHandle);
			if (!colorTarget.IsValid() || !depthTarget.IsValid())
			{
				return;
			}

			var drawingSettings = RenderingUtils.CreateDrawingSettings(
				m_ShaderTagId,
				renderingData,
				cameraData,
				lightData,
				(SortingCriteria)23);
			var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, m_FilteringSettings);
			var rendererList = renderGraph.CreateRendererList(rendererListParams);
			if (!rendererList.IsValid())
			{
				return;
			}

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, m_ProfilingSampler))
			{
				passData.RendererList = rendererList;
				passData.ScaleBiasRt = GetScaleBiasRt(cameraData);
				passData.ScreenParams = GetScreenParams(cameraData);
				builder.UseRendererList(rendererList);
				builder.UseAllGlobalTextures(true);
				builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
				builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.Write);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					context.cmd.ClearRenderTarget(RTClearFlags.Depth, Color.black, 1f, 0);
					context.cmd.SetGlobalVector(s_DrawObjectPassDataPropID, Vector4.zero);
					context.cmd.SetGlobalVector(s_ScaleBiasRtPropID, data.ScaleBiasRt);
					context.cmd.SetGlobalVector(s_ScreenParamsPropID, data.ScreenParams);
					context.cmd.DrawRendererList(data.RendererList);
				});
			}
		}

		private static TextureHandle ImportTexture(RenderGraph renderGraph, RTHandle handle)
		{
			return handle != null ? renderGraph.ImportTexture(handle) : TextureHandle.nullHandle;
		}

		private Vector4 GetScaleBiasRt(UniversalCameraData cameraData)
		{
			var target = m_IsCameraRenderTarget ? null : SekaiUIBuffer.CaptureColorTexHandle;
			var yFlip = target != null
				? cameraData.IsRenderTargetProjectionMatrixFlipped(target)
				: IsCameraTargetProjectionMatrixFlipped(cameraData);
			var flipSign = yFlip ? -1f : 1f;
			return flipSign < 0f
				? new Vector4(flipSign, 1f, -1f, 1f)
				: new Vector4(flipSign, 0f, 1f, 1f);
		}

		private static bool IsCameraTargetProjectionMatrixFlipped(UniversalCameraData cameraData)
		{
			if (!SystemInfo.graphicsUVStartsAtTop)
			{
				return true;
			}

			return cameraData.targetTexture != null ||
				cameraData.cameraType == CameraType.SceneView ||
				cameraData.cameraType == CameraType.Preview;
		}

		private Vector4 GetScreenParams(UniversalCameraData cameraData)
		{
			var descriptor = cameraData.cameraTargetDescriptor;
			var width = descriptor.width;
			var height = descriptor.height;
			if (!m_IsCameraRenderTarget)
			{
				var scale = SekaiUIEffectSettings.Blur.CaptureResolutionScale;
				width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
				height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
			}

			return new Vector4(width, height, 1f + 1f / width, 1f + 1f / height);
		}
	}
}
