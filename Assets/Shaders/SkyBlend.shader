// Небо с переходом между двумя кубами.
//
// Зачем свой шейдер, а не Skybox/Cubemap: у встроенного неба в свойствах одна
// картинка, а подъёму на километр нужно шесть разных. Подменять материал на
// границе поясов — значит получить щелчок в кадре ровно там, где игрок решает,
// перешёл он на новую ступень. Поэтому два куба живут в одном материале, а
// доля второго идёт в _Blend и меняется плавно.
//
// Шейдер нарочно простой: проверить его глазами в этом проекте нечем, сцена
// запускается из кода и скриншота никто не снимал, поэтому лишних строк здесь
// быть не должно — каждая строка это риск, который нельзя увидеть.
Shader "Parkour/SkyBlend"
{
    Properties
    {
        _TexA ("Cube A", Cube) = "" {}
        _TexB ("Cube B", Cube) = "" {}
        _Blend ("Blend", Range(0, 1)) = 0
        _Exposure ("Exposure", Float) = 1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            // Именно samplerCUBE, а не samplerCube: в Cg имя типа пишется
            // заглавными, и строчными компилятор считает его незнакомым
            // идентификатором. Все встроенные шейдеры неба написаны так же.
            samplerCUBE _TexA;
            samplerCUBE _TexB;
            float _Blend;
            float _Exposure;
            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            // Небо движок рисует объектом, у которого в вершинах лежит направление
            // взгляда, а не положение. Поэтому в пробелера класть нечего — только
            // это направление и пробать по нему куб.
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 a = texCUBE(_TexA, i.dir).rgb;
                float3 b = texCUBE(_TexB, i.dir).rgb;

                fixed3 col = lerp(a, b, _Blend) * _Exposure;
                return fixed4(col * _Tint.rgb, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
