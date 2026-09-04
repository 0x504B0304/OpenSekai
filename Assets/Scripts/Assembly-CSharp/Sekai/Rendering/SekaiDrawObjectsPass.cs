using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiDrawObjectsPass : ScriptableRenderPass
	{
		private static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
		private static readonly int s_ScaleBiasRtPropID = Shader.PropertyToID("_ScaleBiasRt");

		private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
		private readonly ProfilingSampler m_ProfilingSampler;
		private FilteringSettings m_FilteringSettings;
		private RenderStateBlock m_RenderStateBlock;
		private bool m_IsOpaque;
		private bool m_SkipExecution;

		private sealed class PassData
		{
			public RendererListHandle RendererList;
			public Vector4 DrawObjectPassData;
			public Vector4 ScaleBiasRt;
		}

		public SekaiDrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, bool skipExecution = false)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiDrawObjectsPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
			renderPassEvent = evt;
			m_IsOpaque = opaque;
			m_SkipExecution = skipExecution;
			m_FilteringSettings = new FilteringSettings(renderQueueRange, ~0);
			m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

			foreach (var shaderTagId in shaderTagIds)
			{
				m_ShaderTagIdList.Add(shaderTagId);
			}
		}

		public SekaiDrawObjectsPass(string profilerTag, SekaiShaderTagType shaderTagType, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange)
			: this(
				profilerTag,
				new[] { new ShaderTagId(SekaiShaderTag.GetTag(shaderTagType)) },
				opaque,
				evt,
				renderQueueRange,
				shaderTagType == SekaiShaderTagType.Default)
		{
		}

		internal void Setup(LayerMask layerMask, StencilState stencilState, int stencilReference)
		{
			m_FilteringSettings.layerMask = layerMask;
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
			// Original SekaiRenderer owns the SRPDefaultUnlit draw stage. In URP the
			// port the built-in UniversalRenderer already draws that tag, so replaying
			// Default here makes transparent UI accumulate alpha twice.
			if (m_SkipExecution)
			{
				return;
			}

			var resourceData = frameData.Get<UniversalResourceData>();
			var renderingData = frameData.Get<UniversalRenderingData>();
			var cameraData = frameData.Get<UniversalCameraData>();
			var lightData = frameData.Get<UniversalLightData>();
			var sortingCriteria = m_IsOpaque
				? cameraData.defaultOpaqueSortFlags
				: SortingCriteria.CommonTransparent;
			var drawingSettings = RenderingUtils.CreateDrawingSettings(
				m_ShaderTagIdList,
				renderingData,
				cameraData,
				lightData,
				sortingCriteria);
			var filteringSettings = m_FilteringSettings;

#if UNITY_EDITOR
			if (cameraData.isPreviewCamera)
			{
				filteringSettings.layerMask = -1;
			}
#endif

			var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
			var rendererList = renderGraph.CreateRendererList(rendererListParams);
			if (!rendererList.IsValid())
			{
				return;
			}

			var yFlip = IsCameraTargetProjectionMatrixFlipped(cameraData);
			var flipSign = yFlip ? -1f : 1f;
			var scaleBias = flipSign < 0f
				? new Vector4(flipSign, 1f, -1f, 1f)
				: new Vector4(flipSign, 0f, 1f, 1f);

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, m_ProfilingSampler))
			{
				passData.RendererList = rendererList;
				passData.DrawObjectPassData = new Vector4(0f, 0f, 0f, m_IsOpaque ? 1f : 0f);
				passData.ScaleBiasRt = scaleBias;
				builder.UseRendererList(rendererList);
				builder.UseAllGlobalTextures(true);
				if (resourceData.activeColorTexture.IsValid())
				{
					builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
				}
				if (resourceData.activeDepthTexture.IsValid())
				{
					builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
				}
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					context.cmd.SetGlobalVector(s_DrawObjectPassDataPropID, data.DrawObjectPassData);
					context.cmd.SetGlobalVector(s_ScaleBiasRtPropID, data.ScaleBiasRt);
					context.cmd.DrawRendererList(data.RendererList);
				});
			}
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
	}
}
