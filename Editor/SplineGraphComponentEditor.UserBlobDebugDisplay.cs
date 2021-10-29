using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using Pastasfuture.SplineGraph.Runtime;

namespace Pastasfuture.SplineGraph.Editor
{
    public partial class SplineGraphComponentEditor : UnityEditor.Editor
    {
        private static void DrawUserBlobVertexDebugDisplay(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, SplineGraphUserBlobSchemaScriptableObject userBlobSchemaScriptableObject, int scheme)
        {
            if (splineGraph.payload.userBlobSchemaVersion == 0) { return; }
            switch (splineGraph.payload.userBlobVertex.schema[scheme].type)
            {
                case SplineGraphUserBlob.Scheme.Type.Float:
                {
                    switch (splineGraph.payload.userBlobVertex.schema[scheme].stride)
                    {
                        case 1:
                        {
                            DrawUserBlobVertexDebugDisplayFloat(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 2:
                        {
                            DrawUserBlobVertexDebugDisplayFloat2(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 3:
                        {
                            var rangeMode = userBlobSchemaScriptableObject.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode;
                            if (rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.Color || rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.ColorHDR)
                            {
                                DrawUserBlobVertexDebugDisplayFloat3Color(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            }
                            else
                            {
                                DrawUserBlobVertexDebugDisplayFloat3(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            }
                            break;
                        }
                        case 4:
                        {
                            var rangeMode = userBlobSchemaScriptableObject.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode;
                            if (rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.Color || rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.ColorHDR)
                            {
                                DrawUserBlobVertexDebugDisplayFloat4Color(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            }
                            else
                            {
                                DrawUserBlobVertexDebugDisplayFloat4(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            }
                            break;
                        }
                        default: Debug.Assert(false); break;
                    }
                    break;
                }

                case SplineGraphUserBlob.Scheme.Type.Int:
                {
                    switch (splineGraph.payload.userBlobVertex.schema[scheme].stride)
                    {
                        case 1:
                        {
                            DrawUserBlobVertexDebugDisplayInt(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 2:
                        {
                            DrawUserBlobVertexDebugDisplayInt2(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 3:
                        {
                            DrawUserBlobVertexDebugDisplayInt3(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 4:
                        {
                            DrawUserBlobVertexDebugDisplayInt4(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        default: Debug.Assert(false); break;
                    }
                    break;
                }

                case SplineGraphUserBlob.Scheme.Type.UInt:
                {
                    switch (splineGraph.payload.userBlobVertex.schema[scheme].stride)
                    {
                        case 1:
                        {
                            DrawUserBlobVertexDebugDisplayUInt(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 2:
                        {
                            DrawUserBlobVertexDebugDisplayUInt2(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 3:
                        {
                            DrawUserBlobVertexDebugDisplayUInt3(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        case 4:
                        {
                            DrawUserBlobVertexDebugDisplayUInt4(ref splineGraph, splineGraphTransform, gizmoSplineSegmentCount, scheme);
                            break;
                        }
                        default: Debug.Assert(false); break;
                    }
                    break;
                }

                default: Debug.Assert(false); break;
            }
        }

        private static void DrawUserBlobVertexDebugDisplayFloat(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                float value = splineGraph.payload.userBlobVertex.GetFloat(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayFloat2(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                float2 value = splineGraph.payload.userBlobVertex.GetFloat2(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayFloat3(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                float3 value = splineGraph.payload.userBlobVertex.GetFloat3(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayFloat4(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                float4 value = splineGraph.payload.userBlobVertex.GetFloat4(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayFloat3Color(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];
                float3 positionWS = splineGraphTransform.TransformPoint(positionOS);
                float handleSize = ComputeUserBlobVertexDebugDisplayHandleSize(positionWS);

                float3 color = splineGraph.payload.userBlobVertex.GetFloat3(scheme, v);
                Handles.color = new Color(color.x, color.y, color.z, 1.0f);
                Handles.DotHandleCap(0, positionOS, quaternion.identity, handleSize, EventType.Repaint);
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayFloat4Color(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];
                float3 positionWS = splineGraphTransform.TransformPoint(positionOS);
                float handleSize = ComputeUserBlobVertexDebugDisplayHandleSize(positionWS);

                float4 color = splineGraph.payload.userBlobVertex.GetFloat4(scheme, v);
                Handles.color = new Color(color.x, color.y, color.z, color.w);
                Handles.DotHandleCap(0, positionOS, quaternion.identity, handleSize, EventType.Repaint);
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayInt(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                int value = splineGraph.payload.userBlobVertex.GetInt(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayInt2(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                int2 value = splineGraph.payload.userBlobVertex.GetInt2(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayInt3(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                int3 value = splineGraph.payload.userBlobVertex.GetInt3(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayInt4(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                int4 value = splineGraph.payload.userBlobVertex.GetInt4(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayUInt(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                uint value = splineGraph.payload.userBlobVertex.GetUInt(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayUInt2(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                uint2 value = splineGraph.payload.userBlobVertex.GetUInt2(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayUInt3(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                uint3 value = splineGraph.payload.userBlobVertex.GetUInt3(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static void DrawUserBlobVertexDebugDisplayUInt4(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Transform splineGraphTransform, int gizmoSplineSegmentCount, int scheme)
        {
            Color handleColorPrevious = Handles.color;
            Matrix4x4 handleMatrixPrevious = Handles.matrix;

            Handles.color = Color.white;
            Handles.matrix = splineGraphTransform.localToWorldMatrix;

            for (Int16 v = 0, vLen = (Int16)splineGraph.vertices.count; v < vLen; ++v)
            {
                DirectedVertex vertex = splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 positionOS = splineGraph.payload.positions.data[v];

                uint4 value = splineGraph.payload.userBlobVertex.GetUInt4(scheme, v);
                Handles.color = Color.white;
                Handles.Label(positionOS, value.ToString());
            }

            Handles.matrix = handleMatrixPrevious;
            Handles.color = handleColorPrevious;
        }

        private static float ComputeUserBlobVertexDebugDisplayHandleSize(float3 positionWS)
        {
            float handleSize = HandleUtility.GetHandleSize(positionWS);
            const float HANDLE_DISPLAY_SIZE = 0.1f;
            handleSize *= HANDLE_DISPLAY_SIZE;
            return handleSize;
        }
    }
}