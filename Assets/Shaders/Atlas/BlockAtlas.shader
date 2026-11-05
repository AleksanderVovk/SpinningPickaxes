Shader "Custom/Block Atlas Pixel Exact"
{
    Properties
    {
        [MainTexture]
        [NoScaleOffset]
        _MainTex ("Atlas Texture", 2D) = "white" {}

        _AtlasColumns ("Atlas Columns", Float) = 8
        _AtlasRows ("Atlas Rows", Float) = 8

        _TextureIndex ("Texture Index", Float) = 0

        [Toggle]
        _FlipX ("Flip X", Float) = 0

        [Toggle]
        _FlipY ("Flip Y", Float) = 0

        [Toggle]
        _FlipRows ("Flip Atlas Rows", Float) = 0

        _Brightness ("Brightness", Range(0, 2)) = 1

        [HDR]
        _DamageColor ("Damage Color", Color) = (1, 0, 0, 1)

        _DamageFlash ("Damage Flash", Range(0, 1)) = 0

        _DamageOverlayStrength
        (
            "Damage Overlay Strength",
            Range(0, 1)
        ) = 1

        _DamageEmission
        (
            "Damage Emission",
            Range(0, 5)
        ) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 100

        Pass
        {
            Name "BlockAtlas"

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _AtlasColumns;
            float _AtlasRows;
            float _Brightness;

            float _FlipX;
            float _FlipY;
            float _FlipRows;

            float _DamageOverlayStrength;
            float _DamageEmission;

            UNITY_INSTANCING_BUFFER_START(Props)

                UNITY_DEFINE_INSTANCED_PROP(float, _TextureIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _DamageFlash)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageColor)

            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata input)
            {
                v2f output;

                UNITY_SETUP_INSTANCE_ID(input);

                UNITY_TRANSFER_INSTANCE_ID(
                    input,
                    output
                );

                output.vertex =
                    UnityObjectToClipPos(input.vertex);

                output.uv = input.uv;

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float columns =
                    max(
                        1.0,
                        floor(_AtlasColumns)
                    );

                float rows =
                    max(
                        1.0,
                        floor(_AtlasRows)
                    );

                float atlasWidth =
                    max(
                        1.0,
                        _MainTex_TexelSize.z
                    );

                float atlasHeight =
                    max(
                        1.0,
                        _MainTex_TexelSize.w
                    );

                float textureCount =
                    columns * rows;

                float textureIndex =
                    UNITY_ACCESS_INSTANCED_PROP(
                        Props,
                        _TextureIndex
                    );

                float index =
                    clamp(
                        floor(textureIndex),
                        0.0,
                        textureCount - 1.0
                    );

                float column =
                    fmod(
                        index,
                        columns
                    );

                float rowFromTop =
                    floor(
                        index / columns
                    );

                float rowFromBottom =
                    rows - 1.0 - rowFromTop;

                if (_FlipRows > 0.5)
                {
                    rowFromBottom = rowFromTop;
                }

                float2 localUV =
                    saturate(input.uv);

                if (_FlipX > 0.5)
                {
                    localUV.x =
                        1.0 - localUV.x;
                }

                if (_FlipY > 0.5)
                {
                    localUV.y =
                        1.0 - localUV.y;
                }

                float tileWidth =
                    atlasWidth / columns;

                float tileHeight =
                    atlasHeight / rows;

                float2 pixel;

                pixel.x =
                    min(
                        floor(localUV.x * tileWidth),
                        tileWidth - 1.0
                    );

                pixel.y =
                    min(
                        floor(localUV.y * tileHeight),
                        tileHeight - 1.0
                    );

                float atlasPixelX =
                    column * tileWidth +
                    pixel.x;

                float atlasPixelY =
                    rowFromBottom * tileHeight +
                    pixel.y;

                float2 atlasUV =
                    float2(
                        (atlasPixelX + 0.5) / atlasWidth,
                        (atlasPixelY + 0.5) / atlasHeight
                    );

                float4 color =
                    tex2Dlod(
                        _MainTex,
                        float4(
                            atlasUV,
                            0.0,
                            0.0
                        )
                    );

                color.rgb *= _Brightness;

                float damageFlash =
                    saturate(
                        UNITY_ACCESS_INSTANCED_PROP(
                            Props,
                            _DamageFlash
                        )
                    );

                float4 damageColor =
                    UNITY_ACCESS_INSTANCED_PROP(
                        Props,
                        _DamageColor
                    );

                float3 originalColor =
                    color.rgb;

                float3 pureDamageColor =
                    damageColor.rgb;

                /*
                 * Основной красный overlay.
                 *
                 * При DamageFlash = 1:
                 * цвет максимально приближается к DamageColor.
                 */
                float3 overlayColor =
                    lerp(
                        originalColor,
                        pureDamageColor,
                        saturate(
                            _DamageOverlayStrength
                        )
                    );

                /*
                 * Смешиваем исходный цвет
                 * с красным overlay.
                 */
                float3 flashedColor =
                    lerp(
                        originalColor,
                        overlayColor,
                        damageFlash
                    );

                /*
                 * Эмиссия делает flash видимым
                 * даже на светлых текстурах.
                 */
                float emissionStrength =
                    damageFlash *
                    _DamageEmission *
                    _DamageOverlayStrength;

                flashedColor +=
                    pureDamageColor *
                    emissionStrength;

                /*
                 * Ограничиваем результат,
                 * чтобы избежать некорректных значений.
                 */
                color.rgb =
                    saturate(
                        flashedColor
                    );

                return color;
            }

            ENDCG
        }
    }

    FallBack Off
}