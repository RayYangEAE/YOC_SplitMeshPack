Shader "Custom/PaperExtrude" {
	Properties
	{
		//_Color ("Main Tint", Color) = (1,1,1,1)
        _MainTex ("Base (RGBA)", 2D) = "white" {}
        //_BumpMap ("Normalmap", 2D) = "bump" {}
    	//_BackColor ("Back Main Color", Color) = (1,1,1,1)
    	//_BackMainTex ("Back Base (RGBA)", 2D) = "white" {}
		_Factor ("Factor", Range(0., 2.)) = 0.02
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Cull off
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom

			#include "UnityCG.cginc"

			struct v2g
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct g2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				fixed4 col : COLOR;
				float3 normal: NORMAL;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2g vert (appdata_base v)
			{
				v2g o;
				o.vertex = v.vertex;
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.normal = v.normal;
				return o;
			}

			float _Factor;
			[maxvertexcount(24)]
			void geom(triangle v2g IN[3], inout TriangleStream<g2f> tristream)
			{
				g2f o;

				float3 edgeA = IN[1].vertex - IN[0].vertex;
				float3 edgeB = IN[2].vertex - IN[0].vertex;
				float3 faceNormal = normalize(cross(edgeA, edgeB));

				for (int i = 0; i < 3; i++)
				{
					float3 vertNormal = normalize(IN[i].normal);
					int iNext=(i+1)%3;
					float3 nextVertNormal = normalize(IN[iNext].normal);

					o.pos = UnityObjectToClipPos(IN[i].vertex);
					o.uv = IN[i].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);
	
					o.pos = UnityObjectToClipPos(IN[i].vertex + float4(vertNormal, 0) * _Factor);
					o.uv = IN[i].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);

					o.pos = UnityObjectToClipPos(IN[iNext].vertex);
					o.uv = IN[iNext].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);
	
					tristream.RestartStrip();
	
					o.pos = UnityObjectToClipPos(IN[i].vertex + float4(vertNormal, 0) * _Factor);
					o.uv = IN[i].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);
	
					o.pos = UnityObjectToClipPos(IN[iNext].vertex);
					o.uv = IN[iNext].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);
	
					o.pos = UnityObjectToClipPos(IN[iNext].vertex + float4(nextVertNormal, 0) * _Factor);
					o.uv = IN[iNext].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);
	
					tristream.RestartStrip();
				}

				for(int j = 0; j < 3; j++)
				{
					o.pos = UnityObjectToClipPos(IN[j].vertex + float4(normalize(IN[j].normal), 0) * _Factor);
					o.uv = IN[j].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = faceNormal;
					tristream.Append(o);
				}

				tristream.RestartStrip();

				for(int k = 0; k < 3; k++)
				{
					o.pos = UnityObjectToClipPos(IN[k].vertex);
					o.uv = IN[k].uv;
					o.col = fixed4(1., 1., 1., 1.);
					o.normal = -faceNormal;
					tristream.Append(o);
				}

				tristream.RestartStrip();

			}
			
			fixed4 frag (g2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * i.col;
				return col;
			}
			ENDCG
		}

	}
	FallBack "Diffuse"
}