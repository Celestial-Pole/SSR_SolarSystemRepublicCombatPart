using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //暂停模拟并用滑条预览炮塔姿态，关闭后恢复机械角度。
    internal sealed class Window_TurretAngles : Window
    {
        private const string Hint = "拖动滑条可直接观察地图上的炮塔。预览时暂停，关闭后恢复原姿态。角度以各型号的机械零位为基准。";
        private readonly Building_ConfigurableTurret turret;
        private readonly Building_CombinedAirDefense combined;
        private float originalYaw, originalPitch, originalMissilePitch;
        private float yaw, pitch, missilePitch;
        private Vector2 scrollPosition;
        public override Vector2 InitialSize => new Vector2(440, Mathf.Min(UI.screenHeight - 60, combined == null ? 400 : 480));

        //允许移动镜头和窗口，暂停期间禁止把预览角度写进存档。
        internal Window_TurretAngles(Building_ConfigurableTurret turret)
        {
            this.turret = turret;
            combined = turret as Building_CombinedAirDefense;
            optionalTitle = "炮塔角度预览";
            doCloseX = doCloseButton = draggable = forcePause = preventSave = true;
            preventCameraMotion = false;
            closeOnAccept = false;
        }

        //在旧预览窗口关闭之后记录实际姿态。
        public override void PreOpen()
        {
            base.PreOpen();
            originalYaw = turret.Aim.Yaw;
            originalPitch = turret.Aim.Pitch;
            if (combined != null) originalMissilePitch = combined.Missiles.Pitch;
            ResetSliders();
        }

        //将面板放在右侧，留出地图中央用于观察。
        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            windowRect.x = UI.screenWidth - windowRect.width - 20;
            windowRect.y = 30;
        }

        //地图或建筑离开视图时结束预览。
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (!turret.Spawned || turret.Map != Find.CurrentMap) Close();
        }

        //恢复打开窗口时的角度，不改变射击目标和伺服速度。
        public override void PostClose()
        {
            turret.Aim.SetPreviewAngles(originalYaw, originalPitch);
            combined?.Missiles.SetPreviewPitch(originalMissilePitch);
            base.PostClose();
        }

        //按文字高度排列滑条，并为关闭按钮保留独立区域。
        public override void DoWindowContents(Rect inRect)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            var oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
                var body = new Rect(0, 0, inRect.width, inRect.height - FooterRowHeight);
                float width = body.width - 16;
                float headingHeight = Text.CalcHeight(turret.LabelCap, width);
                float hintHeight = Text.CalcHeight(Hint, width);
                float axisHeight = Text.LineHeight + 40;
                float height = headingHeight + hintHeight + 24 + axisHeight * (combined == null ? 2 : 3) + 44;
                Widgets.BeginScrollView(body, ref scrollPosition, new Rect(0, 0, width, Mathf.Max(body.height, height)));
                try
                {
                    Widgets.Label(new Rect(0, 0, width, headingHeight), turret.LabelCap);
                    float y = headingHeight + 8;
                    Widgets.Label(new Rect(0, y, width, hintHeight), Hint);
                    y += hintHeight + 16;
                    float oldYaw = yaw, oldPitch = pitch, oldMissilePitch = missilePitch;
                    yaw = DrawAngle(width, ref y, "水平旋转", yaw, turret.Settings.yawRotationRange);
                    pitch = DrawAngle(width, ref y, "炮管俯仰", pitch, turret.Settings.pitchRotationRange);
                    if (combined != null)
                        missilePitch = DrawAngle(width, ref y, "导弹架俯仰", missilePitch, combined.CombinedSettings.missilePitchRange);
                    if (Widgets.ButtonText(new Rect(0, y, width, 36), "恢复原姿态")) ResetSliders();
                    if (yaw != oldYaw || pitch != oldPitch || missilePitch != oldMissilePitch)
                    {
                        turret.Aim.SetPreviewAngles(yaw, pitch);
                        combined?.Missiles.SetPreviewPitch(missilePitch);
                    }
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
                GUI.color = oldColor;
            }
        }

        //显示机械限位内的连续角度，跨零度范围按同一段圆弧展开。
        private static float DrawAngle(float width, ref float y, string label, float angle, Vector2 range)
        {
            var bounds = SliderBounds(range);
            Widgets.Label(new Rect(0, y, width, Text.LineHeight),
                $"{label}：{angle:F1}°（{bounds.x:F0}° ～ {bounds.y:F0}°）");
            y += Text.LineHeight + 4;
            angle = Widgets.HorizontalSlider(new Rect(0, y, width, 24), angle, bounds.x, bounds.y,
                middleAlignment: true, roundTo: 0.1f);
            y += 36;
            return Mathf.Clamp(angle, bounds.x, bounds.y);
        }

        //相同端点表示整周，其余范围以有符号起点连续展开。
        private static Vector2 SliderBounds(Vector2 range)
        {
            float extent = Mathf.Repeat(range.y - range.x, 360);
            float start = Mathf.DeltaAngle(0, range.x);
            return extent < 0.0001f ? new Vector2(0, 360) : new Vector2(start, start + extent);
        }

        //把当前机械角度换算为滑条中的连续角度。
        private static float SliderAngle(float angle, Vector2 range)
        {
            var bounds = SliderBounds(range);
            return bounds.x + Mathf.Repeat(TurretAngleLimits.Clamp(angle, range) - bounds.x, 360);
        }

        //还原滑条初始值，保留模型原有的机械零位。
        private void ResetSliders()
        {
            yaw = SliderAngle(originalYaw, turret.Settings.yawRotationRange);
            pitch = SliderAngle(originalPitch, turret.Settings.pitchRotationRange);
            if (combined != null) missilePitch = SliderAngle(originalMissilePitch, combined.CombinedSettings.missilePitchRange);
        }
    }
}
