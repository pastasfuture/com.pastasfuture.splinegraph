using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Pastasfuture.SplineGraph.Runtime
{
    [CreateAssetMenu(fileName = "SplineGraphUserBlobSchemaExample.asset", menuName = "Spline Graph/SplineGraphUserBlobSchemaExample")]
    public class SplineGraphUserBlobSchemaExample : SplineGraphUserBlobSchemaScriptableObject
    {
        public enum SplineGraphUserBlobSchemaExampleVertex : int
        {
            Radiation = 0,
            PatternIndex,
            Color
        };

        public enum SplineGraphUserBlobSchemaExampleEdge : int
        {
            Color = 0,
            PatternIndex
        };

        private static readonly string[] SCHEMA_VERTEX_NAMES =
        {
            "Radiation",
            "Pattern Index",
            "Color"
        };

        private static readonly string[] SCHEMA_EDGE_NAMES =
        {
            "Color",
            "Pattern Index"
        };

        private enum SplineGraphUserBlobSchemaExampleVertexV3 : int
        {
            Radiation = 0,
            PatternIndex,
            Color
        };

        private enum SplineGraphUserBlobSchemaExampleVertexV2 : int
        {
            Radiation = 0,
            PatternIndex
        };

        private enum SplineGraphUserBlobSchemaExampleVertexV1 : int
        {
            Radiation = 0
        };

        private enum SplineGraphUserBlobSchemaExampleEdgeV3 : int
        {
            Color = 0,
            PatternIndex
        };

        private enum SplineGraphUserBlobSchemaExampleEdgeV2 : int
        {
            Color = 0,
            PatternIndex
        };

        private enum SplineGraphUserBlobSchemaExampleEdgeV1 : int
        {
            PatternIndex = 0
        };

#if UNITY_EDITOR
        private static readonly SplineGraphUserBlob.SchemeEditorOnlyInfo[] SCHEMA_VERTEX_EDITOR_ONLY_INFO =
        {
            new SplineGraphUserBlob.SchemeEditorOnlyInfo // Radiation
            {
                rangeMode = SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.MinMax,
                min = new SplineGraphUserBlob.Value { f = 0.0f },
                max = new SplineGraphUserBlob.Value { f = 1.0f }
            },
            SplineGraphUserBlob.SchemeEditorOnlyInfo.zero, // PatternIndex
            new SplineGraphUserBlob.SchemeEditorOnlyInfo // Color
            {
                rangeMode = SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.Color
            }
        };
        private static readonly SplineGraphUserBlob.SchemeEditorOnlyInfo[] SCHEMA_EDGE_EDITOR_ONLY_INFO =
        {
            new SplineGraphUserBlob.SchemeEditorOnlyInfo // Color
            {
                rangeMode = SplineGraphUserBlob.SchemeEditorOnlyInfo.RangeMode.Color
            },
            SplineGraphUserBlob.SchemeEditorOnlyInfo.zero // PatternIndex
        };
#endif

        private static readonly int SCHEMA_VERSION_LATEST = 3;
        private static readonly SplineGraphUserBlob.Scheme[] SCHEMA_VERTEX_V3 =
        {
            new SplineGraphUserBlob.Scheme // Radiation
            {
                type = SplineGraphUserBlob.Scheme.Type.Float,
                stride = 1,
                offset = 0
            },
            new SplineGraphUserBlob.Scheme // Pattern Index
            {
                type = SplineGraphUserBlob.Scheme.Type.UInt,
                stride = 1,
                offset = 0
            },
            new SplineGraphUserBlob.Scheme // Color
            {
                type = SplineGraphUserBlob.Scheme.Type.Float,
                stride = 4,
                offset = 0
            },
        };
        private static readonly SplineGraphUserBlob.Scheme[] SCHEMA_VERTEX_V2 =
        {
            new SplineGraphUserBlob.Scheme // Radiation
            {
                type = SplineGraphUserBlob.Scheme.Type.Float,
                stride = 1,
                offset = 0
            },
            new SplineGraphUserBlob.Scheme // Pattern Index
            {
                type = SplineGraphUserBlob.Scheme.Type.UInt,
                stride = 1,
                offset = 0
            }
        };
        private static readonly SplineGraphUserBlob.Scheme[] SCHEMA_VERTEX_V1 =
        {
            new SplineGraphUserBlob.Scheme // Radiation
            {
                type = SplineGraphUserBlob.Scheme.Type.Float,
                stride = 1,
                offset = 0
            }
        };
        private static readonly SplineGraphUserBlob.Scheme[] SCHEMA_EDGE_V3 =
        {
            new SplineGraphUserBlob.Scheme // Color
            {
                type = SplineGraphUserBlob.Scheme.Type.Float,
                stride = 4,
                offset = 0
            },
            new SplineGraphUserBlob.Scheme // Pattern Index
            {
                type = SplineGraphUserBlob.Scheme.Type.UInt,
                stride = 1,
                offset = 0
            }
        };
        private static readonly SplineGraphUserBlob.Scheme[] SCHEMA_EDGE_V2 =
        {
            new SplineGraphUserBlob.Scheme // Color
            {
                type = SplineGraphUserBlob.Scheme.Type.Float,
                stride = 4,
                offset = 0
            },
            new SplineGraphUserBlob.Scheme // Pattern Index
            {
                type = SplineGraphUserBlob.Scheme.Type.UInt,
                stride = 1,
                offset = 0
            }
        };
        private static readonly SplineGraphUserBlob.Scheme[] SCHEMA_EDGE_V1 =
        {
            new SplineGraphUserBlob.Scheme // Pattern Index
            {
                type = SplineGraphUserBlob.Scheme.Type.UInt,
                stride = 1,
                offset = 0
            }
        };

        public override string[] GetVertexSchemaNamesReadOnly()
        {
            return SCHEMA_VERTEX_NAMES;
        }

        public override string[] GetEdgeSchemaNamesReadOnly()
        {
            return SCHEMA_EDGE_NAMES;
        }

#if UNITY_EDITOR
        public override SplineGraphUserBlob.SchemeEditorOnlyInfo[] GetVertexSchemaEditorOnlyInfo()
        {
            return SCHEMA_VERTEX_EDITOR_ONLY_INFO;
        }

        public override SplineGraphUserBlob.SchemeEditorOnlyInfo[] GetEdgeSchemaEditorOnlyInfo()
        {
            return SCHEMA_EDGE_EDITOR_ONLY_INFO;
        }
#endif

        public override void CopyVertexSchema(ref NativeArray<SplineGraphUserBlob.Scheme> schema, Allocator allocator)
        {
            CopySchema(SCHEMA_VERTEX_V3, ref schema, allocator);
        }

        public override void CopyEdgeSchema(ref NativeArray<SplineGraphUserBlob.Scheme> schema, Allocator allocator)
        {
            CopySchema(SCHEMA_EDGE_V3, ref schema, allocator);
        }

        protected override void Migrate(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, int versionCurrent, int versionNext, Allocator allocator)
        {
            Debug.Assert(versionCurrent >= 0 && versionCurrent < versionNext);
            Debug.Assert(versionNext >= 0 && versionNext <= SCHEMA_VERSION_LATEST);
            Debug.Assert((versionCurrent + 1) == versionNext);
            Debug.Assert(splineGraph.payload.userBlobSchemaVersion == versionCurrent);

            switch (versionCurrent)
            {
                case 0: MigrateV1FromV0(ref splineGraph, allocator); break;
                case 1: MigrateV2FromV1(ref splineGraph, allocator); break;
                case 2: MigrateV3FromV2(ref splineGraph, allocator); break;
                default: Debug.Assert(false); break;
            }
        }

        private void MigrateV3FromV2(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Allocator allocator)
        {
            Debug.Assert(splineGraph.payload.userBlobSchemaVersion == 2);

            var userBlobVertexNext = new SplineGraphUserBlob.NativeSchemaBlob(SCHEMA_VERTEX_V3, splineGraph.vertices.data.Length, allocator);

            for (Int16 vertexIndex = 0; vertexIndex < splineGraph.vertices.data.Length; ++vertexIndex)
            {
                DirectedVertex vertex = splineGraph.vertices.data[vertexIndex];
                if (vertex.IsValid() == 0) { continue; }

                float radiationValue = splineGraph.payload.userBlobVertex.GetFloat((int)SplineGraphUserBlobSchemaExampleVertexV2.Radiation, vertexIndex);
                userBlobVertexNext.Set((int)SplineGraphUserBlobSchemaExampleVertexV3.Radiation, vertexIndex, radiationValue);
            }
            for (Int16 vertexIndex = 0; vertexIndex < splineGraph.vertices.data.Length; ++vertexIndex)
            {
                DirectedVertex vertex = splineGraph.vertices.data[vertexIndex];
                if (vertex.IsValid() == 0) { continue; }

                float patternIndexValue = splineGraph.payload.userBlobVertex.GetFloat((int)SplineGraphUserBlobSchemaExampleVertexV2.PatternIndex, vertexIndex);
                userBlobVertexNext.Set((int)SplineGraphUserBlobSchemaExampleVertexV3.PatternIndex, vertexIndex, patternIndexValue);
            }
            splineGraph.payload.userBlobVertex.Dispose();
            splineGraph.payload.userBlobVertex = userBlobVertexNext;

            // Edge schema is the same as V2, no need to edit the data.
            splineGraph.payload.userBlobSchemaVersion = 3;
        }

        private void MigrateV2FromV1(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Allocator allocator)
        {
            Debug.Assert(splineGraph.payload.userBlobSchemaVersion == 1);

            var userBlobVertexNext = new SplineGraphUserBlob.NativeSchemaBlob(SCHEMA_VERTEX_V2, splineGraph.vertices.data.Length, allocator);
            var userBlobEdgeNext = new SplineGraphUserBlob.NativeSchemaBlob(SCHEMA_EDGE_V2, splineGraph.edgePoolChildren.data.Length, allocator);

            for (Int16 vertexIndex = 0; vertexIndex < splineGraph.vertices.data.Length; ++vertexIndex)
            {
                DirectedVertex vertex = splineGraph.vertices.data[vertexIndex];
                if (vertex.IsValid() == 0) { continue; }

                float radiationValue = splineGraph.payload.userBlobVertex.GetFloat((int)SplineGraphUserBlobSchemaExampleVertexV1.Radiation, vertexIndex);
                userBlobVertexNext.Set((int)SplineGraphUserBlobSchemaExampleVertexV2.Radiation, vertexIndex, radiationValue);
            }
            splineGraph.payload.userBlobVertex.Dispose();
            splineGraph.payload.userBlobVertex = userBlobVertexNext;

            for (Int16 edgeIndex = 0; edgeIndex < splineGraph.edgePoolChildren.data.Length; ++edgeIndex)
            {
                DirectedEdge edge = splineGraph.edgePoolChildren.data[edgeIndex];
                if (edge.IsValid() == 0) { continue; }

                int patternIndexValue = splineGraph.payload.userBlobEdge.GetInt((int)SplineGraphUserBlobSchemaExampleEdgeV1.PatternIndex, edgeIndex);
                userBlobEdgeNext.Set((int)SplineGraphUserBlobSchemaExampleEdgeV2.PatternIndex, edgeIndex, patternIndexValue);
            }
            splineGraph.payload.userBlobEdge.Dispose();
            splineGraph.payload.userBlobEdge = userBlobEdgeNext;

            splineGraph.payload.userBlobSchemaVersion = 2;
        }

        private void MigrateV1FromV0(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Allocator allocator)
        {
            // Version 0 is treated as null.
            // If Version 0 is encountered, it is due to zero initialization from legacy data that has no user blob.
            // Simply zero initialize with V1 so it can be migrated to the latest version.
            Debug.Assert(splineGraph.payload.userBlobSchemaVersion == 0);

            splineGraph.payload.SetSchema(SCHEMA_VERTEX_V1, SCHEMA_EDGE_V1, 1, allocator);
        }


        public override int GetVersion()
        {
            return SCHEMA_VERSION_LATEST;
        }
    }
}
