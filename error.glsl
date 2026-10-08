#version 140
#define HAS_MOD
#define HAS_DFDX
#define HAS_FLOAT_TEXTURES
#define HAS_SRGB
#define HAS_UNIFORM_BUFFERS
#define FRAGMENT_SHADER

// -- Utilities Start --

// It's literally just called the Z-Library for alphabetical ordering reasons.
//  - 20kdc

// -- varying/attribute/texture2D --

#ifndef HAS_VARYING_ATTRIBUTE
#define texture2D texture
#ifdef VERTEX_SHADER
#define varying out
#define attribute in
#else
#define varying in
#define attribute in
#define gl_FragColor colourOutput
out highp vec4 colourOutput;
#endif
#endif

#ifndef NO_ARRAY_PRECISION
#define ARRAY_LOWP lowp
#define ARRAY_MEDIUMP mediump
#define ARRAY_HIGHP highp
#else
#define ARRAY_LOWP lowp
#define ARRAY_MEDIUMP mediump
#define ARRAY_HIGHP highp
#endif

// -- shadow depth --

// If float textures are supported, puts the values in the R/G fields.
// This assumes RG32F format.
// If float textures are NOT supported.
// This assumes RGBA8 format.
// Operational range is "whatever works for FOV depth"
highp vec4 zClydeShadowDepthPack(highp vec2 val) {
#ifdef HAS_FLOAT_TEXTURES
    return vec4(val, 0.0, 1.0);
#else
    highp vec2 valH = floor(val);
    return vec4(valH / 255.0, val - valH);
#endif
}

// Inverts the previous function.
highp vec2 zClydeShadowDepthUnpack(highp vec4 val) {
#ifdef HAS_FLOAT_TEXTURES
    return val.xy;
#else
    return (val.xy * 255.0) + val.zw;
#endif
}

// -- srgb/linear conversion core --

highp vec4 zFromSrgb(highp vec4 sRGB)
{
    highp vec3 higher = pow((sRGB.rgb + 0.055) / 1.055, vec3(2.4));
    highp vec3 lower = sRGB.rgb / 12.92;
    highp vec3 s = max(vec3(0.0), sign(sRGB.rgb - 0.04045));
    return vec4(mix(lower, higher, s), sRGB.a);
}

highp vec4 zToSrgb(highp vec4 sRGB)
{
    highp vec3 higher = (pow(sRGB.rgb, vec3(0.41666666666667)) * 1.055) - 0.055;
    highp vec3 lower = sRGB.rgb * 12.92;
    highp vec3 s = max(vec3(0.0), sign(sRGB.rgb - 0.0031308));
    return vec4(mix(lower, higher, s), sRGB.a);
}

// -- uniforms --

#ifdef HAS_UNIFORM_BUFFERS
layout (std140) uniform projectionViewMatrices
{
    highp mat3 projectionMatrix;
    highp mat3 viewMatrix;
};

layout (std140) uniform uniformConstants
{
    highp vec2 SCREEN_PIXEL_SIZE;
    highp float TIME;
};
#else
uniform highp mat3 projectionMatrix;
uniform highp mat3 viewMatrix;
uniform highp vec2 SCREEN_PIXEL_SIZE;
uniform highp float TIME;
#endif

uniform sampler2D TEXTURE;
uniform highp vec2 TEXTURE_PIXEL_SIZE;

// -- srgb emulation --

#ifdef HAS_SRGB

highp vec4 zTextureSpec(sampler2D tex, highp vec2 uv)
{
    return texture2D(tex, uv);
}

highp vec4 zAdjustResult(highp vec4 col)
{
    return col;
}
#else
uniform lowp vec2 SRGB_EMU_CONFIG;

highp vec4 zTextureSpec(sampler2D tex, highp vec2 uv)
{
    highp vec4 col = texture2D(tex, uv);
    if (SRGB_EMU_CONFIG.x > 0.5)
    {
        return zFromSrgb(col);
    }
    return col;
}

highp vec4 zAdjustResult(highp vec4 col)
{
    if (SRGB_EMU_CONFIG.y > 0.5)
    {
        return zToSrgb(col);
    }
    return col;
}
#endif

highp vec4 zTexture(highp vec2 uv)
{
    return zTextureSpec(TEXTURE, uv);
}

// -- color --

// Grayscale function for the ITU's Rec BT-709. Primarily intended for HDTVs, but standard sRGB monitors are coincidentally extremely close.
highp float zGrayscale_BT709(highp vec3 col) {
    return dot(col, vec3(0.2126, 0.7152, 0.0722));
}

// Grayscale function for the ITU's Rec BT-601, primarily intended for SDTV, but amazing for a handful of niche use-cases.
highp float zGrayscale_BT601(highp vec3 col) {
    return dot(col, vec3(0.299, 0.587, 0.114));
}

// If you don't have any reason to be specifically using the above grayscale functions, then you should default to this.
highp float zGrayscale(highp vec3 col) {
    return zGrayscale_BT709(col);
}

// -- noise --

//zRandom, zNoise, and zFBM are derived from https://godotshaders.com/snippet/2d-noise/ and https://godotshaders.com/snippet/fractal-brownian-motion-fbm/
highp vec2 zRandom(highp vec2 uv){
    uv = vec2( dot(uv, vec2(127.1,311.7) ),
               dot(uv, vec2(269.5,183.3) ) );
    return -1.0 + 2.0 * fract(sin(uv) * 43758.5453123);
}

highp float zNoise(highp vec2 uv) {
    highp vec2 uv_index = floor(uv);
    highp vec2 uv_fract = fract(uv);

    highp vec2 blur = smoothstep(0.0, 1.0, uv_fract);

    return mix( mix( dot( zRandom(uv_index + vec2(0.0,0.0) ), uv_fract - vec2(0.0,0.0) ),
                     dot( zRandom(uv_index + vec2(1.0,0.0) ), uv_fract - vec2(1.0,0.0) ), blur.x),
                mix( dot( zRandom(uv_index + vec2(0.0,1.0) ), uv_fract - vec2(0.0,1.0) ),
                     dot( zRandom(uv_index + vec2(1.0,1.0) ), uv_fract - vec2(1.0,1.0) ), blur.x), blur.y) * 0.5 + 0.5;
}

highp float zFBM(highp vec2 uv) {
    const int octaves = 6;
    highp float amplitude = 0.5;
    highp float frequency = 3.0;
    highp float value = 0.0;

    for(int i = 0; i < octaves; i++) {
        value += amplitude * zNoise(frequency * uv);
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    return value;
}


// -- generative --

// Function that creates a circular gradient. Screenspace shader bread n butter.
highp float zCircleGradient(highp vec2 ps, highp vec2 coord, highp float maxi, highp float radius, highp float dist, highp float power) {
    highp float rad = (radius * ps.y) * 0.001;
    highp float aspectratio = ps.x / ps.y;
    highp vec2 totaldistance = ((ps * 0.5) - coord) / (rad * ps);
    totaldistance.x *= aspectratio;
    highp float length = (length(totaldistance) * ps.y) - dist;
    return pow(clamp(length, 0.0, maxi), power);
}

// -- Utilities End --

// UV coordinates in texture-space. I.e., (0,0) is the corner of the texture currently being used to draw.
// When drawing a sprite from a texture atlas, (0,0) is the corner of the atlas, not the specific sprite being drawn.
varying highp vec2 UV;

// UV coordinates in quad-space. I.e., when drawing a sprite from a texture atlas (0,0) is the corner of the sprite
// currently being drawn.
varying highp vec2 UV2;

// TBH I'm not sure what this is for. I think it is scree  UV coordiantes, i.e., FRAGCOORD.xy * SCREEN_PIXEL_SIZE ?
// TODO CLYDE Is this still needed?
varying highp vec2 Pos;

// Vertex colour modulation. Note that negative values imply that the LIGHTMAP should be ignored. This is used to avoid
// having to set the texture to a white/blank texture for sprites that have no light shading applied.
varying highp vec4 VtxModulate;

// The current light map. Unless disabled, this is automatically sampled to create the LIGHT vector, which is then used
// to modulate the output colour.
// TODO CLYDE consistent shader variable naming
uniform sampler2D lightMap;

uniform ARRAY_HIGHP int mode;
uniform ARRAY_HIGHP float intensity;
uniform ARRAY_HIGHP float reduced;
uniform ARRAY_HIGHP vec2 origin;
uniform ARRAY_HIGHP vec2 size;
uniform ARRAY_HIGHP float ui_scale;
uniform ARRAY_LOWP vec4 background;


ARRAY_HIGHP float dotTile( ARRAY_HIGHP vec2 p,  ARRAY_HIGHP vec2 tile,  ARRAY_HIGHP vec2 center,  ARRAY_HIGHP vec2 radius,  ARRAY_HIGHP vec2 offset) {
 highp vec2 q = mod ( p - offset , tile ) - center * tile ;
 return max ( 0.0 , 1.0 - length ( q / radius ) ) ;

}
ARRAY_LOWP vec4 over( ARRAY_LOWP vec4 front,  ARRAY_LOWP vec4 back) {
 highp float a = front . a + back . a * ( 1.0 - front . a ) ;
 return vec4 ( ( front . rgb * front . a + back . rgb * back . a * ( 1.0 - front . a ) ) / max ( a , 0.0001 ) , a ) ;

}


void main()
{
    highp vec4 FRAGCOORD = gl_FragCoord;

    // The output colour. This should get set by the shader code block.
    // This will get modified by the LIGHT and MODULATE vectors.
    lowp vec4 COLOR;

    // The light colour, usually sampled from the LIGHTMAP
    lowp vec4 LIGHT;

    // Colour modulation vector.
    highp vec4 MODULATE;

    // Sample the texture outside of the branch / with uniform control flow.
    LIGHT = texture2D(lightMap, Pos);

    if (VtxModulate.x < 0.0)
    {
        // Negative VtxModulate implies unshaded/no lighting.
        MODULATE = -1.0 - VtxModulate;
        LIGHT = vec4(1.0);
    }
    else
    {
        MODULATE = VtxModulate;
    }

    // TODO CLYDE consistent shader variable naming
    // Requires breaking changes.
    lowp vec3 lightSample = LIGHT.xyz;

     highp vec2 screen = vec2 ( FRAGCOORD . x , 1.0 / SCREEN_PIXEL_SIZE . y - FRAGCOORD . y ) ;
 highp vec2 p = ( screen - origin ) / ui_scale ;
 highp vec2 uv = p / max ( size , vec2 ( 1.0 ) ) ;
 highp float t = TIME * ( 1.0 - reduced ) ;
 lowp vec4 effect = vec4 ( 0.0 ) ;
 if ( mode == 1 ) {
 highp float f = mod ( t , 100.0 ) / 100.0 ;
 highp float stars = dotTile ( p , vec2 ( 200.0 , 200.0 ) , vec2 ( . 10 , . 25 ) , vec2 ( 1.0 ) , vec2 ( - 200.0 , 200.0 ) * f ) * . 9 + dotTile ( p , vec2 ( 250.0 , 220.0 ) , vec2 ( . 40 , . 70 ) , vec2 ( 1.0 ) , vec2 ( 250.0 , - 220.0 ) * f ) * . 8 + dotTile ( p , vec2 ( 220.0 , 250.0 ) , vec2 ( . 65 , . 15 ) , vec2 ( 1.0 ) , vec2 ( - 220.0 , - 250.0 ) * f ) * . 8 + dotTile ( p , vec2 ( 180.0 , 240.0 ) , vec2 ( . 85 , . 55 ) , vec2 ( 1.0 ) , vec2 ( 180.0 , 240.0 ) * f ) * . 7 + dotTile ( p , vec2 ( 350.0 , 300.0 ) , vec2 ( . 20 , . 50 ) , vec2 ( 1.5 ) , vec2 ( - 350.0 , 300.0 ) * f ) * . 7 + dotTile ( p , vec2 ( 300.0 , 350.0 ) , vec2 ( . 55 , . 80 ) , vec2 ( 1.5 ) , vec2 ( 300.0 , - 350.0 ) * f ) * . 7 + dotTile ( p , vec2 ( 320.0 , 280.0 ) , vec2 ( . 80 , . 20 ) , vec2 ( 1.5 ) , vec2 ( - 320.0 , 280.0 ) * f ) * . 6 + dotTile ( p , vec2 ( 500.0 , 450.0 ) , vec2 ( . 30 , . 40 ) , vec2 ( 2.0 ) , vec2 ( 500.0 , 450.0 ) * f ) * . 6 + dotTile ( p , vec2 ( 450.0 , 500.0 ) , vec2 ( . 70 , . 75 ) , vec2 ( 2.0 ) , vec2 ( - 450.0 , - 500.0 ) * f ) * . 5 ;
 effect = vec4 ( . 88 , . 94 , 1.0 , min ( stars , 1.0 ) ) ;
 }
 else if ( mode == 2 ) {
 highp vec2 drift = vec2 ( sin ( t * . 09 ) , cos ( t * . 07 ) ) * . 3 ;
 highp float a = max ( 0.0 , 1.0 - length ( ( uv - vec2 ( . 2 , . 3 ) - drift ) / vec2 ( . 6 , . 5 ) ) ) * . 2 ;
 highp float b = max ( 0.0 , 1.0 - length ( ( uv - vec2 ( . 8 , . 7 ) + drift ) / vec2 ( . 5 , . 6 ) ) ) * . 18 ;
 highp float c = max ( 0.0 , 1.0 - length ( ( uv - vec2 ( . 5 , . 5 ) - drift . yx ) / vec2 ( . 55 , . 45 ) ) ) * . 15 ;
 effect = over ( vec4 ( . 55 , . 20 , 1.0 , a ) , over ( vec4 ( . 20 , . 47 , 1.0 , b ) , vec4 ( 1.0 , . 20 , . 59 , c ) ) ) ;
 }
 else if ( mode == 3 ) {
 highp float a = max ( 0.0 , 1.0 - mod ( p . y - t * 20.0 , 18.0 ) / 3.0 ) * . 2 ;
 highp float b = max ( 0.0 , 1.0 - mod ( p . y - t * 28.0 , 28.0 ) / 2.0 ) * . 15 ;
 highp float c = max ( 0.0 , 1.0 - mod ( p . y - t * 42.0 , 42.0 ) / 4.0 ) * . 1 ;
 effect = vec4 ( 0.0 , 1.0 , . 28 , min ( 1.0 , a + b + c ) ) ;
 }
 else if ( mode == 4 ) {
 highp float x = uv . x * . 5 + uv . y * . 2 + sin ( t * . 13 ) * . 3 ;
 highp float band = max ( 0.0 , 1.0 - abs ( x - . 5 ) * 3.0 ) * . 18 ;
 effect = vec4 ( mix ( vec3 ( 0.0 , 1.0 , . 47 ) , vec3 ( . 39 , 0.0 , 1.0 ) , clamp ( x , 0.0 , 1.0 ) ) , band ) ;
 }
 else if ( mode == 5 ) {
 highp float scale = 1.25 - . 75 * cos ( t * 1.256637 ) ;
 highp float r = length ( ( uv - . 5 ) / scale ) ;
 highp float a = r < . 2 ? mix ( . 35 , . 15 , r / . 2 ) : ( r < . 4 ? mix ( . 15 , . 05 , ( r - . 2 ) / . 2 ) : max ( 0.0 , . 05 * ( . 6 - r ) / . 2 ) ) ;
 effect = vec4 ( . 39 , . 55 , 1.0 , a ) ;
 }
 else if ( mode == 6 ) {
 highp float wave = max ( 0.0 , 1.0 - abs ( mod ( ( uv . x + uv . y ) * 5.0 + sin ( t * . 157 ) , 1.0 ) - . 5 ) * 2.0 ) ;
 effect = vec4 ( 0.0 , . 59 , . 86 , wave * . 12 ) ;
 }
 else if ( mode == 7 || mode == 8 || mode == 11 ) {
 highp vec2 offset ;
 lowp vec3 tint ;
 highp vec2 radius ;
 if ( mode == 7 ) {
 offset = vec2 ( sin ( t * . 105 ) * 70.0 , cos ( t * . 105 ) * 60.0 ) ;
 tint = vec3 ( 1.0 , . 86 , . 39 ) ;
 radius = vec2 ( 3.0 ) ;
 }
 else if ( mode == 8 ) {
 offset = vec2 ( t * 6.94 , t * 16.67 ) ;
 tint = vec3 ( 1.0 , . 70 , . 78 ) ;
 radius = vec2 ( 4.0 , 3.0 ) ;
 }
 else {
 offset = vec2 ( 0.0 , - t * 37.5 ) ;
 tint = vec3 ( 1.0 , . 49 , . 15 ) ;
 radius = vec2 ( 2.0 ) ;
 }
 highp float particles = dotTile ( p , vec2 ( 250.0 , 300.0 ) , vec2 ( . 20 , . 30 ) , radius , offset ) * . 7 + dotTile ( p , vec2 ( 300.0 , 250.0 ) , vec2 ( . 45 , . 10 ) , radius . yx , offset * 1.2 ) * . 6 + dotTile ( p , vec2 ( 200.0 , 350.0 ) , vec2 ( . 70 , . 50 ) , radius , offset * . 9 ) * . 6 + dotTile ( p , vec2 ( 350.0 , 280.0 ) , vec2 ( . 90 , . 20 ) , radius , offset * 1.1 ) * . 5 + dotTile ( p , vec2 ( 280.0 , 320.0 ) , vec2 ( . 10 , . 70 ) , radius . yx , offset * 1.3 ) * . 6 + dotTile ( p , vec2 ( 320.0 , 260.0 ) , vec2 ( . 55 , . 85 ) , radius , offset * . 8 ) * . 5 ;
 effect = vec4 ( tint , min ( 1.0 , particles ) ) ;
 }
 else if ( mode == 9 ) {
 highp float phase = uv . x * . 25 + uv . y * . 25 + . 375 * ( 1.0 - cos ( t * . 418879 ) ) ;
 effect = vec4 ( . 5 + . 5 * cos ( 6.283185 * ( phase + vec3 ( 0.0 , . 33 , . 67 ) ) ) , . 15 ) ;
 }
 else if ( mode == 10 ) {
 highp float stripe = mod ( p . x * . 9659 + p . y * . 2588 - t * 24.495 , 10.0 ) ;
 highp float stripe2 = mod ( p . x * . 9848 + p . y * . 1736 - t * 35.42 , 18.0 ) ;
 effect = vec4 ( . 59 , . 78 , 1.0 , max ( 0.0 , 1.0 - stripe ) * . 18 + max ( 0.0 , 1.0 - stripe2 / 2.0 ) * . 12 ) ;
 }
 effect . a *= intensity ;
 COLOR = over ( effect , background ) ;


    LIGHT.xyz = lightSample;

    gl_FragColor = zAdjustResult(COLOR * MODULATE * LIGHT);
}
