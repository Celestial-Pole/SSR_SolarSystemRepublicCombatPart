"""将本项目资源包的网格数据原样内嵌，供 Waylith 读取，不启动 Unity。"""

import hashlib
from pathlib import Path

import UnityPy
from UnityPy.helpers.ResourceReader import get_resource_data


def mesh_snapshot(mesh):
    #记录顶点和索引字节及布局，转换前后必须完全一致。
    vertex = mesh.m_VertexData
    stream = mesh.m_StreamData
    data = bytes(vertex.m_DataSize)
    if stream.path:
        data = get_resource_data(stream.path, mesh.object_reader.assets_file, stream.offset, stream.size)
    if mesh.m_MeshCompression != 0 or not data or vertex.m_VertexCount == 0:
        raise ValueError(f"网格压缩或顶点数据无效：{mesh.m_Name}")
    if any(sub.topology != 0 for sub in mesh.m_SubMeshes):
        raise ValueError(f"网格不是三角形拓扑：{mesh.m_Name}")
    return (mesh.m_Name, vertex.m_VertexCount, data, vertex.m_Channels,
            bytes(mesh.m_IndexBuffer), mesh.m_IndexFormat, mesh.m_SubMeshes)


def main():
    #原包继续供 1.6 使用，转换包只写入 1.7 目录。
    root = Path(__file__).resolve().parents[2]
    source = root / "Asset/Windows/ssr_combat_windows"
    target = root / "1.7/Asset/Windows/ssr_combat_windows"
    env = UnityPy.load(str(source))
    before = {}
    converted = 0
    for obj in env.objects:
        if obj.type.name != "Mesh":
            continue
        mesh = obj.read()
        snapshot = mesh_snapshot(mesh)
        before[obj.path_id] = snapshot
        if mesh.m_StreamData.path:
            mesh.m_VertexData.m_DataSize = snapshot[2]
            mesh.m_StreamData.path = ""
            mesh.m_StreamData.offset = 0
            mesh.m_StreamData.size = 0
            mesh.save()
            converted += 1

    packed = next(iter(env.files.values())).save(packer="lz4")
    result = UnityPy.load(packed)
    after = {}
    for obj in result.objects:
        if obj.type.name != "Mesh":
            continue
        mesh = obj.read()
        if mesh.m_StreamData.path:
            raise ValueError(f"转换后仍包含外置网格：{mesh.m_Name}")
        after[obj.path_id] = mesh_snapshot(mesh)
    if before != after:
        raise ValueError("转换前后的网格顶点、布局或索引不一致，停止输出")

    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(packed)
    target.with_suffix(".source.sha256").write_text(hashlib.sha256(source.read_bytes()).hexdigest(), encoding="utf-8")
    print(f"已内嵌 {converted} 个网格资源流；全部 {len(before)} 个网格数据一致：{target}")


if __name__ == "__main__":
    main()
