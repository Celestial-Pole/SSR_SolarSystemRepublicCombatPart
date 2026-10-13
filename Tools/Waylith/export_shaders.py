"""将炮塔原有合成公式转换为 Waylith GLSL，保留采样核、光照及描边参数。"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'src/SSR.Combat.UnityAsset/Assets/SSR/Outline/Shaders'
OUTPUT = ROOT / '1.7/Shaders'


def glsl(source):
    #这里只转换炮塔已有的 Cg 表达式；新增不兼容语法由 Shader 编译直接报告。
    source = re.sub(r'\bfloat4x4\b', 'mat4', source)
    for size in (2, 3, 4):
        source = re.sub(rf'\bfloat{size}\b', f'vec{size}', source)
    source = source.replace('tex2D(', 'texture(').replace('lerp(', 'mix(').replace('frac(', 'fract(')
    source = source.replace('[unroll]', '').replace(': SV_Target', '')
    source = source.replace('UNITY_PI', '3.141592653589793')
    source = re.sub(r'\binput\b', 'sampleData', source)
    source = re.sub(r'\bpacked\b', 'encoded', source)
    source = source.replace('any(uv < 0)', 'any(lessThan(uv, vec2(0)))')
    source = source.replace('any(uv > 1)', 'any(greaterThan(uv, vec2(1)))')
    source = re.sub(r'mul\(_CaptureToWorld, (vec4\([^;]+?\))\)', r'(_CaptureToWorld * \1)', source)
    source = re.sub(r'(const vec2 \w+\[(\d+)\] = )\{(.*?)\};',
                    lambda m: m[1] + 'vec2[' + m[2] + '](' + m[3] + ');', source, flags=re.S)
    source = source.replace('return 0;\n            return texture', 'return vec4(0);\n            return texture', 1)
    source = source.replace('strength <= 0) return 1;', 'strength <= 0) return vec4(1);')
    source = source.replace('_RotorLightingBlend >= 1)) return 1;', '_RotorLightingBlend >= 1)) return vec4(1);')
    source = source.replace('value.rgb / value.a : 0;', 'value.rgb / value.a : vec3(0);')
    return source


def export_exhaust():
    #烟焰仍使用原噪声、年龄衰减、浓度和高温焰芯公式。
    source = (SOURCE / 'MissileExhaust.shader').read_text(encoding='utf-8')
    source = source[source.index('            float Hash('):source.index('            ENDCG')]
    source = glsl(source).replace('clip(sampleData.height);', 'if (sampleData.height < 0) discard;')
    header = '''#version 450
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec3 vWorldPos;
layout(location = 4) in vec2 vData;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @default = vec4(0.6, 0.6, 0.6, 0.6) @color
    float _Flame;
    float _Age;
    float _Seed;
    float _Clock;
    float _VertexData;
    float _Trail;
    float _Opacity; // @default = 1
    float _FlameBrightness; // @default = 1.35
};
struct Varyings { vec2 uv; float height; vec2 data; };
float saturate(float x) { return clamp(x, 0.0, 1.0); }
'''
    footer = '''
//按原来顶点参数的插值规则接入手工烟焰网格。
void main() { outColor = Frag(Varyings(vUv, vWorldPos.y, mix(vec2(_Age, _Seed), vData, _VertexData))); }
'''
    (OUTPUT / 'SSR_Exhaust.frag.glsl').write_text(header + source + footer, encoding='utf-8')


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = (SOURCE / 'TurretOutline.shader').read_text(encoding='utf-8')
    source = source.split('CGINCLUDE', 1)[1].split('ENDCG', 1)[0]
    source = source.replace('#include "UnityCG.cginc"', '')
    for include in ['TurretGeometryPacking.cginc', 'TurretContactOcclusion.cginc', 'TurretSurfaceLighting.cginc']:
        source = source.replace('#include "' + include + '"', (SOURCE / include).read_text(encoding='utf-8'))
    uniforms = {}
    samplers = []

    def capture(match):
        #把原有材质参数放进同一描述集合，名称不变以便沿用 C# 配置。
        kind, names = match.groups()
        for name in names.split(','):
            name = name.strip()
            if kind == 'sampler2D':
                samplers.append(name)
            else:
                uniforms[name] = kind
        return ''

    source = re.sub(r'^\s*(float(?:[234](?:x[234])?)?|sampler2D)\s+(_\w+(?:,\s*_\w+)*)\s*;\s*$',
                    capture, source, flags=re.M)
    block = 'layout(set = 2, binding = 0) uniform MaterialUniforms\n{\n'
    for name, kind in uniforms.items():
        block += f'    {glsl(kind)} {name};\n'
    block += '};\n'
    for index, name in enumerate(samplers, 1):
        block += f'layout(set = 2, binding = {index}) uniform sampler2D {name};\n'
    common = block + '''
struct v2f_img { vec2 uv; };
float saturate(float x) { return clamp(x, 0.0, 1.0); }
vec2 saturate(vec2 x) { return clamp(x, vec2(0), vec2(1)); }
vec3 saturate(vec3 x) { return clamp(x, vec3(0), vec3(1)); }
'''
    common += glsl(source)
    (OUTPUT / 'SSR_CompositeCommon.glsl').write_text(common, encoding='utf-8')
    for name, function in [('Composite', 'Composite'), ('Resolve', 'Resolve'), ('Contact', 'CaptureTurretContact'),
                           ('ContactFilter', 'FilterTurretContact'), ('SurfaceHeight', 'ResolveSurfaceHeight')]:
        expression = f'{function}(v2f_img(vUv))'
        if name == 'SurfaceHeight':
            expression = f'vec4({expression}, 0, 0, 1)'
        text = f'''#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "SSR_CompositeCommon.glsl"
//使用原始公式执行单个合成通道。
void main() {{ outColor = {expression}; }}
'''
        (OUTPUT / f'SSR_{name}.frag.glsl').write_text(text, encoding='utf-8')
    export_exhaust()
    print('已转换五个光影、描边和高度通道，以及烟焰通道。')


if __name__ == '__main__':
    main()
