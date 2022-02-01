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
    [CustomEditor(typeof(SplineGraphComponent))]
    public partial class SplineGraphComponentEditor : UnityEditor.Editor
    {
        private enum SelectionType
        {
            Vertex = 0,
            Edge = 1
        };
        private SelectionType selectionType = SelectionType.Vertex;
        private List<Int16> selectedIndices = new List<Int16>();

        private enum DragRectState
        {
            None = 0,
            Dragging
        };
        private DragRectState dragRectState = DragRectState.None;
        private float2 dragRectPositionBegin = float2.zero;
        private float2 dragRectPositionEnd = float2.zero;
        private int dragRectHotControlPrevious = 0;

        private bool isExtruding = false;

        private List<Int16> scratchIndices = new List<Int16>(128);

        private static DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> copyScratchWS;

        private void Reset()
        {
            selectionType = SelectionType.Vertex;
            selectedIndices.Clear();
            dragRectState = DragRectState.None;
            dragRectPositionBegin = float2.zero;
            dragRectPositionEnd = float2.zero;
            isExtruding = false;
            scratchIndices.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sgc = target as SplineGraphComponent;
            sgc.Verify();
            EditorGUILayout.BeginVertical();

            if (!TryOnInspectorGUIIsEditingEnabledTool(sgc))
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            {
                OnInspectorGUIUserBlobSchemaTool(sgc);
                OnInspectorGUISplineGraphBinaryDataTool(sgc);
                OnInspectorGUITypeTool(sgc);
                OnInspectorGUIGizmoSplineSegmentCountTool(sgc);
                OnInspectorGUIUserBlobDebugDisplayTool(sgc);
                OnInspectorGUIAddVertexTool(sgc);
                OnInspectorGUIConnectSelectedTool(sgc);
                OnInspectorGUIMergeSelectedVerticesTool(sgc);
                OnInspectorGUISplitEdgeBetweenSelectedVerticesTool(sgc);
                OnInspectorGUIBuildCompactGraphTool(sgc);
                OnInspectorGUICopyGraphTool(sgc);
                OnInspectorGUIPasteGraphTool(sgc);
                OnInspectorGUIReverseTool(sgc);
                OnInspectorGUISelectAllTool(sgc);
                OnInspectorGUIDeselectAllTool(sgc);
                OnInspectorGUIRecenterTransformTool(sgc);
                OnInspectorGUISetValueTool(sgc);
                OnInspectorGUIScaleTool(sgc);
                OnInspectorGUIExtrudeArcTool(sgc);
                OnInspectorGUIEditVertexTool(sgc);
            }
            bool inspectorChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.EndVertical();

            sgc.Verify();
            serializedObject.ApplyModifiedProperties();

            if (inspectorChanged)
            {
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        private void OnSceneGUI()
        {
            // If we are currently tumbling the camera, do not attempt to do anything else.
            if (Event.current.alt) { return; }

            serializedObject.Update();

            var sgc = target as SplineGraphComponent;
            sgc.Verify();

            if (!sgc.isEditingEnabled) { return; }

            OnSceneGUIDrawUserBlobVertexDebugDisplay(sgc);

            OnSceneGUILockSelection(sgc);

            Transform transform = sgc.transform;
            Quaternion rotation = (Tools.pivotRotation == PivotRotation.Local)
                ? transform.rotation
                : Quaternion.identity;


            OnSceneGUIExtrusionToolDetectCancel();
            OnSceneGUISceneViewFrameOverride(sgc);
            OnSceneGUIDeleteSelectedVerticesTool(sgc);

            if ((selectionType == SelectionType.Vertex) && (selectedIndices.Count > 0) && (dragRectState == DragRectState.None))
            {
                if (TryOnSceneGUIVertexTransformTool(out OnSceneGUIVertexTransformToolContext context, sgc, rotation))
                {
                    OnSceneGUIApplyVertexTransformTool(sgc, rotation, ref context);
                    dragRectState = DragRectState.None;
                }
            }

            // WARNING: Make sure to draw selection handles after all modeling logic.
            // This is necessary to make the extrusion hotkey work.
            // Previously, when a vertex was extruded, that would cause a new Handles.Button() to be drawn for that vertex.
            // That would in turn cause the handle IDs to change, forcing a deselection of the current Handles.PositionHandle().
            // This is very brittle, keep an eye on this area of code.
            OnSceneGUISelectVerticesTool(sgc, rotation);


            // Now that we have potentially performed some transforms to the spline graph, tell it to serialize (just to be safe).
            // Otherwise, this serialization would happen next frame (which is probably fine?)
            sgc.Verify();
            serializedObject.ApplyModifiedProperties();
        }
    }
}