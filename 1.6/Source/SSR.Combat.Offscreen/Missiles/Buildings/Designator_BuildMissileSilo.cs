using System.Linq;
using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //按原版研究门槛显示导弹井，使用完整模型的建筑图标。
    public sealed class Designator_BuildMissileSilo : Designator_Build
    {
        //接收 SSR 建筑分类传入的定义，复用原版材质选择和蓝图放置。
        public Designator_BuildMissileSilo(BuildableDef def) : base(def) { }

        //在放置入口检查研究，避免通过快捷选择绕过按钮的锁定状态。
        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            if (!DebugSettings.godMode && !entDef.IsResearchFinished) return ResearchRequirement();
            return base.CanDesignateCell(cell);
        }

        //列出尚未完成的实际研究定义名称。
        private string ResearchRequirement()
        {
            return "需要完成研究：" + string.Join("、", entDef.researchPrerequisites.Where(r => !r.IsFinished).Select(r => r.label));
        }
    }
}
