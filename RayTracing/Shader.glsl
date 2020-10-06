//$WindowWidth - width of the window
//$WindowHeight - height of the window
//$LocalSizeX - local_size_x
//$LocalSizeY - local_size_y

#version 430
#define PI 3.1415926538

struct Material
{
	vec3 specular;
	vec3 albedo;
};

struct Sphere
{
	vec3 center;
	float radius;
	Material mat;
};

struct Ray
{
	vec3 origin;
	vec3 direction;
};

struct HitInfo
{
	vec3 hitPosition;
	vec3 normal;
	float t;
	bool hit;
	int bounces;
};


uniform writeonly image2D destTex;

layout (binding=1, rgba8)
uniform image2D backgroundTex;

uniform mat4 ToWorldSpace;
uniform vec3 CameraPos;
uniform float ViewPortWidth;
uniform float ViewPortHeight;
uniform int BackgroundWidth;
uniform int BackgroundHeight;

layout (local_size_x = $LocalSizeX, local_size_y = $LocalSizeY) in;

vec3 Vec3ToSpherical(vec3 vec)
{
	return vec3(length(vec), atan(vec.x,-vec.z), acos(vec.y/length(vec)));
}

vec2 SphericalToUV(vec3 spherical)
{
	return vec2((spherical.y + PI) / (2*PI +.001), 1-(spherical.z / PI));
}

vec4 SampleBackground(Ray r)
{
	vec2 sampleBackroundUV = SphericalToUV(Vec3ToSpherical(r.direction));
	ivec2 sampleBackgroundPos = ivec2(BackgroundWidth*sampleBackroundUV.x, BackgroundHeight*sampleBackroundUV.y);

	return imageLoad(backgroundTex, sampleBackgroundPos);
}

HitInfo hit_sphere(Sphere sphere, Ray r, float tMin, float tMax, inout Material mat)
{
	vec3 oc = r.origin-sphere.center;
	float a = dot(r.direction, r.direction);
	float b = 2.0*dot(oc,r.direction);
	float c = dot(oc,oc) - sphere.radius*sphere.radius;
    float discriminant = b*b - 4*a*c;
	float tn = discriminant >= 0? (-b - sqrt(discriminant)) / (2.0*a) : -1.0; //(-b - sqrt(discriminant)) / (2.0*a);
	float tp = discriminant >= 0? (-b + sqrt(discriminant)) / (2.0*a) : -1.0;

	float t = min(tn,tp);

	HitInfo hitInfo;
	hitInfo.hitPosition = r.origin + r.direction*t;
	hitInfo.normal = (hitInfo.hitPosition - sphere.center) / sphere.radius;
	hitInfo.t = t;
	hitInfo.hit = discriminant > 0 && (t < tMax && t > tMin);
	hitInfo.bounces = 0;
	mat = hitInfo.hit? sphere.mat : mat;

	return hitInfo;
}

bool FireRay(Ray r, Sphere[3] spheres, float tMax, inout HitInfo closest, inout Material mat)
{
	bool newHit = false;
	Material sphereMat = mat;
	for(int i = 0; i < 3; i++)
	{
		HitInfo info = hit_sphere(spheres[i], r, 0, tMax, sphereMat);
		info.bounces = closest.bounces;
		newHit = newHit || info.hit;
		mat = newHit? sphereMat : mat;
		closest = info.hit? info : closest;
		tMax = (info.hit && info.t < tMax)? info.t : tMax;
	}
	closest.bounces += int(newHit);
	return newHit;
}

HitInfo RayTrace(Ray ray, Sphere[3] spheres, int maxBounces, inout vec3 color)
{
	vec3 result = vec3(0,0,0);

	vec3 energy = vec3(1,1,1);
	Material hitMat = Material(vec3(1,1,1), vec3(0,0,0));

	float tMax = 1.0/0;
	HitInfo closest = HitInfo(vec3(0,0,0), vec3(0,0,0), 0, false, 0);
	bool newHit = FireRay(ray, spheres, tMax, closest, hitMat);
	result += energy * clamp(dot(closest.normal, vec3(1,-1,0)) * -1, .1, 1.0) * hitMat.albedo;
	energy = energy*hitMat.specular;
	
	for(int i = 0; i < maxBounces; i++)
	{
		hitMat = Material(vec3(1,1,1), vec3(0,0,0));
		ray.direction = (newHit)? reflect(ray.direction, closest.normal) : ray.direction;
		ray.origin = (newHit)? closest.hitPosition : ray.origin;
		newHit = FireRay(ray, spheres, tMax, closest, hitMat);
		result += energy * clamp(dot(closest.normal, vec3(1,-1,0)), .1, 1.0) * hitMat.albedo;
		energy = energy*hitMat.specular;
	}

	result+= SampleBackground(ray).xyz * energy;
	color = result;
	return closest;
}

void main()
{
	ivec2 screenPos = ivec2(gl_GlobalInvocationID.xy);
	vec2 UVpos = vec2(float(screenPos.x)/$WindowWidth.0, float(screenPos.y)/$WindowHeight.0);
	vec2 NDCPos = UVpos * 2.0 - vec2(1.0,1.0);
	vec3 VSpos = vec3(NDCPos.x * ViewPortWidth, NDCPos.y * ViewPortHeight, 1);
	vec3 WSpos = (ToWorldSpace * vec4(VSpos,1)).xyz;
	
	
	Sphere[3] spheres = Sphere[3]
		(
			Sphere(vec3(0,0,1), .5, Material(vec3(.6,.6,.6), vec3(1,1,1)) ),
			Sphere(vec3(0,-100.5,1), 100, Material(vec3(.6,.6,.6), vec3(.8,.8,.8)) ), 
			Sphere(vec3(0,100.5,1), 100, Material(vec3(.6,.6,.6), vec3(.8,.8,.8)) )
		);

	Ray ray = Ray(CameraPos, WSpos-CameraPos);

	vec3 resultColor = vec3(0,0,0);
	HitInfo closest = RayTrace(ray, spheres, 5, resultColor);

	//vec4 color = 1f/float(max(closest.bounces,1)) * (closest.hit? vec4(closest.normal, 0) : vec4(0,0,0,0));
	//vec4 color = 1f/float(max(closest.bounces,1)) * (closest.hit? vec4((closest.normal+vec3(1,1,1))*.5,0) : vec4(0,0,0,0));
	//vec4 color = 1f/float(max(closest.bounces,1)) * (closest.hit? vec4(1,1,1,1) : vec4(0,0,0,0));
	
	//color = closest.hit? color : bckColor;
	imageStore(destTex, screenPos, vec4(resultColor,0));
}