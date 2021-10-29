using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Pastasfuture.SplineGraph.Runtime
{
    public abstract class SplineGraphUserBlobSchemaScriptableObject : ScriptableObject
    {
        public void Migrate(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, Allocator allocator)
        {
            while (splineGraph.payload.userBlobSchemaVersion < GetVersion())
            {
                int schemaVersionCurrent = splineGraph.payload.userBlobSchemaVersion;
                int schemaVersionNext = schemaVersionCurrent + 1;
                Migrate(ref splineGraph, schemaVersionCurrent, schemaVersionNext, allocator);
                if (splineGraph.payload.userBlobSchemaVersion != schemaVersionNext)
                {
                    Debug.Assert(false);
                    break;
                }
            }
        }

        public abstract string[] GetVertexSchemaNamesReadOnly();
        public abstract string[] GetEdgeSchemaNamesReadOnly();

#if UNITY_EDITOR
        public abstract SplineGraphUserBlob.SchemeEditorOnlyInfo[] GetVertexSchemaEditorOnlyInfo();
        public abstract SplineGraphUserBlob.SchemeEditorOnlyInfo[] GetEdgeSchemaEditorOnlyInfo();
#endif

        public abstract void CopyVertexSchema(ref NativeArray<SplineGraphUserBlob.Scheme> schema, Allocator allocator);

        public abstract void CopyEdgeSchema(ref NativeArray<SplineGraphUserBlob.Scheme> schema, Allocator allocator);

        protected abstract void Migrate(ref DirectedGraph<SplineGraphPayload, SplineGraphPayloadSerializable> splineGraph, int versionCurrent, int versionNext, Allocator allocator);

        public abstract int GetVersion();

        protected static void CopySchema(SplineGraphUserBlob.Scheme[] src, ref NativeArray<SplineGraphUserBlob.Scheme> dst, Allocator allocator)
        {
            if (!dst.IsCreated || dst.Length != src.Length)
            {
                if (dst.IsCreated)
                {
                    dst.Dispose();
                }

                dst = new NativeArray<SplineGraphUserBlob.Scheme>(src, allocator);
            }
            else
            {
                NativeArray<SplineGraphUserBlob.Scheme>.Copy(src, dst);
            }
        }
    }
}
