using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiCharacterOutlinePass : ScriptableRenderPass
	{
		private static class PropertyId
		{
			public static readonly int OutlineFactorId = Shader.PropertyToID("_SekaiOutlineFactor");
			public static readonly int OutlineWidthId = Shader.PropertyToID("_SekaiOutlineWidth");
		}

		[Serializable]
		public class OutlineSettings
		{
			public float outlineWidthMin = 0.04f;
			public float outlineWidthMax = 0.95f;
			public float outlineDistanceNear = 0.45f;
			public float outlineDistanceFar = 20f;
			public AnimationCurve fovCurve = AnimationCurve.Linear(0f, 1f, 100f, 1f);
		}

		private OutlineSettings m_Settings;

		private sealed class PassData
		{
			public Vector4 OutlineWidth;
			public Vector4 OutlineFactor;
		}

		public void Setup(OutlineSettings settings)
		{
			m_Settings = settings;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var camera = frameData.Get<UniversalCameraData>().camera;
			if (!TryGetOutlineValues(camera, out var outlineWidth, out var outlineFactor))
			{
				return;
			}

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
			{
				passData.OutlineWidth = outlineWidth;
				passData.OutlineFactor = outlineFactor;
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					context.cmd.SetGlobalVector(PropertyId.OutlineWidthId, data.OutlineWidth);
					context.cmd.SetGlobalVector(PropertyId.OutlineFactorId, data.OutlineFactor);
				});
			}
		}

		private bool TryGetOutlineValues(Camera camera, out Vector4 outlineWidth, out Vector4 outlineFactor)
		{
			outlineWidth = default;
			outlineFactor = default;
			if (camera == null || m_Settings == null)
			{
				return false;
			}

			var fov = camera.fieldOfView;
			var fovScale = m_Settings.fovCurve != null ? m_Settings.fovCurve.Evaluate(fov) : fov;
			if (Mathf.Approximately(fovScale, 0f))
			{
				fovScale = 1f;
			}

			outlineWidth = new Vector4(m_Settings.outlineWidthMin * 0.01f, m_Settings.outlineWidthMax * 0.01f, 0f, 0f);
			outlineFactor = new Vector4(
				m_Settings.outlineDistanceNear,
				1f / (m_Settings.outlineDistanceFar - m_Settings.outlineDistanceNear),
				fov / fovScale,
				0f);
			return true;
		}
	}
}
