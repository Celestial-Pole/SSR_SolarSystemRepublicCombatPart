layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _SSRColorTexelSize;
    vec4 _SSRGeometryTexelSize;
    vec4 _OutlineColor;
    float _SilhouetteWidth;
    float _StructureWidth;
    float _NormalThreshold;
    float _DepthThreshold;
    float _Supersampling;
    float _OutputPremultiplied;
    float _ClipOutlineGround;
    float _CaptureWorldHeight;
    float _GroundHeight;
    float _RotorLightingBlend;
    vec3 _RotorAxisDirection;
    mat4 _CaptureToWorld;
    float _SurfaceContactRadius;
    float _SurfaceContactStrength;
    vec4 _SurfaceProjection;
    vec2 _ContactBlurDirection;
    float _SurfaceLighting;
    vec3 _SurfaceKeyDirection;
    vec3 _SurfaceFillDirection;
    vec3 _SurfaceUpDirection;
    mat4 _SSRScreenFromCapture;
    mat4 _SSRCaptureFromScreen;
    vec4 _SSRScreenTexelSize;
    float _SSRObjectId;
    mat4 _SSRMapViewProjection;
    float _SurfaceDepthRange;
};
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _GeometryTex;
layout(set = 2, binding = 3) uniform sampler2D _ContactVisibilityTex;
layout(set = 2, binding = 4) uniform sampler2D _SSRIdTex;

//实时路径在共享屏幕附件上采样，旧路径仍使用原取景纹理坐标。
vec2 SampleUV(vec2 uv)
{
#ifdef SSR_LIVE
    return (_SSRScreenFromCapture * vec4(uv, 0, 1)).xy;
#else
    return uv;
#endif
}

#ifdef SSR_LIVE
//编号采用点采样，跨对象边界的颜色覆盖率单独插值。
vec4 OwnedColor(ivec2 pixel)
{
    if (any(lessThan(pixel, ivec2(0))) || any(greaterThanEqual(pixel, ivec2(_SSRScreenTexelSize.zw)))) return vec4(0);
    vec4 value = texelFetch(_SSRIdTex, pixel, 0);
    return abs(value.a - _SSRObjectId) < 0.25 ? vec4(value.rgb, 1) : vec4(0);
}
#endif

//显式插值覆盖率，防止浮点对象编号在边缘混合成另一个实体。
vec4 SourceColor(vec2 uv)
{
#ifdef SSR_LIVE
    vec2 pixel = SampleUV(uv) * _SSRScreenTexelSize.zw - 0.5;
    ivec2 first = ivec2(floor(pixel));
    vec2 part = fract(pixel);
    vec4 value = mix(mix(OwnedColor(first), OwnedColor(first + ivec2(1, 0)), part.x),
        mix(OwnedColor(first + ivec2(0, 1)), OwnedColor(first + ivec2(1, 1)), part.x), part.y);
    value.rgb = value.a > 0 ? value.rgb / value.a : vec3(0);
    return value;
#else
    return texture(_MainTex, uv);
#endif
}

struct v2f_img { vec2 uv; };
float saturate(float x) { return clamp(x, 0.0, 1.0); }
vec2 saturate(vec2 x) { return clamp(x, vec2(0), vec2(1)); }
vec3 saturate(vec3 x) { return clamp(x, vec3(0), vec3(1)); }

        
        #ifndef SSR_TURRET_GEOMETRY_PACKING
#define SSR_TURRET_GEOMETRY_PACKING

//把单位法线编码为两个十二位八面体坐标，零值专门表示空白背景。
float PackTurretNormal(vec3 normal)
{
    normal /= abs(normal.x) + abs(normal.y) + abs(normal.z);
    vec2 oct = normal.xy;
    if (normal.z < 0)
        oct = (1 - abs(oct.yx)) * vec2(oct.x >= 0 ? 1 : -1, oct.y >= 0 ? 1 : -1);
    vec2 encoded = floor(saturate(oct * 0.5 + 0.5) * 4095 + 0.5);
    return encoded.x + encoded.y * 4096 + 1;
}

//从浮点通道中精确保存的二十四位整数还原观察空间单位法线。
vec3 UnpackTurretNormal(float value)
{
    //符号用于区分转轮表面，绝对值仍保留完整的二十四位法线编码。
    float encoded = abs(value) - 1;
    float high = floor(encoded / 4096);
    vec2 oct = vec2(encoded - high * 4096, high) / 4095 * 2 - 1;
    vec3 normal = vec3(oct, 1 - abs(oct.x) - abs(oct.y));
    float fold = saturate(-normal.z);
    normal.xy += vec2(normal.x >= 0 ? -fold : fold, normal.y >= 0 ? -fold : fold);
    return normalize(normal);
}

#endif












        //对齐点采样实际读取的像素中心，保持表面位置与编码法线来自同一像素。
        vec2 GeometryPixelCenter(vec2 uv)
        {
#ifdef SSR_LIVE
            vec2 pixel = (floor(SampleUV(uv) * _SSRScreenTexelSize.zw) + 0.5) * _SSRScreenTexelSize.xy;
            return (_SSRCaptureFromScreen * vec4(pixel, 0, 1)).xy;
#else
            return (floor(uv * _SSRGeometryTexelSize.zw) + 0.5) * abs(_SSRGeometryTexelSize.xy);
#endif
        }

        //保持两张手动采集纹理使用相同的坐标，超出画面的样本视为背景。
        vec4 Geometry(vec2 uv)
        {
            if (any(lessThan(uv, vec2(0))) || any(greaterThan(uv, vec2(1)))) return vec4(0);
#ifdef SSR_LIVE
            vec2 screen = SampleUV(uv);
            if (any(lessThan(screen, vec2(0))) || any(greaterThanEqual(screen, vec2(1)))) return vec4(0);
            if (abs(texture(_SSRIdTex, screen).a - _SSRObjectId) >= 0.25) return vec4(0);
#endif
            return texture(_GeometryTex, SampleUV(uv));
        }

        #ifndef SSR_TURRET_CONTACT_OCCLUSION
#define SSR_TURRET_CONTACT_OCCLUSION




//将模型空间阴影半径投影到纹理坐标，透视与正交采集共用同一遮蔽尺度。
vec2 TurretContactRadiusUV(vec3 position)
{
    float distance = _SurfaceProjection.z > 0.5 ? 1 : max(0.0001, -position.z);
    return _SurfaceContactRadius * _SurfaceProjection.xy / distance;
}

//判断邻近表面对当前切平面的遮蔽，排除背景、共面误差及半径以外的几何。
float TurretContactSample(vec2 uv, vec3 position, vec3 normal)
{
    vec4 neighbor = Geometry(uv);
    if (neighbor.a == 0) return 0;
    vec3 delta = neighbor.xyz - position;
    float distanceSquared = dot(delta, delta);
    float radius = _SurfaceContactRadius;
    if (distanceSquared <= radius * radius * 0.0001) return 0;
    float distance = sqrt(distanceSquared);
    float horizon = (dot(normal, delta) - radius * 0.015) / distance;
    float facing = smoothstep(0.06, 0.5, horizon);
    float attenuation = 1 - smoothstep(radius * 0.2, radius, distance);
    //高速转轮不作为逐帧变化的遮蔽物，避免它使邻近固定炮身也产生阴影闪动。
    float rotorVisibility = neighbor.a < 0 ? 1 - _RotorLightingBlend : 1;
    return facing * attenuation * rotorVisibility;
}

//在固定模型尺度的空间格点上改变采样方向，打散重复零件投影，采样格不随帧或分辨率变化。
vec2 TurretContactRotation(vec3 position)
{
    vec2 cell = floor(position.xy / max(_SurfaceContactRadius * 0.04, 0.00001));
    float phase = fract(52.9829189 * fract(dot(cell, vec2(0.06711056, 0.00583715)))) * 6.2831853;
    return vec2(cos(phase), sin(phase));
}

//生成全分辨率的独立遮蔽纹理，以空间分散的采样方向避免固定核复制矩形零件的形状。
vec4 CaptureTurretContact(v2f_img sampleData) 
{
    vec2 uv = GeometryPixelCenter(sampleData.uv);
    vec4 center = Geometry(uv);
    float strength = _SurfaceContactStrength * (center.a < 0 ? 1 - _RotorLightingBlend : 1);
    if (center.a == 0 || _SurfaceContactRadius <= 0 || strength <= 0) return vec4(1);
    vec3 normal = UnpackTurretNormal(center.a);
    vec2 radiusUV = TurretContactRadiusUV(center.xyz);
    vec2 rotation = TurretContactRotation(center.xyz);
    const vec2 samples[32] = vec2[32](
        vec2(0.125000, 0.000000), vec2(-0.159645, 0.146248),
        vec2(0.024436, -0.278438), vec2(0.201222, 0.262459),
        vec2(-0.369268, -0.065318), vec2(0.349802, -0.222516),
        vec2(-0.117002, 0.435242), vec2(-0.223136, -0.429634),
        vec2(0.484115, 0.176798), vec2(-0.503641, 0.207896),
        vec2(0.242788, -0.518824), vec2(0.179414, 0.572001),
        vec2(-0.540757, -0.313380), vec2(0.634370, -0.139464),
        vec2(-0.387146, 0.550675), vec2(-0.089440, -0.690200),
        vec2(0.549072, 0.462758), vec2(-0.738878, 0.030555),
        vec2(0.538955, -0.536332), vec2(-0.036058, 0.779792),
        vec2(-0.512818, -0.614527), vec2(0.812360, 0.109302),
        vec2(-0.688311, 0.478909), vec2(0.188086, -0.836061),
        vec2(0.435033, 0.759191), vec2(-0.850448, -0.271316),
        vec2(0.826102, -0.381680), vec2(-0.357888, 0.855156),
        vec2(-0.319407, -0.888034), vec2(0.849909, 0.446688),
        vec2(-0.944035, 0.248845), vec2(0.536596, -0.834530)
    );
    float occlusion = 0;
     for (int index = 0; index < 32; index++)
    {
        vec2 sampleOffset = samples[index];
        vec2 offset = vec2(sampleOffset.x * rotation.x - sampleOffset.y * rotation.y,
            sampleOffset.x * rotation.y + sampleOffset.y * rotation.x);
        occlusion += TurretContactSample(uv + offset * radiusUV, center.xyz, normal);
    }
    float visibility = 1 - saturate(occlusion * (2.6 / 32)) * strength;
    return vec4(visibility, 0, 0, 1);
}

//结合双方切平面距离和法线判断是否属于连续表面，阻止阴影滤波穿过部件边界。
float TurretContactSurfaceWeight(vec4 center, vec3 normal, vec4 neighbor)
{
    if (neighbor.a == 0) return 0;
    //独立过滤固定表面和转轮，旋转切面不改变炮身滤波权重。
    if ((center.a < 0) != (neighbor.a < 0)) return 0;
    vec3 neighborNormal = UnpackTurretNormal(neighbor.a);
    vec3 delta = neighbor.xyz - center.xyz;
    float planeDistance = max(abs(dot(normal, delta)), abs(dot(neighborNormal, delta)));
    float distanceRatio = planeDistance / max(_SurfaceContactRadius * 0.12, 0.00001);
    return exp2(-distanceRatio * distanceRatio) * pow(saturate(dot(normal, neighborNormal)), 16);
}

//只对遮蔽强度做横向或纵向双边滤波，保持模型颜色、细节和外轮廓未经模糊。
vec4 FilterTurretContact(v2f_img sampleData) 
{
    vec2 uv = GeometryPixelCenter(sampleData.uv);
    vec4 center = Geometry(uv);
    if (center.a == 0 || (center.a < 0 && _RotorLightingBlend >= 1)) return vec4(1);
    vec3 normal = UnpackTurretNormal(center.a);
    vec2 texel = abs(_SSRGeometryTexelSize.xy);
    vec2 span = max(TurretContactRadiusUV(center.xyz) * 0.22, texel * 2);
    vec2 stride = span * _ContactBlurDirection / 4;
    float weighted = texture(_MainTex, SampleUV(uv)).r;
    float totalWeight = 1;
     for (int index = -4; index <= 4; index++)
    {
        if (index == 0) continue;
        vec2 neighborUV = uv + stride * index;
        vec4 neighbor = Geometry(neighborUV);
        float weight = exp2(-0.18 * index * index) * TurretContactSurfaceWeight(center, normal, neighbor);
        weighted += texture(_MainTex, SampleUV(neighborUV)).r * weight;
        totalWeight += weight;
    }
    return vec4(weighted / totalWeight, 0, 0, 1);
}

#endif

        #ifndef SSR_TURRET_SURFACE_LIGHTING
#define SSR_TURRET_SURFACE_LIGHTING


//解析计算法线绕转轮轴旋转一周的漫反射均值，保持轴向端面和炮塔俯仰的体积照明。
float AverageRotorLambert(float axialNormal, float axialLight)
{
    float axial = axialNormal * axialLight;
    float radial = sqrt(max(0, (1 - axialNormal * axialNormal) * (1 - axialLight * axialLight)));
    if (radial < 0.00001 || axial >= radial) return max(0, axial);
    if (axial <= -radial) return 0;
    //只积分朝向光源的圆周区间，避免离散角度采样本身产生周期明暗。
    float litAngle = acos(clamp(-axial / radial, -1, 1));
    return (axial * litAngle + radial * sin(litAngle)) / 3.141592653589793;
}

//结合半球环境光、柔和主辅光和宽高光，以独立平滑后的遮蔽纹理塑造凹槽层次。
vec3 ShadeTurretSurface(vec3 color, vec2 uv, vec4 geometry, vec3 normal)
{
    if (_SurfaceLighting < 0.5 || geometry.a == 0) return color;
    float up = saturate(dot(normal, _SurfaceUpDirection) * 0.5 + 0.5);
    float key = saturate(dot(normal, _SurfaceKeyDirection));
    float fill = saturate(dot(normal, _SurfaceFillDirection));
    float diffuse = mix(0.34, 0.60, up) + key * 0.42 + fill * 0.12;
    float rotorBlend = geometry.a < 0 ? _RotorLightingBlend : 0;
    if (rotorBlend > 0)
    {
        float axialNormal = clamp(dot(normal, _RotorAxisDirection), -1, 1);
        float averageUp = dot(_SurfaceUpDirection, _RotorAxisDirection) * axialNormal * 0.5 + 0.5;
        float averageKey = AverageRotorLambert(axialNormal, dot(_SurfaceKeyDirection, _RotorAxisDirection));
        float averageFill = AverageRotorLambert(axialNormal, dot(_SurfaceFillDirection, _RotorAxisDirection));
        float averageDiffuse = mix(0.34, 0.60, averageUp) + averageKey * 0.42 + averageFill * 0.12;
        diffuse = mix(diffuse, averageDiffuse, rotorBlend);
    }
    vec3 viewDirection = _SurfaceProjection.z > 0.5 ? vec3(0, 0, 1) : normalize(-geometry.xyz);
    vec3 halfDirection = normalize(_SurfaceKeyDirection + viewDirection);
    float highlight = pow(saturate(dot(normal, halfDirection)), 24) * key * 0.028 * (1 - rotorBlend);
    float visibility = texture(_ContactVisibilityTex, SampleUV(uv)).r;
    return (color * diffuse + highlight) * visibility;
}

#endif


        //比较两个真实表面的切平面，保留遮挡断层及法线折角。
        float StructureAt(vec2 neighborUV, vec4 center, vec3 centerNormal)
        {
            vec4 neighbor = Geometry(neighborUV);
            if (center.a == 0 || neighbor.a == 0) return 0;
            vec3 neighborNormal = UnpackTurretNormal(neighbor.a);
            float normalDelta = 1 - saturate(dot(centerNormal, neighborNormal));
            vec3 delta = neighbor.xyz - center.xyz;
            float planeDistance = min(abs(dot(centerNormal, delta)), abs(dot(neighborNormal, delta)));
            float normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 1.2, normalDelta);
            float depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 1.5, planeDistance);
            return max(normalEdge, depthEdge);
        }

        //插值边缘强度而非几何属性，使小于一像素的线宽也能连续改变覆盖率。
        float Structure(vec2 neighborUV, vec4 center, vec3 centerNormal)
        {
            vec2 pixel = neighborUV * _SSRGeometryTexelSize.zw - 0.5;
            vec2 fraction = fract(pixel);
            vec2 texel = abs(_SSRGeometryTexelSize.xy);
            vec2 first = (floor(pixel) + 0.5) * texel;
            float bottom = mix(StructureAt(first, center, centerNormal),
                StructureAt(first + vec2(texel.x, 0), center, centerNormal), fraction.x);
            float top = mix(StructureAt(first + vec2(0, texel.y), center, centerNormal),
                StructureAt(first + texel, center, centerNormal), fraction.x);
            return mix(bottom, top, fraction.y);
        }

        //读取连续的颜色覆盖率，画面之外视为空白，避免硬阈值扩张外轮廓。
        float Coverage(vec2 uv)
        {
            if (any(lessThan(uv, vec2(0))) || any(greaterThan(uv, vec2(1)))) return 0;
            return SourceColor(uv).a;
        }

        //采样八个方向，分别控制外轮廓和内部结构线的像素宽度。
        vec4 Composite(v2f_img sampleData) 
        {
            vec2 uv = sampleData.uv;
            vec4 source = SourceColor(uv);
            vec2 geometryUV = GeometryPixelCenter(uv);
            vec4 center = Geometry(geometryUV);
            float occupied = step(0.000001, abs(center.a));
            vec3 centerNormal = occupied > 0 ? UnpackTurretNormal(center.a) : vec3(0, 0, 1);
            source.rgb = ShadeTurretSurface(source.rgb, geometryUV, center, centerNormal);
            float silhouette = 0, structure = 0;
            const vec2 directions[8] = vec2[8](
                vec2(1,0), vec2(-1,0), vec2(0,1), vec2(0,-1),
                vec2(0.7071,0.7071), vec2(-0.7071,0.7071),
                vec2(0.7071,-0.7071), vec2(-0.7071,-0.7071)
            );
             for (int index = 0; index < 8; index++)
            {
                vec2 offset = directions[index] * abs(_SSRColorTexelSize.xy);
                if (_SilhouetteWidth > 0 && source.a < 1)
                {
                    float neighbor = Coverage(uv + offset * _SilhouetteWidth);
                    if (_ClipOutlineGround > 0.5)
                    {
                        vec4 surface = Geometry(uv + offset * _SilhouetteWidth);
                        float height = (_CaptureToWorld * vec4(surface.xyz, 1)).y
                            - offset.y * _SilhouetteWidth * _CaptureWorldHeight;
                        neighbor *= step(0, height);
                    }
                    silhouette = max(silhouette, (1 - source.a) * neighbor);
                }
                if (_StructureWidth > 0 && occupied > 0)
                {
                    vec2 neighborUV = geometryUV + offset * _StructureWidth * 0.5;
                    structure = max(structure, Structure(neighborUV, center, centerNormal));
                }
            }
            float edge = saturate(max(silhouette, structure)) * _OutlineColor.a;
            float alpha = source.a + edge * (1 - source.a);
            //使用预乘颜色合成，避免透明背景在缩小时产生黑边污染。
            vec3 premultiplied = source.rgb * source.a * (1 - edge) + _OutlineColor.rgb * edge;
            return vec4(premultiplied, alpha);
        }

        //平均高分辨率覆盖率；游戏保留预乘颜色，编辑器和 PNG 使用直通透明度。
        vec4 Resolve(v2f_img sampleData) 
        {
            vec4 value;
            if (_Supersampling > 1.5)
            {
                vec2 offset = abs(_SSRColorTexelSize.xy) * 0.5;
                value = (texture(_MainTex, sampleData.uv + offset) + texture(_MainTex, sampleData.uv - offset)
                    + texture(_MainTex, sampleData.uv + vec2(offset.x, -offset.y))
                    + texture(_MainTex, sampleData.uv + vec2(-offset.x, offset.y))) * 0.25;
            }
            else value = texture(_MainTex, sampleData.uv);
            if (_OutputPremultiplied < 0.5)
                value.rgb = value.a > 0.00001 ? value.rgb / value.a : vec3(0);
            return value;
        }

        //轮廓沿用邻近实体的高度，空白背景保持零值。
        float SurfaceHeightAt(vec2 uv)
        {
            vec4 surface = Geometry(uv);
            if (surface.a != 0)
                return max(0, (_CaptureToWorld * vec4(surface.xyz, 1)).y - _GroundHeight);
            float height = 0;
            const vec2 directions[8] = vec2[8](
                vec2(1,0), vec2(-1,0), vec2(0,1), vec2(0,-1),
                vec2(0.7071,0.7071), vec2(-0.7071,0.7071),
                vec2(0.7071,-0.7071), vec2(-0.7071,-0.7071)
            );
             for (int index = 0; index < 8; index++)
            {
                vec2 neighborUV = uv + directions[index] * abs(_SSRGeometryTexelSize.xy) * _SilhouetteWidth;
                vec4 neighbor = Geometry(neighborUV);
                if (neighbor.a != 0)
                    height = max(height, (_CaptureToWorld * vec4(neighbor.xyz, 1)).y - _GroundHeight);
            }
            return height;
        }

        //与颜色使用相同的超采样位置，边缘保留最近实体的高度。
        float ResolveSurfaceHeight(v2f_img sampleData) 
        {
            if (_Supersampling <= 1.5) return SurfaceHeightAt(sampleData.uv);
            vec2 offset = abs(_SSRGeometryTexelSize.xy) * 0.5;
            return max(max(SurfaceHeightAt(sampleData.uv + offset), SurfaceHeightAt(sampleData.uv - offset)),
                max(SurfaceHeightAt(sampleData.uv + vec2(offset.x, -offset.y)),
                    SurfaceHeightAt(sampleData.uv + vec2(-offset.x, offset.y))));
        }
        
