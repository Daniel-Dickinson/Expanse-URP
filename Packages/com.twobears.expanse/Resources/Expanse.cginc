#ifndef EXPANSE_INCLUDED
#define EXPANSE_INCLUDED

//Variable data
float3 _RootPosition;
float3 _FrustrumPosition;
float4x4 _VPMatrix;

//Compute instance data
struct IndirectShaderData
{
	float density;
	float4x4 PositionMatrix;
	float4x4 InversePositionMatrix;
	float4 Extra;
};
uniform StructuredBuffer<IndirectShaderData> _PositionBuffer;

//Standard instance data
struct StandardShaderData
{
	float4 extra;
};
uniform StructuredBuffer<StandardShaderData> _Extra;

//Shared instance data
uniform StructuredBuffer<int> _LOD;

float3 MatrixToPosition(float4x4 positionMatrix)
{
	float x = positionMatrix[0][3];
	float y = positionMatrix[1][3];
	float z = positionMatrix[2][3];

	return float3(x, y, z);
}
float Distance(float3 A, float3 B)
{
	return distance(A, B);
}

//Shadergraph
void InjectSetup_float(float3 A, out float3 Out)
{
	Out = A;
}
void InstanceColorStandard_float(in int instanceID, out float4 color)
{
	int index = _LOD[instanceID];
	color = _Extra[index].extra;
}
void InstanceColorCompute_float(out float4 color)
{
	color = 1;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
	int index = _LOD[unity_InstanceID];
	color = _PositionBuffer[index].Extra;
#endif
}
void PositionColor_float(out float4 color)
{
	color = 1;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
	
	int index = _LOD[unity_InstanceID];
	float4x4 positionMatrix = _PositionBuffer[index].PositionMatrix;
	float3 position = _RootPosition + MatrixToPosition(positionMatrix);

	color.x = position.x / 1000;
	color.y = position.y / 1000;
	color.z = position.z / 1000;
	color.w = 1;
#endif
}
void Distance_float(out float distance)
{
	distance = 1;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

	int index = _LOD[unity_InstanceID];
	float4x4 positionMatrix = _PositionBuffer[index].PositionMatrix;
	float3 position = _RootPosition + MatrixToPosition(positionMatrix);
	float rawDistance = Distance(position, _FrustrumPosition);

	distance =  rawDistance / 1000;
#endif
}

//Instance override
void setupMatricies()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

#ifdef unity_ObjectToWorld
#undef unity_ObjectToWorld
#endif

#ifdef unity_WorldToObject
#undef unity_WorldToObject
#endif
	int index = _LOD[unity_InstanceID];
	unity_ObjectToWorld = _PositionBuffer[index].PositionMatrix;
	unity_WorldToObject = _PositionBuffer[index].InversePositionMatrix;
#endif
}
#endif