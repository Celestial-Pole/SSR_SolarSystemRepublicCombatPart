using System;
using UnityEditor;
using UnityEngine;

namespace SSR.UnityComponent.Outline.Editor
{
    //显示实时描边纹理，并提供目标选择、重新取景和透明图片导出入口。
    public sealed class TurretOutlineWindow : EditorWindow
    {
        [SerializeField] private TurretOutlineCapture capture;
        private double lastRenderTime;

        [MenuItem("SSR/炮塔描边/打开预览")]
        private static void Open()
        {
            ShowCapture(FindObjectOfType<TurretOutlineCapture>());
        }

        //绑定采集器，使相机参数调整立即反映在预览中。
        public static void ShowCapture(TurretOutlineCapture value)
        {
            var window = GetWindow<TurretOutlineWindow>("炮塔描边预览");
            window.capture = value;
            window.minSize = new Vector2(400, 450);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        //交互编辑的非播放状态每秒最多刷新十次，批处理构建不触发预览，异常交给控制台显示。
        private void Tick()
        {
            if (Application.isBatchMode || capture == null || !capture.isActiveAndEnabled || !capture.continuous || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.timeSinceStartup - lastRenderTime < 0.1) return;
            lastRenderTime = EditorApplication.timeSinceStartup;
            try { capture.RenderNow(); }
            catch (Exception exception)
            {
                capture.continuous = false;
                Debug.LogException(exception, capture);
            }
            Repaint();
        }

        //绘制控制入口和带透明背景的输出纹理。
        private void OnGUI()
        {
            capture = (TurretOutlineCapture)EditorGUILayout.ObjectField("采集器", capture, typeof(TurretOutlineCapture), true);
            if (capture == null) { EditorGUILayout.HelpBox("请打开 TurretOutline 场景并指定采集器。", MessageType.Info); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选择相机与参数")) Selection.activeGameObject = capture.gameObject;
                if (GUILayout.Button("重新取景"))
                {
                    Undo.RecordObject(capture.transform, "炮塔描边取景");
                    capture.FrameTarget();
                    capture.RenderNow();
                }
                if (GUILayout.Button("导出透明 PNG")) Export();
            }
            string projection = capture.orthographicProjection ? "正交采集" : "透视采集";
            string outline = capture.structureWidth > 0 ? "外轮廓＋结构折线" : "仅外轮廓";
            EditorGUILayout.LabelField(outline + " · " + projection + " · 透明背景");
            if (capture.output == null) return;
            Rect area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawTextureTransparent(area, capture.output, ScaleMode.ScaleToFit);
        }

        //保存最终直通透明度纹理，恢复读回前的渲染目标。
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("导出炮塔描边", "", capture.target.name + ".png", "png");
            if (string.IsNullOrEmpty(path)) return;
            capture.RenderNow();
            var previous = RenderTexture.active;
            var texture = new Texture2D(capture.output.width, capture.output.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = capture.output;
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                DestroyImmediate(texture);
            }
        }
    }
}
