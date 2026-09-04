using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiCopyPass : ScriptableRenderPass
	{
		private readonly ProfilingSampler m_ProfilingSampler;

		public SekaiCopyPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiCopyPass));
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var source = frameData.Get<UniversalResourceData>().activeColorTexture;
			var destinationHandle = SekaiUIBuffer.CaptureColorTexHandle;
			if (!source.IsValid() || destinationHandle == null)
			{
				return;
			}

			var destination = renderGraph.ImportTexture(destinationHandle);
			if (!destination.IsValid())
			{
				return;
			}

			var parameters = new RenderGraphUtils.BlitMaterialParameters(
				source,
				destination,
				Blitter.GetBlitMaterial(TextureDimension.Tex2D),
				0);
			renderGraph.AddBlitPass(parameters, m_ProfilingSampler.name);
		}
	}
}
