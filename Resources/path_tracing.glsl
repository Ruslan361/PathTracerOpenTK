#version 450 core

in vec2 TexCoord;
out vec4 OutColor;

struct Material
{
    vec3 emmitance;
    vec3 reflectance;
    float roughness;
    float opacity;
};

struct Box
{
    Material material;
    vec3 halfSize;
    mat3 rotation;
    vec3 position;
};

struct Sphere
{
    Material material;
    vec3 position;
    float radius;
};

uniform vec2 uViewportSize;
uniform vec3 uPosition;
uniform vec3 uDirection;
uniform vec3 uUp;
uniform float uFOV;
uniform float uTime;
uniform int uSamples;

#define PI 3.1415926535
#define HALF_PI (PI / 2.0)
#define FAR_DISTANCE 1000000.0

#define MAX_DEPTH 8
#define SPHERE_COUNT 3
#define BOX_COUNT 8
#define N_IN 0.99
#define N_OUT 1.0

Sphere spheres[SPHERE_COUNT];
Box boxes[BOX_COUNT];

void InitializeScene()
{
    spheres[0].position = vec3(2.5, 1.5, -1.5);
    spheres[1].position = vec3(-2.5, 2.5, -1.0);
    spheres[2].position = vec3(0.5, -4.0, 3.0);
    spheres[0].radius = 1.5;
    spheres[1].radius = 1.0;
    spheres[2].radius = 1.0;
    spheres[0].material.roughness = 1.0;
    spheres[1].material.roughness = 0.8;
    spheres[2].material.roughness = 1.0;
    spheres[0].material.opacity = 0.0;
    spheres[1].material.opacity = 0.0;
    spheres[2].material.opacity = 0.8;
    spheres[0].material.reflectance = vec3(1.0, 0.0, 0.0);
    spheres[1].material.reflectance = vec3(1.0, 0.4, 0.0);
    spheres[2].material.reflectance = vec3(1.0, 1.0, 1.0);
    spheres[0].material.emmitance = vec3(0.0);
    spheres[1].material.emmitance = vec3(0.0);
    spheres[2].material.emmitance = vec3(0.0);

    // up
    boxes[0].material.roughness = 0.0;
    boxes[0].material.emmitance = vec3(0.0);
    boxes[0].material.reflectance = vec3(1.0, 1.0, 1.0);
    boxes[0].halfSize = vec3(5.0, 0.5, 5.0);
    boxes[0].position = vec3(0.0, 5.5, 0.0);
    boxes[0].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0
    );

    // down
    boxes[1].material.roughness = 0.3;
    boxes[1].material.opacity = 0.0;
    boxes[1].material.emmitance = vec3(0.0);
    boxes[1].material.reflectance = vec3(1.0, 1.0, 1.0);
    boxes[1].halfSize = vec3(5.0, 0.5, 5.0);
    boxes[1].position = vec3(0.0, -5.5, 0.0);
    boxes[1].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0
    );

    // right
    boxes[2].material.roughness = 0.0;
    boxes[2].material.opacity = 0.0;
    boxes[2].material.emmitance = vec3(0.0);
    boxes[2].material.reflectance = vec3(0.0, 1.0, 0.0);
    boxes[2].halfSize = vec3(5.0, 0.5, 5.0);
    boxes[2].position = vec3(5.5, 0.0, 0.0);
    boxes[2].rotation = mat3(
        0.0, 1.0, 0.0,
        -1.0, 0.0, 0.0,
        0.0, 0.0, 1.0
    );

    // left
    boxes[3].material.roughness = 0.0;
    boxes[3].material.opacity = 0.0;
    boxes[3].material.emmitance = vec3(0.0);
    boxes[3].material.reflectance = vec3(1.0, 0.0, 0.0);
    boxes[3].halfSize = vec3(5.0, 0.5, 5.0);
    boxes[3].position = vec3(-5.5, 0.0, 0.0);
    boxes[3].rotation = mat3(
        0.0, 1.0, 0.0,
        -1.0, 0.0, 0.0,
        0.0, 0.0, 1.0
    );

    // back
    boxes[4].material.roughness = 0.0;
    boxes[4].material.opacity = 0.0;
    boxes[4].material.emmitance = vec3(0.0);
    boxes[4].material.reflectance = vec3(1.0, 1.0, 1.0);
    boxes[4].halfSize = vec3(5.0, 0.5, 5.0);
    boxes[4].position = vec3(0.0, 0.0, -5.5);
    boxes[4].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 0.0, 1.0,
        0.0, 1.0, 0.0
    );

    // light source
    boxes[5].material.roughness = 0.0;
    boxes[5].material.opacity = 0.0;
    boxes[5].material.emmitance = vec3(6.0);
    boxes[5].material.reflectance = vec3(1.0);
    boxes[5].halfSize = vec3(2.5, 0.2, 2.5);
    boxes[5].position = vec3(0.0, 4.8, 0.0);
    boxes[5].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0
    );

    // boxes
    boxes[6].material.roughness = 0.0;
    boxes[6].material.opacity = 0.0;
    boxes[6].material.emmitance = vec3(0.0);
    boxes[6].material.reflectance = vec3(1.0);
    boxes[6].halfSize = vec3(1.5, 3.0, 1.5);
    boxes[6].position = vec3(-2.0, -2.0, -0.0);
    boxes[6].rotation = mat3(
        0.7, 0.0, 0.7,
        0.0, 1.0, 0.0,
        -0.7, 0.0, 0.7
    );
    // boxes
    boxes[7].material.roughness = 0.0;
    boxes[7].material.opacity = 0.0;
    boxes[7].material.emmitance = vec3(0.0);
    boxes[7].material.reflectance = vec3(1.0);
    boxes[7].halfSize = vec3(1.0, 1.5, 1.0);
    boxes[7].position = vec3(2.5, -3.5, -0.0);
    boxes[7].rotation = mat3(
        0.7, 0.0, 0.7,
        0.0, 1.0, 0.0,
        -0.7, 0.0, 0.7
    );
}

float RandomNoise(vec2 co)
{
    co *= fract(uTime * 12.343);
    return fract(sin(dot(co.xy, vec2(12.9898, 78.233))) * 43758.5453);
}

vec3 RandomHemispherePoint(vec2 rand)
{
    float cosTheta = sqrt(1.0 - rand.x);
    float sinTheta = sqrt(rand.x);
    float phi = 2.0 * PI * rand.y;
    return vec3(
        cos(phi) * sinTheta,
        sin(phi) * sinTheta,
        cosTheta
    );
}

vec3 NormalOrientedHemispherePoint(vec2 rand, vec3 n)
{
    vec3 v = RandomHemispherePoint(rand);
    return dot(v, n) < 0.0 ? -v : v;
}

float FresnelSchlick(float nIn, float nOut, vec3 direction, vec3 normal)
{
    float R0 = ((nOut - nIn) * (nOut - nIn)) / ((nOut + nIn) * (nOut + nIn));
    float fresnel = R0 + (1.0 - R0) * pow((1.0 - abs(dot(direction, normal))), 5.0);
    return fresnel;
}

float RaySphereTest(vec3 rayOrigin, vec3 rayDirection, Sphere sphere)
{
    vec3 ro = rayOrigin - sphere.position;
    float b = dot(ro, rayDirection);
    float c = dot(ro, ro) - sphere.radius * sphere.radius;
    float h = b * b - c;
    if (h < 0.0) return -1.0;
    h = sqrt(h);
    return -b - h;
}

float RayBoxTest(vec3 rayOrigin, vec3 rayDirection, Box box)
{
    vec3 ro = (rayOrigin - box.position) * box.rotation;
    vec3 rd = rayDirection * box.rotation;
    vec3 m = 1.0 / rd;
    vec3 s = vec3(
        (rd.x < 0.0) ? 1.0 : -1.0,
        (rd.y < 0.0) ? 1.0 : -1.0,
        (rd.z < 0.0) ? 1.0 : -1.0
    );
    vec3 t1 = m * (-ro + s * box.halfSize);
    vec3 t2 = m * (-ro - s * box.halfSize);
    float tN = max(max(t1.x, t1.y), t1.z);
    float tF = min(min(t2.x, t2.y), t2.z);
    if (tN > tF || tF < 0.0) return -1.0;
    return tN;
}

float RayScene(vec3 rayOrigin, vec3 rayDirection, inout int mtl, inout int sphereId, inout int boxId)
{
    float dist = FAR_DISTANCE;

    for (int i = 0; i < SPHERE_COUNT; ++i)
    {
        float t = RaySphereTest(rayOrigin, rayDirection, spheres[i]);
        if (t > 0.0 && t < dist)
        {
            dist = t;
            mtl = 0;
            sphereId = i;
        }
    }

    for (int i = 0; i < BOX_COUNT; ++i)
    {
        float t = RayBoxTest(rayOrigin, rayDirection, boxes[i]);
        if (t > 0.0 && t < dist)
        {
            dist = t;
            mtl = 1;
            boxId = i;
        }
    }
    
    return (dist < FAR_DISTANCE) ? dist : -1.0;
}

vec3 SphereNormal(vec3 pos, Sphere sphere)
{
    return normalize(pos - sphere.position);
}

vec3 BoxNormal(vec3 hitPosition, Box box)
{
    vec3 posRot = (hitPosition - box.position) * box.rotation;
    vec3 normRot = normalize(sign(posRot) * step(abs(posRot / box.halfSize), abs(posRot / box.halfSize).yzx));
    return normRot * box.rotation;
}

vec3 Trace(vec3 rayOrigin, vec3 rayDirection)
{
    vec3 accum = vec3(0.0);
    vec3 mask = vec3(1.0);
    vec3 hitPos, normal;
    bool inside = false;

    for (int depth = 0; depth < MAX_DEPTH; ++depth)
    {
        int materialType = -1;
        int sphereId = -1;
        int boxId = -1;
        float t = RayScene(rayOrigin, rayDirection, materialType, sphereId, boxId);
        if (t < 0.0) return accum;
        
        hitPos = rayOrigin + rayDirection * t;

        if (materialType == 0)
        {
            normal = SphereNormal(hitPos, spheres[sphereId]);
            if (spheres[sphereId].material.opacity > 0.0)
            {
                float refractionIndex = inside ? N_OUT / N_IN : N_IN / N_OUT;
                float fresnel = FresnelSchlick(N_IN, N_OUT, rayDirection, normal);

                if (spheres[sphereId].material.roughness > 0.0)
                {
                    vec2 rand = vec2(
                        RandomNoise(hitPos.xy + hitPos.yz * depth),
                        RandomNoise(hitPos.yz * depth + hitPos.zx)
                    );
                    vec3 newDir = mix(reflect(rayDirection, normal), NormalOrientedHemispherePoint(rand, normal), spheres[sphereId].material.roughness);
                    rayDirection = normalize(newDir);
                }
                else 
                {
                    rayDirection = reflect(rayDirection, normal);
                }

                if (fresnel < 0.5)
                {
                    vec3 refractDir = refract(rayDirection, normal * (inside ? -1.0 : 1.0), refractionIndex);
                    if (dot(refractDir, refractDir) > 0.0)
                    {
                        rayDirection = normalize(refractDir);
                        inside = !inside;
                    }
                }
                
                accum += mask * spheres[sphereId].material.emmitance;
                mask *= mix(spheres[sphereId].material.reflectance, vec3(1.0, 1.0, 1.0), spheres[sphereId].material.opacity);                
            }
            else
            {
                vec2 rand = vec2(
                    RandomNoise(hitPos.xy + hitPos.yz * depth),
                    RandomNoise(hitPos.yz * depth + hitPos.zx)
                );
                vec3 newDir = mix(reflect(rayDirection, normal), NormalOrientedHemispherePoint(rand, normal), spheres[sphereId].material.roughness);
                rayDirection = normalize(newDir);
                
                accum += mask * spheres[sphereId].material.emmitance;
                mask *= spheres[sphereId].material.reflectance;
            }
        }
        else if (materialType == 1)
        {
            normal = BoxNormal(hitPos, boxes[boxId]);
            vec2 rand = vec2(
                RandomNoise(hitPos.xy + hitPos.yz * depth),
                RandomNoise(hitPos.yz * depth + hitPos.zx)
            );
            vec3 newDir = mix(reflect(rayDirection, normal), NormalOrientedHemispherePoint(rand, normal), boxes[boxId].material.roughness);
            rayDirection = normalize(newDir);
            
            accum += mask * boxes[boxId].material.emmitance;
            mask *= boxes[boxId].material.reflectance;
        }
        
        rayOrigin = hitPos + normal * 0.0001;
    }
    
    return accum;
}

vec3 FrameRender(in vec2 uv)
{
    vec2 jitter = vec2(
        RandomNoise(uv + vec2(uTime, 0.0)),
        RandomNoise(uv + vec2(0.0, uTime))
    );
    
    vec3 right = normalize(cross(uDirection, uUp));
    vec3 view = normalize(uDirection);
    vec3 up = normalize(cross(right, view));
    
    float focalDistance = 1.0;
    vec2 pixelSize = 1.0 / uViewportSize;
    vec2 focalSize = 2.0 * tan(0.5 * uFOV) * focalDistance;
    
    vec2 offset = (jitter * 2.0 - 1.0) * pixelSize;
    vec2 focus = focalSize * (uv + offset - 0.5);
    
    vec3 rayOrigin = uPosition;
    vec3 rayDirection = normalize(view * focalDistance + right * focus.x + up * focus.y);
    
    vec3 color = Trace(rayOrigin, rayDirection);
    
    return color;
}

void main()
{
    InitializeScene();
    
    vec3 color = vec3(0.0);
    for (int i = 0; i < uSamples; i++)
    {
        color += FrameRender(TexCoord);
    }
    
    OutColor = vec4(color, 1.0);
}
