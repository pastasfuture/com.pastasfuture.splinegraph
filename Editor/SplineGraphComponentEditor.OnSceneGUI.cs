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
        private void OnSceneGUIDrawUserBlobVertexDebugDisplay(SplineGraphComponent sgc)
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawUserBlobVertexDebugDisplay(ref sgc.splineGraph, sgc.transform, sgc.gizmoSplineSegmentCount, sgc.splineGraphUserBlobSchema, userBlobDebugDisplayMode);
            }
        }

        private void OnSceneGUILockSelection(SplineGraphComponent sgc)
        {
            if (sgc.isEditingEnabled && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();

                if ((selectionType == SelectionType.Vertex) && selectedIndices.Count > 0)
                {
                    // Just deselect all.
                    selectedIndices.Clear();
                }
                else
                {
                    // Nothing is selected, treat escape in this case as a request to fully exit editing.
                    sgc.isEditingEnabled = false;
                    Reset();
                }
                
            }

            if (sgc.isEditingEnabled)
            {
                Selection.activeGameObject = sgc.gameObject;
            }
        }

        private void OnSceneGUIExtrusionToolDetectCancel()
        {
            if (!Event.current.shift || (GUIUtility.hotControl == 0))
            {
                // Detect when user has stopped holding shift and reset extruding state.
                // Also reset extrude state if user is no longer interacting with a control.
                // This allows us to support the case where user holds shift, drags out, releases mouse but keeps holding shift,
                // clicks mouse, drags out, etc.
                // Feels a little jank that we would do this so high up, but it ensures we have all states covered.
                isExtruding = false;
            }
        }

        private void OnSceneGUIDeleteSelectedVerticesTool(SplineGraphComponent sgc)
        {
            // Delete selected vertices if delete key is pressed.
            if (Event.current != null
                && Event.current.isKey
                && Event.current.type == EventType.KeyUp
                && (Event.current.keyCode == KeyCode.Delete || Event.current.keyCode == KeyCode.Backspace))
            {
                if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 0))
                {
                    sgc.UndoRecord("Spline Graph Vertex Remove", true);
                    Event.current.Use();

                    for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                    {
                        Int16 selectedVertexIndex = selectedIndices[i];

                        sgc.VertexRemove(selectedVertexIndex);
                    }

                    selectedIndices.Clear();
                }
            }
        }

        private void OnSceneGUISceneViewFrameOverride(SplineGraphComponent sgc)
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.F && (selectionType == SelectionType.Vertex) && sgc.splineGraph.vertices.count > 0)
            {
                Event.current.Use();

                float3 selectionMinWS = float.MaxValue;
                float3 selectionMaxWS = -float.MaxValue;

                // If no vertices are selected, frame all vertices.
                for (int i = 0, iLen = (selectedIndices.Count > 0) ? selectedIndices.Count : sgc.splineGraph.vertices.count; i < iLen; ++i)
                {
                    Int16 v = (selectedIndices.Count > 0) ? selectedIndices[i] : (Int16)i;
                    if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    // Approximately take into account leash size when zooming by encapsulating leash cut section cardinal points.
                    for (int leashPointIndex = 0; leashPointIndex < 4; ++leashPointIndex)
                    {
                        float2 leashOS = new float2(leashPointIndex & 1, leashPointIndex >> 1) * 2.0f - 1.0f;
                        leashOS *= sgc.splineGraph.payload.leashes.data[v];

                        float3 samplePositionOS = sgc.splineGraph.payload.positions.data[v] + math.mul(sgc.splineGraph.payload.rotations.data[v], new float3(leashOS, 0.0f));
                        float3 samplePositionWS = sgc.transform.localToWorldMatrix.MultiplyPoint(samplePositionOS);
                        selectionMinWS = math.min(selectionMinWS, samplePositionWS);
                        selectionMaxWS = math.max(selectionMaxWS, samplePositionWS);
                    }
                }

                float3 boundsCenter = (selectionMaxWS - selectionMinWS) * 0.5f + selectionMinWS;
                float3 boundsSize = selectionMaxWS - selectionMinWS;

                SceneView.lastActiveSceneView.Frame(new Bounds(boundsCenter, boundsSize), false);
            }
        }

        private struct OnSceneGUIVertexTransformToolContext
        {
            public float3 positionOffsetOS;
            public quaternion rotationOffsetOS;
            public float3 rotationOffsetOriginOS;
            public float2 scaleOffsetOS;
            public float2 leashOffsetOS;
            public float tangentHandleOffsetScalar;
            public float tangentHandleSign;
            public float bitangentHandleOffsetScalar;
            public float bitangentHandleSign;

            public static readonly OnSceneGUIVertexTransformToolContext zero = new OnSceneGUIVertexTransformToolContext
            {
                positionOffsetOS = float3.zero,
                rotationOffsetOS = quaternion.identity,
                rotationOffsetOriginOS = float3.zero,
                scaleOffsetOS = new float2(1.0f, 1.0f),
                leashOffsetOS = float2.zero,
                tangentHandleOffsetScalar = 0.0f,
                tangentHandleSign = 0.0f,
                bitangentHandleOffsetScalar = 0.0f,
                bitangentHandleSign = 0.0f
            };
        }

        private bool TryOnSceneGUIVertexTransformTool(out OnSceneGUIVertexTransformToolContext c, SplineGraphComponent sgc, quaternion rotation)
        {
            EditorGUI.BeginChangeCheck();
            c = OnSceneGUIVertexTransformToolContext.zero;
            for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
            {
                Int16 selectedVertexIndex = selectedIndices[i];

                float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                quaternion vertexRotationOS = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                float2 vertexScaleOS = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                float2 vertexLeashOS = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];
                vertexRotationOS = math.mul(rotation, vertexRotationOS);

                vertexPositionOS = sgc.transform.TransformPoint(vertexPositionOS);

                // TODO: Make scale respect transform scale.

                if (Tools.current == Tool.Move)
                {
                    if (TryOnSceneGUIMoveTool(out c.positionOffsetOS, sgc, vertexPositionOS, vertexRotationOS))
                    {
                        // Under multi-selection, positionOffsetOS will be the same for all points being transformed.
                        break;
                    }
                }

                else if (Tools.current == Tool.Rotate)
                {
                    if (TryOnSceneGUIRotationTool(out c.rotationOffsetOS, out c.rotationOffsetOriginOS, vertexPositionOS, vertexRotationOS))
                    {
                        break;
                    }
                }

                else if (Tools.current == Tool.Scale)
                {
                    if (TryOnSceneGUIScaleTool(out c.scaleOffsetOS, out c.leashOffsetOS, out c.tangentHandleOffsetScalar, out c.tangentHandleSign, out c.bitangentHandleOffsetScalar, out c.bitangentHandleSign, vertexPositionOS, vertexRotationOS, vertexScaleOS, vertexLeashOS))
                    {
                        break;
                    }
                }
            }

            return EditorGUI.EndChangeCheck();
        }

        private void OnSceneGUIApplyVertexTransformTool(SplineGraphComponent sgc, quaternion rotation, ref OnSceneGUIVertexTransformToolContext c)
        {
            if (Tools.current == Tool.Move)
            {
                OnSceneGUIApplyMoveTool(sgc, c.positionOffsetOS);
            }
            else if (Tools.current == Tool.Rotate)
            {
                OnSceneGUIApplyRotationTool(sgc, rotation, c.rotationOffsetOS, c.rotationOffsetOriginOS);
            }
            else if (Tools.current == Tool.Scale)
            {
                OnSceneGUIApplyScaleTool(sgc, rotation, c.tangentHandleOffsetScalar, c.tangentHandleSign, c.bitangentHandleOffsetScalar, c.bitangentHandleSign, c.scaleOffsetOS, c.leashOffsetOS);
            }
        }

        private bool TryOnSceneGUIMoveTool(out float3 positionOffsetOS, SplineGraphComponent sgc, float3 vertexPositionOS, quaternion vertexRotationOS)
        {
            positionOffsetOS = float3.zero;

            float3 vertexPositionNewOS = Handles.PositionHandle(vertexPositionOS, vertexRotationOS);
            if (math.any(vertexPositionOS != vertexPositionNewOS))
            {
                positionOffsetOS = vertexPositionNewOS - vertexPositionOS;
                return true;
            }
            return false;
        }

        private void OnSceneGUIApplyMoveTool(SplineGraphComponent sgc, float3 positionOffsetOS)
        {
            if (Event.current.shift && !isExtruding)
            {
                // Extrude selected vertex / vertices along offset.
                sgc.UndoRecord("Edited Spline Graph Extude", true);

                isExtruding = true;

                for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                {
                    Int16 selectedVertexIndex = selectedIndices[i];

                    float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                    vertexPositionOS = sgc.transform.TransformPoint(vertexPositionOS);
                    vertexPositionOS += positionOffsetOS;
                    vertexPositionOS = sgc.transform.InverseTransformPoint(vertexPositionOS);

                    quaternion rotationParent = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                    float2 scaleParent = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                    float2 leashParent = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];

                    Int16 selectedVertexChildIndex = sgc.VertexAdd(vertexPositionOS, rotationParent, scaleParent, leashParent);
                    sgc.EdgeAdd(selectedVertexIndex, selectedVertexChildIndex);

                    // Update the selection to the new child vertex instead of the parent.
                    // This makes it convenient to perform multiple extrusions.
                    selectedIndices[i] = selectedVertexChildIndex;
                }
            }
            else
            {
                // Standard move of currently selected vertex / vertices.
                if (isExtruding)
                {
                    sgc.UndoRecord("Edited Spline Graph Extude");
                }
                else
                {
                    sgc.UndoRecord("Edited Spline Graph Vertex Position");
                }

                for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                {
                    Int16 selectedVertexIndex = selectedIndices[i];

                    float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                    vertexPositionOS = sgc.transform.TransformPoint(vertexPositionOS);
                    vertexPositionOS += positionOffsetOS;
                    vertexPositionOS = sgc.transform.InverseTransformPoint(vertexPositionOS);
                    sgc.splineGraph.payload.positions.data[selectedVertexIndex] = vertexPositionOS;
                }

                for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
                {
                    sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
                }
            }
        }

        private bool TryOnSceneGUIRotationTool(out quaternion rotationOffsetOS, out float3 rotationOffsetOriginOS, float3 vertexPositionOS, quaternion vertexRotationOS)
        {
            rotationOffsetOS = quaternion.identity;
            rotationOffsetOriginOS = float3.zero;

            quaternion vertexRotationNewOS = Handles.RotationHandle(vertexRotationOS, vertexPositionOS);
            if (math.any(vertexRotationNewOS.value != vertexRotationOS.value))
            {
                // Compute the quaternion difference.
                rotationOffsetOS = math.mul(vertexRotationNewOS, math.inverse(vertexRotationOS));

                // Cache off the position of the current vertex we are rotating.
                // This will be used to rotate multi-selected vertices about this origin.
                rotationOffsetOriginOS = vertexPositionOS;


                // Under multi-selection, rotationOffsetOS will be the same for all points being transformed.
                return true;
            }

            return false;
        }

        private void OnSceneGUIApplyRotationTool(SplineGraphComponent sgc, quaternion rotation, quaternion rotationOffsetOS, float3 rotationOffsetOriginOS)
        {
            sgc.UndoRecord("Edited Spline Graph Vertex Rotation");

            for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
            {
                Int16 selectedVertexIndex = selectedIndices[i];

                quaternion vertexRotationOS = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                vertexRotationOS = math.mul(rotationOffsetOS, vertexRotationOS);
                sgc.splineGraph.payload.rotations.data[selectedVertexIndex] = vertexRotationOS;

                // In order to support the case where we have multi-selected vertices
                // we need to rotate the positions of all our selected vertices about the vertex
                // whos handle is currently being interacted with.
                float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                vertexPositionOS = sgc.transform.TransformPoint(vertexPositionOS);
                vertexPositionOS -= rotationOffsetOriginOS;

                vertexPositionOS = math.mul(rotationOffsetOS, vertexPositionOS);

                vertexPositionOS += rotationOffsetOriginOS;
                vertexPositionOS = sgc.transform.InverseTransformPoint(vertexPositionOS);
                sgc.splineGraph.payload.positions.data[selectedVertexIndex] = vertexPositionOS;
            }

            for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
            {
                sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
            }
        }

        private bool TryOnSceneGUIScaleTool(out float2 scaleOffsetOS, out float2 leashOffsetOS, out float tangentHandleOffsetScalar, out float tangentHandleSign, out float bitangentHandleOffsetScalar, out float bitangentHandleSign, float3 vertexPositionOS, quaternion vertexRotationOS, float2 vertexScaleOS, float2 vertexLeashOS)
        {
            scaleOffsetOS = new float2(1.0f, 1.0f);
            leashOffsetOS = float2.zero;
            tangentHandleOffsetScalar = 0.0f;
            tangentHandleSign = 1.0f;
            bitangentHandleOffsetScalar = 0.0f;
            bitangentHandleSign = 1.0f;

            bool isDone = false;
            {
                float handleSize = HandleUtility.GetHandleSize(vertexPositionOS) * 1.0f;
                float scaleNewOSX = Handles.ScaleSlider(vertexScaleOS.x, vertexPositionOS, math.mul(vertexRotationOS, new float3(0.0f, 0.0f, -1.0f)), vertexRotationOS, handleSize, 1e-5f);
                float scaleNewOSY = Handles.ScaleSlider(vertexScaleOS.y, vertexPositionOS, math.mul(vertexRotationOS, new float3(0.0f, 0.0f, 1.0f)), vertexRotationOS, handleSize, 1e-5f);
                float2 scaleNewOS = new float2(scaleNewOSX, scaleNewOSY);

                if (math.any(scaleNewOS != vertexScaleOS))
                {
                    scaleOffsetOS.x = (math.abs(vertexScaleOS.x) < 1e-5f) ? 1.0f : (scaleNewOSX / vertexScaleOS.x);
                    scaleOffsetOS.y = (math.abs(vertexScaleOS.y) < 1e-5f) ? 1.0f : (scaleNewOSY / vertexScaleOS.y);

                    // Under multi-selection, scaleOffsetOS will be the same for all points being transformed.
                    isDone = true;
                }
            }

            {
                float handleSize = HandleUtility.GetHandleSize(vertexPositionOS) * 1.0f;
                float leashNewOSX = Handles.ScaleSlider(vertexLeashOS.x, vertexPositionOS, math.mul(vertexRotationOS, new float3(1.0f, 0.0f, 0.0f)), vertexRotationOS, handleSize, 1e-5f);
                float leashNewOSY = Handles.ScaleSlider(vertexLeashOS.y, vertexPositionOS, math.mul(vertexRotationOS, new float3(0.0f, 1.0f, 0.0f)), vertexRotationOS, handleSize, 1e-5f);
                float2 leashNewOS = new float2(leashNewOSX, leashNewOSY);

                if (math.any(leashNewOS != vertexLeashOS))
                {
                    leashOffsetOS = new float2(leashNewOSX, leashNewOSY) - vertexLeashOS;

                    // Under multi-selection, leashOffsetOS will be the same for all points being transformed.
                    isDone = true;
                }
            }

            if (isDone) { return true; }

            {
                float handleSize = HandleUtility.GetHandleSize(vertexPositionOS) * 0.25f;

                Color handleColorPrevious = Handles.color;

                Handles.color = Color.red;
                for (int s = 0; s < 2; ++s)
                {
                    float sign = (s == 0) ? -1.0f : 1.0f;

                    float3 tangentWS = math.mul(vertexRotationOS, new float3(sign, 0.0f, 0.0f));
                    float3 tangentHandlePositionWS = vertexLeashOS.x * tangentWS + vertexPositionOS;
                    float3 tangentHandlePositionNewWS = Handles.Slider(tangentHandlePositionWS, tangentWS, handleSize, Handles.ConeHandleCap, 0.1f);

                    if (math.any(tangentHandlePositionNewWS != tangentHandlePositionWS))
                    {
                        tangentHandleOffsetScalar = math.dot(tangentWS, tangentHandlePositionNewWS - tangentHandlePositionWS);

                        tangentHandleSign = sign;

                        // Under multi-selection, leashOffsetOS and positionOffsetOS will be the same for all points being transformed.
                        isDone = true;
                    }
                }

                Handles.color = Color.green;
                for (int s = 0; s < 2; ++s)
                {
                    float sign = (s == 0) ? -1.0f : 1.0f;

                    float3 bitangentWS = math.mul(vertexRotationOS, new float3(0.0f, sign, 0.0f));
                    float3 bitangentHandlePositionWS = vertexLeashOS.y * bitangentWS + vertexPositionOS;
                    float3 bitangentHandlePositionNewWS = Handles.Slider(bitangentHandlePositionWS, bitangentWS, handleSize, Handles.ConeHandleCap, 0.1f);

                    if (math.any(bitangentHandlePositionNewWS != bitangentHandlePositionWS))
                    {
                        bitangentHandleOffsetScalar = math.dot(bitangentWS, bitangentHandlePositionNewWS - bitangentHandlePositionWS);

                        bitangentHandleSign = sign;

                        // Under multi-selection, leashOffsetOS and positionOffsetOS will be the same for all points being transformed.
                        isDone = true;
                    }
                }
                Handles.color = handleColorPrevious;

            }

            return isDone;
        }

        private void OnSceneGUIApplyScaleTool(SplineGraphComponent sgc, quaternion rotation, float tangentHandleOffsetScalar, float tangentHandleSign, float bitangentHandleOffsetScalar, float bitangentHandleSign, float2 scaleOffsetOS, float2 leashOffsetOS)
        {
            sgc.UndoRecord("Edited Spline Graph Vertex Scale");

            for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
            {
                Int16 selectedVertexIndex = selectedIndices[i];

                // Position changes can happen if we are dragging on the edge of the leash handles.
                // TODO: Could maybe cleanup this code section by adding a notion of feature flags.
                if (math.abs(tangentHandleOffsetScalar) > 1e-5f
                    || math.abs(bitangentHandleOffsetScalar) > 1e-5f)
                {
                    quaternion vertexRotationOS = sgc.splineGraph.payload.rotations.data[selectedVertexIndex];
                    vertexRotationOS = math.mul(rotation, vertexRotationOS);

                    float3 tangentWS = math.mul(vertexRotationOS, new float3(tangentHandleSign, 0.0f, 0.0f));
                    float3 bitangentWS = math.mul(vertexRotationOS, new float3(0.0f, bitangentHandleSign, 0.0f));

                    float3 vertexPositionOS = sgc.splineGraph.payload.positions.data[selectedVertexIndex];
                    vertexPositionOS = sgc.transform.TransformPoint(vertexPositionOS);
                    vertexPositionOS += tangentWS * (tangentHandleOffsetScalar * 0.5f);
                    vertexPositionOS += bitangentWS * (bitangentHandleOffsetScalar * 0.5f);
                    vertexPositionOS = sgc.transform.InverseTransformPoint(vertexPositionOS);
                    sgc.splineGraph.payload.positions.data[selectedVertexIndex] = vertexPositionOS;

                    float2 vertexLeashOS = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];
                    vertexLeashOS = math.max(0.0f, vertexLeashOS + new float2(tangentHandleOffsetScalar * 0.5f, bitangentHandleOffsetScalar * 0.5f));
                    sgc.splineGraph.payload.leashes.data[selectedVertexIndex] = vertexLeashOS;
                }
                else
                {
                    // Standard scale tool.
                    float2 vertexScaleOS = sgc.splineGraph.payload.scales.data[selectedVertexIndex];
                    vertexScaleOS *= scaleOffsetOS;
                    sgc.splineGraph.payload.scales.data[selectedVertexIndex] = vertexScaleOS;

                    float2 vertexLeashOS = sgc.splineGraph.payload.leashes.data[selectedVertexIndex];
                    vertexLeashOS = math.max(0.0f, vertexLeashOS + leashOffsetOS);
                    sgc.splineGraph.payload.leashes.data[selectedVertexIndex] = vertexLeashOS;
                }

            }

            for (Int16 i = 0, iCount = (Int16)selectedIndices.Count; i < iCount; ++i)
            {
                sgc.splineGraph.VertexComputePayloads(selectedIndices[i]);
            }
        }

        private void OnSceneGUISelectVerticesTool(SplineGraphComponent sgc, quaternion rotation)
        {
            if (dragRectState == DragRectState.None)
            {
                if (!TryOnSceneGUISelectVerticesClickTool(sgc, rotation))
                {
                    OnSceneGUISelectVerticesDragToolBegin(sgc, rotation);
                }
            }
            else if (dragRectState == DragRectState.Dragging)
            {
                OnSceneGUISelectVerticesDragToolDrag(sgc, rotation);
            }
        }

        private bool TryOnSceneGUISelectVerticesClickTool(SplineGraphComponent sgc, quaternion rotation)
        {
            bool clicked = false;

            for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
            {
                DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                if (vertex.IsValid() == 0) { continue; }

                float3 vertexPosition = sgc.splineGraph.payload.positions.data[v];
                vertexPosition = sgc.transform.TransformPoint(vertexPosition);

                quaternion vertexRotation = sgc.splineGraph.payload.rotations.data[v];
                vertexRotation = math.mul(rotation, vertexRotation);

                float handleSize = HandleUtility.GetHandleSize(vertexPosition);
                const float HANDLE_DISPLAY_SIZE = 0.05f;
                const float HANDLE_PICK_SIZE = HANDLE_DISPLAY_SIZE + HANDLE_DISPLAY_SIZE * 0.5f;
                if (Handles.Button(vertexPosition, vertexRotation, handleSize * HANDLE_DISPLAY_SIZE, handleSize * HANDLE_PICK_SIZE, Handles.DotHandleCap))
                {
                    if (Event.current.shift && (selectionType == SelectionType.Vertex))
                    {
                        // Additive / Subtractive selection.
                        if (selectedIndices.IndexOf(v) >= 0)
                        {
                            selectedIndices.Remove(v);
                            clicked = true;
                        }
                        else
                        {
                            selectedIndices.Add(v);
                            clicked = true;
                        }

                    }
                    else
                    {
                        selectionType = SelectionType.Vertex;
                        selectedIndices.Clear();
                        selectedIndices.Add(v);
                        clicked = true;
                    }
                    Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                }
            }

            return clicked;
        }

        private void OnSceneGUISelectVerticesDragToolBegin(SplineGraphComponent sgc, quaternion rotation)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                GUIUtility.hotControl = 0;
                dragRectState = DragRectState.Dragging;
                dragRectPositionBegin = Event.current.mousePosition;
                dragRectPositionEnd = dragRectPositionBegin;
            }
        }

        private void OnSceneGUISelectVerticesDragToolDrag(SplineGraphComponent sgc, quaternion rotation)
        {
            dragRectPositionEnd = Event.current.mousePosition;

            float2 dragRectPositionMin = math.min(dragRectPositionBegin, dragRectPositionEnd);
            float2 dragRectPositionMax = math.max(dragRectPositionBegin, dragRectPositionEnd);

            if (Event.current.type == EventType.Repaint)
            {
                Camera cameraCurrent = SceneView.lastActiveSceneView.camera;
                float z = (cameraCurrent.farClipPlane - cameraCurrent.nearClipPlane) * 0.5f + cameraCurrent.nearClipPlane;
                Handles.DrawLine(cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMin.x, cameraCurrent.pixelHeight - dragRectPositionMin.y, z)), cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMax.x, cameraCurrent.pixelHeight - dragRectPositionMin.y, z)), 2.0f);
                Handles.DrawLine(cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMax.x, cameraCurrent.pixelHeight - dragRectPositionMin.y, z)), cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMax.x, cameraCurrent.pixelHeight - dragRectPositionMax.y, z)), 2.0f);
                Handles.DrawLine(cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMax.x, cameraCurrent.pixelHeight - dragRectPositionMax.y, z)), cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMin.x, cameraCurrent.pixelHeight - dragRectPositionMax.y, z)), 2.0f);
                Handles.DrawLine(cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMin.x, cameraCurrent.pixelHeight - dragRectPositionMax.y, z)), cameraCurrent.ScreenToWorldPoint(new float3(dragRectPositionMin.x, cameraCurrent.pixelHeight - dragRectPositionMin.y, z)), 2.0f);
            }

            // Need to also handle the case where the mouse left the window, because in that case we will not get a MouseUp event.
            if ((Event.current.type == EventType.MouseUp && Event.current.button == 0) || Event.current.type == EventType.MouseLeaveWindow)
            {
                Event.current.Use();
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                dragRectState = DragRectState.None;

                if (!Event.current.shift)
                {
                    // Not an additive or subtractive selection.
                    // Completely clear any previously selected vertices before attempting to select more vertices.
                    selectedIndices.Clear();
                }

                for (Int16 v = 0, vCount = (Int16)sgc.splineGraph.vertices.count; v < vCount; ++v)
                {
                    DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                    if (vertex.IsValid() == 0) { continue; }

                    float3 vertexPosition = sgc.splineGraph.payload.positions.data[v];
                    vertexPosition = sgc.transform.TransformPoint(vertexPosition);

                    float3 vertexPositionSS = SceneView.lastActiveSceneView.camera.WorldToScreenPoint(vertexPosition);

                    // Flip Y.
                    vertexPositionSS.y = SceneView.lastActiveSceneView.camera.pixelHeight - vertexPositionSS.y;

                    if (math.all(vertexPositionSS.xy >= dragRectPositionMin) && math.all(vertexPositionSS.xy <= dragRectPositionMax))
                    {
                        if (Event.current.shift)
                        {
                            // Additive / Subtractive selection.
                            if (selectedIndices.IndexOf(v) >= 0)
                            {
                                selectedIndices.Remove(v);
                            }
                            else
                            {
                                selectedIndices.Add(v);
                            }

                        }
                        else
                        {
                            selectedIndices.Add(v);
                        }
                        Repaint(); // Repaint editor to display selection changes in InspectorGUI.
                    }
                }
                
            }
        }

    }
}