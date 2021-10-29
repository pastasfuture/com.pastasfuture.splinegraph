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
        private class SetValueTool
        {
            public bool isFoldoutEnabled = false;

            public bool isPositionEnabled = false;
            public bool isRotationEnabled = false;
            public bool isScaleEnabled = false;
            public bool isLeashEnabled = false;

            public float3 position = float3.zero;
            public float3 rotationEuler = float3.zero;
            public float2 scale = float2.zero;
            public float2 leash = float2.zero;

            public List<UserBlobSchemeState> userBlobSchemeStates = new List<UserBlobSchemeState>();

            public class UserBlobSchemeState
            {
                public bool isEnabled = false;
                public SplineGraphUserBlob.Value x;
                public SplineGraphUserBlob.Value y;
                public SplineGraphUserBlob.Value z;
                public SplineGraphUserBlob.Value w;
            }
        }

        private SetValueTool setValueTool = new SetValueTool();

        public void OnInspectorGUISetValueTool(SplineGraphComponent sgc)
        {
            EditorGUILayout.BeginVertical();

            if (!setValueTool.isFoldoutEnabled && GUILayout.Button("Set Values Tool"))
            {
                setValueTool.isFoldoutEnabled = true;
            }
            if (setValueTool.isFoldoutEnabled)
            {
                setValueTool.isFoldoutEnabled = EditorGUILayout.BeginFoldoutHeaderGroup(setValueTool.isFoldoutEnabled, "Set Values Tool");
                if (setValueTool.isFoldoutEnabled)
                {
                    EditorGUILayout.BeginHorizontal();
                    setValueTool.isPositionEnabled = EditorGUILayout.Toggle(setValueTool.isPositionEnabled, GUILayout.Width(15));
                    using (new EditorGUI.DisabledScope(!setValueTool.isPositionEnabled))
                    {
                        EditorGUILayout.LabelField("Position", GUILayout.Width(100));
                        setValueTool.position = EditorGUILayout.Vector3Field("", setValueTool.position);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    setValueTool.isRotationEnabled = EditorGUILayout.Toggle(setValueTool.isRotationEnabled, GUILayout.Width(15));
                    using (new EditorGUI.DisabledScope(!setValueTool.isRotationEnabled))
                    {
                        EditorGUILayout.LabelField("Rotation", GUILayout.Width(100));
                        setValueTool.rotationEuler = EditorGUILayout.Vector3Field("", setValueTool.rotationEuler);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    setValueTool.isScaleEnabled = EditorGUILayout.Toggle(setValueTool.isScaleEnabled, GUILayout.Width(15));
                    using (new EditorGUI.DisabledScope(!setValueTool.isScaleEnabled))
                    {
                        EditorGUILayout.LabelField("Scale", GUILayout.Width(100));
                        setValueTool.scale = EditorGUILayout.Vector2Field("", setValueTool.scale);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    setValueTool.isLeashEnabled = EditorGUILayout.Toggle(setValueTool.isLeashEnabled, GUILayout.Width(15));
                    using (new EditorGUI.DisabledScope(!setValueTool.isLeashEnabled))
                    {
                        EditorGUILayout.LabelField("Leash", GUILayout.Width(100));
                        setValueTool.leash = EditorGUILayout.Vector2Field("", setValueTool.leash);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (sgc.splineGraph.payload.userBlobSchemaVersion != 0)
                    {
                        while (setValueTool.userBlobSchemeStates.Count > sgc.splineGraph.payload.userBlobVertex.schema.Length)
                        {
                            setValueTool.userBlobSchemeStates.RemoveAt(setValueTool.userBlobSchemeStates.Count - 1);
                        }
                        while (setValueTool.userBlobSchemeStates.Count < sgc.splineGraph.payload.userBlobVertex.schema.Length)
                        {
                            setValueTool.userBlobSchemeStates.Add(new SetValueTool.UserBlobSchemeState());
                        }
                        for (int scheme = 0; scheme < sgc.splineGraph.payload.userBlobVertex.schema.Length; ++scheme)
                        {
                            EditorGUILayout.BeginHorizontal();
                            setValueTool.userBlobSchemeStates[scheme].isEnabled = EditorGUILayout.Toggle(setValueTool.userBlobSchemeStates[scheme].isEnabled, GUILayout.Width(15));
                            using (new EditorGUI.DisabledScope(!setValueTool.userBlobSchemeStates[scheme].isEnabled))
                            {
                                EditorGUILayout.LabelField(sgc.splineGraphUserBlobSchema.GetVertexSchemaNamesReadOnly()[scheme], GUILayout.Width(100));
                                switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].type)
                                {
                                    case SplineGraphUserBlob.Scheme.Type.Int:
                                    {
                                        switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                        {
                                            case 1:
                                            {
                                                setValueTool.userBlobSchemeStates[scheme].x.i = EditorGUILayout.IntField(setValueTool.userBlobSchemeStates[scheme].x.i);
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                }
                                                break;
                                            }

                                            case 2:
                                            {
                                                Vector2Int valueNext = EditorGUILayout.Vector2IntField("", new Vector2Int(setValueTool.userBlobSchemeStates[scheme].x.i, setValueTool.userBlobSchemeStates[scheme].y.i));
                                                setValueTool.userBlobSchemeStates[scheme].x.i = valueNext.x;
                                                setValueTool.userBlobSchemeStates[scheme].y.i = valueNext.y;
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].y.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].y.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                }
                                                break;
                                            }

                                            case 3:
                                            {
                                                Vector3Int valueNext = EditorGUILayout.Vector3IntField("", new Vector3Int(setValueTool.userBlobSchemeStates[scheme].x.i, setValueTool.userBlobSchemeStates[scheme].y.i, setValueTool.userBlobSchemeStates[scheme].z.i));
                                                setValueTool.userBlobSchemeStates[scheme].x.i = valueNext.x;
                                                setValueTool.userBlobSchemeStates[scheme].y.i = valueNext.y;
                                                setValueTool.userBlobSchemeStates[scheme].z.i = valueNext.z;
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].y.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].y.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].z.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].z.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                }
                                                break;
                                            }

                                            case 4:
                                            {
                                                Vector4 valueNext = EditorGUILayout.Vector4Field("", new float4(setValueTool.userBlobSchemeStates[scheme].x.i, setValueTool.userBlobSchemeStates[scheme].y.i, setValueTool.userBlobSchemeStates[scheme].z.i, setValueTool.userBlobSchemeStates[scheme].z.i));
                                                setValueTool.userBlobSchemeStates[scheme].x.i = (int)valueNext.x;
                                                setValueTool.userBlobSchemeStates[scheme].y.i = (int)valueNext.y;
                                                setValueTool.userBlobSchemeStates[scheme].z.i = (int)valueNext.z;
                                                setValueTool.userBlobSchemeStates[scheme].w.i = (int)valueNext.w;
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].y.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].y.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].z.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].z.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].w.i = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].w.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.i,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.i
                                                    );
                                                }
                                                break;
                                            }
                                            default: Debug.Assert(false); break;
                                        }
                                        break;
                                    }

                                    case SplineGraphUserBlob.Scheme.Type.UInt:
                                    {
                                        switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                        {
                                            case 1:
                                            {
                                                uint valueNext = (uint)math.max(0, EditorGUILayout.IntField((int)setValueTool.userBlobSchemeStates[scheme].x.u));
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    valueNext = math.clamp(valueNext, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u);
                                                }
                                                setValueTool.userBlobSchemeStates[scheme].x.u = valueNext;
                                                break;
                                            }

                                            case 2:
                                            {
                                                Vector2Int valueNext = EditorGUILayout.Vector2IntField("", new Vector2Int((int)setValueTool.userBlobSchemeStates[scheme].x.u, (int)setValueTool.userBlobSchemeStates[scheme].y.u));
                                                setValueTool.userBlobSchemeStates[scheme].x.u = (uint)math.max(0, valueNext.x);
                                                setValueTool.userBlobSchemeStates[scheme].y.u = (uint)math.max(0, valueNext.y);
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].y.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].y.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                }
                                                break;
                                            }

                                            case 3:
                                            {
                                                Vector3Int valueNext = EditorGUILayout.Vector3IntField("", new Vector3Int((int)setValueTool.userBlobSchemeStates[scheme].x.u, (int)setValueTool.userBlobSchemeStates[scheme].y.u, (int)setValueTool.userBlobSchemeStates[scheme].z.u));
                                                setValueTool.userBlobSchemeStates[scheme].x.u = (uint)math.max(0, valueNext.x);
                                                setValueTool.userBlobSchemeStates[scheme].y.u = (uint)math.max(0, valueNext.y);
                                                setValueTool.userBlobSchemeStates[scheme].z.u = (uint)math.max(0, valueNext.z);
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].y.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].y.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].z.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].z.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                }
                                                break;
                                            }

                                            case 4:
                                            {
                                                Vector4 valueNext = EditorGUILayout.Vector4Field("", new float4(setValueTool.userBlobSchemeStates[scheme].x.u, setValueTool.userBlobSchemeStates[scheme].y.u, setValueTool.userBlobSchemeStates[scheme].z.u, setValueTool.userBlobSchemeStates[scheme].z.u));
                                                setValueTool.userBlobSchemeStates[scheme].x.u = (uint)math.max(0, (int)valueNext.x);
                                                setValueTool.userBlobSchemeStates[scheme].y.u = (uint)math.max(0, (int)valueNext.y);
                                                setValueTool.userBlobSchemeStates[scheme].z.u = (uint)math.max(0, (int)valueNext.z);
                                                setValueTool.userBlobSchemeStates[scheme].w.u = (uint)math.max(0, (int)valueNext.w);
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    setValueTool.userBlobSchemeStates[scheme].x.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].x.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].y.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].y.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].z.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].z.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                    setValueTool.userBlobSchemeStates[scheme].w.u = math.clamp(
                                                        setValueTool.userBlobSchemeStates[scheme].w.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.u,
                                                        sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.u
                                                    );
                                                }
                                                break;
                                            }
                                            default: Debug.Assert(false); break;
                                        }
                                        break;
                                    }

                                    case SplineGraphUserBlob.Scheme.Type.Float:
                                    {
                                        switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                        {
                                            case 1:
                                            {
                                                float valueNext = EditorGUILayout.FloatField(setValueTool.userBlobSchemeStates[scheme].x.f);
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    valueNext = math.clamp(valueNext, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.f, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.f);
                                                }
                                                setValueTool.userBlobSchemeStates[scheme].x.f = valueNext;
                                                break;
                                            }

                                            case 2:
                                            {
                                                float2 valueNext = EditorGUILayout.Vector2Field("", new float2(setValueTool.userBlobSchemeStates[scheme].x.f, setValueTool.userBlobSchemeStates[scheme].y.f));
                                                if (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode == SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax)
                                                {
                                                    valueNext = math.clamp(valueNext, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.f, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.f);
                                                }
                                                setValueTool.userBlobSchemeStates[scheme].x.f = valueNext.x;
                                                setValueTool.userBlobSchemeStates[scheme].y.f = valueNext.y;
                                                break;
                                            }

                                            case 3:
                                            {
                                                float3 valueNext = new float3(setValueTool.userBlobSchemeStates[scheme].x.f, setValueTool.userBlobSchemeStates[scheme].y.f, setValueTool.userBlobSchemeStates[scheme].z.f);
                                                switch (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode)
                                                {
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.Color:
                                                    {
                                                        Color valueNextColor = EditorGUILayout.ColorField("", new Color(valueNext.x, valueNext.y, valueNext.z, 1.0f));
                                                        valueNext = new float3(valueNextColor.r, valueNextColor.g, valueNextColor.b);
                                                        break;
                                                    }
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.ColorHDR:
                                                    {
                                                        Color valueNextColor = EditorGUILayout.ColorField(GUIContent.none, new Color(valueNext.x, valueNext.y, valueNext.z, 1.0f), showEyedropper: true, showAlpha: false, hdr: true);
                                                        valueNext = new float3(valueNextColor.r, valueNextColor.g, valueNextColor.b);
                                                        break;
                                                    }
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax:
                                                    {
                                                        valueNext = EditorGUILayout.Vector3Field("", valueNext);
                                                        valueNext = math.clamp(valueNext, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.f, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.f);
                                                        break;
                                                    }
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.None:
                                                    {
                                                        valueNext = EditorGUILayout.Vector3Field("", valueNext);
                                                        break;
                                                    }
                                                    default: Debug.Assert(false); break;
                                                }
                                                
                                                setValueTool.userBlobSchemeStates[scheme].x.f = valueNext.x;
                                                setValueTool.userBlobSchemeStates[scheme].y.f = valueNext.y;
                                                setValueTool.userBlobSchemeStates[scheme].z.f = valueNext.z;
                                                break;
                                            }

                                            case 4:
                                            {
                                                float4 valueNext = new float4(setValueTool.userBlobSchemeStates[scheme].x.f, setValueTool.userBlobSchemeStates[scheme].y.f, setValueTool.userBlobSchemeStates[scheme].z.f, setValueTool.userBlobSchemeStates[scheme].w.f);
                                                switch (sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].rangeMode)
                                                {
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.Color:
                                                    {
                                                        Color valueNextColor = EditorGUILayout.ColorField(GUIContent.none, new Color(valueNext.x, valueNext.y, valueNext.z, valueNext.w), showEyedropper: true, showAlpha: true, hdr: false);
                                                        valueNext = new float4(valueNextColor.r, valueNextColor.g, valueNextColor.b, valueNextColor.a);
                                                        break;
                                                    }
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.ColorHDR:
                                                    {
                                                        Color valueNextColor = EditorGUILayout.ColorField(GUIContent.none, new Color(valueNext.x, valueNext.y, valueNext.z, valueNext.w), showEyedropper: true, showAlpha: true, hdr: true);
                                                        valueNext = new float4(valueNextColor.r, valueNextColor.g, valueNextColor.b, valueNextColor.a);
                                                        break;
                                                    }
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax:
                                                    {
                                                        valueNext = EditorGUILayout.Vector4Field("", valueNext);
                                                        valueNext = math.clamp(valueNext, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].min.f, sgc.splineGraphUserBlobSchema.GetVertexSchemaEditorOnlyInfo()[scheme].max.f);
                                                        break;
                                                    }
                                                    case SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.None:
                                                    {
                                                        valueNext = EditorGUILayout.Vector4Field("", valueNext);
                                                        break;
                                                    }
                                                    default: Debug.Assert(false); break;
                                                }

                                                setValueTool.userBlobSchemeStates[scheme].x.f = valueNext.x;
                                                setValueTool.userBlobSchemeStates[scheme].y.f = valueNext.y;
                                                setValueTool.userBlobSchemeStates[scheme].z.f = valueNext.z;
                                                setValueTool.userBlobSchemeStates[scheme].w.f = valueNext.w;
                                                break;
                                            }
                                            default: Debug.Assert(false); break;
                                        }
                                        break;
                                    }

                                    default: Debug.Assert(false); break;
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (selectedIndices.Count > 0 && GUILayout.Button("Apply"))
                    {
                        setValueTool.isFoldoutEnabled = false;
                        sgc.UndoRecord("Spline Graph Set Values", true);

                        if (setValueTool.isPositionEnabled)
                        {
                            for (int i = 0; i < selectedIndices.Count; ++i)
                            {
                                Int16 v = selectedIndices[i];
                                if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                if (vertex.IsValid() == 0) { continue; }

                                sgc.splineGraph.payload.positions.data[v] = setValueTool.position;
                            }
                        }

                        if (setValueTool.isRotationEnabled)
                        {
                            for (int i = 0; i < selectedIndices.Count; ++i)
                            {
                                Int16 v = selectedIndices[i];
                                if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                if (vertex.IsValid() == 0) { continue; }

                                sgc.splineGraph.payload.rotations.data[v] = quaternion.EulerXYZ(math.radians(setValueTool.rotationEuler));
                            }
                        }

                        if (setValueTool.isScaleEnabled)
                        {
                            for (int i = 0; i < selectedIndices.Count; ++i)
                            {
                                Int16 v = selectedIndices[i];
                                if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                if (vertex.IsValid() == 0) { continue; }

                                sgc.splineGraph.payload.scales.data[v] = setValueTool.scale;
                            }
                        }

                        if (setValueTool.isLeashEnabled)
                        {
                            for (int i = 0; i < selectedIndices.Count; ++i)
                            {
                                Int16 v = selectedIndices[i];
                                if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                if (vertex.IsValid() == 0) { continue; }

                                sgc.splineGraph.payload.leashes.data[v] = setValueTool.leash;
                            }
                        }

                        if (sgc.splineGraph.payload.userBlobSchemaVersion != 0)
                        {
                            for (int scheme = 0; scheme < sgc.splineGraph.payload.userBlobVertex.schema.Length; ++scheme)
                            {
                                if (setValueTool.userBlobSchemeStates[scheme].isEnabled)
                                {
                                    switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].type)
                                    {
                                        case SplineGraphUserBlob.Scheme.Type.Int:
                                        {
                                            switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                            {
                                                case 1:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, setValueTool.userBlobSchemeStates[scheme].x.i);
                                                    }
                                                    break;
                                                }
                                                case 2:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new int2(
                                                                setValueTool.userBlobSchemeStates[scheme].x.i,
                                                                setValueTool.userBlobSchemeStates[scheme].y.i
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                case 3:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new int3(
                                                                setValueTool.userBlobSchemeStates[scheme].x.i,
                                                                setValueTool.userBlobSchemeStates[scheme].y.i,
                                                                setValueTool.userBlobSchemeStates[scheme].z.i
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                case 4:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new int4(
                                                                setValueTool.userBlobSchemeStates[scheme].x.i,
                                                                setValueTool.userBlobSchemeStates[scheme].y.i,
                                                                setValueTool.userBlobSchemeStates[scheme].z.i,
                                                                setValueTool.userBlobSchemeStates[scheme].w.i
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                default: Debug.Assert(false); break;
                                            }
                                            break;
                                        }
                                        case SplineGraphUserBlob.Scheme.Type.UInt:
                                        {
                                            switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                            {
                                                case 1:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, setValueTool.userBlobSchemeStates[scheme].x.u);
                                                    }
                                                    break;
                                                }
                                                case 2:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new uint2(
                                                                setValueTool.userBlobSchemeStates[scheme].x.u,
                                                                setValueTool.userBlobSchemeStates[scheme].y.u
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                case 3:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new uint3(
                                                                setValueTool.userBlobSchemeStates[scheme].x.u,
                                                                setValueTool.userBlobSchemeStates[scheme].y.u,
                                                                setValueTool.userBlobSchemeStates[scheme].z.u
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                case 4:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new uint4(
                                                                setValueTool.userBlobSchemeStates[scheme].x.u,
                                                                setValueTool.userBlobSchemeStates[scheme].y.u,
                                                                setValueTool.userBlobSchemeStates[scheme].z.u,
                                                                setValueTool.userBlobSchemeStates[scheme].w.u
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                default: Debug.Assert(false); break;
                                            }
                                            break;
                                        }
                                        case SplineGraphUserBlob.Scheme.Type.Float:
                                        {
                                            switch (sgc.splineGraph.payload.userBlobVertex.schema[scheme].stride)
                                            {
                                                case 1:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v, setValueTool.userBlobSchemeStates[scheme].x.f);
                                                    }
                                                    break;
                                                }
                                                case 2:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new float2(
                                                                setValueTool.userBlobSchemeStates[scheme].x.f,
                                                                setValueTool.userBlobSchemeStates[scheme].y.f
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                case 3:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new float3(
                                                                setValueTool.userBlobSchemeStates[scheme].x.f,
                                                                setValueTool.userBlobSchemeStates[scheme].y.f,
                                                                setValueTool.userBlobSchemeStates[scheme].z.f
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                case 4:
                                                {
                                                    for (int i = 0; i < selectedIndices.Count; ++i)
                                                    {
                                                        Int16 v = selectedIndices[i];
                                                        if (v < 0 || v >= sgc.splineGraph.vertices.count) { continue; }
                                                        DirectedVertex vertex = sgc.splineGraph.vertices.data[v];
                                                        if (vertex.IsValid() == 0) { continue; }

                                                        sgc.splineGraph.payload.userBlobVertex.Set(scheme, v,
                                                            new float4(
                                                                setValueTool.userBlobSchemeStates[scheme].x.f,
                                                                setValueTool.userBlobSchemeStates[scheme].y.f,
                                                                setValueTool.userBlobSchemeStates[scheme].z.f,
                                                                setValueTool.userBlobSchemeStates[scheme].w.f
                                                            )
                                                        );
                                                    }
                                                    break;
                                                }
                                                default: Debug.Assert(false); break;
                                            }
                                            break;
                                        }
                                        default: Debug.Assert(false); break;
                                    }
                                }
                            }
                        }

                    }
                    else if (GUILayout.Button("Cancel"))
                    {
                        setValueTool.isFoldoutEnabled = false;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
