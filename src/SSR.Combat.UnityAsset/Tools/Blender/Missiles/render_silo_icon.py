import os
import bpy
from mathutils import Vector


#将线性材质颜色转换为模型色卡使用的显示颜色。
def display_color(value):
    return 12.92 * value if value <= 0.0031308 else 1.055 * value ** (1 / 2.4) - 0.055


#将显示颜色转换回 Blender 材质所需的线性颜色。
def linear_color(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


#按游戏的半球环境光和主辅光烘焙各面颜色，保留模型色卡的中性色调。
def bake_surface_colors(meshes):
    key = Vector((-0.6, 1, -0.45)).normalized()
    fill = Vector((0.8, 0.5, 0.2)).normalized()
    for obj in meshes:
        obj.data = obj.data.copy()
        sources = [material.diffuse_color[:3] for material in obj.data.materials]
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        materials = {}
        for polygon in obj.data.polygons:
            normal = (normal_matrix @ polygon.normal).normalized()
            #源模型的高度轴为 Z，游戏的高度轴为 Y；编号行从北侧向南侧排列。
            world_normal = Vector((normal.x, normal.z, -normal.y))
            diffuse = 0.34 + 0.26 * max(0, min(1, world_normal.y * 0.5 + 0.5))
            diffuse += max(0, world_normal.dot(key)) * 0.42 + max(0, world_normal.dot(fill)) * 0.12
            color = tuple(linear_color(min(1, display_color(channel) * diffuse))
                          for channel in sources[polygon.material_index])
            color_key = tuple(round(channel, 4) for channel in color)
            if color_key not in materials:
                material = bpy.data.materials.new('导弹井图标表面')
                material.diffuse_color = (*color, 1)
                materials[color_key] = len(obj.data.materials)
                obj.data.materials.append(material)
            polygon.material_index = materials[color_key]


#从闭合导弹井源模型生成透明背景的完整建筑菜单图标。
def render_icon():
    scene = bpy.context.scene
    scene.frame_set(1)
    #菜单图标只显示井体和舱盖，隐藏装填导弹后按井体尺寸取景。
    for number in range(1, 10):
        bpy.data.objects[f'Missile{number:02d}'].hide_render = True
    meshes = [obj for obj in scene.objects if obj.type == 'MESH' and not obj.hide_render]
    bake_surface_colors(meshes)
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    lower = Vector([min(point[index] for point in points) for index in range(3)])
    upper = Vector([max(point[index] for point in points) for index in range(3)])
    center = (lower + upper) * 0.5
    radius = (upper - lower).length * 0.5
    camera_data = bpy.data.cameras.new('导弹井图标相机')
    camera = bpy.data.objects.new('导弹井图标相机', camera_data)
    scene.collection.objects.link(camera)
    direction = Vector((0, -0.5, 0.8660254)).normalized()
    camera.location = center + direction * radius * 4
    camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    #按投影范围收紧留白，使井体完整占据图标主体。
    inverse = camera.rotation_euler.to_matrix().transposed()
    projected = [inverse @ (point - center) for point in points]
    camera_data.ortho_scale = max(max(p.x for p in projected) - min(p.x for p in projected),
                                  max(p.y for p in projected) - min(p.y for p in projected)) * 1.12
    camera_data.clip_end = radius * 10
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    shading = scene.display.shading
    shading.light = 'FLAT'
    shading.color_type = 'MATERIAL'
    shading.show_shadows = False
    shading.show_cavity = True
    shading.cavity_type = 'BOTH'
    shading.curvature_ridge_factor = 0.3
    shading.curvature_valley_factor = 0.5
    shading.show_object_outline = True
    shading.object_outline_color = (0.02, 0.02, 0.02)
    scene.render.film_transparent = True
    #避免 Blender 默认的电影色调映射压暗游戏色卡。
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    scene.render.use_multiview = False
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../../..'))
    scene.render.filepath = os.path.join(root, '1.6/Textures/Things/Building/Security/MissileSilo/MissileSilo_MenuIcon.png')
    os.makedirs(os.path.dirname(scene.render.filepath), exist_ok=True)
    bpy.ops.render.render(write_still=True)


render_icon()
