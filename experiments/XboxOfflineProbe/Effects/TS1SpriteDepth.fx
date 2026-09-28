float4x4 Projection;
float DepthSpan;
float AlphaPass;
texture ColorTexture;
texture DepthTexture;
sampler ColorSampler = sampler_state { Texture=<ColorTexture>; MinFilter=Point; MagFilter=Point; MipFilter=Point; AddressU=Clamp; AddressV=Clamp; };
sampler DepthSampler = sampler_state { Texture=<DepthTexture>; MinFilter=Point; MagFilter=Point; MipFilter=Point; AddressU=Clamp; AddressV=Clamp; };
struct Input { float4 Position:POSITION0; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
struct Output { float4 Position:SV_Position; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
Output VS(Input v) { Output o; o.Position=mul(v.Position,Projection); o.Color=v.Color; o.UV=v.UV; return o; }
void PS(Output v,out float4 color:SV_Target0,out float depth:SV_Depth) {
    color=tex2D(ColorSampler,v.UV)*v.Color;
    if(color.a==0 || (AlphaPass<0.5 && color.a<1) || (AlphaPass>0.5 && color.a>=1)) discard;
    depth=v.Position.z-DepthSpan*(1-tex2D(DepthSampler,v.UV).a);
}
technique SpriteDepth { pass P { VertexShader=compile vs_4_0 VS(); PixelShader=compile ps_4_0 PS(); } }
