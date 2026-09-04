using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiUIBlurSetupPass : ScriptableRenderPass
	{
		private SekaiUIBuffer m_UIBuffer;

		public void Setup(SekaiUIBuffer uiBuffer)
		{
			m_UIBuffer = uiBuffer;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var descriptor = frameData.Get<UniversalCameraData>().cameraTargetDescriptor;
			m_UIBuffer?.TryGetRTHandle(descriptor, descriptor);
		}

		public void Dispose()
		{
			m_UIBuffer?.Dispose();
			m_UIBuffer = null;
		}
	}
}
