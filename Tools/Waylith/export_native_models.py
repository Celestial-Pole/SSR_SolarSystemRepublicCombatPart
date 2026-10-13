"""一次性把已有资源转换为标准 GLB，之后游戏直接读取 GLB，不加载 Unity 资源包。"""
from pathlib import Path
import hashlib
import json
import struct
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

ROOT = Path(__file__).resolve().parents[2]


class Exporter:
    """共享网格与材质，保留每个模型的节点名、挂点、显隐和原始参数。"""
    def __init__(self):
        self.binary = bytearray()
        self.doc = {"asset": {"version": "2.0", "generator": "SSR native model export"},
                    "nodes": [], "scenes": [], "meshes": [], "materials": [],
                    "accessors": [], "bufferViews": []}
        self.geometry = {}
        self.meshes = {}
        self.materials = {}

    def accessor(self, values, kind, components, integer=False):
        """按 glTF 的连续小端布局保存原始精度的数据。"""
        while len(self.binary) % 4:
            self.binary.append(0)
        offset = len(self.binary)
        flat = [v for row in values for v in row]
        self.binary.extend(struct.pack("<" + ("I" if integer else "f") * len(flat), *flat))
        view = len(self.doc["bufferViews"])
        self.doc["bufferViews"].append({"buffer": 0, "byteOffset": offset,
                                         "byteLength": len(self.binary) - offset})
        accessor = {"bufferView": view, "componentType": 5125 if integer else 5126,
                    "count": len(values), "type": kind}
        if not integer and components == 3:
            accessor["min"] = [min(row[i] for row in values) for i in range(3)]
            accessor["max"] = [max(row[i] for row in values) for i in range(3)]
        index = len(self.doc["accessors"])
        self.doc["accessors"].append(accessor)
        return index

    def material(self, pointer):
        """颜色和烟焰参数写入资源，运行时不读取 Unity 材质。"""
        key = pointer.path_id
        if key in self.materials:
            return self.materials[key]
        material = pointer.read()
        shader = material.m_Shader.read().m_ParsedForm.m_Name
        if shader not in ("Unlit/ColorOnly", "SSR/Missiles/Exhaust"):
            raise ValueError(f"不支持的实体材质：{material.m_Name} / {shader}")
        colors = {name: [c.r, c.g, c.b, c.a] for name, c in material.m_SavedProperties.m_Colors}
        color = colors.get("_Color", [1, 1, 1, 1])
        result = {"name": material.m_Name, "doubleSided": True,
                  "pbrMetallicRoughness": {"baseColorFactor": color, "metallicFactor": 0, "roughnessFactor": 1},
                  "extras": {"ssrShader": shader, "floats": dict(material.m_SavedProperties.m_Floats), "colors": colors}}
        if shader == "SSR/Missiles/Exhaust":
            result["alphaMode"] = "BLEND"
        index = len(self.doc["materials"])
        self.doc["materials"].append(result)
        self.materials[key] = index
        return index

    def mesh(self, pointer, material_pointers):
        """GLB 使用右手坐标，镜像 Z 并翻转三角形顺序，加载时还原游戏坐标。"""
        key = (pointer.path_id, tuple(p.path_id for p in material_pointers))
        if key in self.meshes:
            return self.meshes[key]
        if pointer.path_id not in self.geometry:
            mesh = pointer.read()
            handler = MeshHandler(mesh)
            handler.process()
            attrs = {"POSITION": self.accessor([(v[0], v[1], -v[2]) for v in handler.m_Vertices], "VEC3", 3)}
            if handler.m_Normals:
                attrs["NORMAL"] = self.accessor([(v[0], v[1], -v[2]) for v in handler.m_Normals], "VEC3", 3)
            for channel, values in enumerate((handler.m_UV0, handler.m_UV1)):
                if values:
                    attrs[f"TEXCOORD_{channel}"] = self.accessor([v[:2] for v in values], "VEC2", 2)
            triangles = [self.accessor([(v,) for a, b, c in part for v in (a, c, b)], "SCALAR", 1, True)
                         for part in handler.get_triangles()]
            self.geometry[pointer.path_id] = (mesh.m_Name, attrs, triangles)
        name, attrs, triangles = self.geometry[pointer.path_id]
        primitives = [{"attributes": attrs, "indices": indices,
                       "material": self.material(material_pointers[min(i, len(material_pointers) - 1)])}
                      for i, indices in enumerate(triangles)]
        index = len(self.doc["meshes"])
        self.doc["meshes"].append({"name": name, "primitives": primitives})
        self.meshes[key] = index
        return index

    def node(self, game_object):
        """只导出模型数据和挂点，Animator 等组件由现有 C# 动画算法替代。"""
        components = {p.component.type.name: p.component.read() for p in game_object.m_Component}
        transform = components["Transform"]
        p, q, s = transform.m_LocalPosition, transform.m_LocalRotation, transform.m_LocalScale
        node = {"name": game_object.m_Name, "translation": [p.x, p.y, -p.z],
                "rotation": [-q.x, -q.y, q.z, q.w], "scale": [s.x, s.y, s.z],
                "extras": {"active": bool(game_object.m_IsActive)}}
        renderer = components.get("MeshRenderer")
        if "SkinnedMeshRenderer" in components:
            raise ValueError(f"模型含蒙皮，不能当作静态节点导出：{game_object.m_Name}")
        if renderer and renderer.m_Enabled:
            wire = any("Wireframe" in p.read().m_Shader.read().m_ParsedForm.m_Name for p in renderer.m_Materials)
            if not wire:
                node["mesh"] = self.mesh(components["MeshFilter"].m_Mesh, renderer.m_Materials)
        index = len(self.doc["nodes"])
        self.doc["nodes"].append(node)
        node["children"] = [self.node(p.read().m_GameObject.read()) for p in transform.m_Children]
        return index

    def save(self, destination):
        """输出可直接交给 Blender 等工具读取的 GLB 2.0。"""
        self.doc["buffers"] = [{"byteLength": len(self.binary)}]
        metadata = json.dumps(self.doc, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        metadata += b" " * (-len(metadata) % 4)
        self.binary += b"\0" * (-len(self.binary) % 4)
        total = 12 + 8 + len(metadata) + 8 + len(self.binary)
        destination.write_bytes(struct.pack("<III", 0x46546C67, 2, total)
                               + struct.pack("<II", len(metadata), 0x4E4F534A) + metadata
                               + struct.pack("<II", len(self.binary), 0x004E4942) + self.binary)


def main():
    """已有包仅作为迁移输入，日常编译与游戏运行均不再需要它。"""
    source = ROOT / "Asset/Windows/ssr_combat_windows"
    destination = ROOT / "1.7/Models"
    destination.mkdir(exist_ok=True)
    exporter = Exporter()
    env = UnityPy.load(str(source))
    for path in sorted(set(env.container.keys())):
        pointer = env.container[path]
        if pointer.type.name == "GameObject":
            exporter.doc["scenes"].append({"name": path, "nodes": [exporter.node(pointer.read())]})
        elif pointer.type.name == "TextAsset":
            asset = pointer.read()
            (destination / Path(path).name).write_text(asset.m_Script, encoding="utf-8")
    exporter.save(destination / "combat.glb")
    (destination / "migration-source.sha256").write_text(hashlib.sha256(source.read_bytes()).hexdigest(), encoding="utf-8")
    print(f"导出 {len(exporter.doc['scenes'])} 个模型、{len(exporter.geometry)} 份网格、"
          f"{len(exporter.materials)} 个材质；GLB {len(exporter.binary):,} 字节几何数据。")


if __name__ == "__main__":
    main()
