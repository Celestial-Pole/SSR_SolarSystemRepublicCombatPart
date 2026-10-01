using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSR.UnityComponent.Outline
{
    //用独立摄像机采集炮塔，将外轮廓和结构折线合成到透明纹理。
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class TurretOutlineCapture : MonoBehaviour
    {
        public Transform target;
        public RenderTexture output;
        public Shader geometryShader;
        public Shader compositeShader;
        public bool orthographicProjection;
        [Range(1, 2)] public int supersampling = 2;
        [Range(0, 8)] public float silhouetteWidth = 1.5f;
        [Range(0, 8)] public float structureWidth = 0f;
        [Range(5, 120)] public float creaseAngle = 45f;
        [Range(0.0001f, 0.02f)] public float depthThreshold = 0.002f;
        public Color outlineColor = Color.black;
        public bool surfaceLighting = true;
        public bool continuous = true;

        private Camera captureCamera;
        private Material geometryMaterial;
        private Material compositeMaterial;
        private CommandBuffer colorCommands;
        private CommandBuffer geometryCommands;
        private readonly OutlineBuffers buffers = new OutlineBuffers();
        private readonly OutlineDrawList drawList = new OutlineDrawList();

        //将相机设为手动采集，避免将离屏模型重复画到屏幕。
        private void OnEnable()
        {
            captureCamera = GetComponent<Camera>();
            captureCamera.enabled = false;
        }

        //在交互编辑和运行模式更新纹理，批处理构建不触发预览，错误直接报告并停止自动重试。
        private void LateUpdate()
        {
            if (Application.isBatchMode || !continuous || target == null || output == null) return;
            try { RenderNow(); }
            catch (Exception exception)
            {
                continuous = false;
                Debug.LogException(exception, this);
            }
        }

        //准备专用材质和命令缓冲，拒绝未配置的渲染资源。
        private void Prepare()
        {
            if (target == null || output == null || geometryShader == null || compositeShader == null)
                throw new InvalidOperationException("请指定目标炮塔、输出纹理和两个描边 Shader。");
            if (GraphicsSettings.currentRenderPipeline != null)
                throw new NotSupportedException("炮塔描边采集器使用内置渲染管线。");
            if (geometryMaterial == null)
                geometryMaterial = new Material(geometryShader) { hideFlags = HideFlags.HideAndDontSave };
            if (compositeMaterial == null)
                compositeMaterial = new Material(compositeShader) { hideFlags = HideFlags.HideAndDontSave };
            if (colorCommands == null) colorCommands = new CommandBuffer { name = "炮塔颜色采集" };
            if (geometryCommands == null) geometryCommands = new CommandBuffer { name = "炮塔几何采集" };
            if (captureCamera == null) captureCamera = GetComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.orthographic = orthographicProjection;
            captureCamera.cullingMask = 0;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.clear;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            captureCamera.renderingPath = RenderingPath.Forward;
            //最终合成纹理不交给相机预览绘制，避免预览清屏覆盖描边结果。
            captureCamera.targetTexture = null;
            if (!output.IsCreated() && !output.Create())
                throw new InvalidOperationException("无法创建炮塔描边输出纹理。");
            captureCamera.aspect = (float)output.width / output.height;
            drawList.Collect(target);
        }

        //沿当前相机朝向取景，保证模型包围球处于指定投影的视锥内部。
        [ContextMenu("对准并容纳目标")]
        public void FrameTarget()
        {
            Prepare();
            var bounds = drawList.Build(colorCommands, geometryCommands, geometryMaterial);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            float halfAngle = captureCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            halfAngle = Mathf.Min(halfAngle, Mathf.Atan(Mathf.Tan(halfAngle) * captureCamera.aspect));
            float distance = radius / Mathf.Sin(halfAngle) * 1.12f;
            if (orthographicProjection)
            {
                captureCamera.orthographicSize = radius * 1.12f / Mathf.Min(1f, captureCamera.aspect);
                distance = radius * 3;
            }
            transform.position = bounds.center - transform.forward * distance;
            captureCamera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 1.5f);
            captureCamera.farClipPlane = distance + radius * 1.5f;
        }

        //执行颜色、几何、表面光影与描边合成，以及透明抗锯齿输出。
        [ContextMenu("更新描边纹理")]
        public void RenderNow()
        {
            Prepare();
            int scale = Mathf.Clamp(supersampling, 1, 2);
            buffers.Ensure(output.width * scale, output.height * scale);
            var bounds = drawList.Build(colorCommands, geometryCommands, geometryMaterial);
            var previousActive = RenderTexture.active;
            bool previousColorWrite = GL.sRGBWrite;
            try
            {
                Capture(buffers.Color, colorCommands);
                Capture(buffers.Geometry, geometryCommands);
                compositeMaterial.SetTexture("_GeometryTex", buffers.Geometry);
                compositeMaterial.SetFloat("_SilhouetteWidth", silhouetteWidth * scale);
                compositeMaterial.SetFloat("_StructureWidth", structureWidth * scale);
                compositeMaterial.SetFloat("_NormalThreshold", 1f - Mathf.Cos(creaseAngle * Mathf.Deg2Rad));
                compositeMaterial.SetFloat("_DepthThreshold", Mathf.Max(0.000001f, bounds.size.magnitude * depthThreshold));
                compositeMaterial.SetColor("_OutlineColor", outlineColor);
                compositeMaterial.SetFloat("_Supersampling", scale);
                compositeMaterial.SetFloat("_OutputPremultiplied", 0);
                TurretSurfaceLighting.Apply(compositeMaterial, captureCamera, bounds.size.magnitude, surfaceLighting);
                if (surfaceLighting) TurretSurfaceLighting.RenderContact(buffers, compositeMaterial);
                OutlineColorBlit.Draw(buffers.Color, buffers.Composite, compositeMaterial, 0);
                OutlineColorBlit.Draw(buffers.Composite, output, compositeMaterial, 1);
            }
            finally { RenderTexture.active = previousActive; GL.sRGBWrite = previousColorWrite; }
        }

        //只提交目标网格，并在采集结束后恢复相机目标与命令状态。
        private void Capture(RenderTexture texture, CommandBuffer commands)
        {
            var previous = captureCamera.targetTexture;
            bool previousColorWrite = GL.sRGBWrite;
            captureCamera.targetTexture = texture;
            GL.sRGBWrite = texture.sRGB;
            captureCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commands);
            try { captureCamera.Render(); }
            finally
            {
                captureCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commands);
                captureCamera.targetTexture = previous;
                GL.sRGBWrite = previousColorWrite;
            }
        }

        //释放组件私有资源，避免编辑器重载后残留显存和绘制命令。
        private void OnDisable()
        {
            buffers.Dispose();
            colorCommands?.Dispose();
            geometryCommands?.Dispose();
            colorCommands = geometryCommands = null;
            OutlineBuffers.Release(geometryMaterial);
            OutlineBuffers.Release(compositeMaterial);
            geometryMaterial = compositeMaterial = null;
        }
    }
}
