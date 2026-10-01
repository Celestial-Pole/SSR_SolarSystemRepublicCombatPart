using System;
using SSR.UnityComponent.Missiles;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //检查发射配置与真实弹体尺寸，阻止井内点火、回落夹弹和不可达的动力高度。
    internal static class SiloSettingsValidator
    {
        //验证所有舱位共享的时间、动力和随机扰动范围，再核对各舱实际行程。
        internal static void Validate(SiloSettings settings, SiloLayoutData layout)
        {
            if (settings.launchInterval <= 0 || settings.cooldownSeconds < 0
                || settings.ignitionAcceleration <= 0 || settings.ignitionRampSeconds <= 0
                || settings.flightAcceleration <= 0 || settings.minimumTurnRadius <= 0
                || settings.turnRadiusFraction <= 0 || settings.turnRadiusFraction >= 0.5f)
                throw new InvalidOperationException("导弹井发射间隔、冷却或动力参数无效：" + settings.prefabPath);
            if (settings.ejectionSpeed <= 0 || settings.ejectionGravity <= 0 || settings.ignitionDelay <= 0
                || settings.ejectionClearance < 0.08f || settings.ejectionDisturbanceSeconds <= 0)
                throw new InvalidOperationException("冷发射的速度、重力和扰动时长必须为正，弹尾离井余量至少为 0.08 格。");
            if (settings.coldLateralSpeedRange.min < 0 || settings.coldLateralSpeedRange.max > 1
                || settings.coldLateralSpeedRange.min > settings.coldLateralSpeedRange.max)
                throw new InvalidOperationException("冷弹射侧向扰动范围无效：侧速为零至每秒一格。");
            if (settings.flameWidth <= 0 || settings.flameLength <= 0 || settings.flameBrightness <= 0
                || settings.ignitionFlareMultiplier < 1 || settings.ignitionFlareSeconds <= 0)
                throw new InvalidOperationException("导弹尾焰宽度、长度、亮度或点火膨胀参数无效。");
            if (settings.flameTrailSeconds <= 0 || settings.flameTrailLength <= 0
                || settings.flameGasSpeed < 0 || settings.flameTurbulence < 0)
                throw new InvalidOperationException("导弹热尾流寿命、长度、气流速度或扰动幅度无效。");
            if (settings.smokeSpacing < 0.1f || settings.smokeLifetime <= 0
                || settings.smokeStartSize <= 0 || settings.smokeEndSize < settings.smokeStartSize
                || settings.smokeOpacity <= 0 || settings.smokeOpacity > 1)
                throw new InvalidOperationException("导弹烟团间距至少为 0.1 格，尺寸及寿命必须为正，浓度必须在零至一之间。");
            foreach (var slot in layout.slots) ValidateSlot(settings, layout, slot);
        }

        //根据弹尾位置求离井时刻和自由滑行余速，检查点火及推力建立过程仍在上升。
        private static void ValidateSlot(SiloSettings settings, SiloLayoutData layout, SiloSlotData slot)
        {
            float tailHeight = (slot.position + slot.rotation * new Vector3(0, 0, slot.tailZ * slot.scale.z)).y;
            float stroke = layout.clearanceHeight + settings.ejectionClearance - tailHeight;
            float releaseSeconds = 2 * stroke / settings.ejectionSpeed;
            float coast = settings.ignitionDelay - releaseSeconds;
            if (stroke <= 0 || coast <= settings.ejectionDisturbanceSeconds)
                throw new InvalidOperationException("冷弹射点火过早，导向行程和离井扰动尚未完成：" + slot.path);
            float speed = settings.ejectionSpeed - settings.ejectionGravity * coast;
            float lowestSpeed = speed - settings.ejectionGravity * settings.ejectionGravity * settings.ignitionRampSeconds
                / (2 * (settings.ignitionAcceleration + settings.ejectionGravity));
            if (lowestSpeed <= 0)
                throw new InvalidOperationException("导弹点火或推力建立过晚，弹体会先回落，请缩短 ignitionDelay 或 ignitionRampSeconds：" + slot.path);
            float center = slot.position.y + stroke + settings.ejectionSpeed * coast
                - 0.5f * settings.ejectionGravity * coast * coast;
            float tipOffset = (slot.rotation * Vector3.Scale(slot.tip, slot.scale)).y;
            float ramp = settings.ignitionRampSeconds;
            float height = center + tipOffset + speed * ramp
                + (settings.ignitionAcceleration - 2 * settings.ejectionGravity) * ramp * ramp / 6;
            if (settings.ascentHeight <= height)
                throw new InvalidOperationException("抬升终点低于发动机完成推力建立时的弹头高度：" + slot.path);
        }
    }
}
