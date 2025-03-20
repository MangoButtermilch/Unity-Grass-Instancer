using UnityEngine;

namespace Acetix.Grass
{
    struct Chunk
    {
        public Vector3 position;
        public uint instanceStartIndex;
        public uint instanceCount;

        public Chunk(Vector3 p)
        {
            position = p;
            instanceStartIndex = 0;
            instanceCount = 0;
        }
    };
}