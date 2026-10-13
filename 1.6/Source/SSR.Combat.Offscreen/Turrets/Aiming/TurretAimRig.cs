using System;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //绑定单个游戏预制体的活动轴和炮口，统一地图投影与瞄准空间。
    internal sealed class TurretAimRig
    {
        private readonly Building_ConfigurableTurret owner;
        private readonly OffscreenTurretComp comp;
        private readonly TurretAimSettings settings;
        private readonly Quaternion yawRest, pitchRest;
        private readonly Transform[] radars;
        private readonly Quaternion[] radarRest;
        internal readonly GameObject Model;
        internal readonly Transform Yaw, Pitch, Muzzle;

        //只接受分离且父子顺序正确的转向挂点，路径错误直接报告模型名称。
        internal TurretAimRig(Building_ConfigurableTurret owner, OffscreenTurretComp comp, GameObject model)
        {
            this.owner = owner;
            this.comp = comp;
            settings = owner.Settings;
            Model = model;
            var root = Require(settings.rootTransform);
            Yaw = Require(settings.yawPath);
            Pitch = Require(settings.pitchPath);
            Muzzle = Require(settings.shootingOrigine);
            if (Yaw == Pitch || !Yaw.IsChildOf(root) || !Pitch.IsChildOf(Yaw) || !Muzzle.IsChildOf(Pitch))
                throw new InvalidOperationException("炮塔必须使用根节点→旋转→俯仰→炮口的独立层级：" + owner.def.defName);
            yawRest = Yaw.localRotation;
            pitchRest = Pitch.localRotation;
            radars = new Transform[settings.radarPaths.Count];
            radarRest = new Quaternion[radars.Length];
            for (int i = 0; i < radars.Length; i++)
            {
                radars[i] = Require(settings.radarPaths[i]);
                if (!radars[i].IsChildOf(Yaw) || radars[i].IsChildOf(Pitch))
                    throw new InvalidOperationException("雷达须位于偏航节点下，并使用独立于炮管的俯仰转轴：" + settings.radarPaths[i]);
                radarRest[i] = radars[i].localRotation;
            }
        }

        //在逻辑刻中同步模型位置，瞄准无需等待摄像机看见炮塔。
        internal void Synchronize()
        {
            var graphic = owner.Graphic;
            Model.transform.SetPositionAndRotation(owner.MapDrawPosition + graphic.DrawOffset(owner.Rotation),
                Quaternion.Euler(0, owner.Rotation.AsAngle, 0));
            Model.transform.localScale = new Vector3(graphic.drawSize.x, 1, graphic.drawSize.y);
        }

        //更新炮管姿态，并让雷达绕自身转轴跟随炮口方向。
        internal void Apply(float yaw, float pitch)
        {
            Yaw.localRotation = yawRest * Quaternion.AngleAxis(yaw, settings.yawRotationAixe.normalized);
            Pitch.localRotation = pitchRest * Quaternion.AngleAxis(pitch, settings.pitchRotationAixe.normalized);
            for (int i = 0; i < radars.Length; i++)
            {
                Vector3 direction = radars[i].parent.InverseTransformVector(Forward).normalized;
                radars[i].localRotation = Quaternion.FromToRotation(radarRest[i] * Vector3.forward, direction) * radarRest[i];
            }
        }

        //将统一目标坐标转换为与炮塔网格一致的采集空间，可选提前量只影响瞄准。
        internal Vector3 TargetPoint(TurretTargetState state)
        {
            Vector3 anchor = comp.Submission.Frame.GroundAnchor;
            Vector3 point = state.Position + state.Velocity * settings.targeting.leadTimeSeconds;
            float groundProjection = (TurretCaptureProfile.Direction * Vector3.up).z;
            return new Vector3(point.x, anchor.y + Mathf.Max(0, point.y), anchor.z + (point.z - anchor.z) / groundProjection);
        }

        //返回炮口前向，用于求解瞄准误差和开火锥角。
        internal Vector3 Forward => Muzzle.TransformVector(settings.shootingOrigineForward).normalized;

        //记录发射瞬间的网格炮口与地面基准，弹丸不再随炮塔后续转动改变方向。
        internal TurretShotPose CaptureShot(TurretTargetState target)
        {
            return new TurretShotPose(Muzzle.position, Forward, comp.Submission.Frame.GroundAnchor, TargetPoint(target));
        }

        //将实际炮口投影回地图，使二维弹丸出生位置与离屏影像中的炮口重合。
        internal Vector3 MuzzleMapPosition
        {
            get
            {
                Vector3 anchor = comp.Submission.Frame.GroundAnchor;
                return new Vector3(Muzzle.position.x, owner.MapDrawPosition.y,
                    anchor.z + Vector3.Dot(Muzzle.position - anchor, TurretCaptureProfile.Direction * Vector3.up));
            }
        }

        //查找配置挂点，禁止缺失节点被当作已经完成瞄准。
        private Transform Require(string path)
        {
            var node = Model.transform.Find(path);
            if (!node) throw new InvalidOperationException("炮塔挂点不存在：" + owner.def.defName + " / " + path);
            return node;
        }
    }
}
