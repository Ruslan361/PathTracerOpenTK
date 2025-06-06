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

struct Tetrahedron {
    Material material;
    vec3 v0; 
    vec3 v1; 
    vec3 v2; 
    vec3 v3; 
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

#define MAX_DEPTH 10
#define SPHERE_COUNT 3
#define BOX_COUNT 8
#define TETRAHEDRON_COUNT 1
#define N_IN 0.95
#define N_OUT 1.0

Sphere spheres[SPHERE_COUNT];
Box boxes[BOX_COUNT];
Tetrahedron tetrahedrons[TETRAHEDRON_COUNT];

void InitializeScene()
{
    spheres[0].position = vec3(2.5, 1.5, -1.5);
    spheres[1].position = vec3(-2.5, 2.5, -1.0);
    spheres[2].position = vec3(0.5, -4.0, 3.0);
    spheres[0].radius = 2.0;
    spheres[1].radius = 1.0;
    spheres[2].radius = 1.0;
    spheres[0].material.roughness = 1.0;
    spheres[1].material.roughness = 0.8;
    spheres[2].material.roughness = 1.0;
    spheres[0].material.opacity = 0.0;
    spheres[1].material.opacity = 0.0;
    spheres[2].material.opacity = 1.0;
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
    boxes[0].halfSize = vec3(7.5, 0.5, 7.5); // Increased size
    boxes[0].position = vec3(0.0, 8.0, 0.0);   // Moved up
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
    boxes[1].halfSize = vec3(7.5, 0.5, 7.5);  // Increased size
    boxes[1].position = vec3(0.0, -8.0, 0.0); // Moved down
    boxes[1].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0
    );

    // right
    boxes[2].material.roughness = 1.0;
    boxes[2].material.opacity = 0.0;
    boxes[2].material.emmitance = vec3(0.0);
    boxes[2].material.reflectance = vec3(0.125, 1.0, 0.125);
    boxes[2].halfSize = vec3(7.5, 0.5, 7.5); // Increased depth/height for wall
    boxes[2].position = vec3(8.0, 0.0, 0.0);   // Moved right
    boxes[2].rotation = mat3(
        0.0, 1.0, 0.0,
        -1.0, 0.0, 0.0,
        0.0, 0.0, 1.0
    );

    // left
    boxes[3].material.roughness = 1.0;
    boxes[3].material.opacity = 0.0;
    boxes[3].material.emmitance = vec3(0.0);
    boxes[3].material.reflectance = vec3(1.0, 1.0, 1.0);
    boxes[3].halfSize = vec3(7.5, 0.5, 7.5);
    boxes[3].position = vec3(-8, 0.0, 0.0);
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
    boxes[4].halfSize = vec3(7.5, 0.5, 7.5); // Increased width/height for wall
    boxes[4].position = vec3(0.0, 0.0, -8.0);  // Moved back
    boxes[4].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 0.0, 1.0,
        0.0, 1.0, 0.0
    );

    // light source
    boxes[5].material.roughness = 0.0;
    boxes[5].material.opacity = 0.0;
    boxes[5].material.emmitance = vec3(1.0, 2.0, 0.2);
    boxes[5].material.reflectance = vec3(1.0);
    boxes[5].halfSize = vec3(2.5, 0.2, 2.5);
    boxes[5].position = vec3(0.0, 7.2, 0.0); // Adjusted Y position for new ceiling
    boxes[5].rotation = mat3(
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0
    );

    // boxes
    boxes[6].material.roughness = 0.0;
    boxes[6].material.opacity = 0.0;
    boxes[6].material.emmitance = vec3(0.0);
    boxes[6].material.reflectance = vec3(1.0, 1.0, 0.0);
    boxes[6].halfSize = vec3(3.0, 1.5, 3.0);
    boxes[6].position = vec3(-2.0, -6.5, -0.0);
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
    boxes[7].halfSize = vec3(1.0, 2, 1.0);
    boxes[7].position = vec3(4.0, -6.0, -0.0);
    boxes[7].rotation = mat3(
        0.7, 0.0, 0.7,
        0.0, 1.0, 0.0,
        -0.7, 0.0, 0.7
    );

    // Tetrahedron Initialization
    float scale = 1.5;  
    vec3 tetraPosition = vec3(0.0, -5.0, 0.0); // Adjusted for current scene
    
    float rotAngle = PI / 4.0;  
    mat3 rotY = mat3(
        cos(rotAngle), 0.0, sin(rotAngle),
        0.0, 1.0, 0.0,
        -sin(rotAngle), 0.0, cos(rotAngle)
    );
    
    tetrahedrons[0].v0 = tetraPosition + rotY * vec3(-scale, -scale, -scale);
    tetrahedrons[0].v1 = tetraPosition + rotY * vec3(scale, -scale, -scale);
    tetrahedrons[0].v2 = tetraPosition + rotY * vec3(0.0, scale * 1.5, 0.0);  
    tetrahedrons[0].v3 = tetraPosition + rotY * vec3(0.0, -scale, scale);
    tetrahedrons[0].material.roughness = 0.0;       
    tetrahedrons[0].material.opacity = 0.0;        
    tetrahedrons[0].material.emmitance = vec3(0.0); 
    tetrahedrons[0].material.reflectance = vec3(1.0, 0.0, 1.0); 
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

vec3 IdealRefract(vec3 direction, vec3 normal, float nIn, float nOut)
{
    bool fromOutside = dot(normal, direction) < 0.0;
    float ratio = fromOutside ? nOut / nIn : nIn / nOut;

    vec3 refraction, reflection;

    refraction = fromOutside ? refract(direction, normal, ratio) : -refract(-direction, normal, ratio);
    reflection = reflect(direction, normal);

    return refraction == vec3(0.0) ? reflection : refraction;
}

vec3 GetRayDirection(vec2 texcoord, vec2 viewportSize, float fov, vec3 direction, vec3 up)
{
    vec2 texDiff = 0.5 * vec2(1.0 - 2.0 * texcoord.x, 2.0 * texcoord.y - 1.0);
    vec2 angleDiff = texDiff * vec2(viewportSize.x / viewportSize.y, 1.0) * tan(fov * 0.5);

    vec3 rayDirection = normalize(vec3(angleDiff, 1.0f));

    vec3 right = normalize(cross(up, direction));
    mat3 viewToWorld = mat3(
        right,
        up,
        direction
    );

    return viewToWorld * rayDirection;
}

bool IntersectRaySphere(vec3 origin, vec3 direction, Sphere sphere, out float fraction, out vec3 normal)
{
    vec3 L = origin - sphere.position;
    float a = dot(direction, direction);
    float b = 2.0 * dot(L, direction);
    float c = dot(L, L) - sphere.radius * sphere.radius;
    float D = b * b - 4 * a * c;

    if (D < 0.0) return false;

    float r1 = (-b - sqrt(D)) / (2.0 * a);
    float r2 = (-b + sqrt(D)) / (2.0 * a);
        
    if (r1 > 0.0)
        fraction = r1;
    else if (r2 > 0.0)
        fraction = r2;
    else
        return false;

    normal = normalize(direction * fraction + L);

    return true;
}

bool IntersectRayBox(vec3 origin, vec3 direction, Box box, out float fraction, out vec3 normal)
{
    vec3 rd = box.rotation * direction;
    vec3 ro = box.rotation * (origin - box.position);

    vec3 m = vec3(1.0) / rd; 

    vec3 s = vec3((rd.x < 0.0) ? 1.0 : -1.0,
                  (rd.y < 0.0) ? 1.0 : -1.0,
                  (rd.z < 0.0) ? 1.0 : -1.0);
    vec3 t1 = m * (-ro + s * box.halfSize);
    vec3 t2 = m * (-ro - s * box.halfSize);

    float tN = max(max(t1.x, t1.y), t1.z);
    float tF = min(min(t2.x, t2.y), t2.z);

    if (tN > tF || tF < 0.0) return false;

    mat3 txi = transpose(box.rotation);

    if (t1.x > t1.y && t1.x > t1.z)
        normal = txi[0] * s.x;
    else if (t1.y > t1.z)
        normal = txi[1] * s.y;
    else
        normal = txi[2] * s.z;

    fraction = tN;

    return true;
}

bool IntersectRayTriangle(vec3 origin, vec3 direction, vec3 v0, vec3 v1, vec3 v2, out float t, out vec3 norm) {
    vec3 e1 = v1 - v0;
    vec3 e2 = v2 - v0;
    vec3 pvec = cross(direction, e2);
    float det = dot(e1, pvec);
    
    if (abs(det) < 0.0001) return false;
    
    float invDet = 1.0 / det;
    vec3 tvec = origin - v0;
    float u = dot(tvec, pvec) * invDet;
    
    if (u < 0.0 || u > 1.0) return false;
    
    vec3 qvec = cross(tvec, e1);
    float v = dot(direction, qvec) * invDet;
    
    if (v < 0.0 || u + v > 1.0) return false;
    
    t = dot(e2, qvec) * invDet;
    
    if (t < 0.0001) return false; // Intersection must be in front
    
    norm = normalize(cross(e1, e2));
    
    return true;
}

bool IntersectRayTetrahedron(vec3 origin, vec3 direction, Tetrahedron tetrahedron, out float fraction, out vec3 normal) {
    float t_temp;
    vec3 norm_temp;
    
    float closest_t = FAR_DISTANCE;
    bool hit_found = false;
    
    if (IntersectRayTriangle(origin, direction, tetrahedron.v0, tetrahedron.v1, tetrahedron.v2, t_temp, norm_temp)) {
        if (t_temp > 0.0001 && t_temp < closest_t) {
            closest_t = t_temp;
            normal = norm_temp;
            hit_found = true;
        }
    }
    
    if (IntersectRayTriangle(origin, direction, tetrahedron.v0, tetrahedron.v1, tetrahedron.v3, t_temp, norm_temp)) {
        if (t_temp > 0.0001 && t_temp < closest_t) {
            closest_t = t_temp;
            normal = norm_temp;
            hit_found = true;
        }
    }
    
    if (IntersectRayTriangle(origin, direction, tetrahedron.v0, tetrahedron.v2, tetrahedron.v3, t_temp, norm_temp)) {
        if (t_temp > 0.0001 && t_temp < closest_t) {
            closest_t = t_temp;
            normal = norm_temp;
            hit_found = true;
        }
    }
    
    if (IntersectRayTriangle(origin, direction, tetrahedron.v1, tetrahedron.v2, tetrahedron.v3, t_temp, norm_temp)) {
        if (t_temp > 0.0001 && t_temp < closest_t) {
            closest_t = t_temp;
            normal = norm_temp;
            hit_found = true;
        }
    }
    
    if (hit_found) {
        fraction = closest_t;
        return true;
    }
    
    return false;
}

bool CastRay(vec3 rayOrigin, vec3 rayDirection, out float fraction, out vec3 normal, out Material material)
{
    float minDistance = FAR_DISTANCE;

    for (int i = 0; i < SPHERE_COUNT; i++)
    {
        float F;
        vec3 N;
        if (IntersectRaySphere(rayOrigin, rayDirection, spheres[i], F, N) && F < minDistance)
        {
            minDistance = F;
            normal = N;
            material = spheres[i].material;
        }
    }

    for (int i = 0; i < BOX_COUNT; i++)
    {
        float F;
        vec3 N;
        if (IntersectRayBox(rayOrigin, rayDirection, boxes[i], F, N) && F > 0.0001 && F < minDistance)
        {
            minDistance = F;
            normal = N;
            material = boxes[i].material;
        }
    }

    for (int i = 0; i < TETRAHEDRON_COUNT; i++)
    {
        float F;
        vec3 N;
        if (IntersectRayTetrahedron(rayOrigin, rayDirection, tetrahedrons[i], F, N) && F > 0.0001 && F < minDistance)
        {
            minDistance = F;
            normal = N;
            material = tetrahedrons[i].material;
        }
    }

    fraction = minDistance;
    return minDistance != FAR_DISTANCE;
}

bool IsRefracted(float rand, vec3 direction, vec3 normal, float opacity, float nIn, float nOut)
{
    float fresnel = FresnelSchlick(nIn, nOut, direction, normal);
    return opacity > rand && fresnel < rand;
}

vec3 TracePath(vec3 rayOrigin, vec3 rayDirection, float seed)
{
    vec3 L = vec3(0.0);
    vec3 F = vec3(1.0);
    for (int i = 0; i < MAX_DEPTH; i++)
    {
        float fraction;
        vec3 normal;
        Material material;
        bool hit = CastRay(rayOrigin, rayDirection, fraction, normal, material);
        if (hit)
        {
            vec3 newRayOrigin = rayOrigin + fraction * rayDirection;

            vec2 rand = vec2(RandomNoise(seed * TexCoord.xy), seed * RandomNoise(TexCoord.yx));
            vec3 hemisphereDistributedDirection = NormalOrientedHemispherePoint(rand, normal);

            vec3 randomVec = vec3(
                RandomNoise(sin(seed * TexCoord.xy)),
                RandomNoise(cos(seed * TexCoord.xy)),
                RandomNoise(sin(seed * TexCoord.yx))
            );
            randomVec = normalize(2.0 * randomVec - 1.0);

            vec3 tangent = cross(randomVec, normal);
            vec3 bitangent = cross(normal, tangent);
            mat3 transform = mat3(tangent, bitangent, normal);

            vec3 newRayDirection = transform * hemisphereDistributedDirection;
            
            float refractRand = RandomNoise(cos(seed * TexCoord.yx));
            bool refracted = IsRefracted(refractRand, rayDirection, normal, material.opacity, N_IN, N_OUT);
            if (refracted)
            {
                vec3 idealRefraction = IdealRefract(rayDirection, normal, N_IN, N_OUT);
                newRayDirection = normalize(mix(-newRayDirection, idealRefraction, material.roughness));
                newRayOrigin += normal * (dot(newRayDirection, normal) < 0.0 ? -0.8 : 0.8);
            }
            else
            {
                vec3 idealReflection = reflect(rayDirection, normal);
                newRayDirection = normalize(mix(newRayDirection, idealReflection, material.roughness));
                newRayOrigin += normal * 0.8;
            }

            rayDirection = newRayDirection;
            rayOrigin = newRayOrigin;

            L += F * material.emmitance;
            F *= material.reflectance;
        }
        else
        {
            F = vec3(0.0);
        }
    }

    return L;
}

void main()
{
    InitializeScene();

    vec3 direction = GetRayDirection(TexCoord, uViewportSize, uFOV, uDirection, uUp);

    vec3 totalColor = vec3(0.0);
    for (int i = 0; i < uSamples; i++)
    {
        float seed = sin(float(i) * uTime);
        vec3 sampleColor = TracePath(uPosition, direction, seed);
        totalColor += sampleColor;
    }

    vec3 outputColor = totalColor / float(uSamples);
    OutColor = vec4(outputColor, 1.0);
}