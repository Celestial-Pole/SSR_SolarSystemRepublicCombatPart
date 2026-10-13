using System.Collections.Generic;
using Verse;

namespace SSR.Combat.Offscreen;

//接收现有炮塔 XML 的资源字段，模型创建由本项目负责。
public class SsrPrefabProperties : CompProperties
{
    public string shaderForTestAssetBundle;
    public List<string> assetDefs;
    public List<string> leveledPrefabPaths;
}
