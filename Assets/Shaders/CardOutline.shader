// Guarda este archivo como: Assets/Shaders/CardOutline.shader
Shader "UI/CardOutline"
{
    Properties
    {
        _MainTex        ("Sprite Texture",  2D)    = "white" {}
        _Color          ("Tint",            Color) = (1,1,1,1)

        // Outline
        _OutlineColor   ("Outline Color",   Color) = (1,0.85,0.1,1)
        _OutlineWidth   ("Outline Width",   Range(0, 0.05)) = 0.0

        // Unity UI internals (no tocar)
        _StencilComp    ("Stencil Comparison", Float) = 8
        _Stencil        ("Stencil ID",         Float) = 0
        _StencilOp      ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask      ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // ── structs ──────────────────────────────────────────
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
                float4 worldPos   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── variables ────────────────────────────────────────
            sampler2D   _MainTex;
            float4      _MainTex_ST;
            float4      _MainTex_TexelSize;   // (1/w, 1/h, w, h)
            fixed4      _Color;
            fixed4      _OutlineColor;
            float       _OutlineWidth;
            float4      _ClipRect;

            // ── vertex ───────────────────────────────────────────
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPos  = v.vertex;
                OUT.vertex    = UnityObjectToClipPos(OUT.worldPos);
                OUT.texcoord  = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color     = v.color * _Color;
                return OUT;
            }

            // ── fragment ─────────────────────────────────────────
            fixed4 frag(v2f IN) : SV_Target
            {
                // Color del píxel actual
                half4 col = tex2D(_MainTex, IN.texcoord) * IN.color;

                // ── Outline ──────────────────────────────────────
                // Sólo se calcula si _OutlineWidth > 0
                if (_OutlineWidth > 0.0)
                {
                    float2 uv = IN.texcoord;
                    float2 ts = _MainTex_TexelSize.xy; // tamaño de un texel

                    // Muestreamos 8 vecinos alrededor del píxel actual
                    // para ver si alguno tiene alpha > 0 (borde del sprite)
                    float w = _OutlineWidth;
                    float neighborAlpha =
                        tex2D(_MainTex, uv + float2( w,  0) * ts).a +
                        tex2D(_MainTex, uv + float2(-w,  0) * ts).a +
                        tex2D(_MainTex, uv + float2( 0,  w) * ts).a +
                        tex2D(_MainTex, uv + float2( 0, -w) * ts).a +
                        tex2D(_MainTex, uv + float2( w,  w) * ts).a +
                        tex2D(_MainTex, uv + float2(-w,  w) * ts).a +
                        tex2D(_MainTex, uv + float2( w, -w) * ts).a +
                        tex2D(_MainTex, uv + float2(-w, -w) * ts).a;

                    // Si el píxel actual es transparente PERO algún vecino no lo es
                    // → estamos en el borde → pintamos con _OutlineColor
                    if (col.a < 0.01 && neighborAlpha > 0.01)
                        col = _OutlineColor;
                }

                // Unity UI: clip rect y alpha clip
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}